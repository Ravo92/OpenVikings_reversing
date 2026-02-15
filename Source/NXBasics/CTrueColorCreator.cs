using OpenVikings.NXBasics.Structs;

namespace OpenVikings.NXBasics
{
    internal sealed class CTrueColorCreator
    {
        private bool _enabled;

        // this + 4 / +8 / +0xC (masks, stored but not used in GetTrueColorWord directly in the dump)
        internal uint _maskR;
        internal uint _maskG;
        internal uint _maskB;

        // this + 0x10 / 0x14 / 0x18 : shiftDown for (B,G,R) in dump usage
        internal int _blueShiftDown;
        internal int _greenShiftDown;
        internal int _redShiftDown;

        // this + 0x1C / 0x20 / 0x24 : shiftUp for (B,G,R) in dump usage
        internal int _blueShiftUp;
        internal int _greenShiftUp;
        internal int _redShiftUp;

        internal CTrueColorCreator()
        {
            _enabled = false;
        }

        internal bool IsEnabled => _enabled;

        internal void SetTrueColorMasks(uint maskR, uint maskG, uint maskB)
        {
            _maskR = maskR;
            _maskG = maskG;
            _maskB = maskB;

            uint rUp = l_GetNumberOfZeroBitsFromRight(maskR);
            uint gUp = l_GetNumberOfZeroBitsFromRight(maskG);
            uint bUp = l_GetNumberOfZeroBitsFromRight(maskB);

            _blueShiftUp = (int)rUp;  // NOTE: dump stores rUp at +0x1C but uses it with param_1 in the "r" position
            _greenShiftUp = (int)gUp;
            _redShiftUp = (int)bUp;

            int rSet = l_GetNumberOfSetBits(maskR, rUp);
            int gSet = l_GetNumberOfSetBits(maskG, gUp);
            int bSet = l_GetNumberOfSetBits(maskB, bUp);

            _blueShiftDown = 8 - rSet;
            _greenShiftDown = 8 - gSet;
            _redShiftDown = 8 - bSet;

            _enabled = true;
        }

        // C++: GetTrueColorWord(SColorRGB const&) and GetTrueColorWord(uint,uint,uint)
        internal uint GetTrueColorWord(byte r, byte g, byte b)
        {
            if (!_enabled)
            {
                return 0;
            }

            // dump order:
            // (param_3 >> this[0x18]) << this[0x24]  |
            // (param_2 >> this[0x14]) << this[0x20]  |
            // (param_1 >> this[0x10]) << this[0x1C]
            uint pr = ((uint)b >> (_redShiftDown & 0x1F)) << (_redShiftUp & 0x1F);
            uint pg = ((uint)g >> (_greenShiftDown & 0x1F)) << (_greenShiftUp & 0x1F);
            uint pb = ((uint)r >> (_blueShiftDown & 0x1F)) << (_blueShiftUp & 0x1F);

            return pr | pg | pb;
        }

        internal uint GetTrueColorWord(in SColorRGB color)
        {
            return GetTrueColorWord(color.R, color.G, color.B);
        }

        private static uint l_GetNumberOfZeroBitsFromRight(uint value)
        {
            for (uint i = 0; i < 32; i += 4)
            {
                if (((value >> (int)i) & 1) != 0) return i;
                if (((value >> (int)(i + 1)) & 1) != 0) return i + 1;
                if (((value >> (int)(i + 2)) & 1) != 0) return i + 2;
                if (((value >> (int)(i + 3)) & 1) != 0) return i + 3;
            }
            return 0;
        }

        private static int l_GetNumberOfSetBits(uint value, uint startBit)
        {
            if (startBit >= 32)
            {
                return 0;
            }

            int limit = 32 - (int)startBit;
            int count = 0;

            for (uint i = startBit; i < 32; i++)
            {
                if (((value >> (int)i) & 1) == 0)
                {
                    return count;
                }

                count++;
                if (count == limit)
                {
                    return limit;
                }
            }

            return limit;
        }
    }
}