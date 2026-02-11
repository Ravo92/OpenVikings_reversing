using System.Buffers.Binary;

namespace OpenVikings.Dexter
{
    internal static class DexterEndian
    {
        // ===== Pure endian helpers (Get/Put/Convert) =====

        // DexterEndian::GetWordMSB(unsigned short const*)
        internal static ushort GetWordMSB(ushort value)
        {
            return BinaryPrimitives.ReverseEndianness(value);
        }

        // DexterEndian::GetWordLSB(unsigned short const*)
        internal static ushort GetWordLSB(ushort value)
        {
            return value;
        }

        // DexterEndian::GetLongMSB(unsigned int const*)
        internal static uint GetLongMSB(uint value)
        {
            return BinaryPrimitives.ReverseEndianness(value);
        }

        // DexterEndian::GetLongLSB(unsigned int const*)
        internal static uint GetLongLSB(uint value)
        {
            return value;
        }

        // DexterEndian::GetQuadMSB(unsigned long long const*)
        internal static ulong GetQuadMSB(ulong value)
        {
            return BinaryPrimitives.ReverseEndianness(value);
        }

        // DexterEndian::GetQuadLSB(unsigned long long const*)
        // Note: In the pseudo this appears as "void" returning immediately (decompiler artifact).
        // The effective LSB behavior is identity.
        internal static ulong GetQuadLSB(ulong value)
        {
            return value;
        }

        // DexterEndian::PutWordMSB(unsigned short*, unsigned short)
        internal static ushort PutWordMSB(ushort value)
        {
            return BinaryPrimitives.ReverseEndianness(value);
        }

        // DexterEndian::PutWordLSB(unsigned short*, unsigned short)
        internal static ushort PutWordLSB(ushort value)
        {
            return value;
        }

        // DexterEndian::PutLongMSB(unsigned int*, unsigned int)
        internal static uint PutLongMSB(uint value)
        {
            return BinaryPrimitives.ReverseEndianness(value);
        }

        // DexterEndian::PutLongLSB(unsigned int*, unsigned int)
        internal static uint PutLongLSB(uint value)
        {
            return value;
        }

        // DexterEndian::PutQuadMSB(unsigned long long*, unsigned long long)
        internal static ulong PutQuadMSB(ulong value)
        {
            return BinaryPrimitives.ReverseEndianness(value);
        }

        // DexterEndian::PutQuadLSB(unsigned long long*, unsigned long long)
        // Note: In the pseudo this appears as "void" returning immediately (decompiler artifact).
        // The effective LSB behavior is identity.
        internal static ulong PutQuadLSB(ulong value)
        {
            return value;
        }

        // DexterEndian::ConvertWordMSB(unsigned short*)
        internal static void ConvertWordMSB(ref ushort value)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }

        // DexterEndian::ConvertWordLSB(unsigned short*)
        internal static void ConvertWordLSB(ref ushort value)
        {
            // no-op
        }

        // DexterEndian::ConvertLongMSB(unsigned int*)
        internal static void ConvertLongMSB(ref uint value)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }

        // DexterEndian::ConvertLongLSB(unsigned int*)
        internal static void ConvertLongLSB(ref uint value)
        {
            // no-op
        }

        // DexterEndian::ConvertQuadMSB(unsigned long long*)
        internal static void ConvertQuadMSB(ref ulong value)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }

        // DexterEndian::ConvertQuadLSB(unsigned long long*)
        internal static void ConvertQuadLSB(ref ulong value)
        {
            // no-op
        }

        // ===== File IO endian helpers =====
        // These mirror the pseudo functions that read/write specific sized integers via DexterFile.

        // DexterEndian::FileWriteLongMSB(PHYSFS_File*, unsigned int)
        internal static void FileWriteLongMSB(nint fileHandle, uint value)
        {
            byte[] buffer = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
            WriteExactly(fileHandle, buffer, 4);
        }

        // DexterEndian::FileWriteQuadMSB(PHYSFS_File*, unsigned long long)
        internal static void FileWriteQuadMSB(nint fileHandle, ulong value)
        {
            byte[] buffer = new byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
            WriteExactly(fileHandle, buffer, 8);
        }

        // DexterEndian::FileWriteWordMSB(PHYSFS_File*, unsigned short)
        internal static void FileWriteWordMSB(nint fileHandle, ushort value)
        {
            byte[] buffer = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
            WriteExactly(fileHandle, buffer, 2);
        }

        // DexterEndian::FileWriteLongLSB(PHYSFS_File*, unsigned int)
        internal static void FileWriteLongLSB(nint fileHandle, uint value)
        {
            byte[] buffer = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
            WriteExactly(fileHandle, buffer, 4);
        }

        // DexterEndian::FileWriteQuadLSB(PHYSFS_File*, unsigned long long)
        internal static void FileWriteQuadLSB(nint fileHandle, ulong value)
        {
            byte[] buffer = new byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
            WriteExactly(fileHandle, buffer, 8);
        }

        // DexterEndian::FileWriteWordLSB(PHYSFS_File*, unsigned short)
        internal static void FileWriteWordLSB(nint fileHandle, ushort value)
        {
            byte[] buffer = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
            WriteExactly(fileHandle, buffer, 2);
        }

        // DexterEndian::FileReadLongMSB(PHYSFS_File*)
        internal static uint FileReadLongMSB(nint fileHandle)
        {
            byte[] buffer = new byte[4];
            ReadExactly(fileHandle, buffer, 4);
            return BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }

        // DexterEndian::FileReadQuadMSB(PHYSFS_File*)
        internal static ulong FileReadQuadMSB(nint fileHandle)
        {
            byte[] buffer = new byte[8];
            ReadExactly(fileHandle, buffer, 8);
            return BinaryPrimitives.ReadUInt64BigEndian(buffer);
        }

        // DexterEndian::FileReadWordMSB(PHYSFS_File*)
        internal static ushort FileReadWordMSB(nint fileHandle)
        {
            byte[] buffer = new byte[2];
            ReadExactly(fileHandle, buffer, 2);
            return BinaryPrimitives.ReadUInt16BigEndian(buffer);
        }

        // DexterEndian::FileReadLongLSB(PHYSFS_File*)
        internal static uint FileReadLongLSB(nint fileHandle)
        {
            byte[] buffer = new byte[4];
            ReadExactly(fileHandle, buffer, 4);
            return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }

        // DexterEndian::FileReadQuadLSB(PHYSFS_File*)
        internal static ulong FileReadQuadLSB(nint fileHandle)
        {
            byte[] buffer = new byte[8];
            ReadExactly(fileHandle, buffer, 8);
            return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        }

        // DexterEndian::FileReadWordLSB(PHYSFS_File*)
        internal static ushort FileReadWordLSB(nint fileHandle)
        {
            byte[] buffer = new byte[2];
            ReadExactly(fileHandle, buffer, 2);
            return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        }

        // ===== Array IO (LSB/MSB) =====
        // The pseudo uses pointer arithmetic and writes/reads element-by-element.
        // Here it is expressed as managed arrays with identical semantics.

        // DexterEndian::FileWriteWordArrayLSB(PHYSFS_File*, unsigned short const*, unsigned int)
        internal static void FileWriteWordArrayLSB(nint fileHandle, ushort[] values, uint count)
        {
            if (values == null || count == 0)
            {
                return;
            }

            uint limit = count;
            if (limit > (uint)values.Length)
            {
                limit = (uint)values.Length;
            }

            byte[] buffer = new byte[2];

            uint i = 0;
            while (i < limit)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(buffer, values[i]);
                WriteExactly(fileHandle, buffer, 2);
                i++;
            }
        }

        // DexterEndian::FileWriteLongArrayLSB(PHYSFS_File*, unsigned int const*, unsigned int)
        internal static void FileWriteLongArrayLSB(nint fileHandle, uint[] values, uint count)
        {
            if (values == null || count == 0)
            {
                return;
            }

            uint limit = count;
            if (limit > (uint)values.Length)
            {
                limit = (uint)values.Length;
            }

            byte[] buffer = new byte[4];

            uint i = 0;
            while (i < limit)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(buffer, values[i]);
                WriteExactly(fileHandle, buffer, 4);
                i++;
            }
        }

        // DexterEndian::FileWriteWordArrayMSB(PHYSFS_File*, unsigned short const*, unsigned int)
        internal static void FileWriteWordArrayMSB(nint fileHandle, ushort[] values, uint count)
        {
            if (values == null || count == 0)
            {
                return;
            }

            uint limit = count;
            if (limit > (uint)values.Length)
            {
                limit = (uint)values.Length;
            }

            byte[] buffer = new byte[2];

            uint i = 0;
            while (i < limit)
            {
                BinaryPrimitives.WriteUInt16BigEndian(buffer, values[i]);
                WriteExactly(fileHandle, buffer, 2);
                i++;
            }
        }

        // DexterEndian::FileWriteLongArrayMSB(PHYSFS_File*, unsigned int const*, unsigned int)
        internal static void FileWriteLongArrayMSB(nint fileHandle, uint[] values, uint count)
        {
            if (values == null || count == 0)
            {
                return;
            }

            uint limit = count;
            if (limit > (uint)values.Length)
            {
                limit = (uint)values.Length;
            }

            byte[] buffer = new byte[4];

            uint i = 0;
            while (i < limit)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer, values[i]);
                WriteExactly(fileHandle, buffer, 4);
                i++;
            }
        }

        // DexterEndian::FileReadWordArrayLSB(PHYSFS_File*, unsigned short*, unsigned int)
        internal static void FileReadWordArrayLSB(nint fileHandle, ushort[] values, uint count)
        {
            if (values == null || count == 0)
            {
                return;
            }

            uint limit = count;
            if (limit > (uint)values.Length)
            {
                limit = (uint)values.Length;
            }

            byte[] buffer = new byte[2];

            uint i = 0;
            while (i < limit)
            {
                ReadExactly(fileHandle, buffer, 2);
                values[i] = BinaryPrimitives.ReadUInt16LittleEndian(buffer);
                i++;
            }
        }

        // DexterEndian::FileReadLongArrayLSB(PHYSFS_File*, unsigned int*, unsigned int)
        internal static void FileReadLongArrayLSB(nint fileHandle, uint[] values, uint count)
        {
            if (values == null || count == 0)
            {
                return;
            }

            uint limit = count;
            if (limit > (uint)values.Length)
            {
                limit = (uint)values.Length;
            }

            byte[] buffer = new byte[4];

            uint i = 0;
            while (i < limit)
            {
                ReadExactly(fileHandle, buffer, 4);
                values[i] = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
                i++;
            }
        }

        // DexterEndian::FileReadWordArrayMSB(PHYSFS_File*, unsigned short*, unsigned int)
        internal static void FileReadWordArrayMSB(nint fileHandle, ushort[] values, uint count)
        {
            if (values == null || count == 0)
            {
                return;
            }

            uint limit = count;
            if (limit > (uint)values.Length)
            {
                limit = (uint)values.Length;
            }

            byte[] buffer = new byte[2];

            uint i = 0;
            while (i < limit)
            {
                ReadExactly(fileHandle, buffer, 2);
                values[i] = BinaryPrimitives.ReadUInt16BigEndian(buffer);
                i++;
            }
        }

        // DexterEndian::FileReadLongArrayMSB(PHYSFS_File*, unsigned int*, unsigned int)
        internal static void FileReadLongArrayMSB(nint fileHandle, uint[] values, uint count)
        {
            if (values == null || count == 0)
            {
                return;
            }

            uint limit = count;
            if (limit > (uint)values.Length)
            {
                limit = (uint)values.Length;
            }

            byte[] buffer = new byte[4];

            uint i = 0;
            while (i < limit)
            {
                ReadExactly(fileHandle, buffer, 4);
                values[i] = BinaryPrimitives.ReadUInt32BigEndian(buffer);
                i++;
            }
        }

        // ===== Internal IO helpers =====
        // These preserve the "read/write exactly N bytes" behavior from the existing C# code.

        private static void ReadExactly(nint fileHandle, byte[] buffer, int count)
        {
            int offset = 0;

            while (offset < count)
            {
                int read = DexterFile.FileRead(fileHandle, buffer, offset, count - offset, 0);
                if (read <= 0)
                {
                    throw new EndOfStreamException("Unexpected end of file while reading.");
                }

                offset += read;
            }
        }

        private static void WriteExactly(nint fileHandle, byte[] buffer, int count)
        {
            int written = DexterFile.FileWrite(fileHandle, buffer, 0, count);
            if (written != count)
            {
                throw new IOException("Failed to write requested number of bytes.");
            }
        }
    }
}