namespace OpenVikings.NXBasics
{
    internal struct SRectangle
    {
        internal int Left;
        internal int Top;
        internal int Width;
        internal int Height;

        // NXBasics::SRectangle::SRectangle(int, int, int, int)
        internal SRectangle(int left, int top, int width, int height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        // NXBasics::SRectangle::SRectangle(int, int)
        internal SRectangle(int width, int height)
        {
            Left = 0;
            Top = 0;
            Width = width;
            Height = height;
        }

        // NXBasics::SRectangle::SRectangle(NXBasics::SRectangle const&)
        internal SRectangle(in SRectangle other)
        {
            Left = other.Left;
            Top = other.Top;
            Width = other.Width;
            Height = other.Height;
        }

        // NXBasics::SRectangle::SRectangle(NXBasics::SPoint const&)
        internal SRectangle(in SPoint point)
        {
            Left = point.X;
            Top = point.Y;
            Width = 1;
            Height = 1;
        }

        // NXBasics::TEMPNAMEPLACEHOLDERVALUE(NXBasics::SRectangle const&, NXBasics::SRectangle const&)
        public static bool operator ==(SRectangle a, SRectangle b)
        {
            return a.Left == b.Left && a.Top == b.Top && a.Width == b.Width && a.Height == b.Height;
        }

        public static bool operator !=(SRectangle a, SRectangle b)
        {
            return !(a == b);
        }

        public override bool Equals(object? obj)
        {
            if (obj is SRectangle other)
            {
                return this == other;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(Left, Top, Width, Height);
        }

        // NXBasics::SRectangle::Init()
        internal void Init()
        {
            Left = 0;
            Top = 0;
            Width = 0;
            Height = 0;
        }

        // NXBasics::SRectangle::SetVariables(int, int, int, int)
        internal void SetVariables(int left, int top, int width, int height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        // NXBasics::SRectangle::MovePosition(int, int)
        internal void MovePosition(int dx, int dy)
        {
            Left += dx;
            Top += dy;
        }

        // NXBasics::SRectangle::BlowUpSize(int, int)
        internal void BlowUpSize(int amountX, int amountY)
        {
            Left -= amountX;
            Top -= amountY;
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

            int left = bounds.Left;
            int x = Left;
            if (x < left)
            {
                Left = left;
                x = left;
            }

            int top = bounds.Top;
            int y = Top;
            if (y < top)
            {
                Top = top;
                y = top;
            }

            if (bounds.Width + bounds.Left < x + w)
            {
                Left = (bounds.Width + bounds.Left) - w;
            }

            if (bounds.Height + bounds.Top < y + h)
            {
                Top = (bounds.Height + bounds.Top) - h;
            }
        }

        // NXBasics::SRectangle::CutInside(NXBasics::SRectangle const&)
        internal void CutInside(in SRectangle clip)
        {
            int x = Left;
            int clipX = clip.Left;

            if (x < clipX)
            {
                Width = Width + (x - clipX);
                Left = clipX;
                x = clipX;
            }

            int y = Top;
            int clipY = clip.Top;

            if (y < clipY)
            {
                Height = Height + (y - clipY);
                Top = clipY;
                y = clipY;
            }

            if (clip.Width + clip.Left < Width + x)
            {
                Width = (clip.Width + clip.Left) - x;
            }

            if (clip.Height + clip.Top < Height + y)
            {
                Height = (clip.Height + clip.Top) - y;
            }
        }

        // NXBasics::SRectangle::CutInsideX(NXBasics::SRectangle const&)
        internal bool CutInsideX(in SRectangle clip)
        {
            int originalLeft = Left;
            int originalTop = Top;

            int x = originalLeft;
            int clipX = clip.Left;
            int newLeft = x;
            bool changed = false;

            if (x < clipX)
            {
                Width = Width + (x - clipX);
                Left = clipX;
                newLeft = clipX;
                changed = true;
            }

            int y = originalTop;
            int clipY = clip.Top;
            int newTop = y;

            if (y < clipY)
            {
                Height = Height + (y - clipY);
                Top = clipY;
                newTop = clipY;
                changed = true;
            }

            int w = Width;
            int clipRight = clip.Left + clip.Width;
            if (clipRight < w + newLeft)
            {
                Width = clipRight - newLeft;
                changed = true;
            }

            int h = Height;
            int clipBottom = clip.Top + clip.Height;
            if (clipBottom < h + newTop)
            {
                Height = clipBottom - newTop;
                changed = true;
            }

            return changed;
        }

        // NXBasics::SRectangle::CombineWith(NXBasics::SRectangle const&)
        internal void CombineWith(in SRectangle other)
        {
            int otherLeft = other.Left;
            int left = Left;

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
                Left = otherLeft;
                anchorLeft = otherLeft;
                left = otherLeft;
            }

            if (width + left < anchorLeft + other.Width)
            {
                Width = (anchorLeft + other.Width) - left;
            }

            int otherTop = other.Top;
            int top = Top;

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
                Top = otherTop;
                anchorTop = otherTop;
                top = otherTop;
            }

            if (height + top < anchorTop + other.Height)
            {
                Height = (anchorTop + other.Height) - top;
            }
        }

        // NXBasics::SRectangle::IsTouching(NXBasics::SRectangle const&) const
        internal int IsTouching(in SRectangle other)
        {
            if (other.Left < Width + Left && Left < other.Left + other.Width)
            {
                int y = Top;
                if (other.Top < Height + y && y < other.Top + other.Height)
                {
                    return 1;
                }
            }

            return 0;
        }

        // NXBasics::SRectangle::IsEqual(NXBasics::SRectangle const&) const
        internal int IsEqual(in SRectangle other)
        {
            if (other.Left == Left && other.Top == Top && other.Width == Width)
            {
                return other.Height == Height ? 1 : 0;
            }

            return 0;
        }

        // NXBasics::SRectangle::IsSizeEqual(NXBasics::SRectangle const&) const
        internal int IsSizeEqual(in SRectangle other)
        {
            if (other.Width == Width)
            {
                return other.Height == Height ? 1 : 0;
            }

            return 0;
        }

        // NXBasics::SRectangle::IsPositionEqual(NXBasics::SRectangle const&) const
        internal int IsPositionEqual(in SRectangle other)
        {
            if (other.Left == Left)
            {
                return other.Top == Top ? 1 : 0;
            }

            return 0;
        }

        // NXBasics::SRectangle::IsInside(NXBasics::SRectangle const&) const
        internal int IsInside(in SRectangle bounds)
        {
            if (bounds.Left <= Left)
            {
                if (bounds.Top <= Top &&
                    Left + Width - 1 <= bounds.Left + bounds.Width - 1)
                {
                    int bottom = Height + Top - 1;
                    return bottom <= bounds.Top + bounds.Height - 1 ? 1 : 0;
                }
            }

            return 0;
        }

        // NXBasics::SRectangle::MakeSizeWordAlligned()
        internal void MakeSizeWordAlligned()
        {
            Width = (int)((uint)Width & 0xFFFEu);
            Height = (int)((uint)Height & 0xFFFEu);
        }

        // NXBasics::SRectangle::MakeSizeLongAlligned()
        internal void MakeSizeLongAlligned()
        {
            Width = (int)((uint)Width & 0xFFFCu);
            Height = (int)((uint)Height & 0xFFFCu);
        }

        // NXBasics::SRectangle::Validate()
        internal void Validate()
        {
            int w = Width;
            if (w < 0)
            {
                Left += w;
                Width = -w;
            }

            int h = Height;
            if (-1 < h)
            {
                return;
            }

            Top += h;
            Height = -h;
        }
    }
}