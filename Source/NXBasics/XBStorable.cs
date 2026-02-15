namespace OpenVikings.NXBasics
{
    internal static class XBStorable
    {
        // Mirrors: NXBasics::XB_Storable_LoadObject(char const*)
        internal static CStorable LoadObject(string path)
        {
            using CFile file = new(path, true);

            CStorable? obj = LoadObjectOrNull(file);
            return obj ?? throw new InvalidOperationException($"Null storable header in '{path}'.");
        }

        // Mirrors: NXBasics::XB_Storable_LoadObject(NXBasics::CFile&)
        internal static CStorable LoadObject(CFile file)
        {
            CStorable? obj = LoadObjectOrNull(file);
            return obj ?? throw new InvalidOperationException("Null storable header in stream.");
        }

        internal static CStorable? LoadObjectOrNull(CFile file)
        {
            uint id = file.ReadLong();
            uint version = file.ReadLong();

            if (id == 0 && version == 0)
            {
                return null;
            }

            return id switch
            {
                0x3E9 => new CMemory(file, version),
                0x3F3 => new CBitmap(file),
                0x3F4 => new CBobManager(file),
                0x3F5 => new CFont(file, version),
                0x3F6 => new CPalette(file, version),
                0x3F7 => new CRemapTable(file),
                0x3FD => new CStringArray(file, version),
                _ => throw new InvalidOperationException($"Unknown storable id 0x{id:X} (version {version})."),
            };
        }
    }
}