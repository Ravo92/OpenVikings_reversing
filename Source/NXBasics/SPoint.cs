namespace OpenVikings.NXBasics
{
    internal struct SPoint
    {
        internal int X;
        internal int Y;

        // NXBasics::SPoint::SPoint(NXBasics::SPoint const&)
        internal SPoint(in SPoint other)
        {
            X = other.X;
            Y = other.Y;
        }

        // NXBasics::SPoint::SPoint(NXBasics::SRectangle const&)
        internal SPoint(in SRectangle rect)
        {
            X = rect.X;
            Y = rect.Y;
        }

        // NXBasics::TEMPNAMEPLACEHOLDERVALUE(NXBasics::SPoint const&, NXBasics::SPoint const&)
        public static bool operator ==(SPoint left, SPoint right)
        {
            return left.Y == right.Y && left.X == right.X;
        }

        public static bool operator !=(SPoint left, SPoint right)
        {
            return !(left == right);
        }

        public override bool Equals(object? obj)
        {
            if (obj is SPoint other)
            {
                return this == other;
            }

            return false;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        // NXBasics::SPoint::PlaceInside(NXBasics::SRectangle const&)
        internal void PlaceInside(in SRectangle rect)
        {
            int left = rect.X;
            int x = X;

            if (x < left)
            {
                X = left;
                x = left;
            }

            int top = rect.Y;
            int y = Y;

            if (y < top)
            {
                Y = top;
                y = top;
            }

            int right = left + rect.Width - 1;
            if (right < x)
            {
                X = right;
            }

            int bottom = top + rect.Height - 1;
            if (bottom < y)
            {
                Y = bottom;
            }
        }

        // NXBasics::SPoint::IsInside(NXBasics::SRectangle const&) const
        internal readonly bool IsInside(in SRectangle rect)
        {
            int x = X;
            if (rect.X <= x && x <= rect.X + rect.Width - 1)
            {
                int y = Y;
                if (rect.Y <= y && y <= rect.Y + rect.Height - 1)
                {
                    return true;
                }
            }

            return false;
        }
    }
}