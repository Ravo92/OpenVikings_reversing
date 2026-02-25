using OpenVikings.Dexter.Struct;

namespace OpenVikings
{
    // Managed port of OSGeneric.cxx.
    // Notes:
    // - The original uses PhysicsFS (PHYSFS_File*). In C# this is represented as a FileHandle (opaque 64-bit token).
    // - No nint, no pointers, no unsafe.
    // - Many functions are stubs in the original decompile (return 0 / do nothing) and are kept as such.
    // - File open mode logic matches the original bit tests.
    internal sealed class OSGeneric
    {
        internal interface IPhysFsApi
        {
            FileHandle OpenRead(string path);
            FileHandle OpenWrite(string path);
            FileHandle OpenAppend(string path);

            bool Close(FileHandle file);

            long ReadBytes(FileHandle file, byte[] buffer, int offset, int count);
            long WriteBytes(FileHandle file, byte[] buffer, int offset, int count);

            bool Seek(FileHandle file, long position);
            long FileLength(FileHandle file);
        }

        private readonly IPhysFsApi _physfs;

        private readonly object _mutex;
        private bool _mutexHeld;

        internal OSGeneric(IPhysFsApi physfs)
        {
            ArgumentNullException.ThrowIfNull(physfs);

            _physfs = physfs;
            _mutex = new object();
            _mutexHeld = false;
        }

        // OSGeneric::SystemFileOpen(char*, unsigned char)
        internal FileHandle SystemFileOpen(string path, byte mode)
        {
            if (string.IsNullOrEmpty(path))
            {
                return new FileHandle(0);
            }

            // Original:
            // if (((mode & 2) == 0) && ((mode & 0xDF) != 0)) -> write/append
            // else -> read (with lowercase fallback)
            if (((mode & 0x02) == 0) && ((mode & 0xDF) != 0))
            {
                if ((mode & 0x04) == 0)
                {
                    return _physfs.OpenWrite(path);
                }

                return _physfs.OpenAppend(path);
            }

            FileHandle file = _physfs.OpenRead(path);
            if (!file.IsValid)
            {
                // Lowercase fallback (matches DexterString::StringCopy + StringToLower).
                string lower = path.ToLowerInvariant();
                file = _physfs.OpenRead(lower);
            }

            return file;
        }

        // OSGeneric::SystemFileClose(PHYSFS_File*)
        internal void SystemFileClose(FileHandle file)
        {
            if (!file.IsValid)
            {
                return;
            }

            _physfs.Close(file);
        }

        // OSGeneric::SystemFileRead(PHYSFS_File*, void*, unsigned int)
        internal int SystemFileRead(FileHandle file, byte[] buffer, int offset, int count)
        {
            if (!file.IsValid || buffer == null || count <= 0)
            {
                return 0;
            }

            if ((uint)offset > (uint)buffer.Length || (uint)count > (uint)(buffer.Length - offset))
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            long read = _physfs.ReadBytes(file, buffer, offset, count);
            if (read <= 0)
            {
                return 0;
            }

            if (read > int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)read;
        }

        // OSGeneric::SystemFileWrite(PHYSFS_File*, void const*, unsigned int)
        internal int SystemFileWrite(FileHandle file, byte[] buffer, int offset, int count)
        {
            if (!file.IsValid || buffer == null || count <= 0)
            {
                return 0;
            }

            if ((uint)offset > (uint)buffer.Length || (uint)count > (uint)(buffer.Length - offset))
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            long written = _physfs.WriteBytes(file, buffer, offset, count);
            if (written <= 0)
            {
                return 0;
            }

            if (written > int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)written;
        }

        // OSGeneric::SystemFileSeek(PHYSFS_File*, int)
        internal void SystemFileSeek(FileHandle file, int position)
        {
            if (!file.IsValid)
            {
                return;
            }

            _physfs.Seek(file, position);
        }

        // OSGeneric::SystemFileRewind(PHYSFS_File*)
        internal void SystemFileRewind(FileHandle file)
        {
            if (!file.IsValid)
            {
                return;
            }

            _physfs.Seek(file, 0);
        }

        // OSGeneric::SystemFileSize(PHYSFS_File*)
        internal long SystemFileSize(FileHandle file)
        {
            if (!file.IsValid)
            {
                return 0;
            }

            return _physfs.FileLength(file);
        }

        // OSGeneric::MakeDir(char const*)
        internal static ulong MakeDir(string path)
        {
            return 0;
        }

        // OSGeneric::ChangeDir(char const*)
        internal static ulong ChangeDir(string path)
        {
            return 0;
        }

        // OSGeneric::Malloc(unsigned int, unsigned char)
        // The decompile is clearly off (calls malloc with a mangled argument). In managed code, return a byte[].
        internal static byte[] Malloc(int size, byte flags)
        {
            if (size <= 0)
            {
                return [];
            }

            return new byte[size];
        }

        // OSGeneric::Free(void*, unsigned int)
        internal static void Free(byte[]? buffer, uint flags)
        {
            // Managed memory: nothing to do.
        }

        // OSGeneric::MemSet(void*, unsigned char, int)
        internal void MemSet(byte[] buffer, int offset, byte value, int count)
        {
            if (buffer == null || count <= 0)
            {
                return;
            }

            if ((uint)offset > (uint)buffer.Length || (uint)count > (uint)(buffer.Length - offset))
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            buffer.AsSpan(offset, count).Fill(value);
        }

        // OSGeneric::MemCpy(void*, void const*, int)
        internal void MemCpy(byte[] dst, int dstOffset, byte[] src, int srcOffset, int count)
        {
            if (dst == null || src == null || count <= 0)
            {
                return;
            }

            if ((uint)dstOffset > (uint)dst.Length || (uint)count > (uint)(dst.Length - dstOffset))
            {
                throw new ArgumentOutOfRangeException(nameof(dstOffset));
            }

            if ((uint)srcOffset > (uint)src.Length || (uint)count > (uint)(src.Length - srcOffset))
            {
                throw new ArgumentOutOfRangeException(nameof(srcOffset));
            }

            src.AsSpan(srcOffset, count).CopyTo(dst.AsSpan(dstOffset, count));
        }

        // OSGeneric::SignalDexter()
        internal static void SignalDexter()
        {
        }

        // OSGeneric::ThreadRegister(unsigned short)
        internal static void ThreadRegister(ushort threadId)
        {
        }

        // OSGeneric::ThreadRelease(unsigned short)
        internal static void ThreadRelease(ushort threadId)
        {
        }

        // OSGeneric::CleanUp()
        internal static void CleanUp()
        {
        }

        // OSGeneric::ModifyPath(char*)
        internal static void ModifyPath(string path)
        {
        }

        // OSGeneric::SystemFileDelete(char*)
        internal static bool SystemFileDelete(string path)
        {
            return false;
        }

        // OSGeneric::Time()
        internal static ulong Time()
        {
            return 0;
        }

        // OSGeneric::Pause(unsigned int)
        internal static void Pause(uint milliseconds)
        {
            if (milliseconds == 0)
            {
                return;
            }

            Thread.Sleep(checked((int)milliseconds));
        }

        // OSGeneric::SystemSquareRoot(double)
        internal static double SystemSquareRoot(double value)
        {
            return Math.Sqrt(value);
        }

        // OSGeneric::SetWindowTitle(char const*)
        internal static void SetWindowTitle(string title)
        {
        }

        // OSGeneric::MutexInit()
        internal bool MutexInit()
        {
            // Original returns 1.
            return true;
        }

        // OSGeneric::MutexLock()
        internal void MutexLock()
        {
            if (_mutexHeld)
            {
                return;
            }

            Monitor.Enter(_mutex);
            _mutexHeld = true;
        }

        // OSGeneric::MutexUnLock()
        internal void MutexUnLock()
        {
            if (!_mutexHeld)
            {
                return;
            }

            _mutexHeld = false;
            Monitor.Exit(_mutex);
        }

        // OSGeneric::RelaxThread()
        internal static void RelaxThread()
        {
            Thread.Yield();
        }

        // OSGeneric::FailRequester(char const*)
        internal static void FailRequester(string message)
        {
        }

        // OSGeneric::SystemCreateDebugConsole()
        internal static byte SystemCreateDebugConsole()
        {
            // Original returns 1.
            return 1;
        }

        // OSGeneric::SystemDebugConsoleOutput(char const*)
        internal static void SystemDebugConsoleOutput(string message)
        {
        }

        // OSGeneric::SystemOpenURL(char*)
        internal static ulong SystemOpenURL(string url)
        {
            return 0;
        }
    }
}