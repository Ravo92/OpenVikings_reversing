namespace OpenVikings.Dexter
{
    internal sealed class ChunkySurface
    {
        private readonly byte[]? _pixels8;
        private readonly ushort[]? _pixels16;
        private byte[]? _alpha8;

        internal ChunkySurface(int width, int height, int bitsPerPixel) : this(width, height, bitsPerPixel, false)
        {
        }

        internal ChunkySurface(int width, int height, int bitsPerPixel, bool allocateAlpha8)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
            if (bitsPerPixel != 8 && bitsPerPixel != 16) throw new ArgumentOutOfRangeException(nameof(bitsPerPixel));

            Width = width;
            Height = height;
            BitsPerPixel = bitsPerPixel;

            if (bitsPerPixel == 8)
            {
                _pixels8 = new byte[checked(width * height)];
                _pixels16 = null;
            }
            else
            {
                _pixels16 = new ushort[checked(width * height)];
                _pixels8 = null;
            }

            if (allocateAlpha8)
            {
                _alpha8 = new byte[checked(width * height)];
            }
        }

        internal int Width { get; }
        internal int Height { get; }
        internal int BitsPerPixel { get; }

        internal bool HasAlpha8 => _alpha8 != null;

        internal short OriginX { get; set; }
        internal short OriginY { get; set; }

        internal Span<byte> Pixels8
        {
            get
            {
                if (_pixels8 == null) throw new InvalidOperationException("Surface is not 8-bit.");
                return _pixels8;
            }
        }

        internal Span<ushort> Pixels16
        {
            get
            {
                if (_pixels16 == null) throw new InvalidOperationException("Surface is not 16-bit.");
                return _pixels16;
            }
        }

        internal Span<byte> Alpha8
        {
            get
            {
                if (_alpha8 == null) throw new InvalidOperationException("Surface has no alpha buffer.");
                return _alpha8;
            }
        }

        internal void EnsureAlpha8()
        {
            if (_alpha8 != null) return;
            _alpha8 = new byte[checked(Width * Height)];
        }

        internal void Clear8(byte value)
        {
            if (BitsPerPixel != 8) throw new InvalidOperationException("Surface is not 8-bit.");
            Pixels8.Fill(value);
        }

        internal void Clear16(ushort value)
        {
            if (BitsPerPixel != 16) throw new InvalidOperationException("Surface is not 16-bit.");
            Pixels16.Fill(value);
        }

        internal void ClearAlpha8(byte value)
        {
            EnsureAlpha8();
            Alpha8.Fill(value);
        }
    }
}