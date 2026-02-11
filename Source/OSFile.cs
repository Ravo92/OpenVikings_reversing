namespace OpenVikings
{
    [Flags]
    internal enum OSFileOpenMode : byte
    {
        Read = 1,
        Write = 2,
        Append = 4
    }

    internal sealed class OSFile : IDisposable
    {
        private readonly FileStream _stream;

        private OSFile(FileStream stream)
        {
            _stream = stream;
        }

        internal long Position => _stream.Position;
        internal long Length => _stream.Length;

        internal static OSFile Open(string path, OSFileOpenMode mode)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path is empty.", nameof(path));
            }

            bool wantsWrite = (mode & OSFileOpenMode.Write) != 0;
            bool wantsAppend = (mode & OSFileOpenMode.Append) != 0;

            if (wantsAppend)
            {
                FileStream appendStream = new(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                return new OSFile(appendStream);
            }

            if (wantsWrite)
            {
                FileStream writeStream = new(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                return new OSFile(writeStream);
            }

            // Read (with lowercase fallback like the original PhysFS logic)
            try
            {
                FileStream readStream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return new OSFile(readStream);
            }
            catch (FileNotFoundException)
            {
                string lower = path.ToLowerInvariant();
                FileStream readStream = new(lower, FileMode.Open, FileAccess.Read, FileShare.Read);
                return new OSFile(readStream);
            }
        }

        internal int Read(byte[] buffer, int offset, int count)
        {
            return _stream.Read(buffer, offset, count);
        }

        internal void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
        }

        internal void Seek(int position)
        {
            _stream.Seek(position, SeekOrigin.Begin);
        }

        internal void Rewind()
        {
            _stream.Seek(0, SeekOrigin.Begin);
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}
