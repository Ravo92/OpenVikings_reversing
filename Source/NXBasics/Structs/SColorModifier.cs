namespace OpenVikings.NXBasics.Structs
{
    internal readonly struct SColorModifier
    {
        private readonly bool _useGrayBase;
        private readonly float _factorR;
        private readonly float _factorG;
        private readonly float _factorB;

        internal SColorModifier(bool useGrayBase, float factorR, float factorG, float factorB)
        {
            _useGrayBase = useGrayBase;
            _factorR = factorR;
            _factorG = factorG;
            _factorB = factorB;
        }

        internal void Apply(ref byte r, ref byte g, ref byte b)
        {
            int srcR = r;
            int srcG = g;
            int srcB = b;

            if (_useGrayBase)
            {
                int gray = (srcB + srcG + srcR) / 3;
                srcR = gray;
                srcG = gray;
                srcB = gray;
            }

            int scaledR = (int)(srcR * _factorR);
            int scaledG = (int)(srcG * _factorG);
            int scaledB = (int)(srcB * _factorB);

            int overflowR = scaledR - 255;
            if (scaledR < 256)
            {
                overflowR = 0;
            }

            int overflowG = scaledG - 255;
            if (scaledG < 256)
            {
                overflowG = 0;
            }

            int overflowB = scaledB - 255;
            if (scaledB < 256)
            {
                overflowB = 0;
            }

            int halfOverflowR = overflowR;
            if (overflowR < 1)
            {
                halfOverflowR = 0;
            }
            else
            {
                halfOverflowR = overflowR >> 1;
            }

            int halfOverflowG = overflowG;
            if (overflowG < 1)
            {
                halfOverflowG = 0;
            }
            else
            {
                halfOverflowG = overflowG >> 1;
            }

            int halfOverflowB = overflowB;
            if (overflowB < 1)
            {
                halfOverflowB = 0;
            }
            else
            {
                halfOverflowB = overflowB >> 1;
            }

            int outB = scaledB + halfOverflowR + halfOverflowG;
            int outG = scaledG + halfOverflowR + halfOverflowB;
            int outR = scaledR + halfOverflowB + halfOverflowG;

            if (outR < 0) { outR = 0; }
            if (outG < 0) { outG = 0; }
            if (outB < 0) { outB = 0; }

            r = ClampToByte_254To255(outR);
            g = ClampToByte_254To255(outG);
            b = ClampToByte_254To255(outB);
        }

        private static byte ClampToByte_254To255(int value)
        {
            if (value > 0xFE)
            {
                return 0xFF;
            }

            if (value < 0)
            {
                return 0;
            }

            return (byte)value;
        }
    }
}