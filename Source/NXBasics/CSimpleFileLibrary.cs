namespace OpenVikings.NXBasics
{
    internal sealed class CSimpleFileLibrary : IDisposable
    {
        private struct GroupEntry
        {
            internal string Name;
            internal uint Value;
        }

        private struct FileEntry
        {
            internal string FileName;
            internal byte Checksum;
            internal uint Position;
            internal uint Size;
        }

        private readonly CFile _file;
        private uint _fileCount;
        private FileEntry[] _files;

        private uint _groupCount;
        private GroupEntry[] _groups;

        private bool _disposed;

        internal CSimpleFileLibrary(string path)
        {
            _file = new CFile(path, true);

            _fileCount = 0;
            _files = [];
            _groupCount = 0;
            _groups = [];
            _disposed = false;

            ReadUInt32(_file);

            _groupCount = ReadUInt32(_file);
            _fileCount = ReadUInt32(_file);

            if (_groupCount != 0)
            {
                _groups = new GroupEntry[_groupCount];

                for (uint i = 0; i < _groupCount; i++)
                {
                    uint nameLen = ReadUInt32(_file);
                    string name = ReadAsciiStringExact(_file, nameLen);

                    uint value = ReadUInt32(_file);

                    GroupEntry entry;
                    entry.Name = name;
                    entry.Value = value;
                    _groups[i] = entry;
                }
            }

            if (_fileCount != 0)
            {
                _files = new FileEntry[_fileCount];

                for (uint i = 0; i < _fileCount; i++)
                {
                    uint nameLen = ReadUInt32(_file);
                    string fileName = ReadAsciiStringExact(_file, nameLen);

                    byte checksum = CalculateFilenameChecksum(fileName);

                    uint position = ReadUInt32(_file);
                    uint size = ReadUInt32(_file);

                    FileEntry entry;
                    entry.FileName = fileName;
                    entry.Checksum = checksum;
                    entry.Position = position;
                    entry.Size = size;
                    _files[i] = entry;
                }
            }
        }

        internal CFile File
        {
            get
            {
                return _file;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _file.Dispose();
        }

        internal static byte CalculateFilenameChecksum(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return 0;
            }

            byte sum = 0;

            for (int i = 0; i < fileName.Length; i++)
            {
                char lower = ToLowerAscii(fileName[i]);
                unchecked
                {
                    sum = (byte)(sum + (byte)lower);
                }
            }

            return sum;
        }

        internal static byte CalculateFilenameChecksum(byte[] fileName)
        {
            if (fileName == null || fileName.Length == 0)
            {
                return 0;
            }

            int len = GetNullTerminatedLength(fileName);
            if (len == 0)
            {
                return 0;
            }

            byte sum = 0;

            for (int i = 0; i < len; i++)
            {
                byte b = fileName[i];

                if (b >= (byte)'A' && b <= (byte)'Z')
                {
                    b = (byte)(b + 32);
                }

                unchecked
                {
                    sum = (byte)(sum + b);
                }
            }

            return sum;
        }

        internal static bool IsFileInLibrary(CSimpleFileLibrary library, byte[] fileName)
        {
            if (library == null)
            {
                return false;
            }

            return library.GetEntryId(fileName) != uint.MaxValue;
        }

        internal uint GetEntryId(string fileName)
        {
            if (_fileCount == 0 || _files == null || _files.Length == 0 || string.IsNullOrEmpty(fileName))
            {
                return uint.MaxValue;
            }

            byte checksum = CalculateFilenameChecksum(fileName);

            for (uint i = 0; i < _fileCount; i++)
            {
                if (_files[i].Checksum != checksum)
                {
                    continue;
                }

                if (LowStringEquals(fileName, _files[i].FileName, 999))
                {
                    return i;
                }
            }

            return uint.MaxValue;
        }

        internal uint GetEntryId(byte[] fileName)
        {
            if (_fileCount == 0 || _files == null || _files.Length == 0 || fileName == null || fileName.Length == 0)
            {
                return uint.MaxValue;
            }

            byte checksum = CalculateFilenameChecksum(fileName);

            for (uint i = 0; i < _fileCount; i++)
            {
                if (_files[i].Checksum != checksum)
                {
                    continue;
                }

                if (LowBytesEqualsAsciiIgnoreCase(fileName, _files[i].FileName, 999))
                {
                    return i;
                }
            }

            return uint.MaxValue;
        }

        internal uint GetFileSize(byte[] fileName)
        {
            uint id = GetEntryId(fileName);
            if (id == uint.MaxValue)
            {
                return 0;
            }

            return _files[id].Size;
        }

        internal static uint GetFileSize(CSimpleFileLibrary library, byte[] fileName)
        {
            if (library == null)
            {
                return 0;
            }

            return library.GetFileSize(fileName);
        }

        internal uint GetFileInLibraryPosition(byte[] fileName)
        {
            uint id = GetEntryId(fileName);
            if (id == uint.MaxValue)
            {
                return 0;
            }

            return _files[id].Position;
        }

        internal static int GetFileInLibraryPosition(CSimpleFileLibrary library, byte[] fileName)
        {
            if (library == null)
            {
                return 0;
            }

            return (int)library.GetFileInLibraryPosition(fileName);
        }

        internal bool LoadFileOutOfLibrary(string fileName, byte[] destination, uint destinationCapacity)
        {
            if (destination == null)
            {
                return false;
            }

            uint id = GetEntryId(fileName);
            if (id == uint.MaxValue)
            {
                return false;
            }

            uint size = _files[id].Size;

            if (size > destinationCapacity)
            {
                return false;
            }

            if (size > int.MaxValue)
            {
                return false;
            }

            int sizeInt = (int)size;
            if (destination.Length < sizeInt)
            {
                return false;
            }

            _file.SeekToPosition(unchecked((int)_files[id].Position));

            int read = _file.Read(destination, sizeInt);
            return read == sizeInt;
        }

        internal bool LoadFileOutOfLibrary(byte[] fileName, byte[] destination, uint destinationCapacity)
        {
            if (destination == null)
            {
                return false;
            }

            uint id = GetEntryId(fileName);
            if (id == uint.MaxValue)
            {
                return false;
            }

            uint size = _files[id].Size;

            if (size > destinationCapacity)
            {
                return false;
            }

            if (size > int.MaxValue)
            {
                return false;
            }

            int sizeInt = (int)size;
            if (destination.Length < sizeInt)
            {
                return false;
            }

            _file.SeekToPosition(unchecked((int)_files[id].Position));

            int read = _file.Read(destination, sizeInt);
            return read == sizeInt;
        }

        internal bool SeekToFileInLibrary(string fileName)
        {
            uint id = GetEntryId(fileName);
            if (id == uint.MaxValue)
            {
                return false;
            }

            _file.SeekToPosition(unchecked((int)_files[id].Position));
            return true;
        }

        internal bool SeekToFileInLibrary(byte[] fileName)
        {
            uint id = GetEntryId(fileName);
            if (id == uint.MaxValue)
            {
                return false;
            }

            _file.SeekToPosition(unchecked((int)_files[id].Position));
            return true;
        }

        private static uint ReadUInt32(CFile file)
        {
            return file.ReadLong();
        }

        private static string ReadAsciiStringExact(CFile file, uint length)
        {
            if (length == 0)
            {
                return string.Empty;
            }

            if (length > int.MaxValue)
            {
                throw new InvalidOperationException("String length exceeds supported managed size.");
            }

            byte[] buffer = new byte[(int)length];
            int read = file.Read(buffer, (int)length);
            if (read != (int)length)
            {
                throw new InvalidOperationException("Unexpected end of file while reading ASCII string.");
            }

            return System.Text.Encoding.ASCII.GetString(buffer);
        }

        private static char ToLowerAscii(char c)
        {
            if (c >= 'A' && c <= 'Z')
            {
                return (char)(c + 32);
            }

            return c;
        }

        private static bool LowStringEquals(string a, string b, int maxChars)
        {
            if (a == null || b == null)
            {
                return false;
            }

            int limit = a.Length < b.Length ? a.Length : b.Length;
            if (limit > maxChars)
            {
                limit = maxChars;
            }

            for (int i = 0; i < limit; i++)
            {
                char ca = ToLowerAscii(a[i]);
                char cb = ToLowerAscii(b[i]);

                if (ca != cb)
                {
                    return false;
                }
            }

            return a.Length == b.Length;
        }

        private static int GetNullTerminatedLength(byte[] bytes)
        {
            int len = bytes.Length;

            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == 0)
                {
                    len = i;
                    break;
                }
            }

            return len;
        }

        private static bool LowBytesEqualsAsciiIgnoreCase(byte[] a, string b, int maxChars)
        {
            if (a == null || b == null)
            {
                return false;
            }

            int aLen = GetNullTerminatedLength(a);
            int bLen = b.Length;

            int limit = aLen < bLen ? aLen : bLen;
            if (limit > maxChars)
            {
                limit = maxChars;
            }

            for (int i = 0; i < limit; i++)
            {
                byte ba = a[i];
                char cb = b[i];

                if (ba >= (byte)'A' && ba <= (byte)'Z')
                {
                    ba = (byte)(ba + 32);
                }

                char lowerB = ToLowerAscii(cb);

                if (ba != (byte)lowerB)
                {
                    return false;
                }
            }

            return aLen == bLen;
        }
    }
}