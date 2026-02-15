namespace OpenVikings.Dexter
{
    // Managed port of DexterEndian.cxx.
    // Pointer-free: all conversions operate on values or Span<T> / arrays.
    internal static class DexterEndian
    {
        // --------------------------------------------------------------------
        // Value conversions (Get*/Put*/Convert* equivalents)
        // --------------------------------------------------------------------

        // DexterEndian::GetWordMSB(unsigned short const*)
        internal static ushort GetWordMSB(ushort value)
        {
            return (ushort)((value << 8) | (value >> 8));
        }

        // DexterEndian::GetWordLSB(unsigned short const*)
        internal static ushort GetWordLSB(ushort value)
        {
            return value;
        }

        // DexterEndian::GetLongMSB(unsigned int const*)
        internal static uint GetLongMSB(uint value)
        {
            return
                (value >> 24) |
                ((value & 0x00FF0000u) >> 8) |
                ((value & 0x0000FF00u) << 8) |
                (value << 24);
        }

        // DexterEndian::GetLongLSB(unsigned int const*)
        internal static uint GetLongLSB(uint value)
        {
            return value;
        }

        // DexterEndian::GetQuadMSB(unsigned long long const*)
        internal static ulong GetQuadMSB(ulong value)
        {
            return
                (value >> 56) |
                ((value & 0x00FF000000000000ul) >> 40) |
                ((value & 0x0000FF0000000000ul) >> 24) |
                ((value & 0x000000FF00000000ul) >> 8) |
                ((value & 0x00000000FF000000ul) << 8) |
                ((value & 0x0000000000FF0000ul) << 24) |
                ((value & 0x000000000000FF00ul) << 40) |
                (value << 56);
        }

        // DexterEndian::GetQuadLSB(unsigned long long const*)
        internal static ulong GetQuadLSB(ulong value)
        {
            return value;
        }

        // DexterEndian::PutWordMSB(unsigned short*, unsigned short)
        internal static ushort PutWordMSB(ushort value)
        {
            return GetWordMSB(value);
        }

        // DexterEndian::PutWordLSB(unsigned short*, unsigned short)
        internal static ushort PutWordLSB(ushort value)
        {
            return value;
        }

        // DexterEndian::PutLongMSB(unsigned int*, unsigned int)
        internal static uint PutLongMSB(uint value)
        {
            return GetLongMSB(value);
        }

        // DexterEndian::PutLongLSB(unsigned int*, unsigned int)
        internal static uint PutLongLSB(uint value)
        {
            return value;
        }

        // DexterEndian::PutQuadMSB(unsigned long long*, unsigned long long)
        internal static ulong PutQuadMSB(ulong value)
        {
            return GetQuadMSB(value);
        }

        // DexterEndian::PutQuadLSB(unsigned long long*, unsigned long long)
        internal static ulong PutQuadLSB(ulong value)
        {
            return value;
        }

        // DexterEndian::ConvertWordMSB(unsigned short*)
        internal static ushort ConvertWordMSB(ushort value)
        {
            return GetWordMSB(value);
        }

        // DexterEndian::ConvertWordLSB(unsigned short*)
        internal static ushort ConvertWordLSB(ushort value)
        {
            return value;
        }

        // DexterEndian::ConvertLongMSB(unsigned int*)
        internal static uint ConvertLongMSB(uint value)
        {
            return GetLongMSB(value);
        }

        // DexterEndian::ConvertLongLSB(unsigned int*)
        internal static uint ConvertLongLSB(uint value)
        {
            return value;
        }

        // DexterEndian::ConvertQuadMSB(unsigned long long*)
        internal static ulong ConvertQuadMSB(ulong value)
        {
            return GetQuadMSB(value);
        }

        // DexterEndian::ConvertQuadLSB(unsigned long long*)
        internal static ulong ConvertQuadLSB(ulong value)
        {
            return value;
        }

        // --------------------------------------------------------------------
        // File I/O helpers (using DexterFile.FileHandle)
        // These mirror the C++ FileRead*/FileWrite* methods.
        // --------------------------------------------------------------------

        // DexterEndian::FileWriteWordMSB(PHYSFS_File*, unsigned short)
        internal static void FileWriteWordMSB(DexterFile.FileHandle file, ushort value)
        {
            ushort v = GetWordMSB(value);
            byte[] bytes = [(byte)v, (byte)(v >> 8)];
            _ = DexterFile.FileWrite(file, bytes, 0, 2);
        }

        // DexterEndian::FileWriteLongMSB(PHYSFS_File*, unsigned int)
        internal static void FileWriteLongMSB(DexterFile.FileHandle file, uint value)
        {
            uint v = GetLongMSB(value);
            byte[] bytes = [(byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24)];
            _ = DexterFile.FileWrite(file, bytes, 0, 4);
        }

        // DexterEndian::FileWriteQuadMSB(PHYSFS_File*, unsigned long long)
        internal static void FileWriteQuadMSB(DexterFile.FileHandle file, ulong value)
        {
            ulong v = GetQuadMSB(value);
            byte[] bytes =
            [
                (byte)v,
                (byte)(v >> 8),
                (byte)(v >> 16),
                (byte)(v >> 24),
                (byte)(v >> 32),
                (byte)(v >> 40),
                (byte)(v >> 48),
                (byte)(v >> 56),
            ];
            _ = DexterFile.FileWrite(file, bytes, 0, 8);
        }

        // DexterEndian::FileWriteWordLSB(PHYSFS_File*, unsigned short)
        internal static void FileWriteWordLSB(DexterFile.FileHandle file, ushort value)
        {
            byte[] bytes = [(byte)value, (byte)(value >> 8)];
            _ = DexterFile.FileWrite(file, bytes, 0, 2);
        }

        // DexterEndian::FileWriteLongLSB(PHYSFS_File*, unsigned int)
        internal static void FileWriteLongLSB(DexterFile.FileHandle file, uint value)
        {
            byte[] bytes = [(byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)];
            _ = DexterFile.FileWrite(file, bytes, 0, 4);
        }

        // DexterEndian::FileWriteQuadLSB(PHYSFS_File*, unsigned long long)
        internal static void FileWriteQuadLSB(DexterFile.FileHandle file, ulong value)
        {
            byte[] bytes =
            [
                (byte)value,
                (byte)(value >> 8),
                (byte)(value >> 16),
                (byte)(value >> 24),
                (byte)(value >> 32),
                (byte)(value >> 40),
                (byte)(value >> 48),
                (byte)(value >> 56),
            ];
            _ = DexterFile.FileWrite(file, bytes, 0, 8);
        }

        // DexterEndian::FileReadWordMSB(PHYSFS_File*)
        internal static ushort FileReadWordMSB(DexterFile.FileHandle file)
        {
            byte[] bytes = new byte[2];
            int read = DexterFile.FileRead(file, bytes, 0, 2, 0);
            if (read != 2)
            {
                return 0;
            }

            ushort v = (ushort)(bytes[0] | (bytes[1] << 8));
            return GetWordMSB(v);
        }

        // DexterEndian::FileReadLongMSB(PHYSFS_File*)
        internal static uint FileReadLongMSB(DexterFile.FileHandle file)
        {
            byte[] bytes = new byte[4];
            int read = DexterFile.FileRead(file, bytes, 0, 4, 0);
            if (read != 4)
            {
                return 0;
            }

            uint v = (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
            return GetLongMSB(v);
        }

        // DexterEndian::FileReadQuadMSB(PHYSFS_File*)
        internal static ulong FileReadQuadMSB(DexterFile.FileHandle file)
        {
            byte[] bytes = new byte[8];
            int read = DexterFile.FileRead(file, bytes, 0, 8, 0);
            if (read != 8)
            {
                return 0;
            }

            ulong v =
                (ulong)bytes[0] |
                ((ulong)bytes[1] << 8) |
                ((ulong)bytes[2] << 16) |
                ((ulong)bytes[3] << 24) |
                ((ulong)bytes[4] << 32) |
                ((ulong)bytes[5] << 40) |
                ((ulong)bytes[6] << 48) |
                ((ulong)bytes[7] << 56);

            return GetQuadMSB(v);
        }

        // DexterEndian::FileReadWordLSB(PHYSFS_File*)
        internal static ushort FileReadWordLSB(DexterFile.FileHandle file)
        {
            byte[] bytes = new byte[2];
            int read = DexterFile.FileRead(file, bytes, 0, 2, 0);
            if (read != 2)
            {
                return 0;
            }

            return (ushort)(bytes[0] | (bytes[1] << 8));
        }

        // DexterEndian::FileReadLongLSB(PHYSFS_File*)
        internal static uint FileReadLongLSB(DexterFile.FileHandle file)
        {
            byte[] bytes = new byte[4];
            int read = DexterFile.FileRead(file, bytes, 0, 4, 0);
            if (read != 4)
            {
                return 0;
            }

            return (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
        }

        // DexterEndian::FileReadQuadLSB(PHYSFS_File*)
        internal static ulong FileReadQuadLSB(DexterFile.FileHandle file)
        {
            byte[] bytes = new byte[8];
            int read = DexterFile.FileRead(file, bytes, 0, 8, 0);
            if (read != 8)
            {
                return 0;
            }

            return
                (ulong)bytes[0] |
                ((ulong)bytes[1] << 8) |
                ((ulong)bytes[2] << 16) |
                ((ulong)bytes[3] << 24) |
                ((ulong)bytes[4] << 32) |
                ((ulong)bytes[5] << 40) |
                ((ulong)bytes[6] << 48) |
                ((ulong)bytes[7] << 56);
        }

        // --------------------------------------------------------------------
        // Array helpers (FileRead/Write *Array* MSB/LSB)
        // These are pointer-free equivalents of the original loops.
        // --------------------------------------------------------------------

        // DexterEndian::FileWriteWordArrayLSB(PHYSFS_File*, unsigned short const*, unsigned int)
        internal static void FileWriteWordArrayLSB(DexterFile.FileHandle file, ushort[] values, uint count)
        {
            if (values == null || count == 0)
            {
                return;
            }

            uint n = count;
            if (n > (uint)values.Length)
            {
                n = (uint)values.Length;
            }

            byte[] bytes = new byte[checked((int)n) * 2];
            int o = 0;
            for (uint i = 0; i < n; i++)
            {
                ushort v = values[i];
                bytes[o] = (byte)v;
                bytes[o + 1] = (byte)(v >> 8);
                o += 2;
            }

            _ = DexterFile.FileWrite(file, bytes, 0, bytes.Length);
        }

        // DexterEndian::FileWriteLongArrayLSB(PHYSFS_File*, unsigned int const*, unsigned int)
        internal static void FileWriteLongArrayLSB(DexterFile.FileHandle file, uint[] values, uint count)
        {
            if (values == null || count == 0)
            {
                return;
            }

            uint n = count;
            if (n > (uint)values.Length)
            {
                n = (uint)values.Length;
            }

            byte[] bytes = new byte[checked((int)n) * 4];
            int o = 0;
            for (uint i = 0; i < n; i++)
            {
                uint v = values[i];
                bytes[o] = (byte)v;
                bytes[o + 1] = (byte)(v >> 8);
                bytes[o + 2] = (byte)(v >> 16);
                bytes[o + 3] = (byte)(v >> 24);
                o += 4;
            }

            _ = DexterFile.FileWrite(file, bytes, 0, bytes.Length);
        }

        // DexterEndian::FileWriteWordArrayMSB(PHYSFS_File*, unsigned short const*, unsigned int)
        internal static void FileWriteWordArrayMSB(DexterFile.FileHandle file, ushort[] values, uint count)
        {
            if (values == null || count == 0)
            {
                return;
            }

            uint n = count;
            if (n > (uint)values.Length)
            {
                n = (uint)values.Length;
            }

            byte[] bytes = new byte[checked((int)n) * 2];
            int o = 0;
            for (uint i = 0; i < n; i++)
            {
                ushort v = GetWordMSB(values[i]);
                bytes[o] = (byte)v;
                bytes[o + 1] = (byte)(v >> 8);
                o += 2;
            }

            _ = DexterFile.FileWrite(file, bytes, 0, bytes.Length);
        }

        // DexterEndian::FileWriteLongArrayMSB(PHYSFS_File*, unsigned int const*, unsigned int)
        internal static void FileWriteLongArrayMSB(DexterFile.FileHandle file, uint[] values, uint count)
        {
            if (values == null || count == 0)
            {
                return;
            }

            uint n = count;
            if (n > (uint)values.Length)
            {
                n = (uint)values.Length;
            }

            byte[] bytes = new byte[checked((int)n) * 4];
            int o = 0;
            for (uint i = 0; i < n; i++)
            {
                uint v = GetLongMSB(values[i]);
                bytes[o] = (byte)v;
                bytes[o + 1] = (byte)(v >> 8);
                bytes[o + 2] = (byte)(v >> 16);
                bytes[o + 3] = (byte)(v >> 24);
                o += 4;
            }

            _ = DexterFile.FileWrite(file, bytes, 0, bytes.Length);
        }

        // DexterEndian::FileReadWordArrayLSB(PHYSFS_File*, unsigned short*, unsigned int)
        internal static void FileReadWordArrayLSB(DexterFile.FileHandle file, ushort[] destination, uint count)
        {
            if (destination == null || count == 0)
            {
                return;
            }

            uint n = count;
            if (n > (uint)destination.Length)
            {
                n = (uint)destination.Length;
            }

            byte[] bytes = new byte[checked((int)n) * 2];
            int read = DexterFile.FileRead(file, bytes, 0, bytes.Length, 0);
            int items = read / 2;

            int o = 0;
            for (int i = 0; i < items; i++)
            {
                destination[i] = (ushort)(bytes[o] | (bytes[o + 1] << 8));
                o += 2;
            }
        }

        // DexterEndian::FileReadLongArrayLSB(PHYSFS_File*, unsigned int*, unsigned int)
        internal static void FileReadLongArrayLSB(DexterFile.FileHandle file, uint[] destination, uint count)
        {
            if (destination == null || count == 0)
            {
                return;
            }

            uint n = count;
            if (n > (uint)destination.Length)
            {
                n = (uint)destination.Length;
            }

            byte[] bytes = new byte[checked((int)n) * 4];
            int read = DexterFile.FileRead(file, bytes, 0, bytes.Length, 0);
            int items = read / 4;

            int o = 0;
            for (int i = 0; i < items; i++)
            {
                destination[i] = (uint)(bytes[o] | (bytes[o + 1] << 8) | (bytes[o + 2] << 16) | (bytes[o + 3] << 24));
                o += 4;
            }
        }

        // DexterEndian::FileReadWordArrayMSB(PHYSFS_File*, unsigned short*, unsigned int)
        internal static void FileReadWordArrayMSB(DexterFile.FileHandle file, ushort[] destination, uint count)
        {
            if (destination == null || count == 0)
            {
                return;
            }

            uint n = count;
            if (n > (uint)destination.Length)
            {
                n = (uint)destination.Length;
            }

            byte[] bytes = new byte[checked((int)n) * 2];
            int read = DexterFile.FileRead(file, bytes, 0, bytes.Length, 0);
            int items = read / 2;

            int o = 0;
            for (int i = 0; i < items; i++)
            {
                ushort v = (ushort)(bytes[o] | (bytes[o + 1] << 8));
                destination[i] = GetWordMSB(v);
                o += 2;
            }
        }

        // DexterEndian::FileReadLongArrayMSB(PHYSFS_File*, unsigned int*, unsigned int)
        internal static void FileReadLongArrayMSB(DexterFile.FileHandle file, uint[] destination, uint count)
        {
            if (destination == null || count == 0)
            {
                return;
            }

            uint n = count;
            if (n > (uint)destination.Length)
            {
                n = (uint)destination.Length;
            }

            byte[] bytes = new byte[checked((int)n) * 4];
            int read = DexterFile.FileRead(file, bytes, 0, bytes.Length, 0);
            int items = read / 4;

            int o = 0;
            for (int i = 0; i < items; i++)
            {
                uint v = (uint)(bytes[o] | (bytes[o + 1] << 8) | (bytes[o + 2] << 16) | (bytes[o + 3] << 24));
                destination[i] = GetLongMSB(v);
                o += 4;
            }
        }
    }
}