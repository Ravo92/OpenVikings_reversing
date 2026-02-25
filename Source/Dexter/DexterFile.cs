using OpenVikings.Dexter.Struct;

namespace OpenVikings.Dexter
{
    // Managed port of the shown DexterFile pseudo code.
    // Notes:
    // - No unsafe/IntPtr: file handles are represented as object references (whatever OSGeneric returns).
    // - The original code uses a fixed-size "FileList" array (1000 entries) and swap-removes on close.
    // - The original has optional per-file buffering for write/read. This port keeps the same idea with byte[] buffers.
    internal static class DexterFile
    {
        private static OSGeneric? _osGeneric;

        internal static void Bind(OSGeneric osGeneric)
        {
            ArgumentNullException.ThrowIfNull(osGeneric);
            _osGeneric = osGeneric;
        }

        private static OSGeneric OS
        {
            get
            {
                if (_osGeneric == null)
                {
                    throw new InvalidOperationException("DexterFile is not bound. Call DexterFile.Bind(osGeneric) during startup.");
                }

                return _osGeneric;
            }
        }

        // --------------------------------------------------------------------
        // File list (C++: FileList, FileListSize, FileListUsed)
        // --------------------------------------------------------------------

        private const int MaxOpenFiles = 1000;

        private static readonly object _fileListLock = new();

        private static FileEntry[]? _fileList;
        private static int _fileListSize;
        private static int _fileListUsed;

        // C++: DefaultFileBuffer
        private static int _defaultFileBuffer;

        // C++: ContentPath, StoragePath (were char buffers; here strings)
        private static string _contentPath = string.Empty;
        private static string _storagePath = string.Empty;

        // C++: _fopen_count
        private static int _fopenCount;

        // --------------------------------------------------------------------
        // XML storage (C++: XMLData / DAT_... arrays + XMLEntries)
        // --------------------------------------------------------------------

        private const int MaxXmlEntries = 100;
        private static readonly string?[] _xmlKeys = new string?[MaxXmlEntries];
        private static readonly string?[] _xmlValues = new string?[MaxXmlEntries];
        private static readonly bool[] _xmlUsed = new bool[MaxXmlEntries];
        private static int _xmlEntries;

        // --------------------------------------------------------------------
        // Public-ish API mirroring the pseudo code
        // --------------------------------------------------------------------

        internal static void FileSystemShutDown()
        {
            EnsureFileListInitialized();

            // Close all open files that are not mode == '\b' (8).
            // The C++ logic decrements the index after close because close swap-removes the last entry.
            if (_fileListUsed > 0)
            {
                int i = 0;
                while (i < _fileListUsed)
                {
                    FileEntry entry = _fileList![i];
                    if (entry.Mode != 8 && entry.Handle.IsValid)
                    {
                        FileClose(entry.Handle);
                        i--; // compensate for swap-remove
                    }

                    i++;
                }
            }
        }

        internal static void FileClose(FileHandle file)
        {
            DexterOS.MutexLock();
            try
            {
                EnsureFileListInitialized();

                if (!file.IsValid || _fileListUsed == 0)
                {
                    return;
                }

                int index = FindFileIndexByHandle(file);
                if (index < 0)
                {
                    return;
                }

                FileEntry entry = _fileList![index];

                // C++: if mode != '\b' then debug + decrement fopen_count and flush buffered writes if mode == 1
                if (entry.Mode != 8)
                {
                    DexterDebug.CheckDebugMode(8);
                    _fopenCount--;

                    // Mode == 1 => buffered write file in the original.
                    if (entry.Mode == 1 && entry.Buffer != null && entry.BufferCount > 0)
                    {
                        // Write the buffered bytes [0..BufferCount)
                        OS.SystemFileWrite(entry.Handle!, entry.Buffer, 0, entry.BufferCount);

                        entry.BufferOffset = -1;
                        entry.BufferCount = 0;
                    }
                }

                // Free buffer
                entry.Buffer = null;
                entry.BufferSize = 0;
                entry.BufferCount = 0;
                entry.BufferOffset = -1;

                // Close underlying file
                OS.SystemFileClose(entry.Handle!);

                // Swap-remove last into this slot
                _fileListUsed--;
                if (_fileListUsed > 0 && index != _fileListUsed)
                {
                    _fileList[index] = _fileList[_fileListUsed];
                }

                // Clear last slot
                _fileList[_fileListUsed] = FileEntry.CreateEmpty();
            }
            finally
            {
                DexterOS.MutexUnLock();
            }
        }

        internal static bool FileExists(string path, byte modeFlags)
        {
            if (path == null)
            {
                return false;
            }

            string newPath = path;

            // If (flags & 0x26) == 0 => ContentPath, else StoragePath
            if ((modeFlags & 0x26) == 0)
            {
                if (!string.IsNullOrEmpty(_contentPath))
                {
                    newPath = InsertBasePathIfMissing(newPath, _contentPath);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(_storagePath))
                {
                    // In C++: if already begins with StoragePath, do not insert again
                    if (!StartsWith(newPath, _storagePath))
                    {
                        newPath = InsertBasePathIfMissing(newPath, _storagePath);
                    }
                }
            }

            newPath = newPath.Replace("\\", "/");
            OSGeneric.ModifyPath(newPath);

            FileHandle handle = OS.SystemFileOpen(newPath, 0);
            if (handle.IsValid)
            {
                OS.SystemFileClose(handle);
            }

            return handle.IsValid;
        }

        internal static FileHandle FileOpen(string path, byte modeFlags)
        {
            DexterOS.MutexLock();
            try
            {
                EnsureFileListInitialized();

                string newPath = path ?? string.Empty;

                if ((modeFlags & 0x26) == 0)
                {
                    if (!string.IsNullOrEmpty(_contentPath))
                    {
                        newPath = InsertBasePathIfMissing(newPath, _contentPath);
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(_storagePath))
                    {
                        if (!StartsWith(newPath, _storagePath))
                        {
                            newPath = InsertBasePathIfMissing(newPath, _storagePath);
                        }
                    }
                }

                newPath = newPath.Replace("\\", "/");
                OSGeneric.ModifyPath(newPath);

                int slot = FindFreeFileSlotNoLock();
                if (slot < 0)
                {
                    return new FileHandle(0);
                }

                DexterDebug.CheckDebugMode(8);

                FileHandle handle = OS.SystemFileOpen(newPath, modeFlags);
                _fileList![slot].Handle = handle;

                if (!handle.IsValid)
                {
                    RemoveSlotNoLock(slot);
                    return new FileHandle(0);
                }

                _fileList[slot].Path = newPath;
                _fileList[slot].Size = checked((int)OS.SystemFileSize(handle));
                _fileList[slot].Pos = 0;
                _fileList[slot].StreamPos = 0;
                _fileList[slot].Mode = modeFlags;

                // C++: *(plVar4 + 0x24) = 0; then allocate buffer if mode != '\b' and DefaultFileBuffer > 0
                _fileList[slot].Buffer = null;
                _fileList[slot].BufferSize = 0;
                _fileList[slot].BufferCount = 0;
                _fileList[slot].BufferOffset = -1;

                if (modeFlags != 8)
                {
                    if (_defaultFileBuffer > 0)
                    {
                        _fileList[slot].Buffer = new byte[_defaultFileBuffer];
                        _fileList[slot].BufferSize = _defaultFileBuffer;
                        _fileList[slot].BufferCount = 0;
                        _fileList[slot].BufferOffset = -1;
                    }

                    _fopenCount++;
                }

                return handle;
            }
            finally
            {
                DexterOS.MutexUnLock();
            }
        }

        internal static void SetFileBufferSize(int bytes)
        {
            _defaultFileBuffer = bytes;
        }

        internal static int FileRead(FileHandle file, byte[] buffer, int count, byte flags)
        {
            if (buffer == null || count < 1)
            {
                return 0;
            }

            DexterOS.MutexLock();
            try
            {
                EnsureFileListInitialized();

                if (!file.IsValid || _fileListUsed == 0)
                {
                    return 0;
                }

                int index = FindFileIndexByHandle(file);
                if (index < 0)
                {
                    return 0;
                }

                FileEntry entry = _fileList![index];

                int totalRead = 0;
                int writeOffset = 0;

                // If there is a buffer and BufferOffset >= 0, serve from it first.
                if (entry.Buffer != null)
                {
                    int bufOffset = entry.BufferOffset;
                    if (bufOffset >= 0)
                    {
                        int remaining = entry.BufferCount - bufOffset;
                        if (remaining > 0)
                        {
                            int take = Math.Min(count, remaining);
                            Buffer.BlockCopy(entry.Buffer, bufOffset, buffer, 0, take);

                            entry.Pos += take;
                            entry.BufferOffset += take;

                            totalRead += take;
                            writeOffset += take;
                            count -= take;

                            if (count <= 0)
                            {
                                _fileList[index] = entry;
                                return totalRead;
                            }

                            // If buffer consumed, mark invalid like C++ sets 0x124 to 0xffffffff
                            if (entry.BufferOffset >= entry.BufferCount)
                            {
                                entry.BufferOffset = -1;
                            }
                        }
                        else
                        {
                            entry.BufferOffset = -1;
                        }
                    }
                }

                // Ensure OS file pointer is aligned with our logical position when needed.
                if (entry.Pos != entry.StreamPos)
                {
                    OS.SystemFileSeek(file, entry.Pos);
                    entry.StreamPos = entry.Pos;
                    entry.BufferOffset = -1;
                    entry.BufferCount = 0;
                }

                // Original behavior:
                // If (count < bufferSize) and ((flags & 1) == 0) => read a chunk into buffer, then copy count bytes.
                if (entry.Buffer != null && entry.BufferSize > 0 && count < entry.BufferSize && (flags & 1) == 0)
                {
                    int remainingFile = entry.Size - entry.Pos;
                    int toFill = Math.Min(entry.BufferSize, remainingFile);

                    int filled = 0;
                    if (toFill > 0)
                    {
                        filled = OS.SystemFileRead(file, entry.Buffer, 0, toFill);
                    }

                    entry.StreamPos += filled;
                    entry.BufferCount = filled;

                    int take = Math.Min(count, filled);
                    if (take > 0)
                    {
                        Buffer.BlockCopy(entry.Buffer, 0, buffer, writeOffset, take);
                        entry.Pos += take;
                        entry.BufferOffset = take;
                        totalRead += take;
                    }
                    else
                    {
                        entry.BufferOffset = -1;
                    }

                    _fileList[index] = entry;
                    return totalRead;
                }

                // Direct read path
                int maxReadable = entry.Size - entry.Pos;
                int direct = Math.Min(count, maxReadable);

                int got = 0;
                if (direct > 0)
                {
                    byte[] temp = (writeOffset == 0 && direct == buffer.Length) ? buffer : new byte[direct];
                    got = OS.SystemFileRead(file, temp, 0, direct);

                    if (!ReferenceEquals(temp, buffer))
                    {
                        Buffer.BlockCopy(temp, 0, buffer, writeOffset, got);
                    }
                }

                entry.Pos += got;
                entry.StreamPos += got;
                entry.BufferOffset = -1;
                entry.BufferCount = 0;

                totalRead += got;

                _fileList[index] = entry;
                return totalRead;
            }
            finally
            {
                DexterOS.MutexUnLock();
            }
        }

        internal static void FileSeek(FileHandle file, int offset, byte origin)
        {
            DexterOS.MutexLock();
            try
            {
                EnsureFileListInitialized();

                if (!file.IsValid || _fileListUsed == 0)
                {
                    return;
                }

                int index = FindFileIndexByHandle(file);
                if (index < 0)
                {
                    return;
                }

                FileEntry entry = _fileList![index];

                int target = offset;
                if (origin == 1)
                {
                    target = offset + entry.Pos;
                }
                else if (origin == 2)
                {
                    target = offset + entry.Size;
                }

                OS.SystemFileSeek(file, target);
                entry.StreamPos = target;
                entry.Pos = target;

                entry.BufferOffset = -1;
                entry.BufferCount = 0;

                _fileList[index] = entry;
            }
            finally
            {
                DexterOS.MutexUnLock();
            }
        }

        internal static int FileWrite(FileHandle file, byte[] data, int count)
        {
            DexterOS.MutexLock();
            try
            {
                EnsureFileListInitialized();

                if (data == null || count <= 0 || !file.IsValid)
                {
                    return 0;
                }

                int index = FindFileIndexByHandle(file);
                if (index < 0)
                {
                    return 0;
                }

                FileEntry entry = _fileList![index];

                // Mode must not be 0 or 0x10 in C++ check.
                // Here: treat Mode == 0 or 0x10 as non-writable.
                if (entry.Mode == 0 || entry.Mode == 0x10)
                {
                    return 0;
                }

                // Buffered write path: Mode == 1 and buffer exists
                if (entry.Mode == 1 && entry.Buffer != null)
                {
                    int used = entry.BufferOffset;
                    if (used < 0)
                    {
                        entry.BufferOffset = 0;
                        used = 0;
                    }

                    // If buffer would overflow => flush current buffer first (C++ does this)
                    if (entry.BufferSize <= used + count)
                    {
                        if (used > 0)
                        {
                            OS.SystemFileWrite(file, entry.Buffer, 0, used);
                            entry.StreamPos += used;
                            entry.BufferOffset = -1;
                            entry.BufferCount = 0;
                        }

                        // After flush, fall through to direct write
                        OS.SystemFileWrite(file, data, 0, count);
                        entry.StreamPos += count;
                    }
                    else
                    {
                        Buffer.BlockCopy(data, 0, entry.Buffer, used, count);
                        entry.BufferOffset = used + count;
                        entry.BufferCount = Math.Max(entry.BufferCount, entry.BufferOffset);
                    }
                }
                else
                {
                    // Direct write
                    OS.SystemFileWrite(file, data, 0, count);
                    entry.StreamPos += count;
                }

                entry.Pos += count;

                // Size tracking: original code asks OS for size at open; here keep it consistent if writing grows file
                if (entry.Pos > entry.Size)
                {
                    entry.Size = entry.Pos;
                }

                _fileList[index] = entry;
                return count;
            }
            finally
            {
                DexterOS.MutexUnLock();
            }
        }

        internal static int FilePoss(FileHandle file)
        {
            EnsureFileListInitialized();

            if (!file.IsValid || _fileListUsed == 0)
            {
                return -1;
            }

            int index = FindFileIndexByHandle(file);
            if (index < 0)
            {
                return -1;
            }

            return _fileList![index].Pos;
        }

        internal static int FileSize(FileHandle file)
        {
            EnsureFileListInitialized();

            if (!file.IsValid || _fileListUsed == 0)
            {
                return 0;
            }

            int index = FindFileIndexByHandle(file);
            if (index < 0)
            {
                return 0;
            }

            return _fileList![index].Size;
        }

        internal static bool FileWriteString(FileHandle file, string format, params object[] args)
        {
            if (!file.IsValid || format == null)
            {
                return false;
            }

            string text = (args == null || args.Length == 0) ? format : string.Format(format, args);

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);

            int written = FileWrite(file, bytes, bytes.Length);
            return written == bytes.Length;
        }

        internal static void MakeDir(string path, byte modeFlags)
        {
            DexterDebug.CheckDebugMode(0x20);

            string newPath = path ?? string.Empty;

            if ((modeFlags & 0x26) == 0)
            {
                if (!string.IsNullOrEmpty(_contentPath))
                {
                    newPath = InsertBasePathIfMissing(newPath, _contentPath);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(_storagePath))
                {
                    if (!StartsWith(newPath, _storagePath))
                    {
                        newPath = InsertBasePathIfMissing(newPath, _storagePath);
                    }
                }
            }

            _ = newPath.Replace("\\", "/");
        }

        internal static bool FileDelete(string path)
        {
            if (!FileExists(path, 0))
            {
                return false;
            }

            string newPath = path ?? string.Empty;

            if (!string.IsNullOrEmpty(_storagePath))
            {
                if (!StartsWith(newPath, _storagePath))
                {
                    newPath = InsertBasePathIfMissing(newPath, _storagePath);
                }
            }

            newPath = newPath.Replace("\\", "/");

            return OSGeneric.SystemFileDelete(newPath);
        }

        internal static void SetStoragePath(string path)
        {
            if (path == null)
            {
                return;
            }

            int len = DexterString.StringLength(path);
            if (len < 0x100)
            {
                _storagePath = path;
            }
        }

        internal static void XMLInit()
        {
            // C++ counts down and frees; in managed code just clear.
            for (int i = 0; i < _xmlEntries; i++)
            {
                _xmlKeys[i] = null;
                _xmlValues[i] = null;
                _xmlUsed[i] = false;
            }

            _xmlEntries = 0;
        }

        internal static bool XMLSetString(string key, string value)
        {
            if (key == null)
            {
                return false;
            }

            string safeValue = value ?? string.Empty;

            // Update existing
            for (int i = 0; i < _xmlEntries; i++)
            {
                if (_xmlUsed[i] &&
                    DexterString.StringCompare(_xmlKeys[i] ?? string.Empty, key, caseSensitive: false))
                {
                    _xmlKeys[i] = key;
                    _xmlValues[i] = safeValue;
                    _xmlUsed[i] = true;
                    return true;
                }
            }

            // Insert new
            if (_xmlEntries >= MaxXmlEntries)
            {
                return false;
            }

            _xmlKeys[_xmlEntries] = key;
            _xmlValues[_xmlEntries] = safeValue;
            _xmlUsed[_xmlEntries] = true;
            _xmlEntries++;

            return true;
        }

        internal static void XMLSetValue(string key, int value)
        {
            string s = DexterString.IntegerToString(value);
            XMLSetString(key, s);
        }

        internal static int XMLGetValue(string key)
        {
            string? s = XMLGetString(key);
            if (string.IsNullOrEmpty(s))
            {
                return 0;
            }

            return DexterString.StringToInteger(s);
        }

        internal static bool XMLLoad(string path, byte modeFlags)
        {
            FileHandle file = FileOpen(path, modeFlags);
            if (!file.IsValid) return false;

            try
            {
                byte[] chunk = new byte[0x20];

                bool inTag = false;
                bool readingValue = false;

                string currentKey = string.Empty;
                System.Text.StringBuilder keyBuilder = new();
                System.Text.StringBuilder valueBuilder = new();

                int read;
                while ((read = FileRead(file, chunk, chunk.Length, 1)) > 0)
                {
                    for (int i = 0; i < read; i++)
                    {
                        char c = (char)chunk[i];
                        if (c < '!')
                        {
                            c = ' ';
                        }

                        if (inTag)
                        {
                            if (c == '>')
                            {
                                currentKey = DexterString.StringStripWhitespace(keyBuilder.ToString());
                                keyBuilder.Clear();

                                inTag = false;
                                readingValue = true;
                                valueBuilder.Clear();
                            }
                            else
                            {
                                keyBuilder.Append(c);
                            }
                        }
                        else
                        {
                            if (c == '<')
                            {
                                inTag = true;

                                if (readingValue)
                                {
                                    string value = DexterString.StringStripWhitespace(valueBuilder.ToString());
                                    if (!string.IsNullOrEmpty(value))
                                    {
                                        XMLSetString(currentKey, value);
                                    }

                                    readingValue = false;
                                    valueBuilder.Clear();
                                }
                            }
                            else
                            {
                                if (readingValue)
                                {
                                    valueBuilder.Append(c);
                                }
                            }
                        }
                    }
                }

                return true;
            }
            finally
            {
                FileClose(file);
            }
        }

        internal static bool XMLSave(string path, byte modeFlags)
        {
            FileHandle file = FileOpen(path, modeFlags);
            if (!file.IsValid) return false;

            try
            {
                FileWriteString(file, "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>\n");
                FileWriteString(file, "<DexterXML>\n");

                for (int i = 0; i < _xmlEntries; i++)
                {
                    if (_xmlUsed[i])
                    {
                        string key = _xmlKeys[i] ?? string.Empty;
                        string val = _xmlValues[i] ?? string.Empty;
                        FileWriteString(file, "\t<{0}>{1}</{0}>\n", key, val);
                    }
                }

                FileWriteString(file, "</DexterXML>\n");
                return true;
            }
            finally
            {
                FileClose(file);
            }
        }

        internal static bool XMLCompareString(string key, string value)
        {
            string? existing = XMLGetString(key);
            if (existing == null)
            {
                return false;
            }

            return DexterString.StringCompare(existing, value ?? string.Empty, false);
        }

        // C++ ctor just zeroes the paths; emulate with an init method.
        internal static void Init()
        {
            _contentPath = string.Empty;
            _storagePath = string.Empty;
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------

        private static void EnsureFileListInitialized()
        {
            if (_fileList != null)
            {
                return;
            }

            lock (_fileListLock)
            {
                if (_fileList != null)
                {
                    return;
                }

                DexterOS.MutexLock();
                try
                {
                    _fileList = new FileEntry[MaxOpenFiles];
                    for (int i = 0; i < _fileList.Length; i++)
                    {
                        _fileList[i] = FileEntry.CreateEmpty();
                    }

                    _fileListSize = MaxOpenFiles;
                    _fileListUsed = 0;
                }
                finally
                {
                    DexterOS.MutexUnLock();
                }
            }
        }

        // C++ FindFreeFile() returns pointer to next slot; here return index.
        // Must be called while DexterOS mutex is held (matches C++ usage from FileOpen()).
        private static int FindFreeFileSlotNoLock()
        {
            if (_fileListUsed >= _fileListSize)
            {
                return -1;
            }

            int index = _fileListUsed;
            _fileListUsed++;

            _fileList![index] = FileEntry.CreateEmpty();
            return index;
        }

        // Roll back a slot if open failed (managed convenience).
        private static void RemoveSlotNoLock(int index)
        {
            if (_fileListUsed <= 0)
            {
                return;
            }

            _fileListUsed--;

            if (index != _fileListUsed)
            {
                _fileList![index] = _fileList[_fileListUsed];
            }

            _fileList![_fileListUsed] = FileEntry.CreateEmpty();
        }

        private static int FindFileIndexByHandle(FileHandle handle)
        {
            if (_fileList == null || _fileListUsed == 0)
            {
                return -1;
            }

            for (int i = 0; i < _fileListUsed; i++)
            {
                if (_fileList[i].Handle == handle)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string? XMLGetString(string key)
        {
            if (key == null)
            {
                return null;
            }

            for (int i = 0; i < _xmlEntries; i++)
            {
                if (_xmlUsed[i] && DexterString.StringCompare(_xmlKeys[i] ?? string.Empty, key, caseSensitive: false))
                {
                    return _xmlValues[i];
                }
            }

            return null;
        }

        private static string InsertBasePathIfMissing(string path, string basePath)
        {
            if (string.IsNullOrEmpty(basePath))
            {
                return path;
            }

            if (StartsWith(path, basePath))
            {
                return path;
            }

            return basePath + path;
        }

        private static bool StartsWith(string text, string prefix)
        {
            if (text == null || prefix == null)
            {
                return false;
            }

            if (prefix.Length == 0)
            {
                return true;
            }

            if (text.Length < prefix.Length)
            {
                return false;
            }

            return text.StartsWith(prefix, StringComparison.Ordinal);
        }
    }
}