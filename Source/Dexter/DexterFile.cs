using System.Text;

namespace OpenVikings.Dexter
{
    internal static class DexterFile
    {
        internal readonly struct FileHandle
        {
            internal readonly long Value;

            internal FileHandle(long value)
            {
                Value = value;
            }

            internal bool IsValid => Value != 0;

            public override int GetHashCode()
            {
                return Value.GetHashCode();
            }

            public override bool Equals(object? obj)
            {
                if (obj is FileHandle other)
                {
                    return other.Value == Value;
                }

                return false;
            }

            public static bool operator ==(FileHandle a, FileHandle b)
            {
                return a.Value == b.Value;
            }

            public static bool operator !=(FileHandle a, FileHandle b)
            {
                return a.Value != b.Value;
            }

            public override string ToString()
            {
                return Value.ToString();
            }
        }

        private sealed class FileEntry
        {
            internal FileStream Stream;
            internal string Path;
            internal byte Mode;

            internal int FileSize;
            internal int CurrentPos;
            internal int CachedPos;

            internal byte[]? Buffer;
            internal int BufferCapacity;

            // Read cache state
            internal int BufferValidBytes;
            internal int BufferIndex; // next unread byte in Buffer

            // Write cache state
            internal int WriteBufferedBytes;

            internal FileEntry(FileStream stream, string path, byte mode, int fileSize)
            {
                Stream = stream;
                Path = path;
                Mode = mode;

                FileSize = fileSize;
                CurrentPos = checked((int)stream.Position);
                CachedPos = CurrentPos;
            }
        }

        private sealed class XmlEntry
        {
            internal string Key;
            internal string Value;
            internal bool Active;

            internal XmlEntry(string key, string value)
            {
                Key = key;
                Value = value;
                Active = true;
            }
        }

        private static readonly object _sync = new();

        private static FileEntry[]? _fileList;
        private static int _fileListSize;
        private static int _fileListUsed;

        private static long _nextHandle = 1;
        private static readonly Dictionary<long, int> _handleToSlot = new();

        private static int _defaultFileBufferSize;

        private static string _contentPath = string.Empty;
        private static string _storagePath = string.Empty;

        // Approx. original global counter
        private static int _fopenCount;

        // XML storage (original limit: 100)
        private const int MaxXmlEntries = 100;
        private static readonly List<XmlEntry> _xmlEntries = new(MaxXmlEntries);

        // --------------------------------------------------------------------
        // DexterFile::SetMaxFiles(unsigned int)
        // --------------------------------------------------------------------
        internal static void SetMaxFiles(uint maxFiles)
        {
            lock (_sync)
            {
                if (_fileList != null)
                {
                    return;
                }

                int size = checked((int)maxFiles);
                if (size < 0)
                {
                    size = 0;
                }

                _fileList = new FileEntry[size];
                _fileListSize = size;
                _fileListUsed = 0;
                _handleToSlot.Clear();
            }
        }

        // --------------------------------------------------------------------
        // DexterFile::FileSystemShutDown()
        // --------------------------------------------------------------------
        internal static void FileSystemShutDown()
        {
            lock (_sync)
            {
                if (_fileListUsed <= 0 || _fileList == null)
                {
                    return;
                }

                // Close all open files (iterate while list changes)
                int i = 0;
                while (i < _fileListUsed)
                {
                    FileEntry? entry = _fileList[i];
                    if (entry != null)
                    {
                        // Mode '\b' was excluded in C++ for debug counting; still close here.
                        FileHandle handle = FindHandleBySlot_NoLock(i);
                        if (handle.IsValid)
                        {
                            FileClose(handle);
                            i = 0;
                            continue;
                        }
                    }

                    i++;
                }
            }
        }

        // --------------------------------------------------------------------
        // DexterFile::SetFileBufferSize(int)
        // --------------------------------------------------------------------
        internal static void SetFileBufferSize(int size)
        {
            lock (_sync)
            {
                _defaultFileBufferSize = size;
            }
        }

        // --------------------------------------------------------------------
        // DexterFile::SetContentPath(char const*)
        // DexterFile::SetStoragePath(char const*)
        // --------------------------------------------------------------------
        internal static void SetContentPath(string path)
        {
            if (path == null)
            {
                return;
            }

            lock (_sync)
            {
                if (path.Length < 0x100)
                {
                    _contentPath = path;
                }
            }
        }

        internal static void SetStoragePath(string path)
        {
            if (path == null)
            {
                return;
            }

            lock (_sync)
            {
                if (path.Length < 0x100)
                {
                    _storagePath = path;
                }
            }
        }

        internal static string? GetContentPath()
        {
            lock (_sync)
            {
                return string.IsNullOrEmpty(_contentPath) ? null : _contentPath;
            }
        }

        internal static string? GetStoragePath()
        {
            lock (_sync)
            {
                return string.IsNullOrEmpty(_storagePath) ? null : _storagePath;
            }
        }

        // --------------------------------------------------------------------
        // DexterFile::ModifyPath(char*, unsigned char)
        // Managed: returns resolved absolute path.
        // --------------------------------------------------------------------
        internal static string ModifyPath(string path, byte mode)
        {
            if (path == null)
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', '/');

            bool useStorage = (mode & 0x26) != 0;
            string prefix;

            lock (_sync)
            {
                prefix = useStorage ? _storagePath : _contentPath;
            }

            if (!string.IsNullOrEmpty(prefix))
            {
                if (!normalized.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    normalized = prefix.TrimEnd('/', '\\') + "/" + normalized;
                }
            }

            // Keep same behavior as C++: normalize slashes and let OS environment adjust path.
            normalized = normalized.Replace('\\', '/');

            // If OSEnvironment.ModifyPath exists, it should be used here.
            // This call is intentionally safe even if ModifyPath is a no-op in your environment.
            try
            {
                OSEnvironment.ModifyPath(normalized);
            }
            catch
            {
                // Ignore and keep normalized as-is.
            }

            try
            {
                return Path.GetFullPath(normalized);
            }
            catch
            {
                return normalized;
            }
        }

        // --------------------------------------------------------------------
        // DexterFile::FileExists(char*, unsigned char)
        // --------------------------------------------------------------------
        internal static bool FileExists(string path, byte mode)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            string resolved = ModifyPath(path, mode);
            try
            {
                return File.Exists(resolved);
            }
            catch
            {
                return false;
            }
        }

        // --------------------------------------------------------------------
        // DexterFile::MakeDir(char const*, unsigned char)
        // --------------------------------------------------------------------
        internal static void MakeDir(string path, byte mode)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string resolved = ModifyPath(path, mode);
            try
            {
                Directory.CreateDirectory(resolved);
            }
            catch
            {
                // ignore
            }
        }

        // --------------------------------------------------------------------
        // DexterFile::ChangeDir(char const*)
        // C++ called OSGeneric::ChangeDir(&DexterOS::OS); path param ignored in decompile
        // Managed: optional. If path is provided, it is applied.
        // --------------------------------------------------------------------
        internal static void ChangeDir(string? path = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(path))
                {
                    Directory.SetCurrentDirectory(path);
                }
            }
            catch
            {
                // ignore
            }
        }

        // --------------------------------------------------------------------
        // DexterFile::FileDelete(char*)
        // StoragePath is preferred like in C++.
        // --------------------------------------------------------------------
        internal static bool FileDelete(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (!FileExists(path, 0))
            {
                return false;
            }

            string resolved;
            lock (_sync)
            {
                if (!string.IsNullOrEmpty(_storagePath) && !path.StartsWith(_storagePath, System.StringComparison.OrdinalIgnoreCase))
                {
                    resolved = _storagePath.TrimEnd('/', '\\') + "/" + path.Replace('\\', '/');
                }
                else
                {
                    resolved = path.Replace('\\', '/');
                }
            }

            resolved = ModifyPath(resolved, 0x26);

            try
            {
                File.Delete(resolved);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // --------------------------------------------------------------------
        // DexterFile::FindFreeFile()
        // Managed: ensure list exists and allocate a free slot.
        // --------------------------------------------------------------------
        private static int FindFreeFileSlot_NoLock()
        {
            if (_fileList == null)
            {
                // C++ default alloc: 1000 entries
                _fileList = new FileEntry[1000];
                _fileListSize = 1000;
                _fileListUsed = 0;
                _handleToSlot.Clear();
            }

            if (_fileListUsed >= _fileListSize)
            {
                return -1;
            }

            int slot = _fileListUsed;
            _fileListUsed++;
            _fileList[slot] = null;
            return slot;
        }

        // --------------------------------------------------------------------
        // DexterFile::FileOpen(char const*, unsigned char)
        // Returns a managed handle (0 = failure).
        // --------------------------------------------------------------------
        internal static FileHandle FileOpen(string path, byte mode)
        {
            if (string.IsNullOrEmpty(path))
            {
                return new FileHandle(0);
            }

            lock (_sync)
            {
                string resolved = ModifyPath(path, mode);

                int slot = FindFreeFileSlot_NoLock();
                if (slot < 0)
                {
                    return new FileHandle(0);
                }

                bool isWrite = mode == 0x01 || mode == (byte)'!';
                FileAccess access = isWrite ? FileAccess.Write : FileAccess.Read;
                FileMode fileMode = isWrite ? FileMode.Create : FileMode.Open;
                FileShare share = FileShare.Read;

                FileStream stream;
                try
                {
                    stream = new FileStream(resolved, fileMode, access, share);
                }
                catch
                {
                    _fileListUsed--;
                    return new FileHandle(0);
                }

                int size;
                try
                {
                    long length = stream.Length;
                    size = length > int.MaxValue ? int.MaxValue : (int)length;
                }
                catch
                {
                    size = 0;
                }

                FileEntry entry = new(stream, resolved, mode, size);

                // Allocate optional buffer if requested and not special mode '\b'
                if (mode != (byte)'\b' && _defaultFileBufferSize > 0)
                {
                    entry.BufferCapacity = _defaultFileBufferSize;
                    entry.Buffer = new byte[_defaultFileBufferSize];
                    entry.BufferValidBytes = 0;
                    entry.BufferIndex = 0;
                    entry.WriteBufferedBytes = 0;
                }

                _fileList![slot] = entry;

                long handleValue = _nextHandle;
                _nextHandle++;

                _handleToSlot[handleValue] = slot;

                if (mode != (byte)'\b')
                {
                    _fopenCount++;
                }

                return new FileHandle(handleValue);
            }
        }

        // --------------------------------------------------------------------
        // DexterFile::FileClose(PHYSFS_File*)
        // --------------------------------------------------------------------
        internal static void FileClose(FileHandle file)
        {
            lock (_sync)
            {
                if (!TryGetEntry_NoLock(file, out FileEntry? entry, out int slot))
                {
                    return;
                }

                // Flush buffered writes for mode '\x01' (write) if any
                if (entry.Mode == 0x01 && entry.Buffer != null && entry.WriteBufferedBytes > 0)
                {
                    try
                    {
                        entry.Stream.Write(entry.Buffer, 0, entry.WriteBufferedBytes);
                    }
                    catch
                    {
                        // ignore
                    }

                    entry.WriteBufferedBytes = 0;
                }

                try
                {
                    entry.Stream.Dispose();
                }
                catch
                {
                    // ignore
                }

                if (entry.Mode != (byte)'\b')
                {
                    _fopenCount--;
                }

                // Remove by swapping last used entry into this slot (matches C++ semantics)
                int lastSlot = _fileListUsed - 1;
                if (_fileList != null && slot != lastSlot && lastSlot >= 0)
                {
                    FileEntry? moved = _fileList[lastSlot];
                    _fileList[slot] = moved;
                    _fileList[lastSlot] = null;

                    // Update mapping for moved handle
                    long movedHandle = FindHandleBySlot_NoLock(lastSlot).Value;
                    if (movedHandle != 0)
                    {
                        _handleToSlot[movedHandle] = slot;
                    }
                }
                else if (_fileList != null && slot >= 0 && slot < _fileList.Length)
                {
                    _fileList[slot] = null;
                }

                _handleToSlot.Remove(file.Value);
                _fileListUsed = lastSlot < 0 ? 0 : lastSlot;
            }
        }

        // --------------------------------------------------------------------
        // DexterFile::ValidFile(PHYSFS_File*)
        // Managed: FileHandle validity check.
        // --------------------------------------------------------------------
        internal static bool ValidFile(FileHandle file)
        {
            lock (_sync)
            {
                return TryGetEntry_NoLock(file, out FileEntry? _, out int _);
            }
        }

        // --------------------------------------------------------------------
        // DexterFile::FileRead(PHYSFS_File*, void*, int, unsigned char)
        // Managed: reads into buffer[offset..offset+count)
        // Returns number of bytes read.
        // flags bit0: 1 => bypass cache behavior (matches decompile branching).
        // --------------------------------------------------------------------
        internal static int FileRead(FileHandle file, byte[] buffer, int offset, int count, byte flags)
        {
            if (buffer == null || count < 1)
            {
                return 0;
            }

            if (offset < 0 || offset > buffer.Length)
            {
                return 0;
            }

            if (count > buffer.Length - offset)
            {
                return 0;
            }

            lock (_sync)
            {
                if (!TryGetEntry_NoLock(file, out FileEntry? entry, out int _))
                {
                    return 0;
                }

                // Serve from read cache if present
                if (entry.Buffer != null && (flags & 1) == 0)
                {
                    int served = TryServeFromReadCache_NoLock(entry, buffer, offset, count);
                    if (served == count)
                    {
                        entry.CurrentPos += served;
                        return served;
                    }

                    if (served > 0)
                    {
                        entry.CurrentPos += served;
                        offset += served;
                        count -= served;
                    }

                    int additionallyRead = FillReadCacheAndCopy_NoLock(entry, buffer, offset, count);
                    entry.CurrentPos += additionallyRead;
                    return served + additionallyRead;
                }

                // Direct read (no cache)
                int read = 0;
                try
                {
                    read = entry.Stream.Read(buffer, offset, count);
                }
                catch
                {
                    read = 0;
                }

                entry.CurrentPos = checked(entry.CurrentPos + read);
                entry.CachedPos = entry.CurrentPos;
                entry.BufferValidBytes = 0;
                entry.BufferIndex = 0;
                return read;
            }
        }

        // Original compatibility overload: reads 'count' and returns first byte in value.
        internal static int FileRead(FileHandle file, ref byte value, int count, byte flags)
        {
            if (count < 1)
            {
                return 0;
            }

            byte[] temp = new byte[count];
            int read = FileRead(file, temp, 0, count, flags);
            if (read > 0)
            {
                value = temp[0];
            }

            return read;
        }

        // --------------------------------------------------------------------
        // DexterFile::FileWrite(PHYSFS_File*, void const*, int)
        // Managed: writes from buffer[offset..offset+count)
        // Returns bytes written.
        // --------------------------------------------------------------------
        internal static int FileWrite(FileHandle file, byte[] buffer, int offset, int count)
        {
            if (buffer == null || count < 1)
            {
                return 0;
            }

            if (offset < 0 || offset > buffer.Length)
            {
                return 0;
            }

            if (count > buffer.Length - offset)
            {
                return 0;
            }

            lock (_sync)
            {
                if (!TryGetEntry_NoLock(file, out FileEntry? entry, out int _))
                {
                    return 0;
                }

                byte mode = entry.Mode;
                if (mode == 0 || mode == 0x10)
                {
                    // Not writable (matches C++ check: mode != '\0' && mode != '\x10')
                    return 0;
                }

                // Buffered write for mode 0x01 (matches decompile)
                if (mode == 0x01 && entry.Buffer != null)
                {
                    int remaining = entry.BufferCapacity - entry.WriteBufferedBytes;

                    // Flush if overflow
                    if (count > remaining)
                    {
                        if (entry.WriteBufferedBytes > 0)
                        {
                            try
                            {
                                entry.Stream.Write(entry.Buffer, 0, entry.WriteBufferedBytes);
                                entry.CachedPos += entry.WriteBufferedBytes;
                            }
                            catch
                            {
                                // ignore
                            }

                            entry.WriteBufferedBytes = 0;
                        }
                    }

                    // Still too large? write direct
                    if (count > entry.BufferCapacity)
                    {
                        try
                        {
                            entry.Stream.Write(buffer, offset, count);
                        }
                        catch
                        {
                            return 0;
                        }

                        entry.CachedPos += count;
                        entry.CurrentPos += count;
                        UpdateFileSizeAfterWrite_NoLock(entry);
                        return count;
                    }

                    Buffer.BlockCopy(buffer, offset, entry.Buffer, entry.WriteBufferedBytes, count);
                    entry.WriteBufferedBytes += count;
                    entry.CurrentPos += count;
                    UpdateFileSizeAfterWrite_NoLock(entry);
                    return count;
                }

                // Direct write
                try
                {
                    entry.Stream.Write(buffer, offset, count);
                }
                catch
                {
                    return 0;
                }

                entry.CachedPos += count;
                entry.CurrentPos += count;
                UpdateFileSizeAfterWrite_NoLock(entry);
                return count;
            }
        }

        // Compatibility overload: writes repeated value count times (matches your previous helper)
        internal static int FileWrite(FileHandle file, ref byte value, int count)
        {
            if (count < 1)
            {
                return 0;
            }

            byte[] temp = new byte[count];
            temp[0] = value;

            for (int i = 1; i < count; i++)
            {
                temp[i] = value;
            }

            return FileWrite(file, temp, 0, count);
        }

        // --------------------------------------------------------------------
        // DexterFile::FileSeek(PHYSFS_File*, int, unsigned char)
        // origin: 0=begin, 1=current, 2=end
        // --------------------------------------------------------------------
        internal static void FileSeek(FileHandle file, int offset, byte origin)
        {
            lock (_sync)
            {
                if (!TryGetEntry_NoLock(file, out FileEntry? entry, out int _))
                {
                    return;
                }

                int target = offset;

                if (origin == 1)
                {
                    target = checked(entry.CurrentPos + offset);
                }
                else if (origin == 2)
                {
                    target = checked(entry.FileSize + offset);
                }

                if (target < 0)
                {
                    target = 0;
                }

                try
                {
                    entry.Stream.Seek(target, SeekOrigin.Begin);
                }
                catch
                {
                    // ignore
                }

                entry.CurrentPos = target;
                entry.CachedPos = target;
                entry.BufferValidBytes = 0;
                entry.BufferIndex = 0;
            }
        }

        // --------------------------------------------------------------------
        // DexterFile::FileRewind(PHYSFS_File*)
        // --------------------------------------------------------------------
        internal static void FileRewind(FileHandle file)
        {
            FileSeek(file, 0, 0);
        }

        // --------------------------------------------------------------------
        // DexterFile::FilePoss(PHYSFS_File*)
        // --------------------------------------------------------------------
        internal static int FilePoss(FileHandle file)
        {
            lock (_sync)
            {
                if (!TryGetEntry_NoLock(file, out FileEntry? entry, out int _))
                {
                    return -1;
                }

                return entry.CurrentPos;
            }
        }

        // --------------------------------------------------------------------
        // DexterFile::FileSize(PHYSFS_File*)
        // --------------------------------------------------------------------
        internal static int FileSize(FileHandle file)
        {
            lock (_sync)
            {
                if (!TryGetEntry_NoLock(file, out FileEntry? entry, out int _))
                {
                    return 0;
                }

                return entry.FileSize;
            }
        }

        // --------------------------------------------------------------------
        // DexterFile::FileEOF(PHYSFS_File*)
        // Returns 1 if EOF else 0 (matches original).
        // --------------------------------------------------------------------
        internal static ulong FileEOF(FileHandle file)
        {
            lock (_sync)
            {
                if (!TryGetEntry_NoLock(file, out FileEntry? entry, out int _))
                {
                    return 1;
                }

                return entry.CurrentPos >= entry.FileSize ? 1UL : 0UL;
            }
        }

        // --------------------------------------------------------------------
        // Convenience readers/writers (byte/word/long)
        // C++ used MSB variants via DexterEndian for Word/Long.
        // --------------------------------------------------------------------
        internal static byte FileReadByte(FileHandle file)
        {
            byte value = 0;
            _ = FileRead(file, ref value, 1, 0);
            return value;
        }

        internal static ushort FileReadWord(FileHandle file)
        {
            // MSB read for fidelity to the original functions shown.
            return DexterEndian.FileReadWordMSB(file);
        }

        internal static uint FileReadLong(FileHandle file)
        {
            // MSB read for fidelity to the original functions shown.
            return DexterEndian.FileReadLongMSB(file);
        }

        internal static void FileWriteByte(FileHandle file, byte value)
        {
            byte v = value;
            _ = FileWrite(file, ref v, 1);
        }

        internal static void FileWriteWord(FileHandle file, ushort value)
        {
            DexterEndian.FileWriteWordMSB(file, value);
        }

        internal static void FileWriteLong(FileHandle file, uint value)
        {
            DexterEndian.FileWriteLongMSB(file, value);
        }

        // --------------------------------------------------------------------
        // FileWriteString / FileReadString / FileReadLine
        // --------------------------------------------------------------------
        internal static ulong FileWriteString(FileHandle file, string format, params object[] args)
        {
            if (string.IsNullOrEmpty(format))
            {
                return 0;
            }

            string text;
            try
            {
                text = string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args);
            }
            catch
            {
                text = format;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            int written = FileWrite(file, bytes, 0, bytes.Length);

            return written > 0 ? 1UL : 0UL;
        }

        internal static ulong FileReadString(FileHandle file, byte[] buffer, int maxBytes)
        {
            if (buffer == null || maxBytes < 2)
            {
                return 0;
            }

            int count = 0;
            while (count < maxBytes - 1)
            {
                if (FileEOF(file) != 0)
                {
                    break;
                }

                byte b = FileReadByte(file);
                if (b == 0)
                {
                    break;
                }

                buffer[count] = b;
                count++;
            }

            buffer[count] = 0;
            return count > 0 ? 1UL : 0UL;
        }

        internal static ulong FileReadLine(FileHandle file, byte[] buffer, int maxBytes)
        {
            if (buffer == null || maxBytes < 2)
            {
                return 0;
            }

            int count = 0;
            while (count < maxBytes - 1)
            {
                if (FileEOF(file) != 0)
                {
                    break;
                }

                byte b = FileReadByte(file);
                if (b == (byte)'\r' || b == (byte)'\n')
                {
                    // Consume optional second line break char
                    if (FileEOF(file) == 0)
                    {
                        int pos = FilePoss(file);
                        byte next = FileReadByte(file);
                        if (next != (byte)'\r' && next != (byte)'\n')
                        {
                            FileSeek(file, pos, 0);
                        }
                    }

                    break;
                }

                buffer[count] = b;
                count++;
            }

            buffer[count] = 0;
            return count > 0 ? 1UL : 0UL;
        }

        // --------------------------------------------------------------------
        // XML subsystem (managed recreation of the decompile behavior)
        // --------------------------------------------------------------------
        internal static void XMLInit()
        {
            lock (_sync)
            {
                _xmlEntries.Clear();
            }
        }

        internal static ulong XMLSetString(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return 0;
            }

            lock (_sync)
            {
                for (int i = 0; i < _xmlEntries.Count; i++)
                {
                    XmlEntry entry = _xmlEntries[i];
                    if (entry.Active && string.Equals(entry.Key, key, System.StringComparison.Ordinal))
                    {
                        entry.Key = key;
                        entry.Value = value ?? string.Empty;
                        entry.Active = true;
                        return 1;
                    }
                }

                if (_xmlEntries.Count >= MaxXmlEntries)
                {
                    return 0;
                }

                _xmlEntries.Add(new XmlEntry(key, value ?? string.Empty));
                return 1;
            }
        }

        internal static void XMLDropItem(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            lock (_sync)
            {
                for (int i = 0; i < _xmlEntries.Count; i++)
                {
                    XmlEntry entry = _xmlEntries[i];
                    if (entry.Active && string.Equals(entry.Key, key, System.StringComparison.Ordinal))
                    {
                        entry.Active = false;
                    }
                }
            }
        }

        internal static void XMLSetValue(string key, int value)
        {
            string s = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _ = XMLSetString(key, s);
        }

        internal static long XMLGetValue(string key)
        {
            string? s = XMLGetStringManaged(key);
            if (string.IsNullOrEmpty(s))
            {
                return 0;
            }

            if (long.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long v))
            {
                return v;
            }

            return 0;
        }

        internal static string? XMLGetString(string key)
        {
            return XMLGetStringManaged(key);
        }

        private static string? XMLGetStringManaged(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            lock (_sync)
            {
                for (int i = 0; i < _xmlEntries.Count; i++)
                {
                    XmlEntry entry = _xmlEntries[i];
                    if (entry.Active && string.Equals(entry.Key, key, System.StringComparison.Ordinal))
                    {
                        return entry.Value;
                    }
                }
            }

            return null;
        }

        internal static ulong XMLCompareString(string key, string value)
        {
            string? current = XMLGetStringManaged(key);
            if (current == null)
            {
                return 0;
            }

            return string.Equals(current, value ?? string.Empty, System.StringComparison.Ordinal) ? 1UL : 0UL;
        }

        internal static bool XMLLoad(string path, byte mode)
        {
            FileHandle file = FileOpen(path, mode);
            if (!file.IsValid)
            {
                return false;
            }

            try
            {
                byte[] chunk = new byte[0x20];
                StringBuilder text = new(4096);

                while (true)
                {
                    int read = FileRead(file, chunk, 0, chunk.Length, 1);
                    if (read <= 0)
                    {
                        break;
                    }

                    text.Append(Encoding.UTF8.GetString(chunk, 0, read));
                }

                ParseXmlLike(text.ToString());
                return true;
            }
            finally
            {
                FileClose(file);
            }
        }

        internal static bool XMLSave(string path, byte mode)
        {
            FileHandle file = FileOpen(path, mode);
            if (!file.IsValid)
            {
                return false;
            }

            try
            {
                _ = FileWriteString(file, "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>\n");
                _ = FileWriteString(file, "<DexterXML>\n");

                lock (_sync)
                {
                    for (int i = 0; i < _xmlEntries.Count; i++)
                    {
                        XmlEntry entry = _xmlEntries[i];
                        if (!entry.Active)
                        {
                            continue;
                        }

                        string key = entry.Key;
                        string value = entry.Value ?? string.Empty;
                        _ = FileWriteString(file, "\t<{0}>{1}</{0}>\n", key, EscapeXml(value));
                    }
                }

                _ = FileWriteString(file, "</DexterXML>\n");
                return true;
            }
            finally
            {
                FileClose(file);
            }
        }

        private static void ParseXmlLike(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return;
            }

            // Very small XML-ish parser matching the decompile logic: <tag>value</tag>
            int i = 0;
            string? currentTag = null;
            StringBuilder tag = new(64);
            StringBuilder val = new(256);
            bool inTag = false;
            bool readingValue = false;

            while (i < input.Length)
            {
                char c = input[i];
                if (c < '!')
                {
                    c = ' ';
                }

                if (inTag)
                {
                    if (c == '>')
                    {
                        currentTag = tag.ToString().Trim();
                        tag.Clear();
                        inTag = false;
                        readingValue = true;
                        val.Clear();
                    }
                    else if (c == '<')
                    {
                        // malformed; restart
                        tag.Clear();
                    }
                    else
                    {
                        tag.Append(c);
                    }
                }
                else
                {
                    if (c == '<')
                    {
                        if (readingValue && currentTag != null)
                        {
                            string value = val.ToString().Trim();
                            if (value.Length != 0)
                            {
                                _ = XMLSetString(currentTag, UnescapeXml(value));
                            }
                        }

                        readingValue = false;
                        inTag = true;
                        tag.Clear();
                    }
                    else
                    {
                        if (readingValue)
                        {
                            val.Append(c);
                        }
                    }
                }

                i++;
            }
        }

        private static string EscapeXml(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        private static string UnescapeXml(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            return s.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'").Replace("&amp;", "&");
        }

        // --------------------------------------------------------------------
        // Internal helpers
        // --------------------------------------------------------------------
        private static bool TryGetEntry_NoLock(FileHandle file, out FileEntry? entry, out int slot)
        {
            entry = null;
            slot = -1;

            if (!file.IsValid)
            {
                return false;
            }

            if (_fileList == null)
            {
                return false;
            }

            if (!_handleToSlot.TryGetValue(file.Value, out slot))
            {
                return false;
            }

            if ((uint)slot >= (uint)_fileListUsed)
            {
                return false;
            }

            entry = _fileList[slot];
            return entry != null;
        }

        private static FileHandle FindHandleBySlot_NoLock(int slot)
        {
            foreach (KeyValuePair<long, int> kv in _handleToSlot)
            {
                if (kv.Value == slot)
                {
                    return new FileHandle(kv.Key);
                }
            }

            return new FileHandle(0);
        }

        private static int TryServeFromReadCache_NoLock(FileEntry entry, byte[] dst, int dstOffset, int count)
        {
            if (entry.Buffer == null || entry.BufferValidBytes <= 0)
            {
                return 0;
            }

            int available = entry.BufferValidBytes - entry.BufferIndex;
            if (available <= 0)
            {
                entry.BufferValidBytes = 0;
                entry.BufferIndex = 0;
                return 0;
            }

            int take = count <= available ? count : available;
            Buffer.BlockCopy(entry.Buffer, entry.BufferIndex, dst, dstOffset, take);
            entry.BufferIndex += take;

            if (entry.BufferIndex >= entry.BufferValidBytes)
            {
                entry.BufferValidBytes = 0;
                entry.BufferIndex = 0;
            }

            return take;
        }

        private static int FillReadCacheAndCopy_NoLock(FileEntry entry, byte[] dst, int dstOffset, int count)
        {
            if (entry.Buffer == null || entry.BufferCapacity <= 0 || count <= 0)
            {
                return 0;
            }

            int toRead = entry.BufferCapacity;
            int remainingFile = entry.FileSize - entry.CurrentPos;
            if (remainingFile <= 0)
            {
                return 0;
            }

            if (toRead > remainingFile)
            {
                toRead = remainingFile;
            }

            int read;
            try
            {
                read = entry.Stream.Read(entry.Buffer, 0, toRead);
            }
            catch
            {
                read = 0;
            }

            entry.BufferValidBytes = read;
            entry.BufferIndex = 0;
            entry.CachedPos = entry.CurrentPos + read;

            if (read <= 0)
            {
                return 0;
            }

            int take = count <= read ? count : read;
            Buffer.BlockCopy(entry.Buffer, 0, dst, dstOffset, take);
            entry.BufferIndex = take;

            if (entry.BufferIndex >= entry.BufferValidBytes)
            {
                entry.BufferValidBytes = 0;
                entry.BufferIndex = 0;
            }

            return take;
        }

        private static void UpdateFileSizeAfterWrite_NoLock(FileEntry entry)
        {
            if (entry.CurrentPos > entry.FileSize)
            {
                entry.FileSize = entry.CurrentPos;
            }
        }
    }
}