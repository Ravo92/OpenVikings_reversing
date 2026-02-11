namespace OpenVikings.Dexter
{
    internal sealed class ChunkySurface
    {
        private readonly byte[]? _pixels8;
        private readonly ushort[]? _pixels16;

        internal ChunkySurface(int width, int height, int bitsPerPixel)
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
        }

        internal int Width { get; }
        internal int Height { get; }
        internal int BitsPerPixel { get; }

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
    }
}
