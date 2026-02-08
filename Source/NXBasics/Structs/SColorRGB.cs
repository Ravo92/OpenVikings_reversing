namespace OpenVikings.NXBasics.Structs
{
    // Minimal RGB + modifier, matching how CPalette uses them.
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
            byte nr = modifier.Apply(R);
            byte ng = modifier.Apply(G);
            byte nb = modifier.Apply(B);
            return new SColorRGB(nr, ng, nb);
        }
    }
}