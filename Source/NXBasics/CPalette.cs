using OpenVikings.NXBasics.Structs;

namespace OpenVikings.NXBasics
{
    // RE facts:
    // - Palette stores 256 entries
    // - Each entry is effectively 4 bytes in memory (0x400 bytes total copied),
    //   and FindMatchingColor reads channels at +8/+9/+10 relative to base.
    // - Two lazy caches exist:
    //   * HighColor table (256 * 2 bytes) at +0x408
    //   * TrueColor table (256 * 4 bytes) at +0x410
    internal sealed class CPalette : IDisposable
    {
        internal const int EntryCount = 256;
        private const int BytesPerEntryOnDisk = 4;
        private const int PaletteBytesOnDisk = EntryCount * BytesPerEntryOnDisk; // 0x400

        // We keep the palette as RGB triplets.
        // RE indexing suggests bytes exist at offsets "entry*4 + {8..10}" (B,G,R in original memory),
        // but for our engine we store explicit R,G,B.
        private readonly byte[] _r;
        private readonly byte[] _g;
        private readonly byte[] _b;

        // Cached tables (RE: allocated with operator_new__ and freed on change)
        private ushort[]? _highColorTable; // 0x200 bytes
        private uint[]? _trueColorTable;   // 0x400 bytes

        private bool _disposed;

        internal CPalette()
        {
            _r = new byte[EntryCount];
            _g = new byte[EntryCount];
            _b = new byte[EntryCount];

            MakeAllColorsBlack();
        }

        internal CPalette(CPalette other)
        {
            _r = new byte[EntryCount];
            _g = new byte[EntryCount];
            _b = new byte[EntryCount];

            if (other != null)
            {
                Array.Copy(other._r, _r, EntryCount);
                Array.Copy(other._g, _g, EntryCount);
                Array.Copy(other._b, _b, EntryCount);
            }

            PaletteChanged();
        }

        internal CPalette(CFile file, uint dataSize)
        {
            ArgumentNullException.ThrowIfNull(file);

            _r = new byte[EntryCount];
            _g = new byte[EntryCount];
            _b = new byte[EntryCount];

            uint bytesToReadU = dataSize;
            if (bytesToReadU > PaletteBytesOnDisk)
            {
                bytesToReadU = PaletteBytesOnDisk;
            }

            int bytesToRead = unchecked((int)bytesToReadU);
            if (bytesToRead < 0)
            {
                bytesToRead = 0;
            }

            byte[] tmp = new byte[bytesToRead];

            int bytesRead = 0;
            if (bytesToRead > 0)
            {
                bytesRead = file.Read(tmp, bytesToRead);
                if (bytesRead < 0)
                {
                    bytesRead = 0;
                }
                if (bytesRead > bytesToRead)
                {
                    bytesRead = bytesToRead;
                }
            }

            int entryCountInFile = bytesRead / BytesPerEntryOnDisk;
            if (entryCountInFile > EntryCount)
            {
                entryCountInFile = EntryCount;
            }

            for (int i = 0; i < entryCountInFile; i++)
            {
                int o = i * BytesPerEntryOnDisk;

                byte b = tmp[o + 0];
                byte g = tmp[o + 1];
                byte r = tmp[o + 2];

                _r[i] = r;
                _g[i] = g;
                _b[i] = b;
            }

            PaletteChanged();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // RE destructor frees both tables.
            _highColorTable = null;
            _trueColorTable = null;
        }

        internal void CopyIntoPalette(CPalette target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            Array.Copy(_r, target._r, EntryCount);
            Array.Copy(_g, target._g, EntryCount);
            Array.Copy(_b, target._b, EntryCount);

            // RE frees caches on target.
            target.PaletteChanged();
        }

        internal void AssignFrom(CPalette source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Array.Copy(source._r, _r, EntryCount);
            Array.Copy(source._g, _g, EntryCount);
            Array.Copy(source._b, _b, EntryCount);

            // RE frees caches twice (defensive); net effect is "invalidate caches".
            PaletteChanged();
        }

        internal void InitObject()
        {
            // RE: memset(this+8, 0, 0x410) -> palette entries + extra fields cleared.
            // In our representation: clear entries + invalidate caches.
            Array.Clear(_r, 0, EntryCount);
            Array.Clear(_g, 0, EntryCount);
            Array.Clear(_b, 0, EntryCount);
            PaletteChanged();
        }

        internal void MakeAllColorsBlack()
        {
            // RE loops writing zeros to every entry and frees both caches.
            Array.Clear(_r, 0, EntryCount);
            Array.Clear(_g, 0, EntryCount);
            Array.Clear(_b, 0, EntryCount);

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
            // RE frees both cached tables.
            _highColorTable = null;
            _trueColorTable = null;
        }

        internal void SetEntry(int index, byte r, byte g, byte b)
        {
            if ((uint)index >= EntryCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            _r[index] = r;
            _g[index] = g;
            _b[index] = b;

            PaletteChanged();
        }

        internal void GetEntry(int index, out byte r, out byte g, out byte b)
        {
            if ((uint)index >= EntryCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            r = _r[index];
            g = _g[index];
            b = _b[index];
        }

        // ------------------------------------------------------------
        // FindMatchingColor (weighted squared distance)
        // RE weights:
        //   blue:  0x0F
        //   green: 0x41
        //   red:   0x1D
        // distance = (db*0x0F)^2 + (dg*0x41)^2 + (dr*0x1D)^2
        // ------------------------------------------------------------
        internal byte FindMatchingColor(byte r, byte g, byte b)
        {
            uint bestScore = 0x7fffffff;
            int bestIndex = 0;

            for (int i = 0; i < EntryCount; i++)
            {
                int dr = AbsInt(r - _r[i]);
                int dg = AbsInt(g - _g[i]);
                int db = AbsInt(b - _b[i]);

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

        // ------------------------------------------------------------
        // FindNearColor
        // param_4 = threshold
        // param_5 = if true -> Manhattan distance, else weighted squared distance (same weights as matching)
        // If a color is found with distance <= threshold, return immediately.
        // Otherwise return best found.
        // ------------------------------------------------------------
        internal byte FindNearColor(byte r, byte g, byte b, uint threshold, bool manhattan)
        {
            uint bestScore = 0x7fffffff;
            int bestIndex = 0;

            for (int i = 0; i < EntryCount; i++)
            {
                int dr = AbsInt(r - _r[i]);
                int dg = AbsInt(g - _g[i]);
                int db = AbsInt(b - _b[i]);

                uint score;

                if (!manhattan)
                {
                    score = (uint)(db * 0x0F * db * 0x0F
                                 + dg * 0x41 * dg * 0x41
                                 + dr * 0x1D * dr * 0x1D);
                }
                else
                {
                    score = (uint)(dr + dg + db);
                }

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

        // ------------------------------------------------------------
        // Modify: applies a modifier to each color, then invalidates caches.
        // RE calls SColorRGB::Modify for each entry.
        // ------------------------------------------------------------
        internal void Modify(SColorModifier modifier)
        {
            for (int i = 0; i < EntryCount; i++)
            {
                SColorRGB color = new(_r[i], _g[i], _b[i]);
                color.Modify(modifier);

                _r[i] = color.R;
                _g[i] = color.G;
                _b[i] = color.B;
            }

            PaletteChanged();
        }

        // ------------------------------------------------------------
        // High/True color table handling
        // RE uses CXBSystemManager::sHighColorCreatorPtr and ::sTrueColorCreatorPtr
        // to create packed formats with source shifts + destination shifts.
        // In C#, we represent these creators as structured config.
        // ------------------------------------------------------------
        internal ushort[]? GetHighColorTablePtr()
        {
            EnsureHighColorTableBuilt();
            return _highColorTable;
        }

        internal ushort GetHighColorWord(uint index)
        {
            EnsureHighColorTableBuilt();

            if (_highColorTable == null)
            {
                return 0;
            }

            if (index >= EntryCount)
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

            if (_trueColorTable == null)
            {
                return 0;
            }

            if (index >= EntryCount)
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

            CHighColorCreator creator = CXBSystemManager.sHighColorCreatorPtr;
            if (creator == null || !creator.IsEnabled)
            {
                return;
            }

            ushort[] table = new ushort[EntryCount];

            for (int i = 0; i < EntryCount; i++)
            {
                table[i] = creator.GetHighColorWord(_r[i], _g[i], _b[i]);
            }

            _highColorTable = table;
        }

        private void EnsureTrueColorTableBuilt()
        {
            if (_trueColorTable != null)
            {
                return;
            }

            CTrueColorCreator creator = CXBSystemManager.sTrueColorCreatorPtr;
            if (creator == null || !creator.IsEnabled)
            {
                return;
            }

            uint[] table = new uint[EntryCount];

            for (int i = 0; i < EntryCount; i++)
            {
                table[i] = creator.GetTrueColorWord(_r[i], _g[i], _b[i]);
            }

            _trueColorTable = table;
        }

        private static int AbsInt(int value)
        {
            return value < 0 ? -value : value;
        }

        internal static ulong Storable_GetId()
        {
            // RE: returns 0x3f6
            return 0x3F6;
        }
    }

    internal readonly struct SColorModifier
    {
        // Placeholder: implement your real modifier logic (brightness, gamma, etc.)
        private readonly int _delta;

        internal SColorModifier(int delta)
        {
            _delta = delta;
        }

        internal byte Apply(byte channel)
        {
            int v = channel + _delta;
            if (v < 0) { v = 0; }
            if (v > 255) { v = 255; }
            return (byte)v;
        }
    }
}