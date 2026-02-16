namespace OpenVikings.NC2Logic
{
    // Managed port of NC2Logic::CIoHelper (pointer-free).
    // Adjusted to the provided NXBasics::CFile and NXBasics::CMemory signatures:
    // - CFile.Read(byte[] buffer, int size)
    // - CFile.ReadInto(byte[] buffer, int bufferOffset, int size)
    // - CFile.Write(byte[] buffer, int size)
    // - CMemory.Size, CMemory.BufferArray, CMemory.ChangeBufferSize(uint, bool, byte)
    internal sealed class CIoHelper : IDisposable
    {
        private const uint HoixMarker = 0x78696F68u; // "hoix" in little-endian
        private const int TempStringBufferSize = 200;
        private const int ChunkHeaderSizeBytes = 0x20;
        private const int MaxChunkDepth = 5;

        // ---- File mode ----
        private NXBasics.CFile? _file;          // this+0x00
        private byte[]? _tempStringBuffer;                  // this+0x08 (allocated 200 bytes in original)
        private SIoHelperChunk _chunkHeader;                // this+0x10 (0x20 bytes)
        private int _chunkHeaderFilePos;                    // this+0x30 (-1 when invalid)

        // ---- Memory mode ----
        private NXBasics.CMemory? _memory;       // this+0x38
        private uint _memoryUsage;                           // this+0x40

        // ---- Chunk creation tool ----
        private uint _chunkDepth;                            // this+0x50
        private readonly uint[] _chunkStartOffsets;          // this+0x54 .. stack

        internal CIoHelper()
        {
            _chunkStartOffsets = new uint[MaxChunkDepth];
            ResetState();
        }

        public void Dispose()
        {
            _tempStringBuffer = null;
            ResetState();
        }

        // --------------------------------------------------------------------
        // File API
        // --------------------------------------------------------------------

        internal void IO_File_Init(NXBasics.CFile file)
        {
            ArgumentNullException.ThrowIfNull(file);

            _file = file;
            _tempStringBuffer = new byte[TempStringBufferSize];

            _chunkHeader = default;
            _chunkHeader.Marker = HoixMarker;

            _chunkHeaderFilePos = -1;
        }

        internal void IO_File_Exit()
        {
            ResetState();
        }

        internal void IO_File_WriteBinary(byte[] source, uint count)
        {
            if (_file == null) return;
            if (source == null) return;
            if (count == 0) return;

            _file.Write(source, checked((int)count));
        }

        internal void IO_File_ReadBinary(byte[] destination, uint count)
        {
            if (_file == null) return;
            if (destination == null) return;
            if (count == 0) return;

            _file.Read(destination, checked((int)count));
        }

        internal double IO_File_ReadBinaryDouble()
        {
            if (_file == null) return 0.0;

            byte[] buf = new byte[8];
            _file.Read(buf, 8);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(buf);
            }

            return BitConverter.ToDouble(buf, 0);
        }

        internal uint IO_File_ReadBinaryLong()
        {
            if (_file == null) return 0;
            return _file.ReadLong();
        }

        internal ushort IO_File_ReadBinaryWord()
        {
            if (_file == null) return 0;
            return _file.ReadWord();
        }

        internal void IO_File_Skip(uint bytesToSkip)
        {
            if (_file == null) return;

            int pos = _file.GetPosition();
            _file.SeekToPosition(checked(pos + (int)bytesToSkip));
        }

        internal void IO_File_ReadAndUnpackBinary(byte[] destination, uint destinationSize)
        {
            if (_file == null)
            {
                return;
            }

            if (destination == null)
            {
                return;
            }

            byte[] flagBuf = new byte[1];
            _file.Read(flagBuf, 1);
            byte packedFlag = flagBuf[0];

            uint storedSize = _file.ReadLong();

            if (packedFlag == 0)
            {
                int toRead = checked((int)storedSize);

                if (toRead < 0)
                {
                    throw new ArgumentOutOfRangeException();
                }

                if (toRead > destination.Length)
                {
                    throw new ArgumentOutOfRangeException();
                }

                ArgumentOutOfRangeException.ThrowIfLessThan(destinationSize, (uint)toRead);

                _file.Read(destination, toRead);
                return;
            }

            int packedLen = checked((int)storedSize);

            if (packedLen < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            byte[] packed = new byte[packedLen];
            _file.Read(packed, packed.Length);

            bool ok = NXBasics.XBTools.XB_Pack_UnPack(packed, 0, packed.Length, destination, 0, checked((int)destinationSize));
            if (!ok)
            {
                throw new InvalidOperationException("XB_Pack_UnPack failed.");
            }
        }

        internal byte IO_File_ReadByte()
        {
            if (_file == null) return 0;

            byte[] buf = new byte[1];
            _file.Read(buf, 1);
            return buf[0];
        }

        internal uint IO_File_ReadLong()
        {
            if (_file == null) return 0;
            return _file.ReadLong();
        }

        internal ushort IO_File_ReadWord()
        {
            if (_file == null) return 0;
            return _file.ReadWord();
        }

        internal void IO_File_WriteLong(uint value)
        {
            if (_file == null) return;
            _file.WriteLong(value);
        }

        internal void IO_File_WriteWord(ushort value)
        {
            if (_file == null) return;
            _file.WriteWord(value);
        }

        internal void IO_File_WriteByte(byte value)
        {
            if (_file == null) return;

            byte[] buf = [value];
            _file.Write(buf, 1);
        }

        internal void IO_File_WriteString(string text)
        {
            if (_file == null) return;
            if (text == null) return;

            // Original: 1 byte length + N+1 bytes including '\0'.
            byte[] bytes = System.Text.Encoding.Latin1.GetBytes(text);

            int len = bytes.Length;
            if (len > 255) len = 255;

            byte[] header = [(byte)len];
            _file.Write(header, 1);

            byte[] payload = new byte[len + 1];
            if (len != 0)
            {
                Buffer.BlockCopy(bytes, 0, payload, 0, len);
            }
            payload[len] = 0;

            _file.Write(payload, payload.Length);
        }

        internal string IO_File_ReadString()
        {
            if (_file == null) return string.Empty;

            if (_tempStringBuffer == null || _tempStringBuffer.Length != TempStringBufferSize)
            {
                _tempStringBuffer = new byte[TempStringBufferSize];
            }

            byte[] lenBuf = new byte[1];
            _file.Read(lenBuf, 1);
            byte lengthByte = lenBuf[0];

            // Original: if len < 199 => read into temp buffer, else skip and return "".
            if (lengthByte < 199)
            {
                int toRead = checked(lengthByte + 1);
                _file.ReadInto(_tempStringBuffer, 0, toRead);

                int end = 0;
                while (end < toRead && _tempStringBuffer[end] != 0)
                {
                    end++;
                }

                return System.Text.Encoding.Latin1.GetString(_tempStringBuffer, 0, end);
            }

            int pos = _file.GetPosition();
            _file.SeekToPosition(pos + lengthByte + 1);

            _tempStringBuffer[0] = 0;
            return string.Empty;
        }

        internal void IO_File_Chunk_WriteMarkerAndVersion(uint id, uint version, uint length)
        {
            if (_file == null) return;

            SIoHelperChunk chunk = default;
            chunk.Marker = HoixMarker;
            chunk.Id = id;
            chunk.Version = version;
            chunk.Length = length;

            byte[] raw = chunk.ToBytesLittleEndian();
            _file.Write(raw, raw.Length);
        }

        internal bool IO_File_Chunk_CheckMarkerAndVersion(uint expectedId, uint expectedVersion)
        {
            if (_file == null) return false;

            byte[] raw = new byte[ChunkHeaderSizeBytes];
            _file.Read(raw, raw.Length);

            _chunkHeader = SIoHelperChunk.FromBytesLittleEndian(raw);
            FlipSIoHelperChunk(ref _chunkHeader, false);

            _chunkHeaderFilePos = _file.GetPosition();

            if (_chunkHeader.Marker == HoixMarker && _chunkHeader.Id == expectedId)
            {
                return _chunkHeader.Version == expectedVersion;
            }

            _chunkHeaderFilePos = -1;
            return false;
        }

        internal bool IO_File_Chunk_CheckMarkerAndGetVersion(uint expectedId, out uint? version, out uint? length)
        {
            version = null;
            length = null;

            if (_file == null) return false;

            byte[] raw = new byte[ChunkHeaderSizeBytes];
            _file.Read(raw, raw.Length);

            _chunkHeader = SIoHelperChunk.FromBytesLittleEndian(raw);
            FlipSIoHelperChunk(ref _chunkHeader, false);

            _chunkHeaderFilePos = _file.GetPosition();

            if (_chunkHeader.Marker != HoixMarker)
            {
                _chunkHeaderFilePos = -1;
                return false;
            }

            version = _chunkHeader.Version;
            length = _chunkHeader.Length;

            return _chunkHeader.Id == expectedId;
        }

        internal bool IO_File_Chunk_ReadHeader()
        {
            if (_file == null) return false;

            byte[] raw = new byte[ChunkHeaderSizeBytes];
            _file.Read(raw, raw.Length);

            _chunkHeader = SIoHelperChunk.FromBytesLittleEndian(raw);
            FlipSIoHelperChunk(ref _chunkHeader, false);

            _chunkHeaderFilePos = _file.GetPosition();

            if (_chunkHeader.Marker != HoixMarker)
            {
                _chunkHeaderFilePos = -1;
                return false;
            }

            // Original: header.Id != 0x78656e64
            return _chunkHeader.Id != 0x78656E64u;
        }

        internal void IO_File_Chunk_Skip()
        {
            if (_file == null) return;

            if (_chunkHeaderFilePos != -1)
            {
                _file.SeekToPosition(_chunkHeaderFilePos + checked((int)_chunkHeader.Length));
                _chunkHeader = default;
                _chunkHeaderFilePos = -1;
            }
        }

        internal static uint IO_File_Chunk_Tool_LengthOfMarkerAndVersion()
        {
            return ChunkHeaderSizeBytes;
        }

        // --------------------------------------------------------------------
        // Memory API
        // --------------------------------------------------------------------

        internal void IO_Memory_Init(NXBasics.CMemory memory)
        {
            ArgumentNullException.ThrowIfNull(memory);

            _memory = memory;
            _memoryUsage = 0;
        }

        internal void IO_Memory_ReStart()
        {
            _memoryUsage = 0;
        }

        internal void IO_Memory_Exit()
        {
            _memory = null;
            _memoryUsage = 0;
        }

        internal byte[]? IO_Memory_BufferPtr()
        {
            if (_memory == null) return null;
            return _memory.BufferArray;
        }

        internal uint IO_Memory_BufferUsage()
        {
            if (_memory == null) return 0;
            return _memoryUsage;
        }

        internal void IO_Memory_WriteByte(byte value)
        {
            if (_memory == null) return;

            EnsureMemoryCapacity(1);

            byte[]? buf = _memory.BufferArray;
            if (buf == null) return;

            buf[checked((int)_memoryUsage)] = value;
            _memoryUsage += 1;
        }

        internal void IO_Memory_WriteWord(ushort value)
        {
            if (_memory == null) return;

            EnsureMemoryCapacity(2);

            byte[]? buf = _memory.BufferArray;
            if (buf == null) return;

            WriteUInt16LSB(buf, checked((int)_memoryUsage), value);
            _memoryUsage += 2;
        }

        internal void IO_Memory_WriteLong(uint value)
        {
            if (_memory == null) return;

            EnsureMemoryCapacity(4);

            byte[]? buf = _memory.BufferArray;
            if (buf == null) return;

            WriteUInt32LSB(buf, checked((int)_memoryUsage), value);
            _memoryUsage += 4;
        }

        internal void IO_Memory_WriteString(string text)
        {
            if (_memory == null) return;
            if (text == null) return;

            byte[] bytes = System.Text.Encoding.Latin1.GetBytes(text);

            int len = bytes.Length;
            if (len > 255) len = 255;

            uint needed = checked((uint)(len + 2)); // len byte + chars + '\0'
            EnsureMemoryCapacity(needed);

            byte[]? buf = _memory.BufferArray;
            if (buf == null) return;

            int baseIndex = checked((int)_memoryUsage);
            buf[baseIndex] = (byte)len;

            if (len != 0)
            {
                Buffer.BlockCopy(bytes, 0, buf, baseIndex + 1, len);
            }

            buf[baseIndex + 1 + len] = 0;
            _memoryUsage += needed;
        }

        internal void IO_Memory_WriteBinary(byte[] source, uint count)
        {
            if (_memory == null) return;
            if (source == null) return;
            if (count == 0) return;

            EnsureMemoryCapacity(count);

            byte[]? buf = _memory.BufferArray;
            if (buf == null) return;

            Buffer.BlockCopy(source, 0, buf, checked((int)_memoryUsage), checked((int)count));
            _memoryUsage += count;
        }

        // The shown decompile body effectively writes "not packed":
        // [flag=0][u32 length][raw bytes]
        internal void IO_Memory_PackAndWriteBinary(byte[] source, int count, int packAlgorithm)
        {
            _ = packAlgorithm;

            if (_memory == null) return;
            if (source == null) return;
            if (count <= 0) return;

            IO_Memory_WriteByte(0);
            IO_Memory_WriteLong(unchecked((uint)count));
            IO_Memory_WriteBinary(source, unchecked((uint)count));
        }

        // --------------------------------------------------------------------
        // Tool: chunk creation
        // --------------------------------------------------------------------

        internal void IO_Tool_Chunk_StartCreation(uint chunkId, uint version)
        {
            if (_chunkDepth == MaxChunkDepth)
            {
                return;
            }

            if (_chunkDepth == 0)
            {
                _memoryUsage = 0;
            }

            uint currentUsage = _memory != null ? _memoryUsage : 0;
            _chunkStartOffsets[_chunkDepth] = currentUsage;

            SIoHelperChunk header = default;
            header.Marker = HoixMarker;
            header.Id = chunkId;
            header.Version = version;
            header.Length = 0;
            header.Depth = _chunkDepth;
            header.Checksum = 0;

            byte[] raw = header.ToBytesLittleEndian();

            EnsureMemoryCapacity(ChunkHeaderSizeBytes);

            byte[]? buf = _memory?.BufferArray;
            if (buf == null) return;

            Buffer.BlockCopy(raw, 0, buf, checked((int)_memoryUsage), raw.Length);
            _memoryUsage += ChunkHeaderSizeBytes;

            _chunkDepth += 1;
        }

        internal void IO_Tool_Chunk_FinishAndWrite()
        {
            if (_chunkDepth == 0)
            {
                return;
            }

            _chunkDepth -= 1;

            uint startOffset = _chunkStartOffsets[_chunkDepth];

            byte[]? buf = _memory?.BufferArray;
            if (buf == null)
            {
                return;
            }

            uint totalSize = _memoryUsage;
            if (totalSize < ChunkHeaderSizeBytes || totalSize < startOffset + ChunkHeaderSizeBytes)
            {
                return;
            }

            uint payloadLength = checked(totalSize - (startOffset + ChunkHeaderSizeBytes));

            // Patch length at +0x0C
            WriteUInt32LSB(buf, checked((int)(startOffset + 0x0C)), payloadLength);

            if (payloadLength > int.MaxValue)
            {
                return;
            }

            // Patch checksum at +0x14 (checksum of payload)
            uint checksum = NXBasics.XBTools.XB_GetMemoryChecksum(buf, checked((int)(startOffset + ChunkHeaderSizeBytes)), checked((int)payloadLength));
            WriteUInt32LSB(buf, checked((int)(startOffset + 0x14)), checksum);

            if (_chunkDepth == 0)
            {
                _file?.Write(buf, checked((int)_memoryUsage));

                _memoryUsage = 0;
            }
        }

        internal void IO_Tool_Chunk_WriteEndChunk()
        {
            if (_file == null) return;

            byte[] raw = new byte[ChunkHeaderSizeBytes];

            byte[] marker = System.Text.Encoding.Latin1.GetBytes("hoixdnex");
            Buffer.BlockCopy(marker, 0, raw, 0, marker.Length);
            raw[marker.Length] = 0;

            _file.Write(raw, raw.Length);
        }

        // --------------------------------------------------------------------
        // Endian helper (mirrors NC2Logic::FlipSIoHelperChunk)
        // --------------------------------------------------------------------

        internal static void FlipSIoHelperChunk(ref SIoHelperChunk chunk, bool forceFlip)
        {
            if (chunk.Marker == HoixMarker && !forceFlip)
            {
                return;
            }

            chunk.Marker = ConvertUInt32LSB(chunk.Marker);
            chunk.Id = ConvertUInt32LSB(chunk.Id);
            chunk.Version = ConvertUInt32LSB(chunk.Version);
            chunk.Length = ConvertUInt32LSB(chunk.Length);
            chunk.Depth = ConvertUInt32LSB(chunk.Depth);
            chunk.Checksum = ConvertUInt32LSB(chunk.Checksum);
            chunk.Reserved6 = ConvertUInt32LSB(chunk.Reserved6);
            chunk.Reserved7 = ConvertUInt32LSB(chunk.Reserved7);
        }

        // --------------------------------------------------------------------
        // Internal helpers
        // --------------------------------------------------------------------

        private void ResetState()
        {
            _file = null;
            _tempStringBuffer = null;
            _chunkHeader = default;
            _chunkHeaderFilePos = -1;

            _memory = null;
            _memoryUsage = 0;

            _chunkDepth = 0;
            for (int i = 0; i < _chunkStartOffsets.Length; i++)
            {
                _chunkStartOffsets[i] = 0;
            }
        }

        private void EnsureMemoryCapacity(uint additionalBytes)
        {
            if (_memory == null) return;

            uint required = checked(_memoryUsage + additionalBytes);
            uint currentSize = _memory.Size;

            if (currentSize >= required)
            {
                return;
            }

            // Original grows by +10000 (0x2710) in most places.
            uint newSize = checked(currentSize + 10000u);
            if (newSize < required)
            {
                newSize = checked(required + 10000u);
            }

            _memory.ChangeBufferSize(newSize, false, 0xEE);
        }

        private static void WriteUInt16LSB(byte[] buffer, int offset, ushort value)
        {
            buffer[offset + 0] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static void WriteUInt32LSB(byte[] buffer, int offset, uint value)
        {
            buffer[offset + 0] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static uint ConvertUInt32LSB(uint value)
        {
            if (BitConverter.IsLittleEndian)
            {
                return value;
            }

            return (value >> 24)
                 | ((value >> 8) & 0x0000FF00u)
                 | ((value << 8) & 0x00FF0000u)
                 | (value << 24);
        }
    }

    // 0x20 bytes chunk header used by the helper.
    internal struct SIoHelperChunk
    {
        internal uint Marker;     // +0x00
        internal uint Id;         // +0x04
        internal uint Version;    // +0x08
        internal uint Length;     // +0x0C
        internal uint Depth;      // +0x10
        internal uint Checksum;   // +0x14
        internal uint Reserved6;  // +0x18
        internal uint Reserved7;  // +0x1C

        internal readonly byte[] ToBytesLittleEndian()
        {
            byte[] raw = new byte[0x20];

            WriteUInt32(raw, 0x00, Marker);
            WriteUInt32(raw, 0x04, Id);
            WriteUInt32(raw, 0x08, Version);
            WriteUInt32(raw, 0x0C, Length);
            WriteUInt32(raw, 0x10, Depth);
            WriteUInt32(raw, 0x14, Checksum);
            WriteUInt32(raw, 0x18, Reserved6);
            WriteUInt32(raw, 0x1C, Reserved7);

            return raw;
        }

        internal static SIoHelperChunk FromBytesLittleEndian(byte[] raw)
        {
            ArgumentNullException.ThrowIfNull(raw);
            if (raw.Length < 0x20) throw new ArgumentOutOfRangeException(nameof(raw));

            SIoHelperChunk chunk = default;
            chunk.Marker = ReadUInt32(raw, 0x00);
            chunk.Id = ReadUInt32(raw, 0x04);
            chunk.Version = ReadUInt32(raw, 0x08);
            chunk.Length = ReadUInt32(raw, 0x0C);
            chunk.Depth = ReadUInt32(raw, 0x10);
            chunk.Checksum = ReadUInt32(raw, 0x14);
            chunk.Reserved6 = ReadUInt32(raw, 0x18);
            chunk.Reserved7 = ReadUInt32(raw, 0x1C);

            return chunk;
        }

        private static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            buffer[offset + 0] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static uint ReadUInt32(byte[] buffer, int offset)
        {
            return buffer[offset + 0]
                 | ((uint)buffer[offset + 1] << 8)
                 | ((uint)buffer[offset + 2] << 16)
                 | ((uint)buffer[offset + 3] << 24);
        }
    }
}