namespace OpenVikings.NXBasics
{
    internal sealed class CPicture : IDisposable
    {
        internal CBitmap? Bitmap { get; private set; }
        internal CPalette? Palette { get; private set; }

        // RE: this[0x10] used as "owns data" flag for destructor
        private bool _ownsData;

        internal CPicture(string filename)
        {
            InitVars();

            string extension = Path.GetExtension(filename).ToLowerInvariant();

            CMemory memory = new(filename);
            try
            {
                if (extension.StartsWith(".bmp", StringComparison.Ordinal))
                {
                    UnpackBMP(memory);
                }
                else if (extension.StartsWith(".pcx", StringComparison.Ordinal))
                {
                    UnpackPCX(memory);
                }

                // RE: CBitmap::SetPalettePtr(bitmap, palette)
                Bitmap?.SetPalettePtr(Palette);

                _ownsData = true;
            }
            finally
            {
                memory.Dispose();
            }
        }

        internal CPicture(CBitmap bitmap, CPalette? palette, bool ownsData)
        {
            InitVars();
            Bitmap = bitmap;
            Palette = palette;
            _ownsData = ownsData;
        }

        internal void InitVars()
        {
            Bitmap = null;
            Palette = null;
            _ownsData = false;
        }

        public void Dispose()
        {
            if (!_ownsData)
            {
                return;
            }

            Bitmap?.Dispose();
            Bitmap = null;
            Palette?.Dispose();
            Palette = null;

            _ownsData = false;
        }

        internal CBitmap? ExtractBitmap()
        {
            Bitmap?.SetPalettePtr(null);

            CBitmap? result = Bitmap;
            Bitmap = null;
            return result;
        }

        internal CPalette? ExtractPalette()
        {
            Bitmap?.SetPalettePtr(null);

            CPalette? result = Palette;
            Palette = null;
            return result;
        }

        internal void SetBitmapPtr(CBitmap? bitmap)
        {
            if (_ownsData && Bitmap != null)
            {
                Bitmap.Dispose();
            }

            Bitmap = bitmap;
        }

        internal void SetPalettePtr(CPalette? palette)
        {
            if (_ownsData && Palette != null)
            {
                Palette.Dispose();
            }

            Palette = palette;
        }

        // ------------------------------------------------------------
        // PCX
        // ------------------------------------------------------------
        private void UnpackPCX(CMemory memory)
        {
            byte[]? data = memory.BufferArray;
            if (data == null || data.Length < 128)
            {
                return;
            }

            ushort xMax = ReadUInt16LE(data, 8);
            ushort xMin = ReadUInt16LE(data, 4);
            ushort yMax = ReadUInt16LE(data, 10);
            ushort yMin = ReadUInt16LE(data, 6);

            int width = (xMax - xMin) + 1;
            int height = (yMax - yMin) + 1;

            CBitmap bitmap = new(unchecked((uint)width), unchecked((uint)height), 0x08);

            int alignedRowBytes = (width + 1) & ~1;
            int src = 0x80;

            byte[] rowBuffer = new byte[alignedRowBytes];

            for (int y = 0; y < height; y++)
            {
                int written = 0;

                while (written < alignedRowBytes && src < data.Length)
                {
                    byte value = data[src];

                    if (value < 0xC0)
                    {
                        rowBuffer[written] = value;
                        written += 1;
                        src += 1;
                    }
                    else
                    {
                        int count = value & 0x3F;
                        if (src + 1 >= data.Length)
                        {
                            break;
                        }

                        byte fill = data[src + 1];
                        for (int k = 0; k < count && written < alignedRowBytes; k++)
                        {
                            rowBuffer[written] = fill;
                            written += 1;
                        }

                        src += 2;
                    }
                }

                for (int x = 0; x < width; x++)
                {
                    bitmap.Draw_SetPixel(x, y, rowBuffer[x]);
                }
            }

            Bitmap = bitmap;

            if (data.Length >= 769)
            {
                int markerIndex = data.Length - 769;
                int paletteStart = data.Length - 768;

                if (data[markerIndex] != 0x0C)
                {
                    paletteStart = data.Length - 768;
                }

                CPalette palette = new();

                for (int i = 0; i < 256; i++)
                {
                    int p = paletteStart + (i * 3);
                    if (p + 2 >= data.Length)
                    {
                        break;
                    }

                    byte r = data[p + 0];
                    byte g = data[p + 1];
                    byte b = data[p + 2];
                    palette.SetEntry(i, r, g, b);
                }

                Palette = palette;
            }
        }

        private static ushort ReadUInt16LE(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        // ------------------------------------------------------------
        // BMP
        // ------------------------------------------------------------
        private void UnpackBMP(CMemory memory)
        {
            byte[]? data = memory.BufferArray;
            if (data == null || data.Length < 54)
            {
                return;
            }

            // RE reads these fields from the BMP header (little endian):
            // +2  file size
            // +10 pixel data offset
            // +18 width
            // +22 height
            // +28 bits per pixel
            // +30 compression
            uint pixelDataOffset = ReadUInt32LE(data, 10);
            int width = (int)ReadUInt32LE(data, 18);
            int heightRaw = (int)ReadUInt32LE(data, 22);
            ushort bpp = ReadUInt16LE(data, 28);
            uint compression = ReadUInt32LE(data, 30);

            int height = heightRaw;
            bool topDown = false;
            if (heightRaw < 0)
            {
                topDown = true;
                height = -heightRaw;
            }

            // Palette size logic:
            // RE uses biClrUsed at (lVar1 + 0x2E) and defaults to 0x100 if 0.
            uint colorsUsed = ReadUInt32LE(data, 46);
            if (colorsUsed == 0)
            {
                colorsUsed = 256;
            }

            // If 8bpp, load palette (BGRA entries)
            if (bpp == 8)
            {
                CPalette palette = new();

                int paletteOffset = 54;
                int entryCount = (int)Math.Min(colorsUsed, 256);

                for (int i = 0; i < entryCount; i++)
                {
                    int p = paletteOffset + (i * 4);
                    if (p + 3 >= data.Length)
                    {
                        break;
                    }

                    byte b = data[p + 0];
                    byte g = data[p + 1];
                    byte r = data[p + 2];
                    palette.SetEntry(i, r, g, b);
                }

                Palette = palette;
            }

            int pixelOffset = (int)pixelDataOffset;
            if (pixelOffset < 0 || pixelOffset >= data.Length)
            {
                return;
            }

            byte[] pixelData = new byte[data.Length - pixelOffset];
            Buffer.BlockCopy(data, pixelOffset, pixelData, 0, pixelData.Length);

            if (compression == 1 && bpp == 8)
            {
                // BI_RLE8
                Bitmap = DecodeRLE8(width, height, pixelData, topDown);
                return;
            }

            if (compression != 0)
            {
                // Unsupported compression except RLE8 handled above (matches RE "if !=0 return")
                return;
            }

            if (bpp == 8)
            {
                Bitmap = DecodeUnpacked8(width, height, pixelData, topDown);
                return;
            }

            if (bpp == 16)
            {
                Bitmap = DecodeUnpacked16To32(width, height, pixelData, topDown);
                return;
            }

            if (bpp == 24)
            {
                Bitmap = DecodeUnpacked24To32(width, height, pixelData, topDown);
                return;
            }

            if (bpp == 32)
            {
                Bitmap = DecodeUnpacked32To32(width, height, pixelData, topDown);
                return;
            }
        }

        private static uint ReadUInt32LE(byte[] data, int offset)
        {
            return (uint)(data[offset]
                | (data[offset + 1] << 8)
                | (data[offset + 2] << 16)
                | (data[offset + 3] << 24));
        }


        internal static CBitmap DecodeUnpacked8(int width, int height, byte[] src, bool topDown)
        {
            CBitmap bitmap = new(unchecked((uint)width), unchecked((uint)height), 0x08);

            int srcIndex = 0;

            for (int y = 0; y < height; y++)
            {
                int dstY = topDown ? y : (height - 1 - y);

                for (int x = 0; x < width; x++)
                {
                    if (srcIndex >= src.Length)
                    {
                        return bitmap;
                    }

                    bitmap.Draw_SetPixel(x, dstY, src[srcIndex]);
                    srcIndex += 1;
                }

                // IMPORTANT: No 4-byte padding skip for 8bpp in the original.
            }

            return bitmap;
        }

        private static CBitmap DecodeUnpacked16To32(int width, int height, byte[] src, bool topDown)
        {
            CBitmap bitmap = new(unchecked((uint)width), unchecked((uint)height), 0x20);

            int srcIndex = 0;

            for (int y = 0; y < height; y++)
            {
                int dstY = topDown ? y : (height - 1 - y);

                for (int x = 0; x < width; x++)
                {
                    if (srcIndex + 1 >= src.Length)
                    {
                        return bitmap;
                    }

                    ushort packed = (ushort)(src[srcIndex] | (src[srcIndex + 1] << 8));
                    srcIndex += 2;

                    byte r = (byte)((packed >> 7) & 0xF8);
                    byte g = (byte)((packed >> 2) & 0xF8);
                    byte b = (byte)((packed * 8) & 0xFF);

                    uint color = CXBSystemManager.sTrueColorCreatorPtr.GetTrueColorWord(r, g, b);
                    bitmap.Draw_SetPixel(x, dstY, color);
                }

                // IMPORTANT: No padding skip for 16bpp in the original.
            }

            return bitmap;
        }

        private static CBitmap DecodeUnpacked24To32(int width, int height, byte[] src, bool topDown)
        {
            CBitmap bitmap = new(unchecked((uint)width), unchecked((uint)height), 0x20);

            int srcIndex = 0;
            int rowBytes = width * 3;
            int pad = (4 - (rowBytes % 4)) & 3;

            for (int y = 0; y < height; y++)
            {
                int dstY = topDown ? y : (height - 1 - y);

                for (int x = 0; x < width; x++)
                {
                    if (srcIndex + 2 >= src.Length)
                    {
                        return bitmap;
                    }

                    byte b = src[srcIndex + 0];
                    byte g = src[srcIndex + 1];
                    byte r = src[srcIndex + 2];
                    srcIndex += 3;

                    uint color = CXBSystemManager.sTrueColorCreatorPtr.GetTrueColorWord(r, g, b);
                    bitmap.Draw_SetPixel(x, dstY, color);
                }

                srcIndex += pad;
            }

            return bitmap;
        }

        private static CBitmap DecodeUnpacked32To32(int width, int height, byte[] src, bool topDown)
        {
            CBitmap bitmap = new(unchecked((uint)width), unchecked((uint)height), 0x20);

            int srcIndex = 0;

            for (int y = 0; y < height; y++)
            {
                int dstY = topDown ? y : (height - 1 - y);

                for (int x = 0; x < width; x++)
                {
                    if (srcIndex + 3 >= src.Length)
                    {
                        return bitmap;
                    }

                    byte b = src[srcIndex + 0];
                    byte g = src[srcIndex + 1];
                    byte r = src[srcIndex + 2];
                    srcIndex += 4;

                    uint color = CXBSystemManager.sTrueColorCreatorPtr.GetTrueColorWord(r, g, b);
                    bitmap.Draw_SetPixel(x, dstY, color);
                }

                // IMPORTANT: No padding skip needed for 32bpp in the original.
            }

            return bitmap;
        }

        private static CBitmap DecodeRLE8(int width, int height, byte[] src, bool topDown)
        {
            // RE decodes into bottom-up coordinate system by default.
            // We'll write into bitmap coordinates with dstY computed.
            CBitmap bitmap = new(unchecked((uint)width), unchecked((uint)height), 0x08);

            int x = 0;
            int y = topDown ? 0 : (height - 1);

            int index = 0;

            while (index + 1 < src.Length)
            {
                byte count = src[index];
                byte value = src[index + 1];
                index += 2;

                if (count != 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (x >= 0 && x < width && y >= 0 && y < height)
                        {
                            bitmap.Draw_SetPixel(x, y, value);
                        }

                        x += 1;
                        if (x >= width)
                        {
                            x = 0;
                            y = topDown ? (y + 1) : (y - 1);
                        }
                    }

                    continue;
                }

                // Escape
                if (value == 0)
                {
                    // End of line
                    x = 0;
                    y = topDown ? (y + 1) : (y - 1);
                }
                else if (value == 1)
                {
                    // End of bitmap
                    break;
                }
                else if (value == 2)
                {
                    // Delta: next two bytes are dx, dy
                    if (index + 1 >= src.Length)
                    {
                        break;
                    }

                    byte dx = src[index];
                    byte dy = src[index + 1];
                    index += 2;

                    x += dx;
                    y = topDown ? (y + dy) : (y - dy);
                }
                else
                {
                    // Absolute mode: value = number of literal bytes
                    int literalCount = value;

                    for (int i = 0; i < literalCount; i++)
                    {
                        if (index >= src.Length)
                        {
                            break;
                        }

                        byte literal = src[index];
                        index += 1;

                        if (x >= 0 && x < width && y >= 0 && y < height)
                        {
                            bitmap.Draw_SetPixel(x, y, literal);
                        }

                        x += 1;
                        if (x >= width)
                        {
                            x = 0;
                            y = topDown ? (y + 1) : (y - 1);
                        }
                    }

                    // Align to word boundary
                    if ((literalCount & 1) == 1)
                    {
                        index += 1;
                    }
                }
            }

            return bitmap;
        }
    }
}