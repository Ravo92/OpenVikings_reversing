using OpenVikings.NXBasics.Structs;

namespace OpenVikings.NXBasics
{
    // NXBasics::CPalette
    internal sealed class CPalette : CStorable, IDisposable
    {
        internal const int EntryCount = 256;

        private const int BytesPerEntry = 4;
        private const int PaletteBytes = EntryCount * BytesPerEntry; // 0x400

        // Original layout per entry: [B, G, R, ?]
        private readonly byte[] _data;

        private ushort[]? _highColorTable; // 0x200 bytes
        private uint[]? _trueColorTable;   // 0x400 bytes

        private bool _disposed;

        internal CPalette()
        {
            _data = new byte[PaletteBytes];
            L_InitObject();
            L_MakeAllColorsBlack();
        }

        internal CPalette(CPalette other)
        {
            ArgumentNullException.ThrowIfNull(other);

            _data = new byte[PaletteBytes];
            Buffer.BlockCopy(other._data, 0, _data, 0, PaletteBytes);

            PaletteChanged();
        }

        internal CPalette(CFile file, uint dataSize)
        {
            ArgumentNullException.ThrowIfNull(file);

            _data = new byte[PaletteBytes];

            // Decompile does: memset(+8,0,0x410) then Read(...,0x400)
            L_InitObject();

            // Original ignores dataSize and always reads 0x400
            file.Read(_data, PaletteBytes);

            PaletteChanged();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _highColorTable = null;
            _trueColorTable = null;

            _disposed = true;
        }

        // NXBasics::CPalette::l_InitObject()
        internal void L_InitObject()
        {
            Array.Clear(_data, 0, PaletteBytes);
            PaletteChanged();
        }

        // NXBasics::CPalette::l_MakeAllColorsBlack()
        internal void L_MakeAllColorsBlack()
        {
            Array.Clear(_data, 0, PaletteBytes);
            PaletteChanged();
        }

        // NXBasics::CPalette::CopyIntoPalette(NXBasics::CPalette&) const
        internal void CopyIntoPalette(CPalette target)
        {
            ArgumentNullException.ThrowIfNull(target);

            Buffer.BlockCopy(_data, 0, target._data, 0, PaletteBytes);
            target.PaletteChanged();
        }

        // NXBasics::CPalette::operator=(CPalette const&)
        internal void AssignFrom(CPalette source)
        {
            ArgumentNullException.ThrowIfNull(source);

            // Original frees caches before AND after copy; net effect: invalidate caches.
            PaletteChanged();
            Buffer.BlockCopy(source._data, 0, _data, 0, PaletteBytes);
            PaletteChanged();
        }

        internal void FreeHighColorTable()
        {
            _highColorTable = null;
        }

        internal void FreeTrueColorTable()
        {
            _trueColorTable = null;
        }

        internal void PaletteChanged()
        {
            _highColorTable = null;
            _trueColorTable = null;
        }

        internal void SetEntry(int index, byte r, byte g, byte b)
        {
            if ((uint)index >= EntryCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            int o = index * BytesPerEntry;

            _data[o + 0] = b;
            _data[o + 1] = g;
            _data[o + 2] = r;
            // _data[o + 3] unused in decompile

            PaletteChanged();
        }

        internal void GetEntry(int index, out byte r, out byte g, out byte b)
        {
            if ((uint)index >= EntryCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            int o = index * BytesPerEntry;

            b = _data[o + 0];
            g = _data[o + 1];
            r = _data[o + 2];
        }

        // NXBasics::CPalette::Storable_SaveData(NXBasics::CFile&)
        internal override void Storable_SaveData(CFile file)
        {
            ArgumentNullException.ThrowIfNull(file);
            file.Write(_data, PaletteBytes);
        }

        // NXBasics::CPalette::Storable_GetId() const
        internal override uint Storable_GetId()
        {
            return 0x3F6u;
        }

        // ------------------------------------------------------------
        // FindMatchingColor (weighted squared distance)
        // Decompile reads:
        //   R: entry*4 + 10
        //   G: entry*4 + 9
        //   B: entry*4 + 8
        // In our data: [B,G,R,_] => offsets 0,1,2
        // ------------------------------------------------------------
        internal byte FindMatchingColor(byte r, byte g, byte b)
        {
            uint bestScore = 0x7fffffff;
            int bestIndex = 0;

            for (int i = 0; i < EntryCount; i++)
            {
                int o = i * BytesPerEntry;

                int dr = AbsInt(r - _data[o + 2]);
                int dg = AbsInt(g - _data[o + 1]);
                int db = AbsInt(b - _data[o + 0]);

                uint score = (uint)(db * 0x0F * db * 0x0F
                                  + dg * 0x41 * dg * 0x41
                                  + dr * 0x1D * dr * 0x1D);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;

                    if (score == 0)
                    {
                        break;
                    }
                }
            }

            return (byte)bestIndex;
        }

        internal byte FindMatchingColor(SColorRGB color)
        {
            return FindMatchingColor(color.R, color.G, color.B);
        }

        // NXBasics::CPalette::FindNearColor(..., threshold, manhattan)
        internal byte FindNearColor(byte r, byte g, byte b, uint threshold, bool manhattan)
        {
            uint bestScore = 0x7fffffff;
            int bestIndex = 0;

            for (int i = 0; i < EntryCount; i++)
            {
                int o = i * BytesPerEntry;

                int dr = AbsInt(r - _data[o + 2]);
                int dg = AbsInt(g - _data[o + 1]);
                int db = AbsInt(b - _data[o + 0]);

                uint score = manhattan
                    ? (uint)(dr + dg + db)
                    : (uint)(db * 0x0F * db * 0x0F
                           + dg * 0x41 * dg * 0x41
                           + dr * 0x1D * dr * 0x1D);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;

                    if (score <= threshold)
                    {
                        return (byte)i;
                    }
                }
            }

            return (byte)bestIndex;
        }

        // NXBasics::CPalette::Modify(SColorModifier const&)
        internal void Modify(SColorModifier modifier)
        {
            for (int i = 0; i < EntryCount; i++)
            {
                int o = i * 4;

                byte b = _data[o + 0];
                byte g = _data[o + 1];
                byte r = _data[o + 2];

                modifier.Apply(ref r, ref g, ref b);

                _data[o + 0] = b;
                _data[o + 1] = g;
                _data[o + 2] = r;
            }

            PaletteChanged();
        }

        internal ushort[]? GetHighColorTablePtr()
        {
            EnsureHighColorTableBuilt();
            return _highColorTable;
        }

        internal ushort GetHighColorWord(uint index)
        {
            EnsureHighColorTableBuilt();

            if (_highColorTable == null || index >= EntryCount)
            {
                return 0;
            }

            return _highColorTable[index];
        }

        internal uint[]? GetTrueColorTablePtr()
        {
            EnsureTrueColorTableBuilt();
            return _trueColorTable;
        }

        internal uint GetTrueColorWord(uint index)
        {
            EnsureTrueColorTableBuilt();

            if (_trueColorTable == null || index >= EntryCount)
            {
                return 0;
            }

            return _trueColorTable[index];
        }

        internal void BuildHighColorTable()
        {
            _highColorTable = null;
            EnsureHighColorTableBuilt();
        }

        internal void BuildTrueColorTable()
        {
            _trueColorTable = null;
            EnsureTrueColorTableBuilt();
        }

        private void EnsureHighColorTableBuilt()
        {
            if (_highColorTable != null)
            {
                return;
            }

            CHighColorCreator? creator = CXBSystemManager.sHighColorCreatorPtr;
            if (creator == null || !creator.IsEnabled)
            {
                return;
            }

            ushort[] table = new ushort[EntryCount];

            for (int i = 0; i < EntryCount; i++)
            {
                int o = i * BytesPerEntry;
                byte b = _data[o + 0];
                byte g = _data[o + 1];
                byte r = _data[o + 2];

                table[i] = creator.GetHighColorWord(r, g, b);
            }

            _highColorTable = table;
        }

        private void EnsureTrueColorTableBuilt()
        {
            if (_trueColorTable != null)
            {
                return;
            }

            CTrueColorCreator? creator = CXBSystemManager.sTrueColorCreatorPtr;
            if (creator == null || !creator.IsEnabled)
            {
                return;
            }

            uint[] table = new uint[EntryCount];

            for (int i = 0; i < EntryCount; i++)
            {
                int o = i * BytesPerEntry;
                byte b = _data[o + 0];
                byte g = _data[o + 1];
                byte r = _data[o + 2];

                table[i] = creator.GetTrueColorWord(r, g, b);
            }

            _trueColorTable = table;
        }

        private static int AbsInt(int value)
        {
            return value < 0 ? -value : value;
        }
    }
}