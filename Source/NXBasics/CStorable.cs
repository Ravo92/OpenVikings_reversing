namespace OpenVikings.NXBasics
{
    // NXBasics::CStorable
    internal abstract class CStorable
    {
        // NXBasics::CStorable::Storable_GetId() const
        internal abstract uint Storable_GetId();

        // NXBasics::CStorable::Storable_GetVersion() const
        internal virtual uint Storable_GetVersion()
        {
            return 0;
        }

        // NXBasics::CStorable::Storable_SaveData(NXBasics::CFile&)
        internal abstract void Storable_SaveData(CFile file);

        // NXBasics::CStorable::Storable_Save(NXBasics::CFile&)
        internal void Storable_Save(CFile file)
        {
            uint id = Storable_GetId();
            uint version = Storable_GetVersion();

            file.WriteLong(id);
            file.WriteLong(version);

            Storable_SaveData(file);
        }

        // NXBasics::CStorable::Storable_Save(char const*, bool)
        // NXBasics::CStorable::Storable_Save(char const*, bool)
        internal bool Storable_Save(string filePath, bool createDirectories)
        {
            using CFile file = new(filePath, false);

            if (!file.OpenForWriting())
            {
                return false;
            }

            Storable_Save(file);
            return true;
        }

        internal static void Storable_SaveNull(CFile file)
        {
            file.WriteLong(0);
            file.WriteLong(0);
        }
    }
}