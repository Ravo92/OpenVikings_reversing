namespace OpenVikings.NXBasics.Structs
{
    internal readonly struct SColorRGB
    {
        internal byte R { get; }
        internal byte G { get; }
        internal byte B { get; }

        internal SColorRGB(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        internal SColorRGB Modify(SColorModifier modifier)
        {
            byte r = R;
            byte g = G;
            byte b = B;

            modifier.Apply(ref r, ref g, ref b);

            return new SColorRGB(r, g, b);
        }
    }
}