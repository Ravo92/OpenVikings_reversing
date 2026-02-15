using OpenVikings.NC2GuiToolsBase;

namespace OpenVikings.NXBasics
{
    // NXBasics::CStringArray
    internal sealed class CStringArray : CStorable, IDisposable
    {
        private const uint InvalidOffset = 0xFFFFFFFFu;
        private const uint InitialSlotCount = 100u;           // 0x64
        private const uint InitialOffsetsBytes = 400u;        // 100 * 4
        private const uint InsertGrowSlots = 100u;
        private const uint MaxInsertId = 999_999u;

        private const uint InitialStringPoolExtra = 0x2711u;  // 10001
        private const uint GrowStringPoolExtra = 10_000u;

        // this[8] in the decompile: when false, holes can be reused
        private bool _forceSequentialIds;

        // +0x0C
        private uint _stringCount;

        // +0x10 (low dword of 0x6400000001)
        private uint _usedIdCount;

        // +0x14 (high dword of 0x6400000001)
        private uint _slotCount;

        // +0x18
        private uint _stringPoolUsedBytes;

        // +0x20 : offsets table (uint32 offsets into string pool, 0xFFFFFFFF = empty)
        private CMemory? _offsets;

        // +0x28 : string pool bytes (null-terminated strings)
        private CMemory? _stringPool;

        private bool _disposed;

        // NXBasics::CStringArray::CStringArray(bool)
        internal CStringArray(bool forceSequentialIds)
        {
            _forceSequentialIds = forceSequentialIds;

            _stringCount = 0;
            _usedIdCount = 1;
            _slotCount = InitialSlotCount;
            _stringPoolUsedBytes = 0;

            _offsets = new CMemory(InitialOffsetsBytes);
            _offsets.Fill(0xFF);

            _stringPool = null;
        }

        // NXBasics::CStringArray::CStringArray(NXBasics::CFile&, unsigned int)
        internal CStringArray(CFile file, uint version)
        {
            int force = (int)file.ReadLong();
            _forceSequentialIds = force != 0;

            _stringCount = unchecked(file.ReadLong());
            _usedIdCount = unchecked(file.ReadLong());
            _slotCount = unchecked(file.ReadLong());
            _stringPoolUsedBytes = unchecked(file.ReadLong());

            CStorable offsetsObj = NXBasicsApi.XB_Storable_LoadObject(file);
            _offsets = offsetsObj as CMemory;
            if (_offsets != null)
            {
                _offsets.Decrypt(TEncryptMode.Mode1);
                FixOffsetsEndiannessIfNeeded(_offsets);
            }

            file.ReadFlag(out bool hasStringPool);
            if (!hasStringPool)
            {
                _stringPool = null;
            }
            else
            {
                CStorable poolObj = NXBasicsApi.XB_Storable_LoadObject(file);
                _stringPool = poolObj as CMemory;
                _stringPool?.Decrypt(TEncryptMode.Mode1);
            }

            _ = version; // kept for signature match (decompile param_2 unused here)
        }


        ~CStringArray()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _offsets?.Dispose();
            _offsets = null;

            _stringPool?.Dispose();
            _stringPool = null;

            _disposed = true;
        }

        // NXBasics::CStringArray::Storable_SaveData(NXBasics::CFile&)
        internal override void Storable_SaveData(CFile file)
        {
            // In the original, slotCount is set to usedIdCount before saving.
            _slotCount = _usedIdCount;

            _offsets?.ChangeBufferSize(checked(_usedIdCount * 4u), false, 0xEE);

            // Original wrote 0x14 bytes from (this+8): 5 * 4 bytes.
            file.WriteLong(_forceSequentialIds ? 1u : 0u);
            file.WriteLong(_stringCount);
            file.WriteLong(_usedIdCount);
            file.WriteLong(_slotCount);
            file.WriteLong(_stringPoolUsedBytes);

            if (_offsets != null)
            {
                _offsets.Encrypt(TEncryptMode.Mode1);
                _offsets.Storable_Save(file);
                _offsets.Decrypt(TEncryptMode.Mode1);
            }

            if (_stringPool != null)
            {
                file.WriteTrueFlag();

                _stringPool.ChangeBufferSize(_stringPoolUsedBytes, false, 0xEE);
                _stringPool.Encrypt(TEncryptMode.Mode1);
                _stringPool.Storable_Save(file);
                _stringPool.Decrypt(TEncryptMode.Mode1);
            }
            else
            {
                file.WriteFalseFlag();
            }
        }

        // NXBasics::CStringArray::GetStringPtr(unsigned int) const
        // Managed replacement: returns the decoded string or null if missing.
        internal string? GetString(uint stringId)
        {
            uint offset = l_GetStringOffset(stringId);
            if (offset == InvalidOffset)
            {
                return null;
            }

            if (_stringPool == null)
            {
                return null;
            }

            byte[]? pool = _stringPool.BufferArray;
            if (pool == null)
            {
                return null;
            }

            int start = checked((int)offset);
            if ((uint)start >= _stringPoolUsedBytes || start < 0 || start >= pool.Length)
            {
                return null;
            }

            return ReadNullTerminatedLatin1(pool, start, checked((int)_stringPoolUsedBytes));
        }

        // NXBasics::CStringArray::l_GetStringOffset(unsigned int) const
        internal uint l_GetStringOffset(uint stringId)
        {
            if (stringId >= _slotCount)
            {
                return InvalidOffset;
            }

            if (_offsets == null)
            {
                return InvalidOffset;
            }

            byte[]? buf = _offsets.BufferArray;
            if (buf == null)
            {
                return InvalidOffset;
            }

            int index = checked((int)stringId * 4);
            if (index < 0 || index + 4 > buf.Length)
            {
                return InvalidOffset;
            }

            return ReadUInt32LittleEndian(buf, index);
        }

        // NXBasics::CStringArray::InsertString(unsigned int, char const*)
        internal bool InsertString(uint stringId, string text)
        {
            if (_offsets == null)
            {
                return false;
            }

            if (stringId < _slotCount)
            {
                uint existing = l_GetStringOffset(stringId);
                if (existing != InvalidOffset)
                {
                    return false;
                }
            }
            else
            {
                if (stringId > MaxInsertId)
                {
                    return false;
                }

                _slotCount = checked(stringId + InsertGrowSlots);
                _offsets.ChangeBufferSize(checked(stringId * 4u + InitialOffsetsBytes), true, 0xFF);
            }

            byte[] textBytes = System.Text.Encoding.Latin1.GetBytes(text);
            uint bytesToWrite = checked((uint)textBytes.Length + 1u);

            EnsureStringPoolCapacity(bytesToWrite);

            if (_stringPool == null)
            {
                return false;
            }

            byte[]? pool = _stringPool.BufferArray;
            if (pool == null)
            {
                return false;
            }

            // offsets[stringId] = stringPoolUsedBytes
            WriteOffset(stringId, _stringPoolUsedBytes);

            int dst = checked((int)_stringPoolUsedBytes);
            if (dst < 0 || dst + textBytes.Length + 1 > pool.Length)
            {
                return false;
            }

            Buffer.BlockCopy(textBytes, 0, pool, dst, textBytes.Length);
            pool[dst + textBytes.Length] = 0;

            _stringPoolUsedBytes = checked(_stringPoolUsedBytes + bytesToWrite);
            _stringCount = checked(_stringCount + 1u);

            if (_usedIdCount <= stringId)
            {
                _usedIdCount = checked(stringId + 1u);
            }

            return true;
        }

        // NXBasics::CStringArray::InsertString(char const*)
        internal uint InsertString(string text)
        {
            uint candidate = _stringCount;

            if (_stringCount != _slotCount && !_forceSequentialIds)
            {
                if (_slotCount == 0)
                {
                    candidate = 0;
                }
                else
                {
                    uint i = 0;
                    while (i < _slotCount)
                    {
                        if (l_GetStringOffset(i) == InvalidOffset)
                        {
                            candidate = i;
                            break;
                        }

                        i = checked(i + 1u);
                        candidate = _slotCount;
                    }
                }
            }

            bool ok = InsertString(candidate, text);
            if (!ok)
            {
                return InvalidOffset;
            }

            return candidate;
        }

        // NXBasics::CStringArray::l_GetNextFreeStringId() const
        internal uint l_GetNextFreeStringId()
        {
            uint candidate = _stringCount;

            if (_stringCount != _slotCount && !_forceSequentialIds)
            {
                if (_slotCount == 0)
                {
                    return 0;
                }

                uint i = 0;
                while (i < _slotCount)
                {
                    if (l_GetStringOffset(i) == InvalidOffset)
                    {
                        return i;
                    }

                    i = checked(i + 1u);
                    if (i == _slotCount)
                    {
                        return _slotCount;
                    }
                }
            }

            return candidate;
        }

        // NXBasics::CStringArray::Storable_GetId() const
        internal override uint Storable_GetId()
        {
            return 0x3FDu;
        }

        private void EnsureStringPoolCapacity(uint bytesToAppendIncludingNull)
        {
            if (_stringPool == null)
            {
                uint initialSize = checked((bytesToAppendIncludingNull - 1u) + InitialStringPoolExtra);
                _stringPool = new CMemory(initialSize);
                return;
            }

            uint currentCapacity = _stringPool.Size;
            uint needed = checked(_stringPoolUsedBytes + bytesToAppendIncludingNull);

            if (currentCapacity < needed)
            {
                uint newSize = checked(needed + GrowStringPoolExtra);
                _stringPool.ChangeBufferSize(newSize, false, 0xEE);
            }
        }

        private void WriteOffset(uint stringId, uint offset)
        {
            if (_offsets == null)
            {
                return;
            }

            byte[]? buf = _offsets.BufferArray;
            if (buf == null)
            {
                return;
            }

            int index = checked((int)stringId * 4);
            if (index < 0 || index + 4 > buf.Length)
            {
                return;
            }

            WriteUInt32LittleEndian(buf, index, offset);
        }

        private static void FixOffsetsEndiannessIfNeeded(CMemory offsets)
        {
            if (BitConverter.IsLittleEndian)
            {
                return;
            }

            byte[]? buf = offsets.BufferArray;
            if (buf == null)
            {
                return;
            }

            uint size = offsets.Size;
            if (size < 4u)
            {
                return;
            }

            uint wordCount = size >> 2;
            uint i = 0;
            while (i < wordCount)
            {
                int index = checked((int)(i * 4u));
                uint v = ReadUInt32LittleEndian(buf, index);
                WriteUInt32LittleEndian(buf, index, v);
                i = checked(i + 1u);
            }
        }

        private static uint ReadUInt32LittleEndian(byte[] buffer, int index)
        {
            return unchecked((uint)(
                buffer[index + 0]
                | (buffer[index + 1] << 8)
                | (buffer[index + 2] << 16)
                | (buffer[index + 3] << 24)));
        }

        private static void WriteUInt32LittleEndian(byte[] buffer, int index, uint value)
        {
            buffer[index + 0] = unchecked((byte)(value & 0xFFu));
            buffer[index + 1] = unchecked((byte)((value >> 8) & 0xFFu));
            buffer[index + 2] = unchecked((byte)((value >> 16) & 0xFFu));
            buffer[index + 3] = unchecked((byte)((value >> 24) & 0xFFu));
        }

        private static string ReadNullTerminatedLatin1(byte[] buffer, int start, int limitExclusive)
        {
            int end = start;
            while (end < limitExclusive && end < buffer.Length && buffer[end] != 0)
            {
                end++;
            }

            int len = end - start;
            if (len <= 0)
            {
                return string.Empty;
            }

            return System.Text.Encoding.Latin1.GetString(buffer, start, len);
        }
    }
}