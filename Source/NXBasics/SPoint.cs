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
            X = rect.Left;
            Y = rect.Top;
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
            int left = rect.Left;
            int x = X;

            if (x < left)
            {
                X = left;
                x = left;
            }

            int top = rect.Top;
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
        internal readonly int IsInside(in SRectangle rect)
        {
            int x = X;
            if (rect.Left <= x && x <= rect.Left + rect.Width - 1)
            {
                int y = Y;
                if (rect.Top <= y && y <= rect.Top + rect.Height - 1)
                {
                    return 1;
                }
            }

            return 0;
        }

        // Convenience wrapper for call-sites that prefer bool (keeps the 1:1 int method intact).
        internal readonly bool IsInsideBool(in SRectangle rect)
        {
            return IsInside(in rect) != 0;
        }
    }
}