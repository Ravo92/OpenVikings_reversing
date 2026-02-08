namespace OpenVikings.NXBasics
{
    internal struct SRectangle : IEquatable<SRectangle>
    {
        internal int X;
        internal int Y;
        internal int Width;
        internal int Height;

        internal SRectangle(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        internal SRectangle(int width, int height)
        {
            X = 0;
            Y = 0;
            Width = width;
            Height = height;
        }

        internal SRectangle(in SRectangle other)
        {
            X = other.X;
            Y = other.Y;
            Width = other.Width;
            Height = other.Height;
        }

        internal SRectangle(in SPoint point)
        {
            X = point.X;
            Y = point.Y;

            // 0x100000001 => Width=1, Height=1 (two packed ints)
            Width = 1;
            Height = 1;
        }

        public static bool operator ==(SRectangle left, SRectangle right)
        {
            return left.X == right.X
                && left.Y == right.Y
                && left.Width == right.Width
                && left.Height == right.Height;
        }

        public static bool operator !=(SRectangle left, SRectangle right)
        {
            return !(left == right);
        }

        public bool Equals(SRectangle other)
        {
            return this == other;
        }

        public override bool Equals(object? obj)
        {
            if (obj is SRectangle rectangle)
            {
                return this == rectangle;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Width, Height);
        }

        internal void Init()
        {
            X = 0;
            Y = 0;
            Width = 0;
            Height = 0;
        }

        internal void SetVariables(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        internal void MovePosition(int deltaX, int deltaY)
        {
            X = X + deltaX;
            Y = Y + deltaY;
        }

        internal void BlowUpSize(int amountX, int amountY)
        {
            X = X - amountX;
            Y = Y - amountY;
            Width = Width + (amountX * 2);
            Height = Height + (amountY * 2);
        }

        internal void PlaceInside(in SRectangle outer)
        {
            int outerWidth = outer.Width;
            int thisWidth = Width;

            if (outerWidth < thisWidth)
            {
                Width = outerWidth;
                thisWidth = outerWidth;
            }

            int outerHeight = outer.Height;
            int thisHeight = Height;

            if (outerHeight < thisHeight)
            {
                Height = outerHeight;
                thisHeight = outerHeight;
            }

            int outerX = outer.X;
            int thisX = X;

            if (thisX < outerX)
            {
                X = outerX;
                thisX = outerX;
            }

            int outerY = outer.Y;
            int thisY = Y;

            if (thisY < outerY)
            {
                Y = outerY;
                thisY = outerY;
            }

            if (outer.X + outer.Width < thisX + thisWidth)
            {
                X = (outer.X + outer.Width) - thisWidth;
            }

            if (outer.Y + outer.Height < thisY + thisHeight)
            {
                Y = (outer.Y + outer.Height) - thisHeight;
            }
        }

        internal void CutInside(in SRectangle outer)
        {
            int thisX = X;
            int outerX = outer.X;

            if (thisX < outerX)
            {
                Width = Width + (thisX - outerX);
                X = outerX;
                thisX = outerX;
            }

            int thisY = Y;
            int outerY = outer.Y;

            if (thisY < outerY)
            {
                Height = Height + (thisY - outerY);
                Y = outerY;
                thisY = outerY;
            }

            if (outer.X + outer.Width < Width + thisX)
            {
                Width = (outer.X + outer.Width) - thisX;
            }

            if (outer.Y + outer.Height < Height + thisY)
            {
                Height = (outer.Y + outer.Height) - thisY;
            }
        }

        internal bool CutInsideX(in SRectangle outer)
        {
            int originalX = X;
            int originalY = Y;
            int originalWidth = Width;
            int originalHeight = Height;

            int thisX = X;
            int outerX = outer.X;

            if (thisX < outerX)
            {
                Width = Width + (thisX - outerX);
                X = outerX;
                thisX = outerX;
            }

            int thisY = Y;
            int outerY = outer.Y;

            if (thisY < outerY)
            {
                Height = Height + (thisY - outerY);
                Y = outerY;
                thisY = outerY;
            }

            int rightOuter = outer.X + outer.Width;
            if (rightOuter < Width + thisX)
            {
                Width = rightOuter - thisX;
            }

            int bottomOuter = outer.Y + outer.Height;
            if (bottomOuter < Height + thisY)
            {
                Height = bottomOuter - thisY;
            }

            return X != originalX
                || Y != originalY
                || Width != originalWidth
                || Height != originalHeight;
        }

        internal void CombineWith(in SRectangle other)
        {
            // X axis (ported 1:1 from RE logic)
            int otherX = other.X;
            int thisX = X;

            int thisWidth;
            int otherXLocal;

            if (thisX - otherX == 0 || thisX < otherX)
            {
                thisWidth = Width;
                otherXLocal = otherX;
            }
            else
            {
                thisWidth = (thisX - otherX) + Width;
                Width = thisWidth;
                X = otherX;

                otherXLocal = otherX;
                thisX = otherX;
            }

            if (thisWidth + thisX < otherXLocal + other.Width)
            {
                Width = (otherXLocal + other.Width) - thisX;
            }

            // Y axis (ported 1:1 from RE logic)
            int otherY = other.Y;
            int thisY = Y;

            if (thisY - otherY == 0 || thisY < otherY)
            {
                thisWidth = Height;
                otherXLocal = otherY;
            }
            else
            {
                thisWidth = (thisY - otherY) + Height;
                Height = thisWidth;
                Y = otherY;

                otherXLocal = otherY;
                thisY = otherY;
            }

            if (thisWidth + thisY < otherXLocal + other.Height)
            {
                Height = (otherXLocal + other.Height) - thisY;
            }
        }

        internal bool IsTouching(in SRectangle other)
        {
            if (other.X < (Width + X) && X < (other.X + other.Width))
            {
                int thisY = Y;
                if (other.Y < (Height + thisY))
                {
                    return thisY < (other.Y + other.Height);
                }
            }

            return false;
        }

        internal bool IsEqual(in SRectangle other)
        {
            return X == other.X
                && Y == other.Y
                && Width == other.Width
                && Height == other.Height;
        }

        internal bool IsSizeEqual(in SRectangle other)
        {
            return Width == other.Width
                && Height == other.Height;
        }

        internal bool IsPositionEqual(in SRectangle other)
        {
            return X == other.X
                && Y == other.Y;
        }

        internal bool IsInside(in SRectangle outer)
        {
            if (outer.X <= X)
            {
                if (outer.Y <= Y)
                {
                    if (X + Width - 1 <= outer.X + outer.Width - 1)
                    {
                        int bottomMinus1 = Height + Y - 1;
                        return bottomMinus1 <= outer.Y + outer.Height - 1;
                    }
                }
            }

            return false;
        }

        internal void MakeSizeWordAlligned()
        {
            Width = unchecked((int)((uint)Width & 0xFFFEu));
            Height = unchecked((int)((uint)Height & 0xFFFEu));
        }

        internal void MakeSizeLongAlligned()
        {
            Width = unchecked((int)((uint)Width & 0xFFFCu));
            Height = unchecked((int)((uint)Height & 0xFFFCu));
        }

        internal void Validate()
        {
            if (Width < 0)
            {
                X = X + Width;
                Width = -Width;
            }

            if (Height < 0)
            {
                Y = Y + Height;
                Height = -Height;
            }
        }
    }
}