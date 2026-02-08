using OpenVikings.NXBasics.Structs;

namespace OpenVikings.NXBasics
{
    internal sealed class CTrueColorCreator
    {
        // RE: *this == 0 means "disabled".
        internal bool Enabled { get; private set; }

        // RE: stored masks
        internal uint RMask { get; private set; }  // +0x04
        internal uint GMask { get; private set; }  // +0x08
        internal uint BMask { get; private set; }  // +0x0C

        // RE: shifts used by GetTrueColorWord
        internal int RSrcShift { get; private set; } // +0x10
        internal int GSrcShift { get; private set; } // +0x14
        internal int BSrcShift { get; private set; } // +0x18

        internal int RDstShift { get; private set; } // +0x1C
        internal int GDstShift { get; private set; } // +0x20
        internal int BDstShift { get; private set; } // +0x24

        internal CTrueColorCreator()
        {
            Enabled = false;
        }

        internal bool IsEnabled
        {
            get
            {
                return Enabled;
            }
        }

        internal uint GetTrueColorWord(in SColorRGB color)
        {
            return GetTrueColorWord(color.R, color.G, color.B);
        }

        internal uint GetTrueColorWord(byte r, byte g, byte b)
        {
            if (!Enabled)
            {
                return 0;
            }

            uint ru = r;
            uint gu = g;
            uint bu = b;

            int bSrcShift = BSrcShift & 0x1F;
            int bDstShift = BDstShift & 0x1F;

            int gSrcShift = GSrcShift & 0x1F;
            int gDstShift = GDstShift & 0x1F;

            int rSrcShift = RSrcShift & 0x1F;
            int rDstShift = RDstShift & 0x1F;

            uint packed =
                (((bu >> bSrcShift) << bDstShift) & BMask) |
                (((gu >> gSrcShift) << gDstShift) & GMask) |
                (((ru >> rSrcShift) << rDstShift) & RMask);

            return packed;
        }

        internal void SetTrueColorMasks(uint rMask, uint gMask, uint bMask)
        {
            RMask = rMask;
            GMask = gMask;
            BMask = bMask;

            RDstShift = (int)L_GetNumberOfZeroBitsFromRight(rMask);
            int rWidth = L_GetNumberOfSetBits(rMask, (uint)RDstShift);
            RSrcShift = (rWidth >= 8) ? 0 : (8 - rWidth);

            GDstShift = (int)L_GetNumberOfZeroBitsFromRight(gMask);
            int gWidth = L_GetNumberOfSetBits(gMask, (uint)GDstShift);
            GSrcShift = (gWidth >= 8) ? 0 : (8 - gWidth);

            BDstShift = (int)L_GetNumberOfZeroBitsFromRight(bMask);
            int bWidth = L_GetNumberOfSetBits(bMask, (uint)BDstShift);
            BSrcShift = (bWidth >= 8) ? 0 : (8 - bWidth);

            Enabled = true;
        }

        // RE: l_GetNumberOfZeroBitsFromRight(unsigned int)
        private static uint L_GetNumberOfZeroBitsFromRight(uint mask)
        {
            uint bit = 0;

            while (true)
            {
                if (((mask >> (int)(bit & 0x1F)) & 1U) != 0U)
                {
                    return bit;
                }
                if (((mask >> (int)((bit + 1U) & 0x1F)) & 1U) != 0U)
                {
                    return bit + 1U;
                }
                if (((mask >> (int)((bit + 2U) & 0x1F)) & 1U) != 0U)
                {
                    return bit + 2U;
                }
                if (((mask >> (int)((bit + 3U) & 0x1F)) & 1U) != 0U)
                {
                    return bit + 3U;
                }

                bit += 4U;

                if (bit == 0x20U)
                {
                    // RE returns 0 if no bit is set at all
                    return 0;
                }
            }
        }

        // RE: l_GetNumberOfSetBits(unsigned int mask, unsigned int startBit)
        private static int L_GetNumberOfSetBits(uint mask, uint startBit)
        {
            int fallback = 0;

            if (startBit < 0x20U)
            {
                fallback = (int)(0x20U - startBit);

                int i = 0;
                while (true)
                {
                    uint bitIndex = (startBit + (uint)i) & 0x1FU;

                    if (((mask >> (int)bitIndex) & 1U) == 0U)
                    {
                        return i;
                    }

                    uint absoluteIndex = startBit + (uint)i;
                    i++;

                    if (absoluteIndex == 0x1FU)
                    {
                        break;
                    }
                }
            }

            return fallback;
        }
    }
}