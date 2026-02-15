namespace OpenVikings.NXBasics
{
    // Matches the original intent: a small "owned buffer" object with size + pointer,
    // plus some debug fill patterns (0xEE on alloc, 0xED on free).
    internal sealed class CMemory : CStorable, IDisposable
    {
        private const byte AllocFillPattern = 0xEE;
        private const byte FreeFillPattern = 0xED;

        private uint _size;
        private byte[]? _buffer;
        private bool _disposed;

        internal byte[]? BufferArray => _buffer;

        internal uint Size => _size;

        // Expose as span for safe consumers; returns empty span when no buffer.
        internal ReadOnlySpan<byte> Buffer
        {
            get
            {
                if (_buffer == null)
                {
                    return [];
                }

                return _buffer;
            }
        }

        // NXBasics::CMemory::CMemory(unsigned int)
        internal CMemory(uint size)
        {
            _size = 0;
            _buffer = null;

            if (size != 0)
            {
                AllocateMemory(size);
            }
            else
            {
                _size = 0;
                _buffer = null;
            }
        }

        // NXBasics::CMemory::CMemory(NXBasics::CFile&, unsigned int)
        internal CMemory(CFile file, uint param2)
        {
            _size = 0;
            _buffer = null;
            _disposed = false;

            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            // Original: size = ReadLong()
            uint size = file.ReadLong();
            _size = size;

            if (size == 0)
            {
                return;
            }

            AllocateMemory(size);

            if (_buffer == null)
            {
                return;
            }

            int toRead = checked((int)size);
            int read = file.Read(_buffer, toRead);

            // Optional safety: if your FileRead can return partial reads, ensure full read.
            // If your implementation always reads fully, you can remove this check.
            if (read != toRead)
            {
                // Keep it simple: shrink logical size to what was actually read (or throw).
                // Choose one policy; I'd rather throw for corrupted files.
                throw new EndOfStreamException("CMemory: could not read full buffer from file.");
            }

            _ = param2; // kept for signature match
        }

        // NXBasics::CMemory::CMemory(char const*)  (loads entire file into memory)
        internal CMemory(string filePath)
        {
            _size = 0;
            _buffer = null;
            _disposed = false;

            using CFile file = new(filePath, true);

            // If CFile is not auto-opened by ctor, ensure it is open here.
            // Example:
            // if (!file.OpenForReading()) { throw new IOException("Could not open file for reading."); }

            ulong fileSize = file.GetSize();

            if (fileSize > uint.MaxValue)
            {
                throw new InvalidDataException("File too large for CMemory.");
            }

            if (fileSize > int.MaxValue)
            {
                throw new InvalidDataException("File too large to be read into a single managed byte array.");
            }

            uint size = (uint)fileSize;
            _size = size;

            if (size == 0)
            {
                return;
            }

            AllocateMemory(size);

            if (_buffer == null)
            {
                throw new InvalidOperationException("CMemory buffer allocation failed.");
            }

            int toRead = (int)size;
            int totalRead = 0;

            while (totalRead < toRead)
            {
                int read = file.ReadInto(_buffer, toRead - totalRead, totalRead); // if you have offset overload
                if (read <= 0)
                {
                    throw new EndOfStreamException("CMemory: could not read full buffer from file.");
                }

                totalRead += read;
            }
        }

        // NXBasics::CMemory::l_AllocateMemory(unsigned int)
        internal void AllocateMemory(uint size)
        {
            // The original does not free existing memory here; it looks like a private helper
            // used during construction. In C#, we keep it safe and overwrite any old buffer.
            _size = size;

            if (size == 0)
            {
                _buffer = null;
                return;
            }

            int length = checked((int)size);
            _buffer = new byte[length];

            FillInternal(_buffer, AllocFillPattern); // 0xEE
        }

        // NXBasics::CMemory::Fill(unsigned char)
        internal void Fill(byte value)
        {
            ThrowIfDisposed();

            if (_buffer == null)
            {
                return;
            }

            FillInternal(_buffer, value);
        }

        // NXBasics::CMemory::Storable_SaveData(NXBasics::CFile&)
        internal override void Storable_SaveData(CFile file)
        {
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(file);

            file.WriteLong(_size);

            if (_size == 0 || _buffer == null)
            {
                return;
            }

            int toWrite = checked((int)_size);
            file.Write(_buffer, toWrite);
        }

        // NXBasics::CMemory::CopyInto(NXBasics::CMemory const&) const
        // Original copies THIS buffer into the other object's buffer (up to min size).
        internal void CopyInto(CMemory destination)
        {
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(destination);

            destination.ThrowIfDisposed();

            if (_buffer == null || destination._buffer == null)
            {
                return;
            }

            uint bytesToCopy = destination._size;
            if (_size < bytesToCopy)
            {
                bytesToCopy = _size;
            }

            int count = checked((int)bytesToCopy);
            if (count <= 0)
            {
                return;
            }

            System.Buffer.BlockCopy(_buffer, 0, destination._buffer, 0, count);
        }

        // NXBasics::CMemory::ChangeBufferSize(unsigned int, bool, unsigned char)
        // param_2 = preserveAndFillNew, param_3 = fill byte for new region
        internal bool ChangeBufferSize(uint newSize, bool preserveAndFillNew, byte fillByte)
        {
            ThrowIfDisposed();

            if (_size == newSize)
            {
                return true; // original returns 1
            }

            byte[]? oldBuffer = _buffer;
            uint oldSize = _size;

            if (newSize == 0)
            {
                if (oldBuffer != null && oldSize != 0)
                {
                    FillInternal(oldBuffer, FreeFillPattern);
                }

                _buffer = null;
                _size = 0;
                return true;
            }

            int newLength = checked((int)newSize);
            byte[] newBuffer = new byte[newLength];

            // The original alloc path always does an 0xEE fill first.
            FillInternal(newBuffer, AllocFillPattern);

            if (preserveAndFillNew && newSize > oldSize)
            {
                int start = checked((int)oldSize);
                int count = checked((int)(newSize - oldSize));
                if (count > 0)
                {
                    FillRangeInternal(newBuffer, start, count, fillByte);
                }
            }

            uint bytesToCopyU = oldSize;
            if (newSize <= oldSize)
            {
                bytesToCopyU = newSize;
            }

            if (oldBuffer != null && bytesToCopyU != 0)
            {
                int bytesToCopy = checked((int)bytesToCopyU);
                System.Buffer.BlockCopy(oldBuffer, 0, newBuffer, 0, bytesToCopy);

                FillInternal(oldBuffer, FreeFillPattern);
            }

            _buffer = newBuffer;
            _size = newSize;
            return true;
        }

        // NXBasics::CMemory::Encrypt(NXBasics::TEncryptMode)
        internal void Encrypt(TEncryptMode mode)
        {
            ThrowIfDisposed();

            if (_buffer == null || _size == 0)
            {
                return;
            }

            XB_Encrypt_Memory(_buffer, _size, mode);
        }

        // NXBasics::CMemory::Decrypt(NXBasics::TEncryptMode)
        internal void Decrypt(TEncryptMode mode)
        {
            ThrowIfDisposed();

            if (_buffer == null || _size == 0)
            {
                return;
            }

            XB_Decrypt_Memory(_buffer, _size, mode);
        }

        // NXBasics::CMemory::Storable_GetId() const
        internal override uint Storable_GetId()
        {
            return 0x3E9U;
        }

        // NXBasics::CMemory::~CMemory()
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_buffer != null && _size != 0)
            {
                FillInternal(_buffer, FreeFillPattern); // 0xED
            }

            _buffer = null;
            _size = 0;
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (!_disposed)
            {
                return;
            }
            throw new ObjectDisposedException(nameof(CMemory));
        }

        private static void FillInternal(byte[] buffer, byte value)
        {
            // Array.Fill is fine; it’s the closest managed equivalent to MemorySet.
            Array.Fill(buffer, value);
        }

        private static void FillRangeInternal(byte[] buffer, int start, int count, byte value)
        {
            if (count <= 0)
            {
                return;
            }

            // Manual loop to avoid requiring Span APIs if you want to keep it simple.
            int end = start + count;
            for (int i = start; i < end; i++)
            {
                buffer[i] = value;
            }
        }

        // Replace these with your actual engine crypto bindings.
        private static void XB_Encrypt_Memory(byte[] buffer, uint size, TEncryptMode mode)
        {
            // Intentionally left blank: wire up to your implementation.
            // Signature mirrors: (ptr, size, mode)
            _ = buffer;
            _ = size;
            _ = mode;
        }

        private static void XB_Decrypt_Memory(byte[] buffer, uint size, TEncryptMode mode)
        {
            // Intentionally left blank: wire up to your implementation.
            _ = buffer;
            _ = size;
            _ = mode;
        }
    }

    internal enum TEncryptMode : int
    {
        Mode0 = 0,
        Mode1 = 1,
        Mode2 = 2
    }
}