using OpenVikings.NXBasics.Structs;

namespace OpenVikings.NXBasics
{
    internal sealed class CHighColorCreator : IDisposable
    {
        // ---- original state / offsets (semantic mapping) ----

        // *this == 0 / 1
        private bool _enabled;

        // this + 0x18/0x1A/0x1C (ushort masks)
        internal ushort _maskR; // param_1
        internal ushort _maskG; // param_2
        internal ushort _maskB; // param_3

        // this + 0x20/0x24/0x28 (int -> used as (byte)&0x1F in GetHighColorWord)
        internal int _blueShiftDown;
        internal int _greenShiftDown;
        internal int _redShiftDown;

        // this + 0x2C/0x30/0x34 (int -> used as (byte)&0x1F in GetHighColorWord)
        internal int _blueShiftUp;
        internal int _greenShiftUp;
        internal int _redShiftUp;

        // this + 0x38/0x3C/0x40 (derived; used in table builds in the dump)
        internal int _derivedR;
        internal int _derivedG;
        internal int _derivedB;

        // this + 0x44 (ushort combined mask)
        internal ushort _combinedMask;

        // this + 0x46/0x47 (flags: rgb565 / rgb555)
        internal bool _isRgb565;
        internal bool _isRgb555;

        // this + 8  (0x40000 bytes -> 65536 uints)
        private uint[]? _trueColorConvertTable;

        // this + 0x10 (0x20000 bytes -> 65536 ushorts)  (exists in dmp; may be used elsewhere)
        private ushort[]? _scratchTable16;

        private bool _disposed;

        internal CHighColorCreator()
        {
            // C++ ctor: *this=0; ptr8=0; ptr10=0;
            _enabled = false;
            _trueColorConvertTable = null;
            _scratchTable16 = null;
        }

        internal bool IsEnabled => !_disposed && _enabled;

        // ---- C++: GetHighColorWord(SColorRGB const&) / GetHighColorWord(uint,uint,uint) ----
        internal ushort GetHighColorWord(byte r, byte g, byte b)
        {
            if (!IsEnabled)
            {
                return 0;
            }

            uint pr = (uint)(r >> (_redShiftDown & 0x1F)) << (_redShiftUp & 0x1F);
            uint pg = (uint)(g >> (_greenShiftDown & 0x1F)) << (_greenShiftUp & 0x1F);
            uint pb = (uint)(b >> (_blueShiftDown & 0x1F)) << (_blueShiftUp & 0x1F);

            return (ushort)((pr | pg | pb) & 0xFFFFu);
        }

        internal ushort GetHighColorWord(in SColorRGB color)
        {
            return GetHighColorWord(color.R, color.G, color.B);
        }

        // ---- C++: SetHighColorMasks(ushort,ushort,ushort) ----
        internal void SetHighColorMasks(ushort maskR, ushort maskG, ushort maskB)
        {
            _maskR = maskR;
            _maskG = maskG;
            _maskB = maskB;

            // "zero bits from right" == first set bit index
            int rUp = (int)l_GetNumberOfZeroBitsFromRight(maskR);
            int gUp = (int)l_GetNumberOfZeroBitsFromRight(maskG);
            int bUp = (int)l_GetNumberOfZeroBitsFromRight(maskB);

            _redShiftUp = rUp;
            _greenShiftUp = gUp;
            _blueShiftUp = bUp;

            int rSet = (int)l_GetNumberOfSetBits(maskR, (uint)rUp);
            int gSet = (int)l_GetNumberOfSetBits(maskG, (uint)gUp);
            int bSet = (int)l_GetNumberOfSetBits(maskB, (uint)bUp);

            // C++: *(int*)(this+0x28)=8 - iVar22; (red down)
            // Here: down = 8 - (#bits)
            _redShiftDown = 8 - rSet;
            _greenShiftDown = 8 - gSet;
            _blueShiftDown = 8 - bSet;

            // C++ derived: *(int*)(this+0x38)=iVar24+iVar48-5; etc
            _derivedR = rSet + rUp - 5;
            _derivedG = gSet + gUp - 5;
            _derivedB = bSet + bUp - 5;

            _enabled = true;

            // combined mask (dump at 0x44)
            _combinedMask =
                (ushort)(((0x7Fu >> ((8 - rSet) & 0x1F)) << (rUp & 0x1F)) |
                         ((0x7Fu >> ((8 - gSet) & 0x1F)) << (gUp & 0x1F)) |
                         ((0x7Fu >> ((8 - bSet) & 0x1F)) << (bUp & 0x1F)));

            _isRgb565 = (maskG == 0x07E0 && maskR == 0xF800 && maskB == 0x001F);
            _isRgb555 = (maskG == 0x03E0 && maskR == 0x7C00 && maskB == 0x001F);

            // C++ deletes old tables and recreates ptr+0x10 (0x20000) here
            _trueColorConvertTable = null;
            _scratchTable16 = new ushort[65536];
        }

        // ---- C++: GetTrueColorConvertTablePtr() ----
        // In C++ it allocates once and fills based on flags; here: lazy create & fill.
        internal uint[] GetTrueColorConvertTablePtr(CTrueColorCreator trueColorCreator)
        {
            if (_trueColorConvertTable != null)
            {
                return _trueColorConvertTable;
            }

            uint[] table = new uint[65536];

            // Fast special cases in dump (rgb565 / rgb555). The dump writes patterns;
            // functionally this is simply correct component expansion then pack through trueColorCreator.
            if (_isRgb565)
            {
                for (int w = 0; w < 65536; w++)
                {
                    uint ww = (uint)w;

                    uint r5 = (ww >> 11) & 0x1Fu;
                    uint g6 = (ww >> 5) & 0x3Fu;
                    uint b5 = ww & 0x1Fu;

                    byte r = (byte)((r5 * 255u) / 31u);
                    byte g = (byte)((g6 * 255u) / 63u);
                    byte b = (byte)((b5 * 255u) / 31u);

                    table[w] = trueColorCreator.GetTrueColorWord(r, g, b);
                }

                _trueColorConvertTable = table;
                return table;
            }

            if (_isRgb555)
            {
                for (int w = 0; w < 65536; w++)
                {
                    uint ww = (uint)w;

                    uint r5 = (ww >> 10) & 0x1Fu;
                    uint g5 = (ww >> 5) & 0x1Fu;
                    uint b5 = ww & 0x1Fu;

                    byte r = (byte)((r5 * 255u) / 31u);
                    byte g = (byte)((g5 * 255u) / 31u);
                    byte b = (byte)((b5 * 255u) / 31u);

                    table[w] = trueColorCreator.GetTrueColorWord(r, g, b);
                }

                _trueColorConvertTable = table;
                return table;
            }

            // Generic: use THIS creator’s shifts to extract approximate 8-bit components, then pack via trueColorCreator.
            // This matches the intent of the generic branch in the dump (it uses shifts and <<3).
            for (int w = 0; w < 65536; w++)
            {
                ushort ww = (ushort)w;

                uint blue = (uint)((ushort)((ww >> (_blueShiftUp & 0x1F)) & 0xFFu) << (_blueShiftDown & 0x1F));
                uint green = (uint)((ushort)((ww >> (_greenShiftUp & 0x1F)) & 0xFFu) << (_greenShiftDown & 0x1F));
                uint red = (uint)((ushort)((ww >> (_redShiftUp & 0x1F)) & 0xFFu) << (_redShiftDown & 0x1F));

                // clamp to byte range
                byte b = (byte)(blue & 0xFFu);
                byte g = (byte)(green & 0xFFu);
                byte r = (byte)(red & 0xFFu);

                table[w] = trueColorCreator.GetTrueColorWord(r, g, b);
            }

            _trueColorConvertTable = table;
            return table;
        }

        // ---- helpers from dump ----
        private static uint l_GetNumberOfZeroBitsFromRight(ushort value)
        {
            // exact logic of your dump, but compact:
            for (uint i = 0; i < 16; i++)
            {
                if (((value >> (int)i) & 1) != 0)
                {
                    return i;
                }
            }
            return 0;
        }

        private static uint l_GetNumberOfSetBits(ushort value, uint startBit)
        {
            if (startBit >= 16)
            {
                return 0;
            }

            if (((value >> (int)startBit) & 1) == 0)
            {
                return 0;
            }

            uint count = 0;
            for (uint i = startBit; i < 16; i++)
            {
                if (((value >> (int)i) & 1) == 0)
                {
                    return count;
                }

                count++;
            }

            // matches dump behavior: if it reaches bit 15 while still set, returns (0x10-startBit)
            return 16 - startBit;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // C++ dtor deletes ptr8 and ptr10; here: drop references
            _trueColorConvertTable = null;
            _scratchTable16 = null;

            _enabled = false;
            _disposed = true;
        }
    }
}