namespace OpenVikings.Dexter.Struct
{
    internal readonly struct DexRect
    {
        internal DexRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        internal int Left { get; }
        internal int Top { get; }
        internal int Right { get; }
        internal int Bottom { get; }

        internal int Width => Right - Left;
        internal int Height => Bottom - Top;

        internal bool IsEmpty => Width <= 0 || Height <= 0;

        internal DexRect ClampTo(int width, int height)
        {
            int l = Left < 0 ? 0 : Left;
            int t = Top < 0 ? 0 : Top;
            int r = Right > width ? width : Right;
            int b = Bottom > height ? height : Bottom;
            if (r < l) r = l;
            if (b < t) b = t;
            return new DexRect(l, t, r, b);
        }
    }
}
