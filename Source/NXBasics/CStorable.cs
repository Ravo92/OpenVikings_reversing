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

            file.WriteLong(unchecked(id));
            file.WriteLong(unchecked(version));

            Storable_SaveData(file);
        }

        // NXBasics::CStorable::Storable_Save(char const*, bool)
        internal bool Storable_Save(string filePath)
        {
            // The original passes param2 into CFile::CFile as part of its ctor arguments.
            // In our reimplementation we keep the signature for parity, but the flag is not required here.

            using CFile file = new(filePath, false);
            bool opened = file.OpenForWriting();
            if (!opened)
            {
                return false;
            }

            uint id = Storable_GetId();
            uint version = Storable_GetVersion();

            file.WriteLong(unchecked(id));
            file.WriteLong(unchecked(version));

            Storable_SaveData(file);
            return true;
        }

        // Helper for cases where the decompile writes a null object.
        internal static void Storable_SaveNull(CFile file)
        {
            file.WriteLong(0);
            file.WriteLong(0);
        }

        // Placeholder hook: you likely already have/need this for XB_Storable_LoadObject.
        internal static CStorable XB_Storable_LoadObject(CFile file)
        {
            // Implement with your factory/registry by ID.
            // Read ID + version, then create the right instance and let it load its data.
            uint id = file.ReadLong();
            uint version = file.ReadLong();

            if (id == 0 && version == 0)
            {
                return null;
            }

            return StorableFactory.Load(id, version, file);
        }
    }

    // Minimal placeholder for your loader strategy (you already have something similar in your engine).
    internal static class StorableFactory
    {
        internal static CStorable Load(uint id, uint version, CFile file)
        {
            // TODO: register types by id and construct them.
            // Each type would have a ctor (CFile file, uint version) or a LoadData method.
            throw new NotImplementedException();
        }
    }
}