using OpenVikings.Dexter;

namespace OpenVikings.NXBasics
{
    internal sealed class CFile : IDisposable
    {
        // Replaces: private IntPtr _fileHandle;
        private DexterFile.FileHandle _fileHandle;

        // Library support (container stream + relative addressing)
        private long _libraryBaseOffset;

        // this + 0x10 (char buffer)
        private readonly byte[] _fileName;

        // this + 0x114
        private int _libraryId;

        private bool _disposed;

        // ===== Static "globals" (DAT_...) =====

        // System path name slots: 0..4
        private static bool _systemPathUsed0;
        private static bool _systemPathUsed1;
        private static bool _systemPathUsed2;
        private static bool _systemPathUsed3;
        private static bool _systemPathUsed4;

        private static readonly byte[][] _systemPathKeys =
        [
            new byte[0x210], new byte[0x210], new byte[0x210], new byte[0x210], new byte[0x210]
        ];

        private static readonly byte[][] _systemPathValues =
        [
            new byte[0x210], new byte[0x210], new byte[0x210], new byte[0x210], new byte[0x210]
        ];

        private static readonly int[] _systemPathKeyLengths = new int[5];

        // Additional load paths: 0..4 (each 0x105 bytes)
        private static bool _addPathUsed0;
        private static bool _addPathUsed1;
        private static bool _addPathUsed2;
        private static bool _addPathUsed3;
        private static bool _addPathUsed4;

        private static readonly byte[][] _additionalLoadPaths =
        [
            new byte[0x105], new byte[0x105], new byte[0x105], new byte[0x105], new byte[0x105]
        ];

        // File libraries: 0..4
        private static CSimpleFileLibrary _fileLibrary0;
        private static CSimpleFileLibrary _fileLibrary1;
        private static CSimpleFileLibrary _fileLibrary2;
        private static CSimpleFileLibrary _fileLibrary3;
        private static CSimpleFileLibrary _fileLibrary4;

        // ===== ctor / init =====

        private readonly string _resolvedPath;

        // NXBasics::CFile::CFile(char const*, bool)
        internal CFile(string fileName, bool openNow)
        {
            _resolvedPath = fileName;

            _fileName = new byte[0x110]; // at least 0x103 used; ctor bzero covers 0x110 bytes from +8
            L_InitObject();

            // DexterString::StringCopy((char *)(this+0x10), param_1, 0x103)
            DexterString.StringCopy(_fileName, fileName, 0x103);

            // If a system path slot matches the key, prefix the path accordingly.
            // This block is the 1:1 translation of the mStaticVars/DAT_... cascade.
            // Original uses LowStringCompare on C buffers. We keep the same idea.

            int systemId = FileSystem_SystemPathName_GetId(fileName);
            if (systemId != -1)
            {
                // puVar13 points to stored key-length; lVar10 is the id
                int keyLen = _systemPathKeyLengths[systemId];

                // DexterString::StringCopy(dest, systemPathValue[id])
                DexterString.StringCopy(_fileName, _systemPathValues[systemId]);

                // DexterString::StringAttach(dest, param_1 + keyLen)
                string suffix = fileName[keyLen..];
                DexterString.StringAttach(_fileName, suffix);
            }

            // Normalize:
            // - If string starts with ".\" ('.' and '\'), remove the dot
            // - Remove duplicate '\\' sequences by shifting left one char for each occurrence
            int len = DexterString.StringLength(_fileName);
            if (len > 2)
            {
                if (_fileName[0] == (byte)'.' && _fileName[1] == (byte)'\\')
                {
                    // memmove(pCVar1, this+0x12, len-1)
                    // => shift left by 1: copy from index 1 to index 0, count len-1 bytes
                    DexterMemory.MemMove(_fileName, 0, _fileName, 1, len - 1);
                    // String is now shorter by 1 logically; original adjusts sVar11 similarly
                    len--;
                }

                // The original code does an in-place compaction when it finds "\\"
                // It iterates and shifts left everything after the first backslash by 1.
                int i = 1;
                int last = len - 1;

                while (i < last)
                {
                    if (_fileName[i] == (byte)'\\' && _fileName[i + 1] == (byte)'\\')
                    {
                        // shift left from i+1 to end (inclusive) by 1
                        int srcIndex = i + 1;
                        int count = len - srcIndex;
                        DexterMemory.MemMove(_fileName, i, _fileName, srcIndex, count);

                        len--;
                        last = len - 1;

                        // original does iVar9 = iVar9-1 then continues
                        i--;
                    }

                    i++;
                }
            }

            if (openNow)
            {
                OpenForReading(true);
            }
        }

        // NXBasics::CFile::l_InitObject()
        internal void L_InitObject()
        {
            _fileHandle = new DexterFile.FileHandle(0);

            DexterMemory.MemorySet(_fileName, 0, _fileName.Length);

            _libraryId = -1;
        }

        // ===== dtor / close =====

        // NXBasics::CFile::Close()
        internal void Close()
        {
            if (_fileHandle.IsValid)
            {
                DexterFile.FileClose(_fileHandle);
                _fileHandle = new DexterFile.FileHandle(0);
                _libraryId = -1;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Close();

            _disposed = true;
        }

        // ===== helpers =====

        // NXBasics::CFile::FileSystem_Tool_IsDriveIncluded(char const*)
        internal static bool FileSystem_Tool_IsDriveIncluded(string path)
        {
            if (path != null && path.Length > 1)
            {
                return path[1] == ':';
            }

            return false;
        }

        // ===== reading / writing =====

        // NXBasics::CFile::OpenForReading(bool, bool)
        internal ulong OpenForReading(bool param2)
        {
            if (_fileHandle.IsValid)
            {
                DexterFile.FileClose(_fileHandle);
                _fileHandle = new DexterFile.FileHandle(0);
                _libraryId = -1;
            }

            int len = DexterString.StringLength(_fileName);

            bool bVar2;
            byte uVar4;

            int savPos = DexterString.StringSearch(_fileName, ".sav", true);
            int iniPos = DexterString.StringSearch(_fileName, ".ini", true);

            if (savPos == len - 4 || iniPos == len - 4)
            {
                uVar4 = (byte)' ';
                bVar2 = false;
            }
            else
            {
                bVar2 = true;
                uVar4 = 0;
            }

            string nameAsString = DexterString.StringToString(_fileName);

            if (DexterFile.FileExists(nameAsString, uVar4))
            {
                _fileHandle = DexterFile.FileOpen(nameAsString, uVar4);
                if (_fileHandle.IsValid)
                {
                    return 1;
                }
            }

            if (!bVar2)
            {
                uVar4 = (byte)(uVar4 + 0xE0);

                if (DexterFile.FileExists(nameAsString, uVar4))
                {
                    _fileHandle = DexterFile.FileOpen(nameAsString, uVar4);
                    if (_fileHandle.IsValid)
                    {
                        return 1;
                    }
                }
            }

            if (len < 2 || _fileName[1] != (byte)':')
            {
                if (param2)
                {
                    byte[] original = new byte[0x110];
                    Buffer.BlockCopy(_fileName, 0, original, 0, _fileName.Length);

                    if (TryOpenWithAdditionalLoadPaths(uVar4, original))
                    {
                        return 1;
                    }

                    Buffer.BlockCopy(original, 0, _fileName, 0, _fileName.Length);
                }
                else
                {
                    if (TryOpenFromLibraries(uVar4))
                    {
                        return 1;
                    }
                }
            }

            return 0;
        }

        private bool TryOpenWithAdditionalLoadPaths(byte mode, byte[] originalName)
        {
            if (_addPathUsed0 && TryOpenPrefixed(_additionalLoadPaths[0], originalName, mode)) return true;
            if (_addPathUsed1 && TryOpenPrefixed(_additionalLoadPaths[1], originalName, mode)) return true;
            if (_addPathUsed2 && TryOpenPrefixed(_additionalLoadPaths[2], originalName, mode)) return true;
            if (_addPathUsed3 && TryOpenPrefixed(_additionalLoadPaths[3], originalName, mode)) return true;
            if (_addPathUsed4 && TryOpenPrefixed(_additionalLoadPaths[4], originalName, mode)) return true;

            return false;
        }

        private bool TryOpenPrefixed(byte[] prefix, byte[] fileNameBytes, byte mode)
        {
            DexterString.StringCopy(_fileName, prefix);
            DexterString.StringAttach(_fileName, fileNameBytes);

            string combined = DexterString.StringToString(_fileName);

            if (DexterFile.FileExists(combined, mode))
            {
                _fileHandle = DexterFile.FileOpen(combined, mode);
                return _fileHandle.IsValid;
            }

            return false;
        }

        private bool TryOpenFromLibraries(byte mode)
        {
            CSimpleFileLibrary library;
            int libraryIndex;

            if (_fileLibrary0 != null && CSimpleFileLibrary.IsFileInLibrary(_fileLibrary0, _fileName))
            {
                library = _fileLibrary0;
                libraryIndex = 0;
            }
            else if (_fileLibrary1 != null && CSimpleFileLibrary.IsFileInLibrary(_fileLibrary1, _fileName))
            {
                library = _fileLibrary1;
                libraryIndex = 1;
            }
            else if (_fileLibrary2 != null && CSimpleFileLibrary.IsFileInLibrary(_fileLibrary2, _fileName))
            {
                library = _fileLibrary2;
                libraryIndex = 2;
            }
            else if (_fileLibrary3 != null && CSimpleFileLibrary.IsFileInLibrary(_fileLibrary3, _fileName))
            {
                library = _fileLibrary3;
                libraryIndex = 3;
            }
            else if (_fileLibrary4 != null && CSimpleFileLibrary.IsFileInLibrary(_fileLibrary4, _fileName))
            {
                library = _fileLibrary4;
                libraryIndex = 4;
            }
            else
            {
                return false;
            }

            if (library.File == null)
            {
                _fileHandle = new DexterFile.FileHandle(0);
                return false;
            }

            string libraryContainerPath = DexterString.StringToString(library.File.GetFileNameBytesNullTerminated());

            if (!DexterFile.FileExists(libraryContainerPath, mode))
            {
                _fileHandle = new DexterFile.FileHandle(0);
                return false;
            }

            _fileHandle = DexterFile.FileOpen(libraryContainerPath, mode);
            if (!_fileHandle.IsValid)
            {
                return false;
            }

            int pos = unchecked(CSimpleFileLibrary.GetFileInLibraryPosition(library, _fileName));
            DexterFile.FileSeek(_fileHandle, pos, 0);

            _libraryId = libraryIndex;
            return true;
        }

        private static CSimpleFileLibrary GetLibraryByIndex(int index)
        {
            if (index == 0) return _fileLibrary0;
            if (index == 1) return _fileLibrary1;
            if (index == 2) return _fileLibrary2;
            if (index == 3) return _fileLibrary3;
            if (index == 4) return _fileLibrary4;
            return null;
        }

        // NXBasics::CFile::SeekToPosition(unsigned int)
        internal void SeekToPosition(int position)
        {
            if (!_fileHandle.IsValid)
            {
                return;
            }

            if (_libraryId != -1)
            {
                CSimpleFileLibrary lib = GetLibraryByIndex(_libraryId);
                if (lib != null)
                {
                    int offset = CSimpleFileLibrary.GetFileInLibraryPosition(lib, _fileName);
                    position += offset;
                }
            }

            DexterFile.FileSeek(_fileHandle, position, 0);
        }


        // NXBasics::CFile::OpenForWriting(bool)
        internal bool OpenForWriting()
        {
            if (_fileHandle.IsValid)
            {
                DexterFile.FileClose(_fileHandle);
                _fileHandle = new DexterFile.FileHandle(0);
                _libraryId = -1;
            }

            string nameAsString = DexterString.StringToString(_fileName);

            _fileHandle = DexterFile.FileOpen(nameAsString, (byte)'!');
            return _fileHandle.IsValid;
        }

        // NXBasics::CFile::ReadLong()
        internal uint ReadLong()
        {
            if (!_fileHandle.IsValid)
            {
                return 0;
            }

            return DexterEndian.FileReadLongLSB(_fileHandle);
        }

        // NXBasics::CFile::ReadWord()
        internal ushort ReadWord()
        {
            if (!_fileHandle.IsValid)
            {
                return 0;
            }

            return DexterEndian.FileReadWordLSB(_fileHandle);
        }

        // Mirrors: NXBasics::CFile::Read(void*, unsigned int)
        internal int Read(byte[] buffer, int size)
        {
            if (!_fileHandle.IsValid)
            {
                return 0;
            }

            return DexterFile.FileRead(_fileHandle, buffer, 0, size, 0);
        }

        // Managed helper (not in original): read into buffer starting at offset
        internal int ReadInto(byte[] buffer, int bufferOffset, int size)
        {
            if (!_fileHandle.IsValid)
            {
                return 0;
            }

            ArgumentNullException.ThrowIfNull(buffer);

            if (bufferOffset < 0 || size < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (bufferOffset > buffer.Length - size)
            {
                throw new ArgumentException("Buffer too small for requested read.");
            }

            return DexterFile.FileRead(_fileHandle, buffer, bufferOffset, size, 0);
        }

        private static bool TryReadExact(Stream stream, int size, out byte[] buffer)
        {
            buffer = new byte[size];

            int readTotal = 0;
            while (readTotal < size)
            {
                int read;
                try
                {
                    read = stream.Read(buffer, readTotal, size - readTotal);
                }
                catch
                {
                    return false;
                }

                if (read <= 0)
                {
                    return false;
                }

                readTotal += read;
            }

            return true;
        }

        // NXBasics::CFile::WriteLong(unsigned int)
        internal void WriteLong(uint value)
        {
            if (!_fileHandle.IsValid)
            {
                return;
            }

            DexterEndian.FileWriteLongLSB(_fileHandle, value);
        }

        // NXBasics::CFile::WriteWord(unsigned short)
        internal void WriteWord(ushort value)
        {
            if (!_fileHandle.IsValid)
            {
                return;
            }

            DexterEndian.FileWriteWordLSB(_fileHandle, value);
        }

        // NXBasics::CFile::Write(void const*, unsigned int)
        internal void Write(byte[] buffer, int size)
        {
            if (!_fileHandle.IsValid)
            {
                return;
            }

            _ = DexterFile.FileWrite(_fileHandle, buffer, 0, size);
        }

        // Managed helper: write a single byte
        internal void WriteByte(byte value)
        {
            if (!_fileHandle.IsValid)
            {
                return;
            }

            byte b = value;
            _ = DexterFile.FileWrite(_fileHandle, ref b, 1);
        }

        // Managed helper: write buffer region starting at offset
        internal void Write(byte[] buffer, int bufferOffset, int size)
        {
            if (!_fileHandle.IsValid)
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(buffer);

            if (bufferOffset < 0 || size < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (bufferOffset > buffer.Length - size)
            {
                throw new ArgumentException("Buffer too small for requested write.");
            }

            if (size == 0)
            {
                return;
            }

            _ = DexterFile.FileWrite(_fileHandle, buffer, bufferOffset, size);
        }

        // NXBasics::CFile::ReadFlag(bool&)
        internal void ReadFlag(out bool value)
        {
            if (!_fileHandle.IsValid)
            {
                value = false;
                return;
            }

            byte b = 0;
            _ = DexterFile.FileRead(_fileHandle, ref b, 1, 0);
            value = b != 0;
        }

        // NXBasics::CFile::WriteTrueFlag()
        internal void WriteTrueFlag()
        {
            if (!_fileHandle.IsValid)
            {
                return;
            }

            byte b = 1;
            _ = DexterFile.FileWrite(_fileHandle, ref b, 1);
        }

        // NXBasics::CFile::WriteFalseFlag()
        internal void WriteFalseFlag()
        {
            if (!_fileHandle.IsValid)
            {
                return;
            }

            byte b = 0;
            _ = DexterFile.FileWrite(_fileHandle, ref b, 1);
        }

        // NXBasics::CFile::GetPosition() const
        internal int GetPosition()
        {
            if (!_fileHandle.IsValid)
            {
                return 0;
            }

            int pos = DexterFile.FilePoss(_fileHandle);

            if (_libraryId != -1)
            {
                CSimpleFileLibrary lib = GetLibraryByIndex(_libraryId);
                if (lib != null)
                {
                    int offset = CSimpleFileLibrary.GetFileInLibraryPosition(lib, _fileName);
                    pos -= offset;
                }
            }

            return pos;
        }

        // NXBasics::CFile::GetSize() const
        internal ulong GetSize()
        {
            if (!_fileHandle.IsValid)
            {
                return 0;
            }

            if (_libraryId != -1)
            {
                CSimpleFileLibrary lib = GetLibraryByIndex(_libraryId);
                if (lib != null)
                {
                    return CSimpleFileLibrary.GetFileSize(lib, _fileName);
                }
            }

            return (ulong)DexterFile.FileSize(_fileHandle);
        }

        // NXBasics::CFile::IsEndOfFileReached() const
        internal bool IsEndOfFileReached()
        {
            int pos = GetPosition();
            int size;

            if (!_fileHandle.IsValid)
            {
                size = 0;
            }
            else
            {
                if (_libraryId == -1)
                {
                    ulong len = GetSize();
                    size = len > int.MaxValue ? int.MaxValue : (int)len;
                }
                else
                {
                    CSimpleFileLibrary lib = GetLibraryByIndex(_libraryId);
                    if (lib != null)
                    {
                        ulong len = CSimpleFileLibrary.GetFileSize(lib, _fileName);
                        size = len > int.MaxValue ? int.MaxValue : (int)len;
                    }
                    else
                    {
                        size = 0;
                    }
                }
            }

            return pos == size;
        }

        // ===== FileSystem static API =====

        // NXBasics::CFile::FileSystem_Initialize()
        internal static void FileSystem_Initialize()
        {
            // ___bzero(&mStaticVars, 0xf98) => reset all used flags & arrays
            _systemPathUsed0 = false;
            _systemPathUsed1 = false;
            _systemPathUsed2 = false;
            _systemPathUsed3 = false;
            _systemPathUsed4 = false;

            _addPathUsed0 = false;
            _addPathUsed1 = false;
            _addPathUsed2 = false;
            _addPathUsed3 = false;
            _addPathUsed4 = false;

            _fileLibrary0 = null;
            _fileLibrary1 = null;
            _fileLibrary2 = null;
            _fileLibrary3 = null;
            _fileLibrary4 = null;

            for (int i = 0; i < 5; i++)
            {
                DexterMemory.MemorySet(_systemPathKeys[i], 0, _systemPathKeys[i].Length);
                DexterMemory.MemorySet(_systemPathValues[i], 0, _systemPathValues[i].Length);
                _systemPathKeyLengths[i] = 0;

                DexterMemory.MemorySet(_additionalLoadPaths[i], 0, _additionalLoadPaths[i].Length);
            }
        }

        // NXBasics::CFile::FileSystem_SystemPathName_Set(char const*, char const*)
        internal static int FileSystem_SystemPathName_Set(string key, string value)
        {
            int id = FileSystem_SystemPathName_GetId(key);
            if (id == -1)
            {
                int slot;
                if (!_systemPathUsed0) slot = 0;
                else if (!_systemPathUsed1) slot = 1;
                else if (!_systemPathUsed2) slot = 2;
                else if (!_systemPathUsed3) slot = 3;
                else if (!_systemPathUsed4) slot = 4;
                else return -1;

                DexterString.StringCopy(_systemPathKeys[slot], key);
                DexterString.StringCopy(_systemPathValues[slot], value);

                _systemPathKeyLengths[slot] = key.Length;

                SetSystemPathUsed(slot, true);
                return slot;
            }

            DexterString.StringCopy(_systemPathValues[id], value);
            return id;
        }

        // NXBasics::CFile::FileSystem_SystemPathName_GetId(char const*)
        internal static int FileSystem_SystemPathName_GetId(string key)
        {
            if (_systemPathUsed0 && LowStringCompare(_systemPathKeys[0], key, 999) == 0) return 0;
            if (_systemPathUsed1 && LowStringCompare(_systemPathKeys[1], key, 999) == 0) return 1;
            if (_systemPathUsed2 && LowStringCompare(_systemPathKeys[2], key, 999) == 0) return 2;
            if (_systemPathUsed3 && LowStringCompare(_systemPathKeys[3], key, 999) == 0) return 3;
            if (_systemPathUsed4 && LowStringCompare(_systemPathKeys[4], key, 999) == 0) return 4;
            return -1;
        }

        // NXBasics::CFile::FileSystem_SystemPathName_Clear(char const*)
        internal static bool FileSystem_SystemPathName_Clear(string key)
        {
            int id = FileSystem_SystemPathName_GetId(key);
            if (id != -1)
            {
                SetSystemPathUsed(id, false);
            }

            return id != -1;
        }

        // NXBasics::CFile::FileSystem_AdditionalLoadPath_Add(char const*)
        internal static int FileSystem_AdditionalLoadPath_Add(string path)
        {
            if (path == null || DexterString.StringLength(path) == 0)
            {
                return -1;
            }

            int slot;
            if (!_addPathUsed0) slot = 0;
            else if (!_addPathUsed1) slot = 1;
            else if (!_addPathUsed2) slot = 2;
            else if (!_addPathUsed3) slot = 3;
            else if (!_addPathUsed4) slot = 4;
            else return -1;

            DexterString.StringCopy(_additionalLoadPaths[slot], path);

            int length = DexterString.StringLength(_additionalLoadPaths[slot]);
            if (length > 0)
            {
                if (_additionalLoadPaths[slot][length - 1] != (byte)'\\')
                {
                    DexterString.StringAttach(_additionalLoadPaths[slot], "\\");
                }
            }

            SetAdditionalPathUsed(slot, true);
            return slot;
        }

        // NXBasics::CFile::FileSystem_AdditionalLoadPath_Remove(char const*)
        internal static ulong FileSystem_AdditionalLoadPath_Remove(string path)
        {
            if (_addPathUsed0 && LowStringCompare(_additionalLoadPaths[0], path, 999) == 0) { _addPathUsed0 = false; return 0; }
            if (_addPathUsed1 && LowStringCompare(_additionalLoadPaths[1], path, 999) == 0) { _addPathUsed1 = false; return 1; }
            if (_addPathUsed2 && LowStringCompare(_additionalLoadPaths[2], path, 999) == 0) { _addPathUsed2 = false; return 2; }
            if (_addPathUsed3 && LowStringCompare(_additionalLoadPaths[3], path, 999) == 0) { _addPathUsed3 = false; return 3; }
            if (_addPathUsed4 && LowStringCompare(_additionalLoadPaths[4], path, 999) == 0) { _addPathUsed4 = false; return 4; }
            return 0xFFFFFFFF;
        }

        // NXBasics::CFile::FileSystem_FileLibrary_Add(NXBasics::CSimpleFileLibrary const*)
        internal static int FileSystem_FileLibrary_Add(CSimpleFileLibrary library)
        {
            if (library == null)
            {
                return -1;
            }

            if (_fileLibrary0 == null) { _fileLibrary0 = library; return 0; }
            if (_fileLibrary1 == null) { _fileLibrary1 = library; return 1; }
            if (_fileLibrary2 == null) { _fileLibrary2 = library; return 2; }
            if (_fileLibrary3 == null) { _fileLibrary3 = library; return 3; }
            if (_fileLibrary4 == null) { _fileLibrary4 = library; return 4; }

            return -1;
        }

        // NXBasics::CFile::FileSystem_FileLibrary_Remove(NXBasics::CSimpleFileLibrary const*)
        internal static int FileSystem_FileLibrary_Remove(CSimpleFileLibrary library)
        {
            if (library == null)
            {
                return -1;
            }

            if (_fileLibrary0 == library) { _fileLibrary0 = null; return 0; }
            if (_fileLibrary1 == library) { _fileLibrary1 = null; return 1; }
            if (_fileLibrary2 == library) { _fileLibrary2 = null; return 2; }
            if (_fileLibrary3 == library) { _fileLibrary3 = null; return 3; }
            if (_fileLibrary4 == library) { _fileLibrary4 = null; return 4; }

            return -1;
        }

        // NXBasics::CFile::FileSystem_Tool_CreateDirectory(char const*)
        internal static void FileSystem_Tool_CreateDirectory(string path)
        {
            int firstSlash = path.IndexOf('\\');
            if (firstSlash < 0)
            {
                DexterFile.MakeDir(path, (byte)' ');
                return;
            }

            int level = 1;

            while (true)
            {
                string temp = path;
                int idx = -1;
                for (int i = 0; i < level; i++)
                {
                    int start = temp.IndexOf('\\', idx + 1);
                    idx = start;
                    if (idx < 0)
                    {
                        DexterFile.MakeDir(path, (byte)' ');
                        return;
                    }
                }

                string prefix = path[..idx];
                DexterFile.MakeDir(prefix, (byte)' ');

                level++;
            }
        }

        // NXBasics::CFile::FileSystem_Tool_FileExists(char const*, bool)
        internal static bool FileSystem_Tool_FileExists(string fileName, bool param2)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            using CFile file = new(fileName, false);
            ulong result = file.OpenForReading(param2);
            file.Close();
            return result != 0;
        }

        internal byte[] GetFileNameBytesNullTerminated()
        {
            int len = DexterString.StringLength(_fileName);
            byte[] nt = new byte[len + 1];

            if (len > 0)
            {
                Buffer.BlockCopy(_fileName, 0, nt, 0, len);
            }

            nt[len] = 0;
            return nt;
        }

        private static void SetSystemPathUsed(int index, bool used)
        {
            if (index == 0) _systemPathUsed0 = used;
            else if (index == 1) _systemPathUsed1 = used;
            else if (index == 2) _systemPathUsed2 = used;
            else if (index == 3) _systemPathUsed3 = used;
            else if (index == 4) _systemPathUsed4 = used;
        }

        private static void SetAdditionalPathUsed(int index, bool used)
        {
            if (index == 0) _addPathUsed0 = used;
            else if (index == 1) _addPathUsed1 = used;
            else if (index == 2) _addPathUsed2 = used;
            else if (index == 3) _addPathUsed3 = used;
            else if (index == 4) _addPathUsed4 = used;
        }

        private static int LowStringCompare(byte[] leftNullTerminated, string right, int maxLen)
        {
            if (leftNullTerminated == null && right == null) return 0;
            if (leftNullTerminated == null) return -1;
            if (right == null) return 1;

            int i = 0;
            int rightLen = right.Length;

            while (i < maxLen)
            {
                byte lb = i < leftNullTerminated.Length ? leftNullTerminated[i] : (byte)0;
                char rc = i < rightLen ? right[i] : '\0';

                if (lb == 0 && rc == '\0')
                {
                    return 0;
                }

                char lc = (char)lb;

                // case-insensitive ASCII compare like typical engine low-string compares
                if (lc >= 'A' && lc <= 'Z') lc = (char)(lc + 32);
                if (rc >= 'A' && rc <= 'Z') rc = (char)(rc + 32);

                if (lc != rc)
                {
                    return lc < rc ? -1 : 1;
                }

                i++;
            }

            return 0;
        }
    }
}