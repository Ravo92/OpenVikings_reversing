namespace OpenVikings.Dexter.Struct
{
    internal struct FileEntry
    {
        internal FileHandle Handle; // +0x00 (PHYSFS_File*)
        internal int Size;          // +0x08
        internal int Pos;           // +0x0C (logical pos)
        internal int StreamPos;     // +0x10 (OS file pointer pos)
        internal string Path;       // +0x14 (string copy in original)
        internal byte Mode;         // +0x114

        internal byte[]? Buffer;    // +0x118 (ptr)
        internal int BufferSize;    // +0x120
        internal int BufferOffset;  // +0x124 (>=0 means valid; -1 invalid)
        internal int BufferCount;   // how many bytes currently valid in buffer

        internal static FileEntry CreateEmpty()
        {
            FileEntry entry = new()
            {
                Handle = new FileHandle(0),
                Size = 0,
                Pos = 0,
                StreamPos = 0,
                Path = string.Empty,
                Mode = 0x10, // matches the C++ default 0x10 seen in init
                Buffer = null,
                BufferSize = 0,
                BufferOffset = -1,
                BufferCount = 0
            };
            return entry;
        }
    }
}