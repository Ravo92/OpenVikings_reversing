namespace OpenVikings.Dexter.Struct
{
    internal readonly struct DexCol
    {
        internal DexCol(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        internal byte R { get; }
        internal byte G { get; }
        internal byte B { get; }
        internal byte A { get; }
    }
}
