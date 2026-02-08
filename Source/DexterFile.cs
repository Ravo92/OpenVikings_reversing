using System.Runtime.InteropServices;

namespace OpenVikings
{
    internal static class DexterFile
    {
        private static readonly object _sync = new();

        private static readonly Dictionary<IntPtr, FileEntry> _openFiles = [];

        private static int _defaultFileBufferSize = 0;

        private static string _contentPath;
        private static string _storagePath;

        // Stable unique handle generator (replaces GetHashCode()).
        private static long _nextHandle = 1;

        private static string ResolvePath(string path, byte mode)
        {
            string normalized = path.Replace('\\', '/');

            bool useStorage = (mode & 0x26) != 0;
            string prefix = useStorage ? _storagePath : _contentPath;

            if (!string.IsNullOrEmpty(prefix))
            {
                if (!normalized.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    normalized = prefix.TrimEnd('/', '\\') + "/" + normalized;
                }
            }

            return Path.GetFullPath(normalized);
        }

        internal static IntPtr FileOpen(string path, byte mode)
        {
            lock (_sync)
            {
                string resolvedPath = ResolvePath(path, mode);

                bool isWrite = mode == (byte)'!' || mode == 0x01;
                FileAccess access = isWrite ? FileAccess.Write : FileAccess.Read;
                FileMode fileMode = isWrite ? FileMode.Create : FileMode.Open;

                FileShare share = isWrite ? FileShare.Read : FileShare.Read;

                FileStream stream;
                try
                {
                    stream = new FileStream(resolvedPath, fileMode, access, share);
                }
                catch
                {
                    return IntPtr.Zero;
                }

                IntPtr handle = new(_nextHandle);
                _nextHandle++;

                FileEntry entry = new(stream, resolvedPath, mode);
                _openFiles[handle] = entry;

                return handle;
            }
        }


        internal static IntPtr FileOpen(byte[] pathBytes, byte mode)
        {
            if (pathBytes == null)
            {
                return IntPtr.Zero;
            }

            int length = Array.IndexOf(pathBytes, (byte)0);
            if (length < 0)
            {
                length = pathBytes.Length;
            }

            string path = System.Text.Encoding.ASCII.GetString(pathBytes, 0, length);
            return FileOpen(path, mode);
        }

        // Original signature kept for compatibility (reads into buffer[0..count)).
        internal static int FileRead(IntPtr file, ref byte value, int count, byte flags)
        {
            if (count <= 0)
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

        // New overload: reads into buffer[offset..offset+count)
        internal static int FileRead(IntPtr file, byte[] buffer, int offset, int count, byte flags)
        {
            if (buffer == null || count <= 0)
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
                if (!_openFiles.TryGetValue(file, out FileEntry entry))
                {
                    return 0;
                }

                int read = entry.Stream.Read(buffer, offset, count);
                entry.Position += read;
                return read;
            }
        }

        // Optional but very useful: writes from buffer[offset..offset+count)
        internal static int FileWrite(IntPtr file, byte[] buffer, int offset, int count)
        {
            if (buffer == null || count <= 0)
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
                if (!_openFiles.TryGetValue(file, out FileEntry entry))
                {
                    return 0;
                }

                entry.Stream.Write(buffer, offset, count);
                entry.Position += count;

                return count;
            }
        }

        internal static int FileWrite(IntPtr file, ref byte value, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            byte[] temp = new byte[count];
            temp[0] = value;

            for (int i = 1; i < count; i++)
            {
                temp[i] = temp[0];
            }

            return FileWrite(file, temp, 0, count);
        }

        internal static void FileSeek(IntPtr file, int offset, byte origin)
        {
            lock (_sync)
            {
                if (!_openFiles.TryGetValue(file, out FileEntry entry))
                {
                    return;
                }

                SeekOrigin seekOrigin =
                    origin == 0 ? SeekOrigin.Begin :
                    origin == 1 ? SeekOrigin.Current :
                    origin == 2 ? SeekOrigin.End :
                    SeekOrigin.Begin;

                long target = entry.Stream.Seek(offset, seekOrigin);

                entry.Position = target;
                entry.Size = entry.Stream.Length;
            }
        }

        internal static int FilePoss(IntPtr file)
        {
            lock (_sync)
            {
                if (!_openFiles.TryGetValue(file, out FileEntry entry))
                {
                    return -1;
                }

                return unchecked((int)entry.Stream.Position);
            }
        }

        internal static int FileSize(IntPtr file)
        {
            lock (_sync)
            {
                if (!_openFiles.TryGetValue(file, out FileEntry entry))
                {
                    return 0;
                }

                long length = entry.Stream.Length;
                entry.Size = length;

                if (length > int.MaxValue)
                {
                    return int.MaxValue;
                }

                return (int)length;
            }
        }

        internal static ulong FileEOF(IntPtr file)
        {
            lock (_sync)
            {
                if (!_openFiles.TryGetValue(file, out FileEntry entry))
                {
                    return 1;
                }

                return entry.Stream.Position >= entry.Stream.Length ? 1UL : 0UL;
            }
        }

        internal static void FileClose(IntPtr file)
        {
            lock (_sync)
            {
                if (!_openFiles.TryGetValue(file, out FileEntry entry))
                {
                    return;
                }

                entry.Stream.Dispose();
                _openFiles.Remove(file);
            }
        }

        internal static bool FileExists(string path, byte mode)
        {
            lock (_sync)
            {
                string resolved;

                try
                {
                    resolved = ResolvePath(path, mode);
                }
                catch
                {
                    return false;
                }

                try
                {
                    using FileStream stream = new(resolved, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        internal static bool FileExists(byte[] pathBytes, byte mode)
        {
            if (pathBytes == null)
            {
                return false;
            }

            int length = Array.IndexOf(pathBytes, (byte)0);
            if (length < 0)
            {
                length = pathBytes.Length;
            }

            string path = System.Text.Encoding.ASCII.GetString(pathBytes, 0, length);
            return FileExists(path, mode);
        }

        internal static void MakeDir(string path, byte mode)
        {
            string resolved = ResolvePath(path, mode);
            Directory.CreateDirectory(resolved);
        }

        internal static void MakeDir(byte[] pathBytes, byte mode)
        {
            if (pathBytes == null)
            {
                return;
            }

            int length = Array.IndexOf(pathBytes, (byte)0);
            if (length < 0)
            {
                length = pathBytes.Length;
            }

            string path = System.Text.Encoding.ASCII.GetString(pathBytes, 0, length);
            MakeDir(path, mode);
        }

        internal static void SetFileBufferSize(int size)
        {
            _defaultFileBufferSize = size;
        }

        internal static void SetContentPath(string path)
        {
            _contentPath = path;
        }

        internal static void SetStoragePath(string path)
        {
            _storagePath = path;
        }
    }

    internal sealed class FileEntry
    {
        internal FileStream Stream;
        internal string Path;
        internal byte Mode;

        internal long Position;
        internal long Size;

        internal FileEntry(FileStream stream, string path, byte mode)
        {
            Stream = stream;
            Path = path;
            Mode = mode;

            Size = stream.Length;
            Position = stream.Position;
        }
    }
}