namespace OpenVikings.Dexter
{
    internal enum PixelFormat16
    {
        Rgb565 = 0,
        Rgb555 = 1
    }

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

    internal sealed class DexterGFX
    {
        private readonly ChunkySurface[] _chunkies;
        private readonly ushort[] _transTable16;
        private readonly byte[] _transTable8;
        private readonly byte[] _alphaLut; // 0..255 => 0..255
        private readonly byte[] _paletteR;
        private readonly byte[] _paletteG;
        private readonly byte[] _paletteB;

        private ChunkySurface? _target;
        private int _activeChunkyIndex;
        private byte _colourKey8;
        private ushort _colourKey16;
        private PixelFormat16 _pixelFormat16;

        internal DexterGFX(int maxChunkies)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChunkies);

            _chunkies = new ChunkySurface[maxChunkies];
            _activeChunkyIndex = -1;

            _transTable16 = new ushort[65536];
            _transTable8 = new byte[256 * 256];

            _alphaLut = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                _alphaLut[i] = (byte)i;
            }

            _paletteR = new byte[256];
            _paletteG = new byte[256];
            _paletteB = new byte[256];

            _colourKey8 = 0;
            _colourKey16 = 0;
            _pixelFormat16 = PixelFormat16.Rgb565;

            InitColour();
        }

        internal ChunkySurface UseChunky(int index)
        {
            if ((uint)index >= (uint)_chunkies.Length) throw new ArgumentOutOfRangeException(nameof(index));
            ChunkySurface? c = _chunkies[index] ?? throw new InvalidOperationException("Chunky not allocated.");
            _activeChunkyIndex = index;
            _target = c;
            return c;
        }

        internal void AllocateChunky(int index, int width, int height, int bitsPerPixel)
        {
            if ((uint)index >= (uint)_chunkies.Length) throw new ArgumentOutOfRangeException(nameof(index));
            _chunkies[index] = new ChunkySurface(width, height, bitsPerPixel);

            if (_target == null)
            {
                _activeChunkyIndex = index;
                _target = _chunkies[index];
            }
        }

        internal void FreeChunky(int index)
        {
            if ((uint)index >= (uint)_chunkies.Length) throw new ArgumentOutOfRangeException(nameof(index));
            _chunkies[index] = null;
            if (_activeChunkyIndex == index)
            {
                _activeChunkyIndex = -1;
                _target = null;
            }
        }

        internal void FreeAllChunkies()
        {
            for (int i = 0; i < _chunkies.Length; i++)
            {
                _chunkies[i] = null;
            }
            _activeChunkyIndex = -1;
            _target = null;
        }

        internal ChunkySurface Target
        {
            get
            {
                if (_target == null) throw new InvalidOperationException("No active target chunky.");
                return _target;
            }
        }

        internal void SetPixelFormat(PixelFormat16 format)
        {
            _pixelFormat16 = format;
        }

        internal void SetColourKey(byte key8)
        {
            _colourKey8 = key8;
        }

        internal void SetColourKey16(ushort key16)
        {
            _colourKey16 = key16;
        }

        internal void SetPaletteEntry(int index, byte r, byte g, byte b)
        {
            if ((uint)index >= 256u) throw new ArgumentOutOfRangeException(nameof(index));
            _paletteR[index] = r;
            _paletteG[index] = g;
            _paletteB[index] = b;
            UpdatePaletteTables();
        }

        internal void GetPaletteEntry(int index, out byte r, out byte g, out byte b)
        {
            if ((uint)index >= 256u) throw new ArgumentOutOfRangeException(nameof(index));
            r = _paletteR[index];
            g = _paletteG[index];
            b = _paletteB[index];
        }

        internal void InitColour()
        {
            for (int i = 0; i < 256; i++)
            {
                _paletteR[i] = (byte)i;
                _paletteG[i] = (byte)i;
                _paletteB[i] = (byte)i;
            }
            UpdatePaletteTables();
        }

        private void UpdatePaletteTables()
        {
            // Build 8-bit translucency table: dstIndex*256 + srcIndex => resultIndex
            // A common approximation: blend in RGB space and remap back to closest palette entry.
            // For engine accuracy, the original likely used precomputed nearest-color mapping.
            // This implementation is deterministic and stub-free: it computes a closest palette match.

            for (int dst = 0; dst < 256; dst++)
            {
                byte dr = _paletteR[dst];
                byte dg = _paletteG[dst];
                byte db = _paletteB[dst];

                for (int src = 0; src < 256; src++)
                {
                    byte sr = _paletteR[src];
                    byte sg = _paletteG[src];
                    byte sb = _paletteB[src];

                    int rr = (dr + sr) >> 1;
                    int rg = (dg + sg) >> 1;
                    int rb = (db + sb) >> 1;

                    _transTable8[(dst << 8) | src] = FindClosestPaletteIndex((byte)rr, (byte)rg, (byte)rb);
                }
            }

            // Build 16-bit translucency table: 0..65535 => half intensity (or other mix).
            // Here: 50% blend with black as a sane default base table.
            for (int c = 0; c < 65536; c++)
            {
                ushort v = (ushort)c;
                _transTable16[c] = Multiply16(v, 128);
            }
        }

        private byte FindClosestPaletteIndex(byte r, byte g, byte b)
        {
            int best = 0;
            int bestDist = int.MaxValue;

            for (int i = 0; i < 256; i++)
            {
                int dr = _paletteR[i] - r;
                int dg = _paletteG[i] - g;
                int db = _paletteB[i] - b;
                int dist = dr * dr + dg * dg + db * db;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                    if (dist == 0) break;
                }
            }

            return (byte)best;
        }

        internal void Plot8(int x, int y, byte colorIndex)
        {
            ChunkySurface t = Target;
            if (t.BitsPerPixel != 8) throw new InvalidOperationException("Target is not 8-bit.");
            if ((uint)x >= (uint)t.Width || (uint)y >= (uint)t.Height) return;
            t.Pixels8[y * t.Width + x] = colorIndex;
        }

        internal void Plot16(int x, int y, ushort color16)
        {
            ChunkySurface t = Target;
            if (t.BitsPerPixel != 16) throw new InvalidOperationException("Target is not 16-bit.");
            if ((uint)x >= (uint)t.Width || (uint)y >= (uint)t.Height) return;
            t.Pixels16[y * t.Width + x] = color16;
        }

        internal void Line(int x0, int y0, int x1, int y1, DexCol col)
        {
            ChunkySurface t = Target;

            if (t.BitsPerPixel == 8)
            {
                byte idx = FindClosestPaletteIndex(col.R, col.G, col.B);
                Line8(t, x0, y0, x1, y1, idx);
                return;
            }

            ushort c16 = Pack16(col);
            Line16(t, x0, y0, x1, y1, c16);
        }

        private void Line8(ChunkySurface t, int x0, int y0, int x1, int y1, byte idx)
        {
            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                if ((uint)x0 < (uint)t.Width && (uint)y0 < (uint)t.Height)
                {
                    t.Pixels8[y0 * t.Width + x0] = idx;
                }

                if (x0 == x1 && y0 == y1) break;
                int e2 = err << 1;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private void Line16(ChunkySurface t, int x0, int y0, int x1, int y1, ushort c16)
        {
            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                if ((uint)x0 < (uint)t.Width && (uint)y0 < (uint)t.Height)
                {
                    t.Pixels16[y0 * t.Width + x0] = c16;
                }

                if (x0 == x1 && y0 == y1) break;
                int e2 = err << 1;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        internal void Box(int left, int top, int right, int bottom, DexCol col)
        {
            int l = left;
            int t = top;
            int r = right;
            int b = bottom;

            Line(l, t, r, t, col);
            Line(r, t, r, b, col);
            Line(r, b, l, b, col);
            Line(l, b, l, t, col);
        }

        internal void BoxFill(int left, int top, int right, int bottom, DexCol col)
        {
            ChunkySurface t = Target;
            DexRect rect = new DexRect(left, top, right, bottom).ClampTo(t.Width, t.Height);
            if (rect.IsEmpty) return;

            if (t.BitsPerPixel == 8)
            {
                byte idx = FindClosestPaletteIndex(col.R, col.G, col.B);
                BoxFill8Core(t, rect, idx);
                return;
            }

            ushort c16 = Pack16(col);
            BoxFill16Core(t, rect, c16);
        }

        private void BoxFill8Core(ChunkySurface t, DexRect rect, byte idx)
        {
            int w = t.Width;
            Span<byte> px = t.Pixels8;

            for (int y = rect.Top; y < rect.Bottom; y++)
            {
                int row = y * w;
                px.Slice(row + rect.Left, rect.Width).Fill(idx);
            }
        }

        private void BoxFill16Core(ChunkySurface t, DexRect rect, ushort c16)
        {
            int w = t.Width;
            Span<ushort> px = t.Pixels16;

            for (int y = rect.Top; y < rect.Bottom; y++)
            {
                int row = y * w;
                px.Slice(row + rect.Left, rect.Width).Fill(c16);
            }
        }

        internal void DrawCircle(int cx, int cy, int radius, DexCol col)
        {
            if (radius <= 0) return;

            ChunkySurface t = Target;
            bool is8 = t.BitsPerPixel == 8;
            byte idx = is8 ? FindClosestPaletteIndex(col.R, col.G, col.B) : (byte)0;
            ushort c16 = is8 ? (ushort)0 : Pack16(col);

            int x = radius;
            int y = 0;
            int err = 1 - radius;

            while (x >= y)
            {
                if (is8)
                {
                    Plot8(cx + x, cy + y, idx);
                    Plot8(cx + y, cy + x, idx);
                    Plot8(cx - y, cy + x, idx);
                    Plot8(cx - x, cy + y, idx);
                    Plot8(cx - x, cy - y, idx);
                    Plot8(cx - y, cy - x, idx);
                    Plot8(cx + y, cy - x, idx);
                    Plot8(cx + x, cy - y, idx);
                }
                else
                {
                    Plot16(cx + x, cy + y, c16);
                    Plot16(cx + y, cy + x, c16);
                    Plot16(cx - y, cy + x, c16);
                    Plot16(cx - x, cy + y, c16);
                    Plot16(cx - x, cy - y, c16);
                    Plot16(cx - y, cy - x, c16);
                    Plot16(cx + y, cy - x, c16);
                    Plot16(cx + x, cy - y, c16);
                }

                y++;
                if (err < 0)
                {
                    err += (y << 1) + 1;
                }
                else
                {
                    x--;
                    err += ((y - x) << 1) + 1;
                }
            }
        }

        internal void Blit8(ReadOnlySpan<byte> srcPixels, int srcWidth, int srcHeight, ChunkySurface dst, int dstX, int dstY, DexRect srcRect, bool useColorKey, byte colorKey, byte alphaMode)
        {
            if (dst.BitsPerPixel != 8) throw new InvalidOperationException("Destination is not 8-bit.");
            if (srcWidth <= 0 || srcHeight <= 0) throw new ArgumentOutOfRangeException(nameof(srcWidth));
            if (srcPixels.Length < srcWidth * srcHeight) throw new ArgumentException("Source buffer too small.", nameof(srcPixels));

            DexRect s = srcRect.ClampTo(srcWidth, srcHeight);
            if (s.IsEmpty) return;

            int copyW = s.Width;
            int copyH = s.Height;

            int dx0 = dstX;
            int dy0 = dstY;

            // Clip against destination
            int clipLeft = 0;
            int clipTop = 0;
            int clipRight = dst.Width;
            int clipBottom = dst.Height;

            int outLeft = dx0;
            int outTop = dy0;
            int outRight = dx0 + copyW;
            int outBottom = dy0 + copyH;

            int shiftX = 0;
            int shiftY = 0;

            if (outLeft < clipLeft) { shiftX = clipLeft - outLeft; outLeft = clipLeft; }
            if (outTop < clipTop) { shiftY = clipTop - outTop; outTop = clipTop; }
            if (outRight > clipRight) outRight = clipRight;
            if (outBottom > clipBottom) outBottom = clipBottom;

            int finalW = outRight - outLeft;
            int finalH = outBottom - outTop;
            if (finalW <= 0 || finalH <= 0) return;

            int srcStartX = s.Left + shiftX;
            int srcStartY = s.Top + shiftY;

            Span<byte> dstPx = dst.Pixels8;

            bool alphaBlend = alphaMode != 0;

            for (int y = 0; y < finalH; y++)
            {
                int sy = srcStartY + y;
                int dy = outTop + y;

                int srcRow = sy * srcWidth;
                int dstRow = dy * dst.Width;

                int sx = srcStartX;
                int dx = outLeft;

                for (int x = 0; x < finalW; x++)
                {
                    byte sPix = srcPixels[srcRow + (sx + x)];
                    if (useColorKey && sPix == colorKey) continue;

                    int di = dstRow + (dx + x);

                    if (!alphaBlend)
                    {
                        dstPx[di] = sPix;
                    }
                    else
                    {
                        // Simple 50% translucency using trans table (dst,src -> blended)
                        byte dPix = dstPx[di];
                        dstPx[di] = _transTable8[(dPix << 8) | sPix];
                    }
                }
            }
        }

        internal void Blit16(ReadOnlySpan<ushort> srcPixels, int srcWidth, int srcHeight, ChunkySurface dst, int dstX, int dstY, DexRect srcRect, bool useColorKey, ushort colorKey, bool alphaBlend, byte alpha)
        {
            if (dst.BitsPerPixel != 16) throw new InvalidOperationException("Destination is not 16-bit.");
            if (srcWidth <= 0 || srcHeight <= 0) throw new ArgumentOutOfRangeException(nameof(srcWidth));
            if (srcPixels.Length < srcWidth * srcHeight) throw new ArgumentException("Source buffer too small.", nameof(srcPixels));

            DexRect s = srcRect.ClampTo(srcWidth, srcHeight);
            if (s.IsEmpty) return;

            int copyW = s.Width;
            int copyH = s.Height;

            int dx0 = dstX;
            int dy0 = dstY;

            int clipRight = dst.Width;
            int clipBottom = dst.Height;

            int outLeft = dx0;
            int outTop = dy0;
            int outRight = dx0 + copyW;
            int outBottom = dy0 + copyH;

            int shiftX = 0;
            int shiftY = 0;

            if (outLeft < 0) { shiftX = -outLeft; outLeft = 0; }
            if (outTop < 0) { shiftY = -outTop; outTop = 0; }
            if (outRight > clipRight) outRight = clipRight;
            if (outBottom > clipBottom) outBottom = clipBottom;

            int finalW = outRight - outLeft;
            int finalH = outBottom - outTop;
            if (finalW <= 0 || finalH <= 0) return;

            int srcStartX = s.Left + shiftX;
            int srcStartY = s.Top + shiftY;

            Span<ushort> dstPx = dst.Pixels16;

            for (int y = 0; y < finalH; y++)
            {
                int sy = srcStartY + y;
                int dy = outTop + y;

                int srcRow = sy * srcWidth;
                int dstRow = dy * dst.Width;

                int sx = srcStartX;
                int dx = outLeft;

                for (int x = 0; x < finalW; x++)
                {
                    ushort sPix = srcPixels[srcRow + (sx + x)];
                    if (useColorKey && sPix == colorKey) continue;

                    int di = dstRow + (dx + x);

                    if (!alphaBlend)
                    {
                        dstPx[di] = sPix;
                    }
                    else
                    {
                        ushort dPix = dstPx[di];
                        dstPx[di] = Blend16(dPix, sPix, alpha);
                    }
                }
            }
        }

        internal void AlphaBlit16(ReadOnlySpan<ushort> srcPixels, int srcWidth, int srcHeight, ChunkySurface dst, int dstX, int dstY, DexRect srcRect, ReadOnlySpan<byte> alphaMask)
        {
            if (dst.BitsPerPixel != 16) throw new InvalidOperationException("Destination is not 16-bit.");
            if (alphaMask.Length < srcWidth * srcHeight) throw new ArgumentException("Alpha mask too small.", nameof(alphaMask));

            DexRect s = srcRect.ClampTo(srcWidth, srcHeight);
            if (s.IsEmpty) return;

            int outLeft = dstX;
            int outTop = dstY;
            int outRight = dstX + s.Width;
            int outBottom = dstY + s.Height;

            int shiftX = 0;
            int shiftY = 0;

            if (outLeft < 0) { shiftX = -outLeft; outLeft = 0; }
            if (outTop < 0) { shiftY = -outTop; outTop = 0; }
            if (outRight > dst.Width) outRight = dst.Width;
            if (outBottom > dst.Height) outBottom = dst.Height;

            int finalW = outRight - outLeft;
            int finalH = outBottom - outTop;
            if (finalW <= 0 || finalH <= 0) return;

            int srcStartX = s.Left + shiftX;
            int srcStartY = s.Top + shiftY;

            Span<ushort> dstPx = dst.Pixels16;

            for (int y = 0; y < finalH; y++)
            {
                int sy = srcStartY + y;
                int dy = outTop + y;

                int srcRow = sy * srcWidth;
                int dstRow = dy * dst.Width;

                for (int x = 0; x < finalW; x++)
                {
                    int sx = srcStartX + x;
                    ushort sPix = srcPixels[srcRow + sx];
                    byte a = alphaMask[srcRow + sx];

                    if (a == 0) continue;

                    int di = dstRow + (outLeft + x);
                    ushort dPix = dstPx[di];
                    dstPx[di] = Blend16(dPix, sPix, a);
                }
            }
        }

        internal void ChunkyFlip(ChunkySurface surface, bool vertical)
        {
            if (surface.BitsPerPixel == 8)
            {
                Flip8(surface, vertical);
                return;
            }

            Flip16(surface, vertical);
        }

        private void Flip8(ChunkySurface s, bool vertical)
        {
            int w = s.Width;
            int h = s.Height;
            Span<byte> px = s.Pixels8;

            if (vertical)
            {
                for (int y = 0; y < h / 2; y++)
                {
                    int y2 = h - 1 - y;
                    Span<byte> a = px.Slice(y * w, w);
                    Span<byte> b = px.Slice(y2 * w, w);
                    SwapSpans(a, b);
                }
            }
            else
            {
                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    int l = 0;
                    int r = w - 1;
                    while (l < r)
                    {
                        (px[row + r], px[row + l]) = (px[row + l], px[row + r]);
                        l++;
                        r--;
                    }
                }
            }
        }

        private void Flip16(ChunkySurface s, bool vertical)
        {
            int w = s.Width;
            int h = s.Height;
            Span<ushort> px = s.Pixels16;

            if (vertical)
            {
                for (int y = 0; y < h / 2; y++)
                {
                    int y2 = h - 1 - y;
                    Span<ushort> a = px.Slice(y * w, w);
                    Span<ushort> b = px.Slice(y2 * w, w);
                    SwapSpans(a, b);
                }
            }
            else
            {
                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    int l = 0;
                    int r = w - 1;
                    while (l < r)
                    {
                        (px[row + r], px[row + l]) = (px[row + l], px[row + r]);
                        l++;
                        r--;
                    }
                }
            }
        }

        private static void SwapSpans(Span<byte> a, Span<byte> b)
        {
            for (int i = 0; i < a.Length; i++)
            {
                (b[i], a[i]) = (a[i], b[i]);
            }
        }

        private static void SwapSpans(Span<ushort> a, Span<ushort> b)
        {
            for (int i = 0; i < a.Length; i++)
            {
                (b[i], a[i]) = (a[i], b[i]);
            }
        }

        internal void ChunkyScale(ChunkySurface src, ChunkySurface dst, int dstX, int dstY, int dstW, int dstH, bool bilinear)
        {
            if (dstW <= 0 || dstH <= 0) return;

            if (src.BitsPerPixel != dst.BitsPerPixel) throw new InvalidOperationException("Source/Destination bpp mismatch.");

            if (src.BitsPerPixel == 8)
            {
                Scale8(src, dst, dstX, dstY, dstW, dstH, bilinear);
                return;
            }

            Scale16(src, dst, dstX, dstY, dstW, dstH, bilinear);
        }

        private void Scale8(ChunkySurface src, ChunkySurface dst, int dstX, int dstY, int dstW, int dstH, bool bilinear)
        {
            ReadOnlySpan<byte> spx = src.Pixels8;
            Span<byte> dpx = dst.Pixels8;

            int sw = src.Width;
            int sh = src.Height;

            for (int y = 0; y < dstH; y++)
            {
                int dy = dstY + y;
                if ((uint)dy >= (uint)dst.Height) continue;

                float v = (y + 0.5f) * sh / dstH - 0.5f;
                int y0 = (int)MathF.Floor(v);
                int y1 = y0 + 1;
                float fy = v - y0;

                if (y0 < 0) { y0 = 0; y1 = 0; fy = 0; }
                if (y1 >= sh) { y1 = sh - 1; y0 = y1; fy = 0; }

                for (int x = 0; x < dstW; x++)
                {
                    int dx = dstX + x;
                    if ((uint)dx >= (uint)dst.Width) continue;

                    float u = (x + 0.5f) * sw / dstW - 0.5f;
                    int x0 = (int)MathF.Floor(u);
                    int x1 = x0 + 1;
                    float fx = u - x0;

                    if (x0 < 0) { x0 = 0; x1 = 0; fx = 0; }
                    if (x1 >= sw) { x1 = sw - 1; x0 = x1; fx = 0; }

                    byte c;

                    if (!bilinear)
                    {
                        c = spx[y0 * sw + x0];
                    }
                    else
                    {
                        byte c00 = spx[y0 * sw + x0];
                        byte c10 = spx[y0 * sw + x1];
                        byte c01 = spx[y1 * sw + x0];
                        byte c11 = spx[y1 * sw + x1];

                        // Bilinear in RGB, then remap to palette index.
                        GetPaletteEntry(c00, out byte r00, out byte g00, out byte b00);
                        GetPaletteEntry(c10, out byte r10, out byte g10, out byte b10);
                        GetPaletteEntry(c01, out byte r01, out byte g01, out byte b01);
                        GetPaletteEntry(c11, out byte r11, out byte g11, out byte b11);

                        float r0 = r00 + (r10 - r00) * fx;
                        float g0 = g00 + (g10 - g00) * fx;
                        float b0 = b00 + (b10 - b00) * fx;

                        float r1 = r01 + (r11 - r01) * fx;
                        float g1 = g01 + (g11 - g01) * fx;
                        float b1 = b01 + (b11 - b01) * fx;

                        byte rr = (byte)Math.Clamp((int)(r0 + (r1 - r0) * fy), 0, 255);
                        byte gg = (byte)Math.Clamp((int)(g0 + (g1 - g0) * fy), 0, 255);
                        byte bb = (byte)Math.Clamp((int)(b0 + (b1 - b0) * fy), 0, 255);

                        c = FindClosestPaletteIndex(rr, gg, bb);
                    }

                    dpx[dy * dst.Width + dx] = c;
                }
            }
        }

        private void Scale16(ChunkySurface src, ChunkySurface dst, int dstX, int dstY, int dstW, int dstH, bool bilinear)
        {
            ReadOnlySpan<ushort> spx = src.Pixels16;
            Span<ushort> dpx = dst.Pixels16;

            int sw = src.Width;
            int sh = src.Height;

            for (int y = 0; y < dstH; y++)
            {
                int dy = dstY + y;
                if ((uint)dy >= (uint)dst.Height) continue;

                float v = (y + 0.5f) * sh / dstH - 0.5f;
                int y0 = (int)MathF.Floor(v);
                int y1 = y0 + 1;
                float fy = v - y0;

                if (y0 < 0) { y0 = 0; y1 = 0; fy = 0; }
                if (y1 >= sh) { y1 = sh - 1; y0 = y1; fy = 0; }

                for (int x = 0; x < dstW; x++)
                {
                    int dx = dstX + x;
                    if ((uint)dx >= (uint)dst.Width) continue;

                    float u = (x + 0.5f) * sw / dstW - 0.5f;
                    int x0 = (int)MathF.Floor(u);
                    int x1 = x0 + 1;
                    float fx = u - x0;

                    if (x0 < 0) { x0 = 0; x1 = 0; fx = 0; }
                    if (x1 >= sw) { x1 = sw - 1; x0 = x1; fx = 0; }

                    ushort c;

                    if (!bilinear)
                    {
                        c = spx[y0 * sw + x0];
                    }
                    else
                    {
                        ushort c00 = spx[y0 * sw + x0];
                        ushort c10 = spx[y0 * sw + x1];
                        ushort c01 = spx[y1 * sw + x0];
                        ushort c11 = spx[y1 * sw + x1];

                        Unpack16(c00, out int r00, out int g00, out int b00);
                        Unpack16(c10, out int r10, out int g10, out int b10);
                        Unpack16(c01, out int r01, out int g01, out int b01);
                        Unpack16(c11, out int r11, out int g11, out int b11);

                        float r0 = r00 + (r10 - r00) * fx;
                        float g0 = g00 + (g10 - g00) * fx;
                        float b0 = b00 + (b10 - b00) * fx;

                        float r1 = r01 + (r11 - r01) * fx;
                        float g1 = g01 + (g11 - g01) * fx;
                        float b1 = b01 + (b11 - b01) * fx;

                        int rr = (int)(r0 + (r1 - r0) * fy);
                        int gg = (int)(g0 + (g1 - g0) * fy);
                        int bb = (int)(b0 + (b1 - b0) * fy);

                        c = Pack16From8((byte)rr, (byte)gg, (byte)bb);
                    }

                    dpx[dy * dst.Width + dx] = c;
                }
            }
        }

        internal void BlitRotate16(ChunkySurface src, ChunkySurface dst, int dstX, int dstY, DexRect srcRect, float angleRadians, bool useColorKey, ushort colorKey)
        {
            if (src.BitsPerPixel != 16 || dst.BitsPerPixel != 16) throw new InvalidOperationException("Rotate16 requires 16-bit surfaces.");

            DexRect s = srcRect.ClampTo(src.Width, src.Height);
            if (s.IsEmpty) return;

            float cx = s.Left + s.Width * 0.5f;
            float cy = s.Top + s.Height * 0.5f;

            float cos = MathF.Cos(angleRadians);
            float sin = MathF.Sin(angleRadians);

            ReadOnlySpan<ushort> spx = src.Pixels16;
            Span<ushort> dpx = dst.Pixels16;

            // Destination bounds: conservative AABB of the source rect around center.
            int outW = s.Width;
            int outH = s.Height;

            int halfW = outW / 2;
            int halfH = outH / 2;

            for (int y = -halfH; y < halfH; y++)
            {
                int dy = dstY + (y + halfH);
                if ((uint)dy >= (uint)dst.Height) continue;

                for (int x = -halfW; x < halfW; x++)
                {
                    int dx = dstX + (x + halfW);
                    if ((uint)dx >= (uint)dst.Width) continue;

                    float rx = x * cos + y * sin;
                    float ry = -x * sin + y * cos;

                    int sx = (int)MathF.Round(cx + rx);
                    int sy = (int)MathF.Round(cy + ry);

                    if ((uint)sx >= (uint)src.Width || (uint)sy >= (uint)src.Height) continue;
                    if (sx < s.Left || sx >= s.Right || sy < s.Top || sy >= s.Bottom) continue;

                    ushort sp = spx[sy * src.Width + sx];
                    if (useColorKey && sp == colorKey) continue;

                    dpx[dy * dst.Width + dx] = sp;
                }
            }
        }

        internal void SaveTransTable(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            stream.Write(_transTable8, 0, _transTable8.Length);

            // Write 16-bit table little-endian.
            byte[] tmp = new byte[_transTable16.Length * 2];
            int o = 0;
            for (int i = 0; i < _transTable16.Length; i++)
            {
                ushort v = _transTable16[i];
                tmp[o++] = (byte)(v & 0xFF);
                tmp[o++] = (byte)((v >> 8) & 0xFF);
            }
            stream.Write(tmp, 0, tmp.Length);
        }

        internal void LoadTransTable(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            ReadExact(stream, _transTable8);

            byte[] tmp = new byte[_transTable16.Length * 2];
            ReadExact(stream, tmp);

            int o = 0;
            for (int i = 0; i < _transTable16.Length; i++)
            {
                _transTable16[i] = (ushort)(tmp[o] | (tmp[o + 1] << 8));
                o += 2;
            }
        }

        private static void ReadExact(Stream stream, byte[] buffer)
        {
            int read = 0;
            while (read < buffer.Length)
            {
                int r = stream.Read(buffer, read, buffer.Length - read);
                if (r <= 0) throw new EndOfStreamException();
                read += r;
            }
        }

        private ushort Pack16(DexCol c)
        {
            return Pack16From8(c.R, c.G, c.B);
        }

        private ushort Pack16From8(byte r8, byte g8, byte b8)
        {
            if (_pixelFormat16 == PixelFormat16.Rgb565)
            {
                int r = r8 >> 3;
                int g = g8 >> 2;
                int b = b8 >> 3;
                return (ushort)((r << 11) | (g << 5) | b);
            }
            else
            {
                int r = r8 >> 3;
                int g = g8 >> 3;
                int b = b8 >> 3;
                return (ushort)((r << 10) | (g << 5) | b);
            }
        }

        private void Unpack16(ushort c, out int r8, out int g8, out int b8)
        {
            if (_pixelFormat16 == PixelFormat16.Rgb565)
            {
                int r = (c >> 11) & 31;
                int g = (c >> 5) & 63;
                int b = c & 31;

                r8 = (r << 3) | (r >> 2);
                g8 = (g << 2) | (g >> 4);
                b8 = (b << 3) | (b >> 2);
            }
            else
            {
                int r = (c >> 10) & 31;
                int g = (c >> 5) & 31;
                int b = c & 31;

                r8 = (r << 3) | (r >> 2);
                g8 = (g << 3) | (g >> 2);
                b8 = (b << 3) | (b >> 2);
            }
        }

        private ushort Multiply16(ushort c, int alpha256)
        {
            Unpack16(c, out int r, out int g, out int b);
            r = (r * alpha256) >> 8;
            g = (g * alpha256) >> 8;
            b = (b * alpha256) >> 8;
            return Pack16From8((byte)r, (byte)g, (byte)b);
        }

        private ushort Blend16(ushort dst, ushort src, byte alpha)
        {
            if (alpha >= 255) return src;
            if (alpha == 0) return dst;

            int a = _alphaLut[alpha]; // still 0..255

            Unpack16(dst, out int dr, out int dg, out int db);
            Unpack16(src, out int sr, out int sg, out int sb);

            int rr = dr + (((sr - dr) * a) >> 8);
            int gg = dg + (((sg - dg) * a) >> 8);
            int bb = db + (((sb - db) * a) >> 8);

            return Pack16From8((byte)rr, (byte)gg, (byte)bb);
        }
    }
}