using System.Buffers.Binary;

namespace OpenVikings
{
    internal static class DexterEndian
    {
        internal static uint FileReadLongLSB(IntPtr fileHandle)
        {
            byte[] buffer = new byte[4];
            ReadExactly(fileHandle, buffer, 4);
            return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }

        internal static ushort FileReadWordLSB(IntPtr fileHandle)
        {
            byte[] buffer = new byte[2];
            ReadExactly(fileHandle, buffer, 2);
            return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        }

        internal static void FileWriteLongLSB(IntPtr fileHandle, uint value)
        {
            byte[] buffer = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
            WriteExactly(fileHandle, buffer, 4);
        }

        internal static void FileWriteWordLSB(IntPtr fileHandle, ushort value)
        {
            byte[] buffer = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
            WriteExactly(fileHandle, buffer, 2);
        }

        private static void ReadExactly(IntPtr fileHandle, byte[] buffer, int count)
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

        private static void WriteExactly(IntPtr fileHandle, byte[] buffer, int count)
        {
            int written = DexterFile.FileWrite(fileHandle, buffer, 0, count);
            if (written != count)
            {
                throw new IOException("Failed to write requested number of bytes.");
            }
        }
    }
}