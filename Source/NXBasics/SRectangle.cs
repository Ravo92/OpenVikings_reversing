namespace OpenVikings.NXBasics
{
    // NXBasics::SRectangle
    internal struct SRectangle : IEquatable<SRectangle>
    {
        internal int X;
        internal int Y;
        internal int Width;
        internal int Height;

        // NXBasics::SRectangle::SRectangle(int, int, int, int)
        internal SRectangle(int left, int top, int width, int height)
        {
            X = left;
            Y = top;
            Width = width;
            Height = height;
        }

        // NXBasics::SRectangle::SRectangle(int, int)
        internal SRectangle(int width, int height)
        {
            X = 0;
            Y = 0;
            Width = width;
            Height = height;
        }

        // NXBasics::SRectangle::SRectangle(NXBasics::SRectangle const&)
        internal SRectangle(in SRectangle other)
        {
            X = other.X;
            Y = other.Y;
            Width = other.Width;
            Height = other.Height;
        }

        // NXBasics::SRectangle::SRectangle(NXBasics::SPoint const&)
        internal SRectangle(in SPoint point)
        {
            X = point.X;
            Y = point.Y;
            Width = 1;
            Height = 1;
        }

        // NXBasics::operator==(SRectangle const&, SRectangle const&)
        public static bool operator ==(SRectangle a, SRectangle b)
        {
            return a.X == b.X &&
                   a.Y == b.Y &&
                   a.Width == b.Width &&
                   a.Height == b.Height;
        }

        public static bool operator !=(SRectangle a, SRectangle b)
        {
            return !(a == b);
        }

        public override bool Equals(object? obj)
        {
            if (obj is SRectangle other)
            {
                return Equals(other);
            }

            return false;
        }

        public bool Equals(SRectangle other)
        {
            return this == other;
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(X, Y, Width, Height);
        }

        // NXBasics::SRectangle::Init()
        internal void Init()
        {
            X = 0;
            Y = 0;
            Width = 0;
            Height = 0;
        }

        // NXBasics::SRectangle::SetVariables(int, int, int, int)
        internal void SetVariables(int left, int top, int width, int height)
        {
            X = left;
            Y = top;
            Width = width;
            Height = height;
        }

        // NXBasics::SRectangle::MovePosition(int, int)
        internal void MovePosition(int dx, int dy)
        {
            X += dx;
            Y += dy;
        }

        // NXBasics::SRectangle::BlowUpSize(int, int)
        internal void BlowUpSize(int amountX, int amountY)
        {
            X -= amountX;
            Y -= amountY;
            Width += amountX * 2;
            Height += amountY * 2;
        }

        // NXBasics::SRectangle::PlaceInside(NXBasics::SRectangle const&)
        internal void PlaceInside(in SRectangle bounds)
        {
            int maxW = bounds.Width;
            int w = Width;
            if (maxW < w)
            {
                Width = maxW;
                w = maxW;
            }

            int maxH = bounds.Height;
            int h = Height;
            if (maxH < h)
            {
                Height = maxH;
                h = maxH;
            }

            int left = bounds.X;
            int x = X;
            if (x < left)
            {
                X = left;
                x = left;
            }

            int top = bounds.Y;
            int y = Y;
            if (y < top)
            {
                Y = top;
                y = top;
            }

            if (bounds.Width + bounds.X < x + w)
            {
                X = (bounds.Width + bounds.X) - w;
            }

            if (bounds.Height + bounds.Y < y + h)
            {
                Y = (bounds.Height + bounds.Y) - h;
            }
        }

        // NXBasics::SRectangle::CutInside(NXBasics::SRectangle const&)
        internal void CutInside(in SRectangle clip)
        {
            int x = X;
            int clipX = clip.X;

            if (x < clipX)
            {
                Width = Width + (x - clipX);
                X = clipX;
                x = clipX;
            }

            int y = Y;
            int clipY = clip.Y;

            if (y < clipY)
            {
                Height = Height + (y - clipY);
                Y = clipY;
                y = clipY;
            }

            if (clip.Width + clip.X < Width + x)
            {
                Width = (clip.Width + clip.X) - x;
            }

            if (clip.Height + clip.Y < Height + y)
            {
                Height = (clip.Height + clip.Y) - y;
            }
        }

        // NXBasics::SRectangle::CutInsideX(NXBasics::SRectangle const&)
        internal bool CutInsideX(in SRectangle clip)
        {
            int originalLeft = X;
            int originalTop = Y;

            int x = originalLeft;
            int y = originalTop;

            int clipX = clip.X;
            int clipY = clip.Y;

            bool changed = false;

            if (x < clipX)
            {
                Width = Width + (x - clipX);
                X = clipX;
                x = clipX;
                changed = true;
            }

            if (y < clipY)
            {
                Height = Height + (y - clipY);
                Y = clipY;
                y = clipY;
                changed = true;
            }

            int clipRight = clip.X + clip.Width;
            if (clipRight < Width + x)
            {
                Width = clipRight - x;
                changed = true;
            }

            int clipBottom = clip.Y + clip.Height;
            if (clipBottom < Height + y)
            {
                Height = clipBottom - y;
                changed = true;
            }

            return changed;
        }

        // NXBasics::SRectangle::CombineWith(NXBasics::SRectangle const&)
        internal void CombineWith(in SRectangle other)
        {
            int otherLeft = other.X;
            int left = X;

            int width;
            int anchorLeft;

            if (left - otherLeft == 0 || left < otherLeft)
            {
                width = Width;
                anchorLeft = otherLeft;
            }
            else
            {
                width = (left - otherLeft) + Width;
                Width = width;
                X = otherLeft;
                anchorLeft = otherLeft;
                left = otherLeft;
            }

            if (width + left < anchorLeft + other.Width)
            {
                Width = (anchorLeft + other.Width) - left;
            }

            int otherTop = other.Y;
            int top = Y;

            int height;
            int anchorTop;

            if (top - otherTop == 0 || top < otherTop)
            {
                height = Height;
                anchorTop = otherTop;
            }
            else
            {
                height = (top - otherTop) + Height;
                Height = height;
                Y = otherTop;
                anchorTop = otherTop;
                top = otherTop;
            }

            if (height + top < anchorTop + other.Height)
            {
                Height = (anchorTop + other.Height) - top;
            }
        }

        // NXBasics::SRectangle::IsTouching(NXBasics::SRectangle const&) const
        internal readonly bool IsTouching(in SRectangle other)
        {
            return other.X < X + Width &&
                   X < other.X + other.Width &&
                   other.Y < Y + Height &&
                   Y < other.Y + other.Height;
        }

        // NXBasics::SRectangle::IsEqual(NXBasics::SRectangle const&) const
        internal readonly bool IsEqual(in SRectangle other)
        {
            return other.X == X && other.Y == Y && other.Width == Width && other.Height == Height;
        }

        // NXBasics::SRectangle::IsSizeEqual(NXBasics::SRectangle const&) const
        internal readonly bool IsSizeEqual(in SRectangle other)
        {
            return other.Width == Width && other.Height == Height;
        }

        // NXBasics::SRectangle::IsPositionEqual(NXBasics::SRectangle const&) const
        internal readonly bool IsPositionEqual(in SRectangle other)
        {
            return other.X == X && other.Y == Y;
        }

        // NXBasics::SRectangle::IsInside(NXBasics::SRectangle const&) const
        internal readonly bool IsInside(in SRectangle bounds)
        {
            if (bounds.X <= X)
            {
                if (bounds.Y <= Y &&
                    X + Width - 1 <= bounds.X + bounds.Width - 1)
                {
                    int bottom = Height + Y - 1;
                    return bottom <= bounds.Y + bounds.Height - 1;
                }
            }

            return false;
        }

        // NXBasics::SRectangle::MakeSizeWordAlligned()
        internal void MakeSizeWordAlligned()
        {
            Width = unchecked((int)(unchecked((uint)Width) & 0xFFFEu));
            Height = unchecked((int)(unchecked((uint)Height) & 0xFFFEu));
        }

        // NXBasics::SRectangle::MakeSizeLongAlligned()
        internal void MakeSizeLongAlligned()
        {
            Width = unchecked((int)(unchecked((uint)Width) & 0xFFFCu));
            Height = unchecked((int)(unchecked((uint)Height) & 0xFFFCu));
        }

        // NXBasics::SRectangle::Validate()
        internal void Validate()
        {
            int w = Width;
            if (w < 0)
            {
                X += w;
                Width = -w;
            }

            int h = Height;
            if (h >= 0)
            {
                return;
            }

            Y += h;
            Height = -h;
        }
    }
}