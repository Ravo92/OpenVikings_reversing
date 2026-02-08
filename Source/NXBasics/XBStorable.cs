namespace OpenVikings.NXBasics
{
    internal static class XBStorable
    {
        // RE type ids (Storable_GetId)
        internal const uint IdMemory = 0x3E9;
        internal const uint IdBitmap = 0x3F3;
        internal const uint IdBobManager = 0x3F4;
        internal const uint IdFont = 0x3F5;
        internal const uint IdPalette = 0x3F6;
        internal const uint IdRemapTable = 0x3F7;
        internal const uint IdStringArray = 0x3FD;

        internal static object? LoadObject(CFile file)
        {
            ArgumentNullException.ThrowIfNull(file);

            uint typeId = file.ReadLong();
            uint dataSize = file.ReadLong();

            return typeId switch
            {
                IdMemory => new CMemory(file, dataSize),
                IdBitmap => new CBitmap(file, dataSize),
                IdBobManager => new CBobManager(file, dataSize),
                IdFont => new CFont(file, dataSize),
                IdPalette => new CPalette(file, dataSize),
                IdRemapTable => (object)new CRemapTable(file, dataSize),
                IdStringArray => (object)new CStringArray(file, dataSize),
                _ => null,// RE: returns 0x0 if unknown
            };
        }

        internal static object? LoadObject(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentException("Filename must not be null/empty.", nameof(filename));
            }

            // RE: CFile::CFile(path, true) then LoadObject(file) then destructor
            using CFile file = new(filename, true);
            return LoadObject(file);
        }
    }
}