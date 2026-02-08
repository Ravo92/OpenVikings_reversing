namespace OpenVikings.NXBasics
{
    internal struct SPoint : IEquatable<SPoint>
    {
        internal int X;
        internal int Y;

        internal SPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        internal SPoint(in SPoint other)
        {
            X = other.X;
            Y = other.Y;
        }

        internal SPoint(in SRectangle rect)
        {
            X = rect.X;
            Y = rect.Y;
        }

        public static bool operator ==(SPoint left, SPoint right)
        {
            return left.X == right.X && left.Y == right.Y;
        }

        public static bool operator !=(SPoint left, SPoint right)
        {
            return !(left == right);
        }

        public bool Equals(SPoint other)
        {
            return this == other;
        }

        public override bool Equals(object? obj)
        {
            if (obj is SPoint point)
            {
                return this == point;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        internal void PlaceInside(in SRectangle rect)
        {
            int rectX = rect.X;
            int rectY = rect.Y;

            int thisX = X;
            if (thisX < rectX)
            {
                X = rectX;
                thisX = rectX;
            }

            int thisY = Y;
            if (thisY < rectY)
            {
                Y = rectY;
                thisY = rectY;
            }

            int maxX = rectX + rect.Width - 1;
            if (maxX < thisX)
            {
                X = maxX;
            }

            int maxY = rectY + rect.Height - 1;
            if (maxY < thisY)
            {
                Y = maxY;
            }
        }

        internal bool IsInside(in SRectangle rect)
        {
            int rectX = rect.X;

            if (rectX <= X && X <= rectX + rect.Width - 1)
            {
                int rectY = rect.Y;
                int thisY = Y;

                if (rectY <= thisY)
                {
                    return thisY <= rectY + rect.Height - 1;
                }
            }

            return false;
        }
    }
}