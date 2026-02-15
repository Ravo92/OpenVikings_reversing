using OpenVikings.NXBasics.Structs;

namespace OpenVikings.NXBasics
{
    internal sealed class CRemapTable : CStorable
    {
        private const int TableSize = 0x100;
        private readonly byte[] _table;

        // NXBasics::CRemapTable::CRemapTable()
        internal CRemapTable()
        {
            _table = new byte[TableSize];
            L_InitObject();
        }

        // NXBasics::CRemapTable::l_InitObject()
        internal void L_InitObject()
        {
            Array.Clear(_table, 0, _table.Length);
        }

        // NXBasics::CRemapTable::CRemapTable(NXBasics::CPalette const&, NXBasics::CPalette const&, unsigned int, unsigned int)
        internal CRemapTable(CPalette sourcePalette, CPalette targetPalette, uint startIndex, uint endIndex)
        {
            ArgumentNullException.ThrowIfNull(sourcePalette);
            ArgumentNullException.ThrowIfNull(targetPalette);

            _table = new byte[TableSize];
            L_InitObject();
            L_CreateRemapTable(sourcePalette, targetPalette, startIndex, endIndex);
        }

        // NXBasics::CRemapTable::l_CreateRemapTable(NXBasics::CPalette const&, NXBasics::CPalette const&, unsigned int, unsigned int)
        internal void L_CreateRemapTable(CPalette sourcePalette, CPalette targetPalette, uint startIndex, uint endIndex)
        {
            ArgumentNullException.ThrowIfNull(sourcePalette);
            ArgumentNullException.ThrowIfNull(targetPalette);

            if (startIndex > endIndex)
            {
                return;
            }

            if (endIndex >= CPalette.EntryCount)
            {
                endIndex = (uint)CPalette.EntryCount - 1;
            }

            for (uint i = startIndex; i <= endIndex; i++)
            {
                sourcePalette.GetEntry((int)i, out byte r, out byte g, out byte b);
                _table[i] = targetPalette.FindMatchingColor(r, g, b);
            }
        }

        // NXBasics::CRemapTable::CRemapTable(NXBasics::CPalette const&, bool, float, float, float)
        internal CRemapTable(CPalette palette, bool modifierFlag, float p1, float p2, float p3)
        {
            ArgumentNullException.ThrowIfNull(palette);

            _table = new byte[TableSize];
            L_InitObject();

            CPalette modifiedPalette = new(palette);

            SColorModifier modifier = new(modifierFlag, p1, p2, p3);
            modifiedPalette.Modify(modifier);

            for (int i = 0; i < CPalette.EntryCount; i++)
            {
                modifiedPalette.GetEntry(i, out byte r, out byte g, out byte b);
                _table[i] = palette.FindMatchingColor(r, g, b);
            }
        }

        // NXBasics::CRemapTable::CRemapTable(NXBasics::CFile&, unsigned int)
        internal CRemapTable(CFile file)
        {
            ArgumentNullException.ThrowIfNull(file);

            _table = new byte[TableSize];
            L_InitObject();

            int bytesRead = file.Read(_table, TableSize);
            if (bytesRead < TableSize)
            {
                Array.Clear(_table, bytesRead < 0 ? 0 : bytesRead, bytesRead < 0 ? TableSize : (TableSize - bytesRead));
            }
        }

        // NXBasics::CRemapTable::Storable_SaveData(NXBasics::CFile&)
        internal override void Storable_SaveData(CFile file)
        {
            ArgumentNullException.ThrowIfNull(file);
            file.Write(_table, TableSize);
        }

        // NXBasics::CRemapTable::Storable_GetId() const
        internal override uint Storable_GetId()
        {
            return 0x3F7;
        }

        // Compatibility: existing code expects "Table256".
        // Exposes the underlying table without copying.
        internal ReadOnlySpan<byte> Table256
        {
            get { return _table; }
        }

        // If some call sites truly require byte[] (not Span), use this instead.
        // Keep it separate so new code prefers the Span property.
        internal byte[] Table256Bytes
        {
            get { return _table; }
        }

        internal byte this[int index]
        {
            get { return _table[index]; }
            set { _table[index] = value; }
        }

        internal ReadOnlySpan<byte> AsSpan()
        {
            return _table;
        }
    }
}