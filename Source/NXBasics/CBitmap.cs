using OpenVikings.NC2GuiToolsBase;

namespace OpenVikings.NXBasics
{
    internal sealed class CBitmap : IDisposable
    {
        #region Initialization and layout notes

        // --- Fields (layout-oriented, based on offsets in your dump) ---

        // +0x08
        private byte _bpp; // 0x08 / 0x10 / 0x20

        // +0x0C.. (SRectangle at +0x0C)
        private SRectangle _rect;

        // +0x28
        private CMemory _memoryOwner; // owned memory (or null when view/external)

        // +0x30 / +0x38 / +0x40
        private nint _ptr8;   // raw pointer for 8bpp
        private nint _ptr16;  // raw pointer for 16bpp
        private nint _ptr32;  // raw pointer for 32bpp

        // +0x48 / +0x4C / +0x50
        private int _pitchPixels;   // "width" or inherited pitch for virtual
        private int _bytesPerPixel; // 1/2/4
        private int _strideBytes;   // bytesPerPixel * pitchPixels

        // +0x54
        private byte _isVirtual; // 0/1 (byte in dump)

        // +0x58 / +0x5C / +0x60
        private int _virtualSrcX;
        private int _virtualSrcY;
        private CBitmap _virtualParent;

        // +0x68
        private CPalette _palette;
        private byte[]? _externalBuffer;
        private int _fixedXDistance;

        internal enum BitmapFormat : byte
        {
            Indexed8 = 0x08,
            HighColor16 = 0x10,
            TrueColor32 = 0x20
        }

        // RE mapping (based on your offsets):
        // 0x14 = width
        // 0x18 = height
        // 0x48 = stride (in pixels, not bytes)  -> used as "rowPitch"
        // 0x30 = base pointer non-null check
        // 0x38 = 16bpp buffer base
        // 0x40 = 32bpp buffer base
        // for 8bpp they read from 0x30 as byte buffer base
        internal int Width { get { return _rect.Width; } }
        internal int Height { get { return _rect.Height; } }
        internal int StridePixels { get { return _pitchPixels; } }

        private object _palettePtr;

        // Managed backing buffers (you likely already have these in your reimplementation)
        private byte[] _pixels8;
        private ushort[] _pixels16;
        private uint[] _pixels32;

        // --- Ctors / init ---

        internal byte[]? Pixels8 => _pixels8;


        internal CBitmap()
        {
            ResetCore();
        }

        internal void L_InitObject()
        {
            ResetCore();
        }

        private void ResetCore()
        {
            _bpp = 0x08;

            _memoryOwner = null;
            _externalBuffer = null;

            _ptr8 = 0;
            _ptr16 = 0;
            _ptr32 = 0;

            _pitchPixels = 0;
            _bytesPerPixel = 1;
            _strideBytes = 0;

            _isVirtual = 0x00;
            _virtualSrcX = 0;
            _virtualSrcY = 0;
            _virtualParent = null;

            _palette = null;
            _palettePtr = null;

            _pixels8 = null;
            _pixels16 = null;
            _pixels32 = null;

            _fixedXDistance = 0;

            _rect.Init();
        }

        internal CBitmap(in SRectangle rect, byte bpp) : this()
        {
            // NXBasics::CBitmap::CBitmap(NXBasics::SRectangle const&, unsigned char)
            L_ConstructNonVirtual(in rect, bpp);
        }

        internal CBitmap(uint width, uint height, byte bpp) : this()
        {
            // NXBasics::CBitmap::CBitmap(unsigned int, unsigned int, unsigned char)
            SRectangle local = new(0, 0, (int)width, (int)height);
            L_ConstructNonVirtual(in local, bpp);
        }

        internal CBitmap(CBitmap source, in SRectangle rect) : this()
        {
            // NXBasics::CBitmap::CBitmap(NXBasics::CBitmap const&, NXBasics::SRectangle const&)
            L_ConstructVirtual(source, in rect);
        }

        internal CBitmap(CBitmap source, int x, int y, uint w, uint h) : this()
        {
            // NXBasics::CBitmap::CBitmap(NXBasics::CBitmap const&, int, int, unsigned int, unsigned int)
            SRectangle local = new(x, y, (int)w, (int)h);
            L_ConstructVirtual(source, in local);
        }

        internal CBitmap(uint width, uint height, byte bpp, byte[] externalPtr, uint pitchPixels) : this()
        {
            // NXBasics::CBitmap::CBitmap(unsigned int, unsigned int, unsigned char, unsigned char*, unsigned int)
            // externalPtr: represent external memory. You likely map this to your own memory/pinning system.
            // Here we treat it as "pointer-like" input to mirror the original signature.
            L_ConstructExternalMemory(width, height, bpp, externalPtr, pitchPixels);
        }

        internal CBitmap(CFile file) : this()
        {
            byte[] tmp = new byte[1];
            file.Read(tmp, 1);
            _bpp = tmp[0];

            int x = unchecked((int)file.ReadLong());
            int y = unchecked((int)file.ReadLong());
            int w = unchecked((int)file.ReadLong());
            int h = unchecked((int)file.ReadLong());

            _rect.X = x;
            _rect.Y = y;
            _rect.Width = w;
            _rect.Height = h;

            _memoryOwner = NXBasicsApi.XB_Storable_LoadObject(file);

            _pitchPixels = _rect.Width;
            _bytesPerPixel = (_bpp == 0x20) ? 4 : (_bpp == 0x10) ? 2 : 1;
            _strideBytes = checked(_bytesPerPixel * _pitchPixels);

            _isVirtual = 0x00;

            L_SetRawMemoryPtr();
        }

        // NXBasics::CBitmap::~CBitmap()
        public void Dispose()
        {
            _memoryOwner?.Dispose();
            _memoryOwner = null;
        }
        #endregion

        #region NXBasics::CBitmap::L_...()
        // NXBasics::CBitmap::L_ConstructNonVirtual(NXBasics::SRectangle const&, unsigned char)
        internal void L_ConstructNonVirtual(in SRectangle rect, byte bpp)
        {
            int pixelCount = checked(rect.Width * rect.Height);

            if (bpp == 0x20)
            {
                _bpp = 0x20;
                _bytesPerPixel = 4;
                _memoryOwner = new CMemory(checked((uint)(pixelCount * 4)));
            }
            else if (bpp == 0x10)
            {
                _bpp = 0x10;
                _bytesPerPixel = 2;
                _memoryOwner = new CMemory(checked((uint)(pixelCount * 2)));
            }
            else
            {
                _bpp = 0x08;
                _bytesPerPixel = 1;
                _memoryOwner = new CMemory(checked((uint)pixelCount));
            }

            _rect.SetVariables(0, 0, rect.Width, rect.Height);

            _pitchPixels = rect.Width;
            _strideBytes = checked(_bytesPerPixel * _pitchPixels);

            _isVirtual = 0x00;
            _virtualSrcX = 0;
            _virtualSrcY = 0;
            _virtualParent = null;
        }

        // NXBasics::CBitmap::L_ConstructVirtual(NXBasics::CBitmap const&, NXBasics::SRectangle const&)
        internal void L_ConstructVirtual(CBitmap source, in SRectangle requestedRect)
        {
            _isVirtual = 0x01;

            _memoryOwner = null;

            _bpp = source._bpp;

            SRectangle local = requestedRect;

            // In the original, this check used source._ptr8 != 0 to mean "source has pixel memory".
            // In managed version, we treat "has memory" as: non-virtual source with a non-null owner (or virtual with a valid parent).
            bool sourceHasPixels = source != null &&
                                   (source._isVirtual != 0x00 ? source._virtualParent != null : source._memoryOwner != null);

            if (!sourceHasPixels)
            {
                return;
            }

            if (!local.IsTouching(source._rect))
            {
                return;
            }

            if (local.Width <= 0 || local.Height <= 0) return;
            local.CutInside(source._rect);

            // This is the same offset math as original (pixel index inside parent)
            // NOTE: We keep it as int because your sizes are small; use checked to be safe.
            int pixelIndex = checked(source._pitchPixels * local.Y + local.X);

            _virtualSrcX = local.X;
            _virtualSrcY = local.Y;
            _virtualParent = source;

            // Set this bitmap's rectangle to start at (0,0) with local's size.
            // Use whichever exists in your SRectangle implementation:
            _rect.SetVariables(0, 0, local.Width, local.Height);
            // or: SRectangle.SetVariables(ref _rect, 0, 0, local.Width, local.Height);

            _pitchPixels = source._pitchPixels;
            _bytesPerPixel = source._bytesPerPixel;
            _strideBytes = source._strideBytes;

            // Optional: if you want a cached byte-offset for faster addressing later:
            // _virtualByteOffset = checked(pixelIndex * _bytesPerPixel);
            _ = pixelIndex; // keep variable if you want to use it later
        }

        // NXBasics::CBitmap::L_ConstructExternalMemory(...)
        // NOTE: externalPtr here is "pointer-like"; map this to your actual raw memory system.
        // Expect: you have a way to pin/obtain nint from it. We call it ExternalMemory.GetRawPtr(externalPtr).
        internal void L_ConstructExternalMemory(uint width, uint height, byte bpp, byte[] externalPtr, uint pitchPixels)
        {
            _bpp = bpp;
            _memoryOwner = null;

            _externalBuffer = externalPtr;

            if (bpp == 0x20)
            {
                _bytesPerPixel = 4;
            }
            else if (bpp == 0x10)
            {
                _bytesPerPixel = 2;
            }
            else
            {
                _bytesPerPixel = 1;
            }

            _pitchPixels = checked((int)pitchPixels);
            _strideBytes = _bytesPerPixel * _pitchPixels;

            _rect.SetVariables(0, 0, checked((int)width), checked((int)height));

            _isVirtual = 0x00;
            _virtualSrcX = 0;
            _virtualSrcY = 0;
            _virtualParent = null;
        }

        // NXBasics::CBitmap::L_SetRawMemoryPtr()
        internal void L_SetRawMemoryPtr()
        {
            if (_memoryOwner == null)
            {
                return;
            }

            int requiredBytes = checked(_strideBytes * _rect.Height);
            if (_memoryOwner.Size < (uint)requiredBytes)
            {
                throw new InvalidOperationException("CBitmap: memory owner buffer is smaller than expected for current layout.");
            }
        }

        // NXBasics::CBitmap::L_FindMatchingColor(NXBasics::SColorRGB const&) const
        private bool TryGetPixelBuffer(out byte[]? buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel)
        {
            bytesPerPixel = _bytesPerPixel;

            if (_memoryOwner != null)
            {
                byte[] arr = _memoryOwner.BufferArray;
                if (arr == null)
                {
                    buffer = null;
                    baseOffsetBytes = 0;
                    pitchPixels = 0;
                    return false;
                }

                buffer = arr;
                baseOffsetBytes = 0;
                pitchPixels = _pitchPixels;
                return true;
            }

            if (_externalBuffer != null)
            {
                buffer = _externalBuffer;
                baseOffsetBytes = 0;
                pitchPixels = _pitchPixels;
                return true;
            }

            if (_isVirtual != 0x00 && _virtualParent != null)
            {
                if (!_virtualParent.TryGetPixelBuffer(out buffer, out int parentBase, out int parentPitch, out int parentBpp))
                {
                    baseOffsetBytes = 0;
                    pitchPixels = 0;
                    bytesPerPixel = _bytesPerPixel;
                    return false;
                }

                bytesPerPixel = parentBpp;

                int pixelIndex = checked(parentPitch * _virtualSrcY + _virtualSrcX);
                baseOffsetBytes = checked(parentBase + pixelIndex * bytesPerPixel);
                pitchPixels = parentPitch;
                return true;
            }

            buffer = null;
            baseOffsetBytes = 0;
            pitchPixels = 0;
            return false;
        }
        #endregion

        #region NXBasics::CBitmap::Fill()
        // NXBasics::CBitmap::Fill(unsigned char) const
        // NXBasics::CBitmap::Fill(unsigned short) const
        // NXBasics::CBitmap::Fill(unsigned int) const
        internal void Fill(byte paletteIndex)
        {
            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel))
            {
                return;
            }

            if (_bpp == 0x20)
            {
                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                uint trueColor = palette.GetTrueColorWord((uint)paletteIndex);
                FillBlockLongLE(buffer, baseOffsetBytes, _rect.Width, _rect.Height, pitchPixels, trueColor);
                return;
            }

            if (_bpp == 0x10)
            {
                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                ushort highColor = palette.GetHighColorWord((uint)paletteIndex);
                FillBlockWordLE(buffer, baseOffsetBytes, _rect.Width, _rect.Height, pitchPixels, highColor);
                return;
            }

            if (_bpp == 0x08)
            {
                FillBlockByte(buffer, baseOffsetBytes, _rect.Width, _rect.Height, pitchPixels, paletteIndex);
            }
        }

        private static void FillBlockByte(byte[] buffer, int baseOffsetBytes, int width, int height, int pitchPixels, byte value)
        {
            for (int y = 0; y < height; y++)
            {
                int rowStart = checked(baseOffsetBytes + y * pitchPixels);
                for (int x = 0; x < width; x++)
                {
                    buffer[rowStart + x] = value;
                }
            }
        }

        private static void FillBlockWordLE(byte[] buffer, int baseOffsetBytes, int width, int height, int pitchPixels, ushort value)
        {
            byte lo = (byte)(value & 0xFF);
            byte hi = (byte)((value >> 8) & 0xFF);

            int rowStrideBytes = checked(pitchPixels * 2);

            for (int y = 0; y < height; y++)
            {
                int rowStart = checked(baseOffsetBytes + y * rowStrideBytes);
                for (int x = 0; x < width; x++)
                {
                    int i = rowStart + x * 2;
                    buffer[i + 0] = lo;
                    buffer[i + 1] = hi;
                }
            }
        }

        private static void FillBlockLongLE(byte[] buffer, int baseOffsetBytes, int width, int height, int pitchPixels, uint value)
        {
            byte b0 = (byte)(value & 0xFF);
            byte b1 = (byte)((value >> 8) & 0xFF);
            byte b2 = (byte)((value >> 16) & 0xFF);
            byte b3 = (byte)((value >> 24) & 0xFF);

            int rowStrideBytes = checked(pitchPixels * 4);

            for (int y = 0; y < height; y++)
            {
                int rowStart = checked(baseOffsetBytes + y * rowStrideBytes);
                for (int x = 0; x < width; x++)
                {
                    int i = rowStart + x * 4;
                    buffer[i + 0] = b0;
                    buffer[i + 1] = b1;
                    buffer[i + 2] = b2;
                    buffer[i + 3] = b3;
                }
            }
        }

        // NXBasics::CBitmap::Fill(NXBasics::SColorRGB const&) const
        internal void Fill(in SColorRGB color)
        {
            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel))
            {
                return;
            }

            if (_bpp == 0x20)
            {
                uint trueColor = CXBSystemManager.sTrueColorCreatorPtr.GetTrueColorWord(in color);
                FillBlockLongLE(buffer, baseOffsetBytes, _rect.Width, _rect.Height, pitchPixels, trueColor);
                return;
            }

            if (_bpp == 0x10)
            {
                ushort highColor = CXBSystemManager.sHighColorCreatorPtr.GetHighColorWord(in color);
                FillBlockWordLE(buffer, baseOffsetBytes, _rect.Width, _rect.Height, pitchPixels, highColor);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                byte idx = palette.FindMatchingColor(color.R, color.G, color.B);
                Fill(idx);
            }
        }
        #endregion

        #region NXBasics::CBitmap::FillWithBitmap()
        // NXBasics::CBitmap::FillWithBitmap(NXBasics::CBitmap const&, int, int) const
        internal void FillWithBitmap(CBitmap tile, int startX, int startY)
        {
            if (tile == null)
            {
                return;
            }

            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (!tile.TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (startY >= _rect.Height)
            {
                return;
            }

            // Prevent infinite loops
            if (tile._rect.Width <= 0 || tile._rect.Height <= 0)
            {
                return;
            }

            // Optional safety (only if CopyIntoBitmap doesn't handle conversion)
            // if (_bpp != tile._bpp)
            // {
            //     return;
            // }

            int y = startY;

            while (y < _rect.Height)
            {
                int x = startX;

                if (x < _rect.Width)
                {
                    while (x < _rect.Width)
                    {
                        tile.CopyIntoBitmap(this, x, y);
                        x += tile._rect.Width;
                    }
                }

                y += tile._rect.Height;
            }
        }

        // NXBasics::CBitmap::FillWithBitmap(NXBasics::SRectangle const&, NXBasics::CBitmap const&, int, int) const
        internal void FillWithBitmap(in SRectangle area, CBitmap tile, int startX, int startY)
        {
            if (tile == null)
            {
                return;
            }

            CBitmap view = new();
            view.L_ConstructVirtual(this, in area);

            try
            {
                if (!view.TryGetPixelBuffer(out _, out _, out _, out _))
                {
                    return;
                }

                if (!tile.TryGetPixelBuffer(out _, out _, out _, out _))
                {
                    return;
                }

                if (startY >= view._rect.Height)
                {
                    return;
                }

                if (tile._rect.Width <= 0 || tile._rect.Height <= 0)
                {
                    return;
                }

                int y = startY;

                while (y < view._rect.Height)
                {
                    int x = startX;

                    if (x < view._rect.Width)
                    {
                        while (x < view._rect.Width)
                        {
                            tile.CopyIntoBitmap(view, x, y);
                            x += tile._rect.Width;
                        }
                    }

                    y += tile._rect.Height;
                }
            }
            finally
            {
                view.Dispose();
            }
        }
        #endregion

        #region NXBasics::CBitmap::GetHighColorTablePtr() || GetTrueColorTablePtr() || SetPalettePtr()
        // NXBasics::CBitmap::GetHighColorTablePtr() const
        internal ushort[] GetHighColorTablePtr()
        {
            return (_palette ?? CXBSystemManager.sPalettePtr).GetHighColorTablePtr();
        }

        internal uint[] GetTrueColorTablePtr()
        {
            return (_palette ?? CXBSystemManager.sPalettePtr).GetTrueColorTablePtr();
        }

        // NXBasics::CBitmap::SetPalettePtr(NXBasics::CPalette*)
        internal void SetPalettePtr(CPalette palette)
        {
            _palette = palette;
        }
        #endregion

        #region NXBasics::CBitmap::Draw_GetPixel
        // NXBasics::CBitmap::Draw_GetPixel_Word(int, int) const
        internal ushort Draw_GetPixel_Word(int x, int y)
        {
            if (_bpp != 0x10)
            {
                return 0;
            }

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel))
            {
                return 0;
            }

            if (bytesPerPixel != 2)
            {
                return 0;
            }

            int left = _rect.X;
            int top = _rect.Y;
            int rightExclusive = checked(_rect.X + _rect.Width);
            int bottomExclusive = checked(_rect.Y + _rect.Height);

            if (x < left || x >= rightExclusive || y < top || y >= bottomExclusive)
            {
                return 0;
            }

            int relX = x - left;
            int relY = y - top;

            int pixelIndex = checked(relY * pitchPixels + relX);
            int byteIndex = checked(baseOffsetBytes + pixelIndex * 2);

            if ((uint)(byteIndex + 1) >= (uint)buffer.Length)
            {
                return 0;
            }

            return (ushort)(buffer[byteIndex + 0] | (buffer[byteIndex + 1] << 8));
        }

        // NXBasics::CBitmap::Draw_GetPixel_Long(int, int) const
        // NXBasics::CBitmap::Draw_GetPixel_Long(int, int) const
        internal uint Draw_GetPixel_Long(int x, int y)
        {
            if (_bpp != 0x20)
            {
                return 0;
            }

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel))
            {
                return 0;
            }

            if (bytesPerPixel != 4)
            {
                return 0;
            }

            int left = _rect.X;
            int top = _rect.Y;
            int rightExclusive = checked(_rect.X + _rect.Width);
            int bottomExclusive = checked(_rect.Y + _rect.Height);

            if (x < left || x >= rightExclusive || y < top || y >= bottomExclusive)
            {
                return 0;
            }

            int relX = x - left;
            int relY = y - top;

            int pixelIndex = checked(relY * pitchPixels + relX);
            int byteIndex = checked(baseOffsetBytes + pixelIndex * 4);

            if ((uint)(byteIndex + 3) >= (uint)buffer.Length)
            {
                return 0;
            }

            return (uint)(
                buffer[byteIndex + 0] |
                (buffer[byteIndex + 1] << 8) |
                (buffer[byteIndex + 2] << 16) |
                (buffer[byteIndex + 3] << 24));
        }

        // NXBasics::CBitmap::Draw_GetPixel(int, int) const
        internal byte Draw_GetPixel(int x, int y)
        {
            if (_bpp != 0x08)
            {
                return 0;
            }

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel))
            {
                return 0;
            }

            if (bytesPerPixel != 1)
            {
                return 0;
            }

            int left = _rect.X;
            int top = _rect.Y;
            int rightExclusive = checked(_rect.X + _rect.Width);
            int bottomExclusive = checked(_rect.Y + _rect.Height);

            if (x < left || x >= rightExclusive || y < top || y >= bottomExclusive)
            {
                return 0;
            }

            int relX = x - left;
            int relY = y - top;

            int index = checked(baseOffsetBytes + relY * pitchPixels + relX);
            if ((uint)index >= (uint)buffer.Length)
            {
                return 0;
            }

            return buffer[index];
        }
        #endregion

        #region NXBasics::CBitmap::CopyIntoBitmap
        // NXBasics::CBitmap::CopyIntoBitmap(NXBasics::CBitmap const&, int, int) const
        // NOTE:
        // This is a *direct* structural translation. It references helpers you likely already have
        // (XB_Tool_*_CopyBlock, tables, creators, rectangle ops, etc.)
        internal void CopyIntoBitmap(CBitmap destination, int dstX, int dstY)
        {
            if (destination == null)
            {
                return;
            }

            // Same behavior as your original: special full-bitmap conversions when rects match.
            if (_bpp == 0x10 && destination._bpp == 0x20)
            {
                if (RectEquals(in _rect, in destination._rect) && _rect.Height > 0)
                {
                    Convert16To32Whole(destination);
                }
                return;
            }

            if (_bpp == 0x20 && destination._bpp == 0x10)
            {
                if (RectEquals(in _rect, in destination._rect) && _rect.Height > 0)
                {
                    Convert32To16Whole(destination);
                }
                return;
            }

            // Resolve pixel buffers (replaces ptr checks)
            if (!TryGetPixelBuffer(out byte[] srcBuf, out int srcBase, out int srcPitch, out int srcBppBytes))
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[] dstBuf, out int dstBase, out int dstPitch, out int dstBppBytes))
            {
                return;
            }

            // Destination bounds in destination space: (0,0,width,height)
            SRectangle dstBounds = new();
            dstBounds.SetVariables(0, 0, destination._rect.Width, destination._rect.Height);

            // Placing rectangle where this bitmap would be drawn into destination.
            SRectangle placing = new();
            placing.SetVariables(dstX, dstY, _rect.Width, _rect.Height);

            if (!placing.IsTouching(dstBounds))
            {
                return;
            }

            placing.CutInside(dstBounds);

            if (placing.Width <= 0 || placing.Height <= 0)
            {
                return;
            }

            int copyWidth = placing.Width;
            int copyHeight = placing.Height;

            // Source start inside THIS bitmap (relative to placement)
            int srcX = placing.X - dstX;
            int srcY = placing.Y - dstY;

            // Destination start
            int dstStartX = placing.X;
            int dstStartY = placing.Y;

            if (_bpp == 0x20 && destination._bpp == 0x20)
            {
                int srcOffset = checked(srcBase + checked((srcY * srcPitch + srcX) * 4));
                int dstOffset = checked(dstBase + checked((dstStartY * dstPitch + dstStartX) * 4));
                CopyBlockRaw(srcBuf, srcOffset, srcPitch, dstBuf, dstOffset, dstPitch, copyWidth, copyHeight, 4);
                return;
            }

            if (_bpp == 0x10 && destination._bpp == 0x10)
            {
                int srcOffset = checked(srcBase + checked((srcY * srcPitch + srcX) * 2));
                int dstOffset = checked(dstBase + checked((dstStartY * dstPitch + dstStartX) * 2));
                CopyBlockRaw(srcBuf, srcOffset, srcPitch, dstBuf, dstOffset, dstPitch, copyWidth, copyHeight, 2);
                return;
            }

            if (_bpp == 0x08 && destination._bpp == 0x08)
            {
                int srcOffset = checked(srcBase + (srcY * srcPitch + srcX));
                int dstOffset = checked(dstBase + (dstStartY * dstPitch + dstStartX));
                CopyBlockRaw(srcBuf, srcOffset, srcPitch, dstBuf, dstOffset, dstPitch, copyWidth, copyHeight, 1);
                return;
            }

            if (_bpp == 0x08 && destination._bpp == 0x20)
            {
                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                uint[] table = palette.GetTrueColorTablePtr();
                if (table == null)
                {
                    return;
                }

                Convert8To32Block(srcBuf, srcBase, srcPitch, srcX, srcY, dstBuf, dstBase, dstPitch, dstStartX, dstStartY, copyWidth, copyHeight, table);
                return;
            }

            if (_bpp == 0x08 && destination._bpp == 0x10)
            {
                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                ushort[] table = palette.GetHighColorTablePtr();
                if (table == null)
                {
                    return;
                }

                Convert8To16Block(srcBuf, srcBase, srcPitch, srcX, srcY, dstBuf, dstBase, dstPitch, dstStartX, dstStartY, copyWidth, copyHeight, table);
                return;
            }
        }

        private static bool RectEquals(in SRectangle a, in SRectangle b)
        {
            return a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
        }

        private static void CopyBlockRaw(byte[] src, int srcOffsetBytes, int srcPitchPixels, byte[] dst, int dstOffsetBytes, int dstPitchPixels, int widthPixels, int heightPixels, int bytesPerPixel)
        {
            int srcRowStrideBytes = checked(srcPitchPixels * bytesPerPixel);
            int dstRowStrideBytes = checked(dstPitchPixels * bytesPerPixel);
            int rowBytes = checked(widthPixels * bytesPerPixel);

            for (int y = 0; y < heightPixels; y++)
            {
                int s = checked(srcOffsetBytes + y * srcRowStrideBytes);
                int d = checked(dstOffsetBytes + y * dstRowStrideBytes);
                Buffer.BlockCopy(src, s, dst, d, rowBytes);
            }
        }

        private static void Convert8To16Block(byte[] src, int srcBase, int srcPitch, int srcX, int srcY, byte[] dst, int dstBase, int dstPitch, int dstX, int dstY, int width, int height, ushort[] table)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitch + srcX);
                int dstRow = checked(dstBase + checked(((dstY + y) * dstPitch + dstX) * 2));

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];
                    ushort v = table[idx];

                    int di = dstRow + x * 2;
                    dst[di + 0] = (byte)(v & 0xFF);
                    dst[di + 1] = (byte)((v >> 8) & 0xFF);
                }
            }
        }

        private static void Convert8To32Block(byte[] src, int srcBase, int srcPitch, int srcX, int srcY, byte[] dst, int dstBase, int dstPitch, int dstX, int dstY, int width, int height, uint[] table)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitch + srcX);
                int dstRow = checked(dstBase + checked(((dstY + y) * dstPitch + dstX) * 4));

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];
                    uint v = table[idx];

                    int di = dstRow + x * 4;
                    dst[di + 0] = (byte)(v & 0xFF);
                    dst[di + 1] = (byte)((v >> 8) & 0xFF);
                    dst[di + 2] = (byte)((v >> 16) & 0xFF);
                    dst[di + 3] = (byte)((v >> 24) & 0xFF);
                }
            }
        }

        private void Convert16To32Whole(CBitmap destination)
        {
            if (!TryGetPixelBuffer(out byte[] srcBuf, out int srcBase, out int srcPitch, out int srcBppBytes))
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[] dstBuf, out int dstBase, out int dstPitch, out int dstBppBytes))
            {
                return;
            }

            // Expect 16bpp source, 32bpp dest
            if (_bpp != 0x10 || destination._bpp != 0x20)
            {
                return;
            }

            int width = _rect.Width;
            int height = _rect.Height;

            // If you already have a palette-table for 16->32, use it here.
            // Otherwise this is a placeholder conversion policy (needs your engine's exact format).
            // For now: treat 16-bit as 5-6-5 and expand (common).
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + y * checked(srcPitch * 2));
                int dstRow = checked(dstBase + y * checked(dstPitch * 4));

                for (int x = 0; x < width; x++)
                {
                    int si = srcRow + x * 2;
                    ushort v = (ushort)(srcBuf[si + 0] | (srcBuf[si + 1] << 8));

                    uint rgb = Convert565To888(v);

                    int di = dstRow + x * 4;
                    dstBuf[di + 0] = (byte)(rgb & 0xFF);
                    dstBuf[di + 1] = (byte)((rgb >> 8) & 0xFF);
                    dstBuf[di + 2] = (byte)((rgb >> 16) & 0xFF);
                    dstBuf[di + 3] = (byte)((rgb >> 24) & 0xFF);
                }
            }
        }

        private void Convert32To16Whole(CBitmap destination)
        {
            if (!TryGetPixelBuffer(out byte[] srcBuf, out int srcBase, out int srcPitch, out int srcBppBytes))
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[] dstBuf, out int dstBase, out int dstPitch, out int dstBppBytes))
            {
                return;
            }

            // Expect 32bpp source, 16bpp dest
            if (_bpp != 0x20 || destination._bpp != 0x10)
            {
                return;
            }

            int width = _rect.Width;
            int height = _rect.Height;

            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + y * checked(srcPitch * 4));
                int dstRow = checked(dstBase + y * checked(dstPitch * 2));

                for (int x = 0; x < width; x++)
                {
                    int si = srcRow + x * 4;
                    byte b = srcBuf[si + 0];
                    byte g = srcBuf[si + 1];
                    byte r = srcBuf[si + 2];

                    ushort v = Convert888To565(r, g, b);

                    int di = dstRow + x * 2;
                    dstBuf[di + 0] = (byte)(v & 0xFF);
                    dstBuf[di + 1] = (byte)((v >> 8) & 0xFF);
                }
            }
        }

        private static uint Convert565To888(ushort v)
        {
            // Common 5-6-5 expand. If engine uses a different 16-bit layout, replace this.
            uint r5 = (uint)((v >> 11) & 0x1F);
            uint g6 = (uint)((v >> 5) & 0x3F);
            uint b5 = (uint)(v & 0x1F);

            uint r8 = (r5 << 3) | (r5 >> 2);
            uint g8 = (g6 << 2) | (g6 >> 4);
            uint b8 = (b5 << 3) | (b5 >> 2);

            // BGRA in little endian bytes (matches our FillBlockLongLE writing order)
            return (0xFFu << 24) | (r8 << 16) | (g8 << 8) | b8;
        }

        private static ushort Convert888To565(byte r, byte g, byte b)
        {
            ushort r5 = (ushort)(r >> 3);
            ushort g6 = (ushort)(g >> 2);
            ushort b5 = (ushort)(b >> 3);

            return (ushort)((r5 << 11) | (g6 << 5) | b5);
        }

        // NXBasics::CBitmap::CopyIntoBitmap_Remap(NXBasics::CBitmap const&, int, int, unsigned char const*) const
        // NXBasics::CBitmap::CopyIntoBitmap_Remap(NXBasics::CBitmap const&, int, int, unsigned char const*) const
        internal void CopyIntoBitmap_Remap(CBitmap destination, int dstX, int dstY, byte[] remap)
        {
            if (destination == null)
            {
                return;
            }

            if (remap == null || remap.Length < 256)
            {
                return;
            }

            if (_bpp != 0x08)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] srcBuf, out int srcBase, out int srcPitch, out int srcBytesPerPixel))
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[] dstBuf, out int dstBase, out int dstPitch, out int dstBytesPerPixel))
            {
                return;
            }

            // Destination bounds in destination space.
            SRectangle dstBounds = new();
            dstBounds.SetVariables(0, 0, destination._rect.Width, destination._rect.Height);

            // Where we want to place this bitmap.
            SRectangle placing = new();
            placing.SetVariables(dstX, dstY, _rect.Width, _rect.Height);

            if (!placing.IsTouching(dstBounds))
            {
                return;
            }

            placing.CutInside(dstBounds);

            if (placing.Width <= 0 || placing.Height <= 0)
            {
                return;
            }

            int copyWidth = placing.Width;
            int copyHeight = placing.Height;

            int srcX = placing.X - dstX;
            int srcY = placing.Y - dstY;

            int dstStartX = placing.X;
            int dstStartY = placing.Y;

            if (destination._bpp == 0x20)
            {
                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                uint[] table = pal.GetTrueColorTablePtr();
                if (table == null)
                {
                    return;
                }

                Remap8To32Block(srcBuf, srcBase, srcPitch, srcX, srcY,
                                dstBuf, dstBase, dstPitch, dstStartX, dstStartY,
                                copyWidth, copyHeight, remap, table);
                return;
            }

            if (destination._bpp == 0x10)
            {
                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                ushort[] table = pal.GetHighColorTablePtr();
                if (table == null)
                {
                    return;
                }

                Remap8To16Block(srcBuf, srcBase, srcPitch, srcX, srcY,
                                dstBuf, dstBase, dstPitch, dstStartX, dstStartY,
                                copyWidth, copyHeight, remap, table);
                return;
            }

            if (destination._bpp == 0x08)
            {
                Remap8To8Block(srcBuf, srcBase, srcPitch, srcX, srcY,
                               dstBuf, dstBase, dstPitch, dstStartX, dstStartY,
                               copyWidth, copyHeight, remap);
            }
        }

        private static void Remap8To8Block(byte[] src, int srcBase, int srcPitch, int srcX, int srcY,
                                           byte[] dst, int dstBase, int dstPitch, int dstX, int dstY,
                                           int width, int height, byte[] remap)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitch + srcX);
                int dstRow = checked(dstBase + (dstY + y) * dstPitch + dstX);

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];
                    dst[dstRow + x] = remap[idx];
                }
            }
        }

        private static void Remap8To16Block(byte[] src, int srcBase, int srcPitch, int srcX, int srcY,
                                            byte[] dst, int dstBase, int dstPitch, int dstX, int dstY,
                                            int width, int height, byte[] remap, ushort[] table)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitch + srcX);
                int dstRow = checked(dstBase + checked(((dstY + y) * dstPitch + dstX) * 2));

                for (int x = 0; x < width; x++)
                {
                    byte idx = remap[src[srcRow + x]];
                    ushort v = table[idx];

                    int di = dstRow + x * 2;
                    dst[di + 0] = (byte)(v & 0xFF);
                    dst[di + 1] = (byte)((v >> 8) & 0xFF);
                }
            }
        }

        private static void Remap8To32Block(byte[] src, int srcBase, int srcPitch, int srcX, int srcY,
                                            byte[] dst, int dstBase, int dstPitch, int dstX, int dstY,
                                            int width, int height, byte[] remap, uint[] table)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitch + srcX);
                int dstRow = checked(dstBase + checked(((dstY + y) * dstPitch + dstX) * 4));

                for (int x = 0; x < width; x++)
                {
                    byte idx = remap[src[srcRow + x]];
                    uint v = table[idx];

                    int di = dstRow + x * 4;
                    dst[di + 0] = (byte)(v & 0xFF);
                    dst[di + 1] = (byte)((v >> 8) & 0xFF);
                    dst[di + 2] = (byte)((v >> 16) & 0xFF);
                    dst[di + 3] = (byte)((v >> 24) & 0xFF);
                }
            }
        }


        // NXBasics::CBitmap::CopyIntoBitmap_RemapTwice(NXBasics::CBitmap const&, int, int, unsigned char const*, unsigned char const*) const
        // NXBasics::CBitmap::CopyIntoBitmap_RemapTwice(...)
        // remapTwice: idx' = remapB[ remapA[idx] ]
        internal void CopyIntoBitmap_RemapTwice(CBitmap destination, int dstX, int dstY, byte[] remapA, byte[] remapB)
        {
            if (destination == null)
            {
                return;
            }

            if (remapA == null || remapA.Length < 256)
            {
                return;
            }

            if (remapB == null || remapB.Length < 256)
            {
                return;
            }

            if (_bpp != 0x08)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] srcBuf, out int srcBase, out int srcPitch, out int srcBytesPerPixel))
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[] dstBuf, out int dstBase, out int dstPitch, out int dstBytesPerPixel))
            {
                return;
            }

            SRectangle dstBounds = new();
            dstBounds.SetVariables(0, 0, destination._rect.Width, destination._rect.Height);

            SRectangle placing = new();
            placing.SetVariables(dstX, dstY, _rect.Width, _rect.Height);

            if (!placing.IsTouching(dstBounds))
            {
                return;
            }

            placing.CutInside(dstBounds);

            if (placing.Width <= 0 || placing.Height <= 0)
            {
                return;
            }

            int copyWidth = placing.Width;
            int copyHeight = placing.Height;

            int srcX = placing.X - dstX;
            int srcY = placing.Y - dstY;

            int dstStartX = placing.X;
            int dstStartY = placing.Y;

            if (destination._bpp == 0x20)
            {
                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                uint[] table = pal.GetTrueColorTablePtr();
                if (table == null)
                {
                    return;
                }

                RemapTwice8To32Block(srcBuf, srcBase, srcPitch, srcX, srcY,
                                     dstBuf, dstBase, dstPitch, dstStartX, dstStartY,
                                     copyWidth, copyHeight, remapA, remapB, table);
                return;
            }

            if (destination._bpp == 0x10)
            {
                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                ushort[] table = pal.GetHighColorTablePtr();
                if (table == null)
                {
                    return;
                }

                RemapTwice8To16Block(srcBuf, srcBase, srcPitch, srcX, srcY,
                                     dstBuf, dstBase, dstPitch, dstStartX, dstStartY,
                                     copyWidth, copyHeight, remapA, remapB, table);
                return;
            }

            if (destination._bpp == 0x08)
            {
                RemapTwice8To8Block(srcBuf, srcBase, srcPitch, srcX, srcY,
                                    dstBuf, dstBase, dstPitch, dstStartX, dstStartY,
                                    copyWidth, copyHeight, remapA, remapB);
            }
        }

        private static void RemapTwice8To8Block(byte[] src, int srcBase, int srcPitch, int srcX, int srcY,
                                                byte[] dst, int dstBase, int dstPitch, int dstX, int dstY,
                                                int width, int height, byte[] remapA, byte[] remapB)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitch + srcX);
                int dstRow = checked(dstBase + (dstY + y) * dstPitch + dstX);

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];
                    byte a = remapA[idx];
                    byte b = remapB[a];
                    dst[dstRow + x] = b;
                }
            }
        }

        private static void RemapTwice8To16Block(byte[] src, int srcBase, int srcPitch, int srcX, int srcY,
                                                 byte[] dst, int dstBase, int dstPitch, int dstX, int dstY,
                                                 int width, int height, byte[] remapA, byte[] remapB, ushort[] table)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitch + srcX);
                int dstRow = checked(dstBase + checked(((dstY + y) * dstPitch + dstX) * 2));

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];
                    byte a = remapA[idx];
                    byte b = remapB[a];

                    ushort v = table[b];

                    int di = dstRow + x * 2;
                    dst[di + 0] = (byte)(v & 0xFF);
                    dst[di + 1] = (byte)((v >> 8) & 0xFF);
                }
            }
        }

        private static void RemapTwice8To32Block(byte[] src, int srcBase, int srcPitch, int srcX, int srcY,
                                                 byte[] dst, int dstBase, int dstPitch, int dstX, int dstY,
                                                 int width, int height, byte[] remapA, byte[] remapB, uint[] table)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitch + srcX);
                int dstRow = checked(dstBase + checked(((dstY + y) * dstPitch + dstX) * 4));

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];
                    byte a = remapA[idx];
                    byte b = remapB[a];

                    uint v = table[b];

                    int di = dstRow + x * 4;
                    dst[di + 0] = (byte)(v & 0xFF);
                    dst[di + 1] = (byte)((v >> 8) & 0xFF);
                    dst[di + 2] = (byte)((v >> 16) & 0xFF);
                    dst[di + 3] = (byte)((v >> 24) & 0xFF);
                }
            }
        }


        // NXBasics::CBitmap::CopyIntoBitmap_ColorKeyed(...)
        internal void CopyIntoBitmap_ColorKeyed(CBitmap destination, int dstX, int dstY, byte colorKey)
        {
            if (destination == null)
            {
                return;
            }

            if (_bpp != 0x08)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] srcBuf, out int srcBase, out int srcPitch, out int srcBytesPerPixel))
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[] dstBuf, out int dstBase, out int dstPitch, out int dstBytesPerPixel))
            {
                return;
            }

            SRectangle dstBounds = new();
            dstBounds.SetVariables(0, 0, destination._rect.Width, destination._rect.Height);

            SRectangle placing = new();
            placing.SetVariables(dstX, dstY, _rect.Width, _rect.Height);

            if (!placing.IsTouching(dstBounds))
            {
                return;
            }

            placing.CutInside(dstBounds);

            if (placing.Width <= 0 || placing.Height <= 0)
            {
                return;
            }

            int copyWidth = placing.Width;
            int copyHeight = placing.Height;

            int srcX = placing.X - dstX;
            int srcY = placing.Y - dstY;

            int dstStartX = placing.X;
            int dstStartY = placing.Y;

            if (destination._bpp == 0x20)
            {
                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                uint[] table = pal.GetTrueColorTablePtr();
                if (table == null)
                {
                    return;
                }

                ColorKey8To32Block(srcBuf, srcBase, srcPitch, srcX, srcY,
                                   dstBuf, dstBase, dstPitch, dstStartX, dstStartY,
                                   copyWidth, copyHeight, colorKey, table);
                return;
            }

            if (destination._bpp == 0x10)
            {
                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                ushort[] table = pal.GetHighColorTablePtr();
                if (table == null)
                {
                    return;
                }

                ColorKey8To16Block(srcBuf, srcBase, srcPitch, srcX, srcY,
                                   dstBuf, dstBase, dstPitch, dstStartX, dstStartY,
                                   copyWidth, copyHeight, colorKey, table);
                return;
            }

            if (destination._bpp == 0x08)
            {
                ColorKey8To8Block(srcBuf, srcBase, srcPitch, srcX, srcY,
                                  dstBuf, dstBase, dstPitch, dstStartX, dstStartY,
                                  copyWidth, copyHeight, colorKey);
            }
        }

        private static void ColorKey8To8Block(byte[] src, int srcBase, int srcPitch, int srcX, int srcY,
                                              byte[] dst, int dstBase, int dstPitch, int dstX, int dstY,
                                              int width, int height, byte colorKey)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitch + srcX);
                int dstRow = checked(dstBase + (dstY + y) * dstPitch + dstX);

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];
                    if (idx != colorKey)
                    {
                        dst[dstRow + x] = idx;
                    }
                }
            }
        }

        private static void ColorKey8To16Block(byte[] src, int srcBase, int srcPitch, int srcX, int srcY,
                                               byte[] dst, int dstBase, int dstPitch, int dstX, int dstY,
                                               int width, int height, byte colorKey, ushort[] table)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitch + srcX);
                int dstRow = checked(dstBase + checked(((dstY + y) * dstPitch + dstX) * 2));

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];
                    if (idx == colorKey)
                    {
                        continue;
                    }

                    ushort v = table[idx];

                    int di = dstRow + x * 2;
                    dst[di + 0] = (byte)(v & 0xFF);
                    dst[di + 1] = (byte)((v >> 8) & 0xFF);
                }
            }
        }

        private static void ColorKey8To32Block(byte[] src, int srcBase, int srcPitch, int srcX, int srcY,
                                               byte[] dst, int dstBase, int dstPitch, int dstX, int dstY,
                                               int width, int height, byte colorKey, uint[] table)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitch + srcX);
                int dstRow = checked(dstBase + checked(((dstY + y) * dstPitch + dstX) * 4));

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];
                    if (idx == colorKey)
                    {
                        continue;
                    }

                    uint v = table[idx];

                    int di = dstRow + x * 4;
                    dst[di + 0] = (byte)(v & 0xFF);
                    dst[di + 1] = (byte)((v >> 8) & 0xFF);
                    dst[di + 2] = (byte)((v >> 16) & 0xFF);
                    dst[di + 3] = (byte)((v >> 24) & 0xFF);
                }
            }
        }

        // NXBasics::CBitmap::CopyIntoBitmap_ColorKeyed_Remap(...)
        internal void CopyIntoBitmap_ColorKeyed_Remap(CBitmap destination, int dstX, int dstY, byte colorKey, byte[] remap)
        {
            if (destination == null)
            {
                return;
            }

            if (remap == null || remap.Length < 256)
            {
                return;
            }

            if (_bpp != 0x08)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] srcBuf, out int srcBase, out int srcPitch, out int srcBytesPerPixel))
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[] dstBuf, out int dstBase, out int dstPitch, out int dstBytesPerPixel))
            {
                return;
            }

            SRectangle dstBounds = new SRectangle();
            dstBounds.SetVariables(0, 0, destination._rect.Width, destination._rect.Height);

            SRectangle placing = new SRectangle();
            placing.SetVariables(dstX, dstY, _rect.Width, _rect.Height);

            if (!placing.IsTouching(dstBounds))
            {
                return;
            }

            placing.CutInside(dstBounds);

            if (placing.Width <= 0 || placing.Height <= 0)
            {
                return;
            }

            int copyWidth = placing.Width;
            int copyHeight = placing.Height;

            int srcX = placing.X - dstX;
            int srcY = placing.Y - dstY;

            int dstStartX = placing.X;
            int dstStartY = placing.Y;

            if (destination._bpp == 0x20)
            {
                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                uint[] table = pal.GetTrueColorTablePtr();
                if (table == null)
                {
                    return;
                }

                ColorKeyRemap8To32Block(srcBuf, srcBase, srcPitch, srcX, srcY,
                                        dstBuf, dstBase, dstPitch, dstStartX, dstStartY,
                                        copyWidth, copyHeight, colorKey, remap, table);
                return;
            }

            if (destination._bpp == 0x10)
            {
                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                ushort[] table = pal.GetHighColorTablePtr();
                if (table == null)
                {
                    return;
                }

                ColorKeyRemap8To16Block(srcBuf, srcBase, srcPitch, srcX, srcY,
                                        dstBuf, dstBase, dstPitch, dstStartX, dstStartY,
                                        copyWidth, copyHeight, colorKey, remap, table);
                return;
            }

            if (destination._bpp == 0x08)
            {
                ColorKeyRemap8To8Block(srcBuf, srcBase, srcPitch, srcX, srcY,
                                       dstBuf, dstBase, dstPitch, dstStartX, dstStartY,
                                       copyWidth, copyHeight, colorKey, remap);
            }
        }

        // NXBasics::CBitmap::CopyIntoBitmap_ColorKeyed_RemapTwice(...)
        internal void CopyIntoBitmap_ColorKeyed_RemapTwice(CBitmap destination, int dstX, int dstY, byte colorKey, byte[] remapA, byte[] remapB)
        {
            if (destination == null)
            {
                return;
            }

            if (remapA == null || remapA.Length < 256)
            {
                return;
            }

            if (remapB == null || remapB.Length < 256)
            {
                return;
            }

            if (_bpp != 0x08)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] srcBuf, out int srcBase, out int srcPitch, out int srcBytesPerPixel))
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[] dstBuf, out int dstBase, out int dstPitch, out int dstBytesPerPixel))
            {
                return;
            }

            SRectangle dstBounds = new SRectangle();
            dstBounds.SetVariables(0, 0, destination._rect.Width, destination._rect.Height);

            SRectangle placing = new SRectangle();
            placing.SetVariables(dstX, dstY, _rect.Width, _rect.Height);

            if (!placing.IsTouching(dstBounds))
            {
                return;
            }

            placing.CutInside(dstBounds);

            if (placing.Width <= 0 || placing.Height <= 0)
            {
                return;
            }

            int copyWidth = placing.Width;
            int copyHeight = placing.Height;

            int srcX = placing.X - dstX;
            int srcY = placing.Y - dstY;

            int dstStartX = placing.X;
            int dstStartY = placing.Y;

            if (destination._bpp == 0x20)
            {
                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                uint[] table = pal.GetTrueColorTablePtr();
                if (table == null)
                {
                    return;
                }

                ColorKeyRemapTwice8To32Block(srcBuf, srcBase, srcPitch, srcX, srcY,
                                             dstBuf, dstBase, dstPitch, dstStartX, dstStartY,
                                             copyWidth, copyHeight, colorKey, remapA, remapB, table);
                return;
            }

            if (destination._bpp == 0x10)
            {
                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                ushort[] table = pal.GetHighColorTablePtr();
                if (table == null)
                {
                    return;
                }

                ColorKeyRemapTwice8To16Block(srcBuf, srcBase, srcPitch, srcX, srcY,
                                             dstBuf, dstBase, dstPitch, dstStartX, dstStartY,
                                             copyWidth, copyHeight, colorKey, remapA, remapB, table);
                return;
            }

            if (destination._bpp == 0x08)
            {
                ColorKeyRemapTwice8To8Block(srcBuf, srcBase, srcPitch, srcX, srcY,
                                            dstBuf, dstBase, dstPitch, dstStartX, dstStartY,
                                            copyWidth, copyHeight, colorKey, remapA, remapB);
            }
        }

        private static void ColorKeyRemap8To8Block(byte[] src, int srcBase, int srcPitch, int srcX, int srcY,
                                           byte[] dst, int dstBase, int dstPitch, int dstX, int dstY,
                                           int width, int height, byte colorKey, byte[] remap)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitch + srcX);
                int dstRow = checked(dstBase + (dstY + y) * dstPitch + dstX);

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];
                    if (idx != colorKey)
                    {
                        dst[dstRow + x] = remap[idx];
                    }
                }
            }
        }

        private static void ColorKeyRemap8To16Block(byte[] src, int srcBase, int srcPitch, int srcX, int srcY,
                                                    byte[] dst, int dstBase, int dstPitch, int dstX, int dstY,
                                                    int width, int height, byte colorKey, byte[] remap, ushort[] table)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitch + srcX);
                int dstRow = checked(dstBase + checked(((dstY + y) * dstPitch + dstX) * 2));

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];
                    if (idx == colorKey)
                    {
                        continue;
                    }

                    byte mapped = remap[idx];
                    ushort v = table[mapped];

                    int di = dstRow + x * 2;
                    dst[di + 0] = (byte)(v & 0xFF);
                    dst[di + 1] = (byte)((v >> 8) & 0xFF);
                }
            }
        }

        private static void ColorKeyRemap8To32Block(byte[] src, int srcBase, int srcPitch, int srcX, int srcY,
                                                    byte[] dst, int dstBase, int dstPitch, int dstX, int dstY,
                                                    int width, int height, byte colorKey, byte[] remap, uint[] table)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitch + srcX);
                int dstRow = checked(dstBase + checked(((dstY + y) * dstPitch + dstX) * 4));

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];
                    if (idx == colorKey)
                    {
                        continue;
                    }

                    byte mapped = remap[idx];
                    uint v = table[mapped];

                    int di = dstRow + x * 4;
                    dst[di + 0] = (byte)(v & 0xFF);
                    dst[di + 1] = (byte)((v >> 8) & 0xFF);
                    dst[di + 2] = (byte)((v >> 16) & 0xFF);
                    dst[di + 3] = (byte)((v >> 24) & 0xFF);
                }
            }
        }

        private static void ColorKeyRemapTwice8To8Block(byte[] src, int srcBase, int srcPitch, int srcX, int srcY,
                                                        byte[] dst, int dstBase, int dstPitch, int dstX, int dstY,
                                                        int width, int height, byte colorKey, byte[] remapA, byte[] remapB)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitch + srcX);
                int dstRow = checked(dstBase + (dstY + y) * dstPitch + dstX);

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];
                    if (idx != colorKey)
                    {
                        byte a = remapA[idx];
                        byte b = remapB[a];
                        dst[dstRow + x] = b;
                    }
                }
            }
        }

        private static void ColorKeyRemapTwice8To16Block(byte[] src, int srcBase, int srcPitch, int srcX, int srcY,
                                                         byte[] dst, int dstBase, int dstPitch, int dstX, int dstY,
                                                         int width, int height, byte colorKey, byte[] remapA, byte[] remapB, ushort[] table)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitch + srcX);
                int dstRow = checked(dstBase + checked(((dstY + y) * dstPitch + dstX) * 2));

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];
                    if (idx == colorKey)
                    {
                        continue;
                    }

                    byte mapped = remapB[remapA[idx]];
                    ushort v = table[mapped];

                    int di = dstRow + x * 2;
                    dst[di + 0] = (byte)(v & 0xFF);
                    dst[di + 1] = (byte)((v >> 8) & 0xFF);
                }
            }
        }

        private static void ColorKeyRemapTwice8To32Block(byte[] src, int srcBase, int srcPitch, int srcX, int srcY,
                                                         byte[] dst, int dstBase, int dstPitch, int dstX, int dstY,
                                                         int width, int height, byte colorKey, byte[] remapA, byte[] remapB, uint[] table)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitch + srcX);
                int dstRow = checked(dstBase + checked(((dstY + y) * dstPitch + dstX) * 4));

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];
                    if (idx == colorKey)
                    {
                        continue;
                    }

                    byte mapped = remapB[remapA[idx]];
                    uint v = table[mapped];

                    int di = dstRow + x * 4;
                    dst[di + 0] = (byte)(v & 0xFF);
                    dst[di + 1] = (byte)((v >> 8) & 0xFF);
                    dst[di + 2] = (byte)((v >> 16) & 0xFF);
                    dst[di + 3] = (byte)((v >> 24) & 0xFF);
                }
            }
        }

        // NXBasics::CBitmap::CopyIntoBitmap_HalfSize(...)
        internal void CopyIntoBitmap_HalfSize(CBitmap destination, int dstX, int dstY)
        {
            if (destination == null)
            {
                return;
            }

            if (_bpp != destination._bpp)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] srcBuf, out int srcBase, out int srcPitch, out int srcBytesPerPixel))
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[] dstBuf, out int dstBase, out int dstPitch, out int dstBytesPerPixel))
            {
                return;
            }

            // Needs identical byte size per pixel for raw copy.
            if (srcBytesPerPixel != dstBytesPerPixel)
            {
                return;
            }

            uint srcXSkip = 0;

            SRectangle dstBounds = new SRectangle();
            dstBounds.SetVariables(0, 0, destination._rect.Width, destination._rect.Height);

            SRectangle placing = new SRectangle();
            placing.SetVariables(dstX, dstY, _rect.Width / 2, _rect.Height / 2);

            int startX = placing.X;
            int startY = placing.Y;

            int ySkip = 0;

            if (placing.Y < 0)
            {
                ySkip = -placing.Y;
                placing.Y = 0;
            }

            if (placing.X < 0)
            {
                srcXSkip = (uint)(-placing.X);
                placing.X = 0;
            }

            // Original uses IsInside(placing, dstBounds)
            if (!placing.IsInside(dstBounds))
            {
                return;
            }

            if (ySkip >= placing.Height || placing.Height <= 0 || placing.Width <= 0)
            {
                return;
            }

            // xStart mirrors: int xStart=0; if (0 < startX) xStart = startX;
            int xStart = 0;
            if (0 < startX)
            {
                xStart = startX;
            }

            if (_bpp == 0x20)
            {
                CopyHalf_32(srcBuf, srcBase, srcPitch, (int)srcXSkip, ySkip,
                            dstBuf, dstBase, dstPitch, placing.X, placing.Y,
                            placing.Width, placing.Height, xStart, startX);
                return;
            }

            if (_bpp == 0x10)
            {
                CopyHalf_16(srcBuf, srcBase, srcPitch, (int)srcXSkip, ySkip,
                            dstBuf, dstBase, dstPitch, placing.X, placing.Y,
                            placing.Width, placing.Height, xStart, startX);
                return;
            }

            if (_bpp == 0x08)
            {
                CopyHalf_8(srcBuf, srcBase, srcPitch, (int)srcXSkip, ySkip,
                           dstBuf, dstBase, dstPitch, placing.X, placing.Y,
                           placing.Width, placing.Height, xStart, startX);
            }
        }

        private static void CopyHalf_32(byte[] src, int srcBase, int srcPitchPixels, int srcXSkip, int ySkip,
                                byte[] dst, int dstBase, int dstPitchPixels, int dstX, int dstY,
                                int width, int height, int xStart, int startX)
        {
            // srcPtr in dump: ((srcPitch*ySkip + srcXSkip) * 2) * 4 bytes
            int srcRow = checked(srcBase + checked(((srcPitchPixels * ySkip + srcXSkip) * 2) * 4));

            // dstPtr in dump: ((placingY*dstPitch + placingX) * 4 bytes)
            int dstRow = checked(dstBase + checked(((dstY * dstPitchPixels) + dstX) * 4));

            int row = ySkip;
            while (row < height)
            {
                uint x = (uint)srcXSkip;

                // srcCol = (xStart * 2) + (startX * -2)  == 2*(xStart - startX)
                int srcColPixels = (xStart * 2) - (startX * 2);

                if (srcXSkip < width)
                {
                    while (x < (uint)width)
                    {
                        int si = checked(srcRow + checked((srcColPixels * 4)));
                        int di = checked(dstRow + checked(((int)x) * 4));

                        dst[di + 0] = src[si + 0];
                        dst[di + 1] = src[si + 1];
                        dst[di + 2] = src[si + 2];
                        dst[di + 3] = src[si + 3];

                        x++;
                        srcColPixels += 2;
                    }
                }

                // srcPtr += (srcPitch*2) * 4 bytes
                srcRow = checked(srcRow + checked((srcPitchPixels * 2) * 4));
                // dstPtr += dstPitch * 4 bytes
                dstRow = checked(dstRow + checked(dstPitchPixels * 4));

                row++;
            }
        }

        private static void CopyHalf_16(byte[] src, int srcBase, int srcPitchPixels, int srcXSkip, int ySkip,
                                        byte[] dst, int dstBase, int dstPitchPixels, int dstX, int dstY,
                                        int width, int height, int xStart, int startX)
        {
            int srcRow = checked(srcBase + checked(((srcPitchPixels * ySkip + srcXSkip) * 2) * 2));
            int dstRow = checked(dstBase + checked(((dstY * dstPitchPixels) + dstX) * 2));

            int row = ySkip;
            while (row < height)
            {
                int x = srcXSkip;
                int srcColPixels = (xStart * 2) - (startX * 2);

                if (srcXSkip < width)
                {
                    while (x < width)
                    {
                        int si = checked(srcRow + checked(srcColPixels * 2));
                        int di = checked(dstRow + checked(x * 2));

                        dst[di + 0] = src[si + 0];
                        dst[di + 1] = src[si + 1];

                        x++;
                        srcColPixels += 2;
                    }
                }

                srcRow = checked(srcRow + checked((srcPitchPixels * 2) * 2));
                dstRow = checked(dstRow + checked(dstPitchPixels * 2));

                row++;
            }
        }

        private static void CopyHalf_8(byte[] src, int srcBase, int srcPitchPixels, int srcXSkip, int ySkip,
                                       byte[] dst, int dstBase, int dstPitchPixels, int dstX, int dstY,
                                       int width, int height, int xStart, int startX)
        {
            int srcRow = checked(srcBase + checked((srcPitchPixels * ySkip + srcXSkip) * 2));
            int dstRow = checked(dstBase + checked((dstY * dstPitchPixels) + dstX));

            int row = ySkip;
            while (row < height)
            {
                uint x = (uint)srcXSkip;
                int srcColPixels = (xStart * 2) - (startX * 2);

                if (srcXSkip < width)
                {
                    while (x < (uint)width)
                    {
                        int si = checked(srcRow + srcColPixels);
                        int di = checked(dstRow + (int)x);

                        dst[di] = src[si];

                        x++;
                        srcColPixels += 2;
                    }
                }

                srcRow = checked(srcRow + (srcPitchPixels * 2));
                dstRow = checked(dstRow + dstPitchPixels);

                row++;
            }
        }

        // NXBasics::CBitmap::CopyIntoBitmap_HalfSize_ColorKeyed(...)
        internal void CopyIntoBitmap_HalfSize_ColorKeyed(CBitmap destination, int dstX, int dstY, byte colorKey)
        {
            if (destination == null)
            {
                return;
            }

            if (_bpp != 0x08)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] srcBuf, out int srcBase, out int srcPitch, out int srcBytesPerPixel))
            {
                return;
            }

            if (srcBytesPerPixel != 1)
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[] dstBuf, out int dstBase, out int dstPitch, out int dstBytesPerPixel))
            {
                return;
            }

            uint srcXSkip = 0;

            SRectangle dstBounds = new SRectangle();
            dstBounds.SetVariables(0, 0, destination._rect.Width, destination._rect.Height);

            SRectangle placing = new SRectangle();
            placing.SetVariables(dstX, dstY, _rect.Width / 2, _rect.Height / 2);

            int startX = placing.X;
            int ySkip = 0;

            if (placing.Y < 0)
            {
                ySkip = -placing.Y;
                placing.Y = 0;
            }

            if (placing.X < 0)
            {
                srcXSkip = (uint)(-placing.X);
                placing.X = 0;
            }

            if (!placing.IsInside(dstBounds))
            {
                return;
            }

            if (ySkip >= placing.Height)
            {
                return;
            }

            int xStart = 0;
            if (0 < startX)
            {
                xStart = startX;
            }

            // srcPtr in dump: _ptr8 + ((srcPitch * ySkip + srcXSkip) * 2)
            int srcRow = checked(srcBase + checked((srcPitch * ySkip + (int)srcXSkip) * 2));

            if (destination._bpp == 0x20)
            {
                if (dstBytesPerPixel != 4)
                {
                    return;
                }

                CPalette palette = _palette;
                palette ??= CXBSystemManager.sPalettePtr;

                uint[] table = palette.GetTrueColorTablePtr();
                if (table == null)
                {
                    return;
                }

                int dstRow = checked(dstBase + checked(((placing.Y * dstPitch) + placing.X) * 4));

                int row = ySkip;
                while (row < placing.Height)
                {
                    ulong x = srcXSkip;
                    int srcCol = (xStart * 2) - (startX * 2);

                    if ((int)srcXSkip < placing.Width)
                    {
                        while ((int)x < placing.Width)
                        {
                            int si = checked(srcRow + srcCol);
                            byte idx = srcBuf[si];

                            if (idx != colorKey)
                            {
                                uint color = table[idx];

                                int di = checked(dstRow + checked(((int)x) * 4));
                                dstBuf[di + 0] = (byte)(color & 0xFF);
                                dstBuf[di + 1] = (byte)((color >> 8) & 0xFF);
                                dstBuf[di + 2] = (byte)((color >> 16) & 0xFF);
                                dstBuf[di + 3] = (byte)((color >> 24) & 0xFF);
                            }

                            x++;
                            srcCol += 2;
                        }
                    }

                    // srcPtr += srcPitch * 2
                    srcRow = checked(srcRow + (srcPitch * 2));
                    // dstPtr += dstPitch * 4
                    dstRow = checked(dstRow + checked(dstPitch * 4));

                    row++;
                }

                return;
            }

            if (destination._bpp == 0x10)
            {
                if (dstBytesPerPixel != 2)
                {
                    return;
                }

                CPalette palette = _palette;
                palette ??= CXBSystemManager.sPalettePtr;

                ushort[] table = palette.GetHighColorTablePtr();
                if (table == null)
                {
                    return;
                }

                int dstRow = checked(dstBase + checked(((placing.Y * dstPitch) + placing.X) * 2));

                int row = ySkip;
                while (row < placing.Height)
                {
                    ulong x = srcXSkip;
                    int srcCol = (xStart * 2) - (startX * 2);

                    if ((int)srcXSkip < placing.Width)
                    {
                        while ((int)x < placing.Width)
                        {
                            int si = checked(srcRow + srcCol);
                            byte idx = srcBuf[si];

                            if (idx != colorKey)
                            {
                                ushort w = table[idx];

                                int di = checked(dstRow + checked(((int)x) * 2));
                                dstBuf[di + 0] = (byte)(w & 0xFF);
                                dstBuf[di + 1] = (byte)((w >> 8) & 0xFF);
                            }

                            x++;
                            srcCol += 2;
                        }
                    }

                    srcRow = checked(srcRow + (srcPitch * 2));
                    dstRow = checked(dstRow + checked(dstPitch * 2));

                    row++;
                }

                return;
            }

            if (destination._bpp == 0x08)
            {
                if (dstBytesPerPixel != 1)
                {
                    return;
                }

                int dstRow = checked(dstBase + (placing.Y * dstPitch) + placing.X);

                int row = ySkip;
                while (row < placing.Height)
                {
                    uint x = srcXSkip;
                    int srcCol = (xStart * 2) - (startX * 2);

                    if ((int)srcXSkip < placing.Width)
                    {
                        while ((int)x < placing.Width)
                        {
                            int si = checked(srcRow + srcCol);
                            byte idx = srcBuf[si];

                            if (idx != colorKey)
                            {
                                int di = checked(dstRow + (int)x);
                                dstBuf[di] = idx;
                            }

                            x++;
                            srcCol += 2;
                        }
                    }

                    srcRow = checked(srcRow + (srcPitch * 2));
                    dstRow = checked(dstRow + dstPitch);

                    row++;
                }
            }
        }

        // NXBasics::CBitmap::CopyIntoBitmap_HalfSize_ColorKeyed_Remap(NXBasics::CBitmap const&, int, int, unsigned char, unsigned char const*) const
        // NXBasics::CBitmap::CopyIntoBitmap_HalfSize_ColorKeyed_RemapTwice(NXBasics::CBitmap const&, int, int, unsigned char, unsigned char const*, unsigned char const*) const
        internal void CopyIntoBitmap_HalfSize_ColorKeyed_Remap(CBitmap destination, int dstX, int dstY, byte colorKey, byte[] remap)
        {
            CopyIntoBitmap_HalfSize_ColorKeyed_RemapCore(destination, dstX, dstY, colorKey, remap, null, false);
        }

        internal void CopyIntoBitmap_HalfSize_ColorKeyed_RemapTwice(CBitmap destination, int dstX, int dstY, byte colorKey, byte[] remapA, byte[] remapB)
        {
            CopyIntoBitmap_HalfSize_ColorKeyed_RemapCore(destination, dstX, dstY, colorKey, remapA, remapB, true);
        }

        private void CopyIntoBitmap_HalfSize_ColorKeyed_RemapCore(CBitmap destination, int dstX, int dstY, byte colorKey, byte[] remapA, byte[]? remapB, bool longAlign)
        {
            if (destination is null)
            {
                return;
            }

            if (_bpp != 0x08)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] srcBuf, out int srcBase, out int srcPitch, out int srcBytesPerPixel))
            {
                return;
            }

            if (srcBytesPerPixel != 1)
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[] dstBuf, out int dstBase, out int dstPitch, out int dstBytesPerPixel))
            {
                return;
            }

            if (remapA is null || remapA.Length < 256)
            {
                return;
            }

            if (remapB is not null && remapB.Length < 256)
            {
                return;
            }

            uint srcXSkip = 0;

            SRectangle dstBounds = new(0, 0, destination._rect.Width, destination._rect.Height);

            int x0 = dstX;
            int y0 = dstY;

            SRectangle placing;

            if (longAlign)
            {
                x0 = dstX & ~1;
                y0 = dstY & ~1;

                placing = new SRectangle(x0, y0, _rect.Width / 2, _rect.Height / 2);

                dstBounds.MakeSizeLongAlligned();
                placing.MakeSizeLongAlligned();
            }
            else
            {
                placing = new SRectangle(dstX, dstY, _rect.Width / 2, _rect.Height / 2);
            }

            int originalPlacingX = placing.X;
            int ySkip = 0;

            if (placing.Y < 0)
            {
                ySkip = -placing.Y;
                placing.Y = 0;
            }

            if (placing.X < 0)
            {
                srcXSkip = (uint)(-placing.X);
                placing.X = 0;
            }

            if (!placing.IsInside(dstBounds))
            {
                return;
            }

            byte Map(byte idx)
            {
                if (remapB is null)
                {
                    return remapA[idx];
                }

                return remapB[remapA[idx]];
            }

            int xStart = 0;
            if (0 < originalPlacingX)
            {
                xStart = originalPlacingX;
            }

            int srcXBase = (xStart - originalPlacingX) * 2;

            ReadOnlySpan<byte> src = srcBuf;
            Span<byte> dst = dstBuf;

            // Destination base offset in bytes (top-left of placing rectangle)
            int dstPlacingBase = dstBase + ((placing.Y * dstPitch) + placing.X) * dstBytesPerPixel;

            if (dstBytesPerPixel == 4) // 0x20
            {
                CPalette palette = _palette;
                palette ??= CXBSystemManager.sPalettePtr;

                ushort[] table = palette.GetHighColorTablePtr();
                if (table == null)
                {
                    return;
                }

                for (int row = ySkip; row < placing.Height; row++)
                {
                    int srcY = row * 2;
                    int srcRowByte = srcBase + (srcY * srcPitch) + ((int)srcXSkip * 2);

                    int dstRowByte = dstPlacingBase + ((row - ySkip) * dstPitch * 4);

                    for (int x = (int)srcXSkip; x < placing.Width; x++)
                    {
                        int srcIndex = srcRowByte + (srcXBase + (x * 2));
                        if ((uint)srcIndex >= (uint)src.Length)
                        {
                            continue;
                        }

                        byte idx = src[srcIndex];
                        if (idx == colorKey)
                        {
                            continue;
                        }

                        byte mapped = Map(idx);
                        uint color = table[mapped];

                        int dstIndex = dstRowByte + (x * 4);
                        if ((uint)(dstIndex + 3) >= (uint)dst.Length)
                        {
                            continue;
                        }

                        // Little-endian write (avoids pointers/unsafe)
                        dst[dstIndex + 0] = (byte)(color);
                        dst[dstIndex + 1] = (byte)(color >> 8);
                        dst[dstIndex + 2] = (byte)(color >> 16);
                        dst[dstIndex + 3] = (byte)(color >> 24);
                    }
                }

                return;
            }

            if (dstBytesPerPixel == 2) // 0x10
            {
                CPalette palette = _palette;
                palette ??= CXBSystemManager.sPalettePtr;

                ushort[] table = palette.GetHighColorTablePtr();
                if (table == null)
                {
                    return;
                }

                for (int row = ySkip; row < placing.Height; row++)
                {
                    int srcY = row * 2;
                    int srcRowByte = srcBase + (srcY * srcPitch) + ((int)srcXSkip * 2);

                    int dstRowByte = dstPlacingBase + ((row - ySkip) * dstPitch * 2);

                    for (int x = (int)srcXSkip; x < placing.Width; x++)
                    {
                        int srcIndex = srcRowByte + (srcXBase + (x * 2));
                        if ((uint)srcIndex >= (uint)src.Length)
                        {
                            continue;
                        }

                        byte idx = src[srcIndex];
                        if (idx == colorKey)
                        {
                            continue;
                        }

                        byte mapped = Map(idx);
                        ushort w = table[mapped];

                        int dstIndex = dstRowByte + (x * 2);
                        if ((uint)(dstIndex + 1) >= (uint)dst.Length)
                        {
                            continue;
                        }

                        dst[dstIndex + 0] = (byte)(w);
                        dst[dstIndex + 1] = (byte)(w >> 8);
                    }
                }

                return;
            }

            if (dstBytesPerPixel == 1) // 0x08
            {
                for (int row = ySkip; row < placing.Height; row++)
                {
                    int srcY = row * 2;
                    int srcRowByte = srcBase + (srcY * srcPitch) + ((int)srcXSkip * 2);

                    int dstRowByte = dstPlacingBase + ((row - ySkip) * dstPitch);

                    for (int x = (int)srcXSkip; x < placing.Width; x++)
                    {
                        int srcIndex = srcRowByte + (srcXBase + (x * 2));
                        if ((uint)srcIndex >= (uint)src.Length)
                        {
                            continue;
                        }

                        byte idx = src[srcIndex];
                        if (idx == colorKey)
                        {
                            continue;
                        }

                        byte mapped = Map(idx);

                        int dstIndex = dstRowByte + x;
                        if ((uint)dstIndex >= (uint)dst.Length)
                        {
                            continue;
                        }

                        dst[dstIndex] = mapped;
                    }
                }
            }
        }

        // NXBasics::CBitmap::CopyIntoBitmap_HalfSize_Optimized(NXBasics::CBitmap const&) const
        internal void CopyIntoBitmap_HalfSize_Optimized(CBitmap destination)
        {
            if (destination == null)
            {
                return;
            }

            int srcW = _rect.Width;
            int srcH = _rect.Height;

            int dstW = destination._rect.Width;
            int dstH = destination._rect.Height;

            // src must be exactly 2x dst
            if (srcW != dstW * 2)
            {
                CopyIntoBitmap_HalfSize(destination, 0, 0);
                return;
            }

            if (srcH != dstH * 2)
            {
                CopyIntoBitmap_HalfSize(destination, 0, 0);
                return;
            }

            // must match bpp and only 16 or 32 supported by optimized path
            if (_bpp != destination._bpp)
            {
                CopyIntoBitmap_HalfSize(destination, 0, 0);
                return;
            }

            if (_bpp != 0x20 && _bpp != 0x10)
            {
                CopyIntoBitmap_HalfSize(destination, 0, 0);
                return;
            }

            // native requires even height and src width aligned (srcW & 0x3f) == 0
            if (((srcH & 1) != 0) || ((srcW & 0x3F) != 0))
            {
                CopyIntoBitmap_HalfSize(destination, 0, 0);
                return;
            }

            if (!TryGetPixelBuffer(out byte[] srcBuf, out int srcBase, out int srcPitch, out int srcBytesPerPixel))
            {
                CopyIntoBitmap_HalfSize(destination, 0, 0);
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[] dstBuf, out int dstBase, out int dstPitch, out int dstBytesPerPixel))
            {
                CopyIntoBitmap_HalfSize(destination, 0, 0);
                return;
            }

            int bytesPerPixel = (_bpp == 0x10) ? 2 : 4;

            if (srcBytesPerPixel != bytesPerPixel || dstBytesPerPixel != bytesPerPixel)
            {
                CopyIntoBitmap_HalfSize(destination, 0, 0);
                return;
            }

            if (dstW <= 0 || dstH <= 0)
            {
                return;
            }

            int srcRowStrideBytes = checked(srcPitch * bytesPerPixel);
            int dstRowStrideBytes = checked(dstPitch * bytesPerPixel);

            // Nearest-neighbor half-size:
            // dst(x,y) = src(2x,2y)
            for (int y = 0; y < dstH; y++)
            {
                int srcRow = checked(srcBase + (2 * y) * srcRowStrideBytes);
                int dstRow = checked(dstBase + y * dstRowStrideBytes);

                for (int x = 0; x < dstW; x++)
                {
                    int srcPixel = checked(srcRow + (2 * x) * bytesPerPixel);
                    int dstPixel = checked(dstRow + x * bytesPerPixel);

                    if (bytesPerPixel == 2)
                    {
                        dstBuf[dstPixel + 0] = srcBuf[srcPixel + 0];
                        dstBuf[dstPixel + 1] = srcBuf[srcPixel + 1];
                    }
                    else
                    {
                        dstBuf[dstPixel + 0] = srcBuf[srcPixel + 0];
                        dstBuf[dstPixel + 1] = srcBuf[srcPixel + 1];
                        dstBuf[dstPixel + 2] = srcBuf[srcPixel + 2];
                        dstBuf[dstPixel + 3] = srcBuf[srcPixel + 3];
                    }
                }
            }
        }

        // NXBasics::CBitmap::CopyIntoBitmap_FitIn(NXBasics::CBitmap const&) const
        internal void CopyIntoBitmap_FitIn(CBitmap destination)
        {
            if (destination == null)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] srcBuf, out int srcBase, out int srcPitch, out int srcBytesPerPixel))
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[] dstBuf, out int dstBase, out int dstPitch, out int dstBytesPerPixel))
            {
                return;
            }

            int dstW = destination._rect.Width;
            int dstH = destination._rect.Height;

            if (dstW <= 0 || dstH <= 0)
            {
                return;
            }

            int srcW = _rect.Width;
            int srcH = _rect.Height;

            if (srcW <= 0 || srcH <= 0)
            {
                return;
            }

            if (_bpp == 0x20)
            {
                if (destination._bpp != 0x20)
                {
                    return;
                }

                if (srcBytesPerPixel != 4 || dstBytesPerPixel != 4)
                {
                    return;
                }

                int srcRowStrideBytes = checked(srcPitch * 4);
                int dstRowStrideBytes = checked(dstPitch * 4);

                for (int y = 0; y < dstH; y++)
                {
                    int srcY = (srcH * y) / dstH;

                    for (int x = 0; x < dstW; x++)
                    {
                        int srcX = (srcW * x) / dstW;

                        uint pixel = ReadPixel32(srcBuf, srcBase, srcRowStrideBytes, srcX, srcY);
                        WritePixel32(dstBuf, dstBase, dstRowStrideBytes, x, y, pixel);
                    }
                }

                return;
            }

            if (_bpp == 0x10)
            {
                if (destination._bpp != 0x10)
                {
                    return;
                }

                if (srcBytesPerPixel != 2 || dstBytesPerPixel != 2)
                {
                    return;
                }

                int srcRowStrideBytes = checked(srcPitch * 2);
                int dstRowStrideBytes = checked(dstPitch * 2);

                // Original had an accumulator for srcX, but it is equivalent to (srcW * x) / dstW.
                for (int y = 0; y < dstH; y++)
                {
                    int srcY = (srcH * y) / dstH;

                    for (int x = 0; x < dstW; x++)
                    {
                        int srcX = (srcW * x) / dstW;

                        ushort pixel = ReadPixel16(srcBuf, srcBase, srcRowStrideBytes, srcX, srcY);
                        WritePixel16(dstBuf, dstBase, dstRowStrideBytes, x, y, pixel);
                    }
                }

                return;
            }

            if (_bpp != 0x08)
            {
                return;
            }

            // 8-bit source => destination may be 32/16/8
            if (destination._bpp == 0x20)
            {
                if (dstBytesPerPixel != 4)
                {
                    return;
                }

                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                uint[] table = pal.GetTrueColorTablePtr();
                if (table == null)
                {
                    return;
                }

                int srcRowStrideBytes = srcPitch;          // 1 byte per pixel
                int dstRowStrideBytes = checked(dstPitch * 4);

                for (int y = 0; y < dstH; y++)
                {
                    int srcY = (srcH * y) / dstH;
                    int srcRow = checked(srcBase + srcY * srcRowStrideBytes);

                    for (int x = 0; x < dstW; x++)
                    {
                        int srcX = (srcW * x) / dstW;
                        int si = srcRow + srcX;

                        if ((uint)si >= (uint)srcBuf.Length)
                        {
                            WritePixel32(dstBuf, dstBase, dstRowStrideBytes, x, y, 0);
                            continue;
                        }

                        byte idx = srcBuf[si];
                        uint color = table[idx];
                        WritePixel32(dstBuf, dstBase, dstRowStrideBytes, x, y, color);
                    }
                }

                return;
            }

            if (destination._bpp == 0x10)
            {
                if (dstBytesPerPixel != 2)
                {
                    return;
                }

                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                ushort[] table = pal.GetHighColorTablePtr();
                if (table == null)
                {
                    return;
                }

                int srcRowStrideBytes = srcPitch;          // 1 byte per pixel
                int dstRowStrideBytes = checked(dstPitch * 2);

                for (int y = 0; y < dstH; y++)
                {
                    int srcY = (srcH * y) / dstH;
                    int srcRow = checked(srcBase + srcY * srcRowStrideBytes);

                    for (int x = 0; x < dstW; x++)
                    {
                        int srcX = (srcW * x) / dstW;
                        int si = srcRow + srcX;

                        if ((uint)si >= (uint)srcBuf.Length)
                        {
                            WritePixel16(dstBuf, dstBase, dstRowStrideBytes, x, y, 0);
                            continue;
                        }

                        byte idx = srcBuf[si];
                        ushort color = table[idx];
                        WritePixel16(dstBuf, dstBase, dstRowStrideBytes, x, y, color);
                    }
                }

                return;
            }

            if (destination._bpp == 0x08)
            {
                if (dstBytesPerPixel != 1)
                {
                    return;
                }

                int srcRowStrideBytes = srcPitch; // 1 byte per pixel
                int dstRowStrideBytes = dstPitch;

                for (int y = 0; y < dstH; y++)
                {
                    int srcY = (srcH * y) / dstH;
                    int srcRow = checked(srcBase + srcY * srcRowStrideBytes);
                    int dstRow = checked(dstBase + y * dstRowStrideBytes);

                    for (int x = 0; x < dstW; x++)
                    {
                        int srcX = (srcW * x) / dstW;
                        int si = srcRow + srcX;
                        int di = dstRow + x;

                        if ((uint)si >= (uint)srcBuf.Length || (uint)di >= (uint)dstBuf.Length)
                        {
                            continue;
                        }

                        dstBuf[di] = srcBuf[si];
                    }
                }
            }
        }

        private static ushort ReadPixel16(byte[] buffer, int baseOffsetBytes, int rowStrideBytes, int x, int y)
        {
            int bi = checked(baseOffsetBytes + y * rowStrideBytes + x * 2);
            if ((uint)(bi + 1) >= (uint)buffer.Length)
            {
                return 0;
            }

            return (ushort)(buffer[bi + 0] | (buffer[bi + 1] << 8));
        }

        private static void WritePixel16(byte[] buffer, int baseOffsetBytes, int rowStrideBytes, int x, int y, ushort value)
        {
            int bi = checked(baseOffsetBytes + y * rowStrideBytes + x * 2);
            if ((uint)(bi + 1) >= (uint)buffer.Length)
            {
                return;
            }

            buffer[bi + 0] = (byte)(value & 0xFF);
            buffer[bi + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static uint ReadPixel32(byte[] buffer, int baseOffsetBytes, int rowStrideBytes, int x, int y)
        {
            int bi = checked(baseOffsetBytes + y * rowStrideBytes + x * 4);
            if ((uint)(bi + 3) >= (uint)buffer.Length)
            {
                return 0;
            }

            return (uint)(
                buffer[bi + 0] |
                (buffer[bi + 1] << 8) |
                (buffer[bi + 2] << 16) |
                (buffer[bi + 3] << 24));
        }

        private static void WritePixel32(byte[] buffer, int baseOffsetBytes, int rowStrideBytes, int x, int y, uint value)
        {
            int bi = checked(baseOffsetBytes + y * rowStrideBytes + x * 4);
            if ((uint)(bi + 3) >= (uint)buffer.Length)
            {
                return;
            }

            buffer[bi + 0] = (byte)(value & 0xFF);
            buffer[bi + 1] = (byte)((value >> 8) & 0xFF);
            buffer[bi + 2] = (byte)((value >> 16) & 0xFF);
            buffer[bi + 3] = (byte)((value >> 24) & 0xFF);
        }
        #endregion

        #region NXBasics::CBitmap::CopyOutOfBitmap()
        // NXBasics::CBitmap::CopyOutOfBitmap(NXBasics::CBitmap const&, int, int) const
        internal void CopyOutOfBitmap(CBitmap source, int srcX, int srcY)
        {
            if (source == null)
            {
                return;
            }

            if (_bpp != 0x08 || source._bpp != 0x08)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] dstBuf, out int dstBase, out int dstPitch, out int dstBytesPerPixel))
            {
                return;
            }

            if (!source.TryGetPixelBuffer(out byte[] srcBuf, out int srcBase, out int srcPitch, out int srcBytesPerPixel))
            {
                return;
            }

            if (dstBytesPerPixel != 1 || srcBytesPerPixel != 1)
            {
                return;
            }

            // Desired source area in source-space
            SRectangle r = new();
            r.SetVariables(srcX, srcY, _rect.Width, _rect.Height);

            if (!r.IsTouching(source._rect))
            {
                return;
            }

            r.CutInside(source._rect);

            if (r.Width <= 0 || r.Height <= 0)
            {
                return;
            }

            // Copy into this bitmap starting at (0,0) like the original (dstPtr = _ptr8).
            // If dst rect offset matters later, this needs adjustment, but the original clearly writes from start.
            int copyWidth = r.Width;
            int copyHeight = r.Height;

            int srcStartX = r.X - source._rect.X;
            int srcStartY = r.Y - source._rect.Y;

            for (int y = 0; y < copyHeight; y++)
            {
                int srcRow = checked(srcBase + (srcStartY + y) * srcPitch + srcStartX);
                int dstRow = checked(dstBase + y * dstPitch);

                if ((uint)srcRow >= (uint)srcBuf.Length || (uint)dstRow >= (uint)dstBuf.Length)
                {
                    continue;
                }

                int count = copyWidth;

                int srcAvail = srcBuf.Length - srcRow;
                if (count > srcAvail)
                {
                    count = srcAvail;
                }

                int dstAvail = dstBuf.Length - dstRow;
                if (count > dstAvail)
                {
                    count = dstAvail;
                }

                if (count > 0)
                {
                    Buffer.BlockCopy(srcBuf, srcRow, dstBuf, dstRow, count);
                }
            }
        }
        #endregion

        #region NXBasics::CBitmap::Draw_SetPixel
        // NXBasics::CBitmap::Draw_SetPixel(int, int, unsigned char) const
        internal void Draw_SetPixel(int x, int y, byte index)
        {
            if (!IsPointInside(x, y))
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel))
            {
                return;
            }

            int relX = x - _rect.X;
            int relY = y - _rect.Y;

            if (_bpp == 0x20)
            {
                if (bytesPerPixel != 4)
                {
                    return;
                }

                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                uint color = pal.GetTrueColorWord(index);

                int rowStrideBytes = checked(pitchPixels * 4);
                int bi = checked(baseOffsetBytes + relY * rowStrideBytes + relX * 4);

                if ((uint)(bi + 3) >= (uint)buffer.Length)
                {
                    return;
                }

                buffer[bi + 0] = (byte)(color & 0xFF);
                buffer[bi + 1] = (byte)((color >> 8) & 0xFF);
                buffer[bi + 2] = (byte)((color >> 16) & 0xFF);
                buffer[bi + 3] = (byte)((color >> 24) & 0xFF);
                return;
            }

            if (_bpp == 0x10)
            {
                if (bytesPerPixel != 2)
                {
                    return;
                }

                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                ushort color = pal.GetHighColorWord(index);

                int rowStrideBytes = checked(pitchPixels * 2);
                int bi = checked(baseOffsetBytes + relY * rowStrideBytes + relX * 2);

                if ((uint)(bi + 1) >= (uint)buffer.Length)
                {
                    return;
                }

                buffer[bi + 0] = (byte)(color & 0xFF);
                buffer[bi + 1] = (byte)((color >> 8) & 0xFF);
                return;
            }

            if (_bpp == 0x08)
            {
                if (bytesPerPixel != 1)
                {
                    return;
                }

                int bi = checked(baseOffsetBytes + relY * pitchPixels + relX);
                if ((uint)bi >= (uint)buffer.Length)
                {
                    return;
                }

                buffer[bi] = index;
            }
        }

        // NXBasics::CBitmap::Draw_SetPixel(int, int, unsigned short) const
        internal void Draw_SetPixel(int x, int y, ushort value)
        {
            if (_bpp != 0x10)
            {
                return;
            }

            if (!IsPointInside(x, y))
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel))
            {
                return;
            }

            if (bytesPerPixel != 2)
            {
                return;
            }

            int relX = x - _rect.X;
            int relY = y - _rect.Y;

            int rowStrideBytes = checked(pitchPixels * 2);
            int bi = checked(baseOffsetBytes + relY * rowStrideBytes + relX * 2);

            if ((uint)(bi + 1) >= (uint)buffer.Length)
            {
                return;
            }

            buffer[bi + 0] = (byte)(value & 0xFF);
            buffer[bi + 1] = (byte)((value >> 8) & 0xFF);
        }

        // NXBasics::CBitmap::Draw_SetPixel(int, int, unsigned int) const
        internal void Draw_SetPixel(int x, int y, uint value)
        {
            if (_bpp != 0x20)
            {
                return;
            }

            if (!IsPointInside(x, y))
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel))
            {
                return;
            }

            if (bytesPerPixel != 4)
            {
                return;
            }

            int relX = x - _rect.X;
            int relY = y - _rect.Y;

            int rowStrideBytes = checked(pitchPixels * 4);
            int bi = checked(baseOffsetBytes + relY * rowStrideBytes + relX * 4);

            if ((uint)(bi + 3) >= (uint)buffer.Length)
            {
                return;
            }

            buffer[bi + 0] = (byte)(value & 0xFF);
            buffer[bi + 1] = (byte)((value >> 8) & 0xFF);
            buffer[bi + 2] = (byte)((value >> 16) & 0xFF);
            buffer[bi + 3] = (byte)((value >> 24) & 0xFF);
        }

        // NXBasics::CBitmap::Draw_SetPixel(int, int, NXBasics::SColorRGB const&) const
        internal void Draw_SetPixel(int x, int y, in SColorRGB color)
        {
            if (!IsPointInside(x, y))
            {
                return;
            }

            if (_bpp == 0x20)
            {
                uint v = CXBSystemManager.sTrueColorCreatorPtr.GetTrueColorWord(in color);
                Draw_SetPixel(x, y, v);
                return;
            }

            if (_bpp == 0x10)
            {
                ushort v = CXBSystemManager.sHighColorCreatorPtr.GetHighColorWord(color.R, color.G, color.B);
                Draw_SetPixel(x, y, v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                byte idx = pal.FindMatchingColor(color.R, color.G, color.B);
                Draw_SetPixel(x, y, idx);
            }
        }

        // NXBasics::CBitmap::Draw_SetPixelUnclipped(int, int, NXBasics::SColorRGB const&) const
        internal void Draw_SetPixelUnclipped(int x, int y, in SColorRGB color)
        {
            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel))
            {
                return;
            }

            int relX = x - _rect.X;
            int relY = y - _rect.Y;

            if (relX < 0 || relY < 0)
            {
                return;
            }

            if (_bpp == 0x20)
            {
                if (bytesPerPixel != 4)
                {
                    return;
                }

                uint v = CXBSystemManager.sTrueColorCreatorPtr.GetTrueColorWord(in color);

                int rowStrideBytes = checked(pitchPixels * 4);
                int bi = checked(baseOffsetBytes + relY * rowStrideBytes + relX * 4);

                if ((uint)(bi + 3) >= (uint)buffer.Length)
                {
                    return;
                }

                buffer[bi + 0] = (byte)(v & 0xFF);
                buffer[bi + 1] = (byte)((v >> 8) & 0xFF);
                buffer[bi + 2] = (byte)((v >> 16) & 0xFF);
                buffer[bi + 3] = (byte)((v >> 24) & 0xFF);
                return;
            }

            if (_bpp == 0x10)
            {
                if (bytesPerPixel != 2)
                {
                    return;
                }

                ushort v = CXBSystemManager.sHighColorCreatorPtr.GetHighColorWord(color.R, color.G, color.B);

                int rowStrideBytes = checked(pitchPixels * 2);
                int bi = checked(baseOffsetBytes + relY * rowStrideBytes + relX * 2);

                if ((uint)(bi + 1) >= (uint)buffer.Length)
                {
                    return;
                }

                buffer[bi + 0] = (byte)(v & 0xFF);
                buffer[bi + 1] = (byte)((v >> 8) & 0xFF);
                return;
            }

            if (_bpp == 0x08)
            {
                if (bytesPerPixel != 1)
                {
                    return;
                }

                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                byte idx = pal.FindMatchingColor(color.R, color.G, color.B);

                int bi = checked(baseOffsetBytes + relY * pitchPixels + relX);
                if ((uint)bi >= (uint)buffer.Length)
                {
                    return;
                }

                buffer[bi] = idx;
            }
        }

        // NXBasics::CBitmap::IsPointInside(int, int) const
        internal bool IsPointInside(int x, int y)
        {
            int rightExclusive = checked(_rect.X + _rect.Width);
            int bottomExclusive = checked(_rect.Y + _rect.Height);

            if (_rect.X <= x && x < rightExclusive && _rect.Y <= y)
            {
                return y < bottomExclusive;
            }

            return false;
        }
        #endregion

        #region NXBasics::CBitmap::Draw_Box()
        // NXBasics::CBitmap::Draw_Box(NXBasics::SRectangle, unsigned char) const
        internal void Draw_Box(ref SRectangle rect, byte index)
        {
            rect.Validate();

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel))
            {
                return;
            }

            if (!rect.IsTouching(_rect))
            {
                return;
            }

            rect.CutInside(_rect);

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            int relX = rect.X - _rect.X;
            int relY = rect.Y - _rect.Y;

            if (_bpp == 0x20)
            {
                if (bytesPerPixel != 4)
                {
                    return;
                }

                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                uint color = pal.GetTrueColorWord(index);

                int rowStrideBytes = checked(pitchPixels * 4);
                int start = checked(baseOffsetBytes + relY * rowStrideBytes + relX * 4);

                FillBlock32(buffer, start, rowStrideBytes, rect.Width, rect.Height, color);
                return;
            }

            if (_bpp == 0x10)
            {
                if (bytesPerPixel != 2)
                {
                    return;
                }

                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                ushort color = pal.GetHighColorWord(index);

                int rowStrideBytes = checked(pitchPixels * 2);
                int start = checked(baseOffsetBytes + relY * rowStrideBytes + relX * 2);

                FillBlock16(buffer, start, rowStrideBytes, rect.Width, rect.Height, color);
                return;
            }

            if (_bpp == 0x08)
            {
                if (bytesPerPixel != 1)
                {
                    return;
                }

                int rowStrideBytes = pitchPixels;
                int start = checked(baseOffsetBytes + relY * rowStrideBytes + relX);

                FillBlock8(buffer, start, rowStrideBytes, rect.Width, rect.Height, index);
            }
        }

        // NXBasics::CBitmap::Draw_Box(NXBasics::SRectangle, unsigned short) const
        internal void Draw_Box(ref SRectangle rect, ushort value)
        {
            rect.Validate();

            if (_bpp != 0x10)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel))
            {
                return;
            }

            if (bytesPerPixel != 2)
            {
                return;
            }

            if (!rect.IsTouching(_rect))
            {
                return;
            }

            rect.CutInside(_rect);

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            int relX = rect.X - _rect.X;
            int relY = rect.Y - _rect.Y;

            int rowStrideBytes = checked(pitchPixels * 2);
            int start = checked(baseOffsetBytes + relY * rowStrideBytes + relX * 2);

            FillBlock16(buffer, start, rowStrideBytes, rect.Width, rect.Height, value);
        }

        // NXBasics::CBitmap::Draw_Box(NXBasics::SRectangle, unsigned int) const
        internal void Draw_Box(ref SRectangle rect, uint value)
        {
            rect.Validate();

            if (_bpp != 0x20)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel))
            {
                return;
            }

            if (bytesPerPixel != 4)
            {
                return;
            }

            if (!rect.IsTouching(_rect))
            {
                return;
            }

            rect.CutInside(_rect);

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            int relX = rect.X - _rect.X;
            int relY = rect.Y - _rect.Y;

            int rowStrideBytes = checked(pitchPixels * 4);
            int start = checked(baseOffsetBytes + relY * rowStrideBytes + relX * 4);

            FillBlock32(buffer, start, rowStrideBytes, rect.Width, rect.Height, value);
        }

        // NXBasics::CBitmap::Draw_Box(NXBasics::SRectangle const&, NXBasics::SColorRGB const&) const
        internal void Draw_Box(in SRectangle input, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                SRectangle r = input;
                uint v = CXBSystemManager.sTrueColorCreatorPtr.GetTrueColorWord(in color);
                Draw_Box(ref r, v);
                return;
            }

            if (_bpp == 0x10)
            {
                SRectangle r = input;
                ushort v = CXBSystemManager.sHighColorCreatorPtr.GetHighColorWord(color.R, color.G, color.B);
                Draw_Box(ref r, v);
                return;
            }

            if (_bpp == 0x08)
            {
                SRectangle r = input;

                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                byte idx = pal.FindMatchingColor(color.R, color.G, color.B);

                Draw_Box(ref r, idx);
            }
        }
        #endregion

        #region NXBasics::CBitmap::Draw_Rectangle()
        // NXBasics::CBitmap::Draw_Rectangle(NXBasics::SRectangle, unsigned char) const
        internal void Draw_Rectangle(ref SRectangle rect, byte index)
        {
            rect.Validate();

            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (!rect.IsTouching(_rect))
            {
                return;
            }

            SRectangle top = new(rect.X, rect.Y, rect.Width, 1);
            Draw_Box(ref top, index);

            SRectangle left = new(rect.X, rect.Y, 1, rect.Height);
            Draw_Box(ref left, index);

            SRectangle bottom = new(rect.X, rect.Y + rect.Height - 1, rect.Width, 1);
            Draw_Box(ref bottom, index);

            SRectangle right = new(rect.X + rect.Width - 1, rect.Y, 1, rect.Height);
            Draw_Box(ref right, index);
        }

        // NXBasics::CBitmap::Draw_Rectangle(NXBasics::SRectangle const&, NXBasics::SColorRGB const&) const
        internal void Draw_Rectangle(in SRectangle rect, in SColorRGB color)
        {
            SRectangle r = new(in rect);

            if (_bpp == 0x20)
            {
                CTrueColorCreator trueColorCreator = CXBSystemManager.sTrueColorCreatorPtr;
                if (trueColorCreator == null || !trueColorCreator.IsEnabled)
                {
                    return;
                }

                uint v = trueColorCreator.GetTrueColorWord(in color);
                Draw_Rectangle(ref r, v);
                return;
            }

            if (_bpp == 0x10)
            {
                CHighColorCreator highColorCreator = CXBSystemManager.sHighColorCreatorPtr;
                if (highColorCreator == null || !highColorCreator.IsEnabled)
                {
                    return;
                }

                ushort v = highColorCreator.GetHighColorWord(color.R, color.G, color.B);
                Draw_Rectangle(ref r, v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                byte idx = pal.FindMatchingColor(color);
                Draw_Rectangle(ref r, idx);
            }
        }

        // NXBasics::CBitmap::Draw_Rectangle(NXBasics::SRectangle, unsigned short) const
        internal void Draw_Rectangle(ref SRectangle rect, ushort value)
        {
            rect.Validate();

            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (!rect.IsTouching(_rect))
            {
                return;
            }

            Draw_HorizontalLine(rect.X, rect.Y, (uint)rect.Width, value);
            Draw_VerticalLine(rect.X, rect.Y, (uint)rect.Height, value);
            Draw_HorizontalLine(rect.X, rect.Y + rect.Height - 1, (uint)rect.Width, value);
            Draw_VerticalLine(rect.X + rect.Width - 1, rect.Y, (uint)rect.Height, value);
        }

        // NXBasics::CBitmap::Draw_Rectangle(NXBasics::SRectangle, unsigned int) const
        internal void Draw_Rectangle(ref SRectangle rect, uint value)
        {
            rect.Validate();

            if (_bpp != 0x20)
            {
                return;
            }

            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (!rect.IsTouching(_rect))
            {
                return;
            }

            Draw_HorizontalLine(rect.X, rect.Y, (uint)rect.Width, value);
            Draw_VerticalLine(rect.X, rect.Y, (uint)rect.Height, value);
            Draw_HorizontalLine(rect.X, rect.Y + rect.Height - 1, (uint)rect.Width, value);
            Draw_VerticalLine(rect.X + rect.Width - 1, rect.Y, (uint)rect.Height, value);
        }
        #endregion

        #region FillBlock8/16/32
        private static void FillBlock8(byte[] buffer, int startIndex, int rowStrideBytes, int widthPixels, int heightPixels, byte value)
        {
            if (widthPixels <= 0 || heightPixels <= 0)
            {
                return;
            }

            int rowBytes = widthPixels;

            for (int y = 0; y < heightPixels; y++)
            {
                int rowStart = checked(startIndex + y * rowStrideBytes);
                if ((uint)rowStart >= (uint)buffer.Length)
                {
                    continue;
                }

                int count = rowBytes;
                int avail = buffer.Length - rowStart;
                if (count > avail)
                {
                    count = avail;
                }

                for (int i = 0; i < count; i++)
                {
                    buffer[rowStart + i] = value;
                }
            }
        }

        private static void FillBlock16(byte[] buffer, int startIndex, int rowStrideBytes, int widthPixels, int heightPixels, ushort value)
        {
            if (widthPixels <= 0 || heightPixels <= 0)
            {
                return;
            }

            byte lo = (byte)(value & 0xFF);
            byte hi = (byte)((value >> 8) & 0xFF);

            int rowBytes = checked(widthPixels * 2);

            for (int y = 0; y < heightPixels; y++)
            {
                int rowStart = checked(startIndex + y * rowStrideBytes);
                if ((uint)rowStart >= (uint)buffer.Length)
                {
                    continue;
                }

                int count = rowBytes;
                int avail = buffer.Length - rowStart;
                if (count > avail)
                {
                    count = avail;
                }

                // ensure even count for 16-bit writes
                count = count & ~1;

                for (int i = 0; i < count; i += 2)
                {
                    buffer[rowStart + i + 0] = lo;
                    buffer[rowStart + i + 1] = hi;
                }
            }
        }

        private static void FillBlock32(byte[] buffer, int startIndex, int rowStrideBytes, int widthPixels, int heightPixels, uint value)
        {
            if (widthPixels <= 0 || heightPixels <= 0)
            {
                return;
            }

            byte b0 = (byte)(value & 0xFF);
            byte b1 = (byte)((value >> 8) & 0xFF);
            byte b2 = (byte)((value >> 16) & 0xFF);
            byte b3 = (byte)((value >> 24) & 0xFF);

            int rowBytes = checked(widthPixels * 4);

            for (int y = 0; y < heightPixels; y++)
            {
                int rowStart = checked(startIndex + y * rowStrideBytes);
                if ((uint)rowStart >= (uint)buffer.Length)
                {
                    continue;
                }

                int count = rowBytes;
                int avail = buffer.Length - rowStart;
                if (count > avail)
                {
                    count = avail;
                }

                // ensure multiple of 4
                count = count & ~3;

                for (int i = 0; i < count; i += 4)
                {
                    buffer[rowStart + i + 0] = b0;
                    buffer[rowStart + i + 1] = b1;
                    buffer[rowStart + i + 2] = b2;
                    buffer[rowStart + i + 3] = b3;
                }
            }
        }
        #endregion

        #region NXBasics::CBitmap::Draw_...()

        // NXBasics::CBitmap::Draw_HorizontalLine(int, int, unsigned int, unsigned char) const
        internal void Draw_HorizontalLine(int x, int y, uint width, byte index)
        {
            SRectangle tmp = new(x, y, checked((int)width), 1);
            Draw_Box(ref tmp, index);
        }

        // NXBasics::CBitmap::Draw_VerticalLine(int, int, unsigned int, unsigned char) const
        internal void Draw_VerticalLine(int x, int y, uint height, byte index)
        {
            SRectangle tmp = new(x, y, 1, checked((int)height));
            Draw_Box(ref tmp, index);
        }

        // NXBasics::CBitmap::Draw_HorizontalLine(int, int, unsigned int, unsigned short) const
        internal void Draw_HorizontalLine(int x, int y, uint width, ushort value)
        {
            if (_bpp != 0x10)
            {
                return;
            }

            SRectangle tmp = new(x, y, checked((int)width), 1);
            if (!TryPrepareDrawRect(ref tmp))
            {
                return;
            }

            Draw_Box(ref tmp, value);
        }

        // NXBasics::CBitmap::Draw_VerticalLine(int, int, unsigned int, unsigned short) const
        internal void Draw_VerticalLine(int x, int y, uint height, ushort value)
        {
            if (_bpp != 0x10)
            {
                return;
            }

            SRectangle tmp = new(x, y, 1, checked((int)height));
            if (!TryPrepareDrawRect(ref tmp))
            {
                return;
            }

            Draw_Box(ref tmp, value);
        }

        // NXBasics::CBitmap::Draw_HorizontalLine(int, int, unsigned int, unsigned int) const
        internal void Draw_HorizontalLine(int x, int y, uint width, uint value)
        {
            if (_bpp != 0x20)
            {
                return;
            }

            SRectangle tmp = new(x, y, checked((int)width), 1);
            if (!TryPrepareDrawRect(ref tmp))
            {
                return;
            }

            Draw_Box(ref tmp, value);
        }

        // NXBasics::CBitmap::Draw_VerticalLine(int, int, unsigned int, unsigned int) const
        internal void Draw_VerticalLine(int x, int y, uint height, uint value)
        {
            if (_bpp != 0x20)
            {
                return;
            }

            SRectangle tmp = new(x, y, 1, checked((int)height));
            if (!TryPrepareDrawRect(ref tmp))
            {
                return;
            }

            Draw_Box(ref tmp, value);
        }

        // NXBasics::CBitmap::Draw_StippleBox(NXBasics::SRectangle, unsigned char) const
        internal void Draw_StippleBox(ref SRectangle rect, byte index)
        {
            if (!TryPrepareDrawRectAndClip(ref rect))
            {
                return;
            }

            int x0 = rect.X;
            int y0 = rect.Y;
            int xEndExclusive = x0 + rect.Width;
            int yEndExclusive = y0 + rect.Height;

            byte parity = (byte)(x0 & 1);

            if (_bpp == 0x20)
            {
                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                uint color = palette.GetTrueColorWord(index);

                DrawStippleCore(x0, y0, xEndExclusive, yEndExclusive, parity, color);
                return;
            }

            if (_bpp == 0x10)
            {
                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                ushort color = palette.GetHighColorWord(index);

                DrawStippleCore(x0, y0, xEndExclusive, yEndExclusive, parity, color);
                return;
            }

            if (_bpp == 0x08)
            {
                DrawStippleCore(x0, y0, xEndExclusive, yEndExclusive, parity, index);
            }
        }

        // NXBasics::CBitmap::Draw_HorizontalLine(int, int, unsigned int, NXBasics::SColorRGB const&) const
        internal void Draw_HorizontalLine(int x, int y, uint width, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                uint v = CXBSystemManager.sTrueColorCreatorPtr.GetTrueColorWord(in color);
                Draw_HorizontalLine(x, y, width, v);
                return;
            }

            if (_bpp == 0x10)
            {
                ushort v = CXBSystemManager.sHighColorCreatorPtr.GetHighColorWord(color.R, color.G, color.B);
                Draw_HorizontalLine(x, y, width, v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                byte idx = palette.FindMatchingColor(color);
                Draw_HorizontalLine(x, y, width, idx);
            }
        }

        // NXBasics::CBitmap::Draw_VerticalLine(int, int, unsigned int, NXBasics::SColorRGB const&) const
        internal void Draw_VerticalLine(int x, int y, uint height, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                uint v = CXBSystemManager.sTrueColorCreatorPtr.GetTrueColorWord(in color);
                Draw_VerticalLine(x, y, height, v);
                return;
            }

            if (_bpp == 0x10)
            {
                ushort v = CXBSystemManager.sHighColorCreatorPtr.GetHighColorWord(color.R, color.G, color.B);
                Draw_VerticalLine(x, y, height, v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                byte idx = palette.FindMatchingColor(color);
                Draw_VerticalLine(x, y, height, idx);
            }
        }

        // NXBasics::CBitmap::Draw_Line(int, int, int, int, unsigned char) const
        internal void Draw_Line(int x0, int y0, int x1, int y1, byte index)
        {
            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (!IsLinePossiblyVisible(x0, y0, x1, y1))
            {
                return;
            }

            DrawLineCore(x0, y0, x1, y1, index);
        }

        // NXBasics::CBitmap::Draw_Line(int, int, int, int, unsigned short) const
        internal void Draw_Line(int x0, int y0, int x1, int y1, ushort value)
        {
            if (_bpp != 0x10)
            {
                return;
            }

            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (!IsLinePossiblyVisible(x0, y0, x1, y1))
            {
                return;
            }

            DrawLineCore(x0, y0, x1, y1, value);
        }

        // NXBasics::CBitmap::Draw_Line(int, int, int, int, unsigned int) const
        internal void Draw_Line(int x0, int y0, int x1, int y1, uint value)
        {
            if (_bpp != 0x20)
            {
                return;
            }

            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (!IsLinePossiblyVisible(x0, y0, x1, y1))
            {
                return;
            }

            DrawLineCore(x0, y0, x1, y1, value);
        }

        // NXBasics::CBitmap::Draw_Line(int, int, int, int, NXBasics::SColorRGB const&) const
        internal void Draw_Line(int x0, int y0, int x1, int y1, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                uint v = CXBSystemManager.sTrueColorCreatorPtr.GetTrueColorWord(in color);
                Draw_Line(x0, y0, x1, y1, v);
                return;
            }

            if (_bpp == 0x10)
            {
                ushort v = CXBSystemManager.sHighColorCreatorPtr.GetHighColorWord(color.R, color.G, color.B);
                Draw_Line(x0, y0, x1, y1, v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                byte idx = palette.FindMatchingColor(color);
                Draw_Line(x0, y0, x1, y1, idx);
            }
        }

        // ------------------------
        // Helpers (refactor only)
        // ------------------------

        private bool TryPrepareDrawRect(ref SRectangle rect)
        {
            rect.Validate();

            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return false;
            }

            if (!rect.IsTouching(_rect))
            {
                return false;
            }

            return true;
        }

        private bool TryPrepareDrawRectAndClip(ref SRectangle rect)
        {
            rect.Validate();

            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return false;
            }

            if (!rect.IsTouching(_rect))
            {
                return false;
            }

            rect.CutInside(_rect);

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return false;
            }

            return true;
        }

        private bool IsLinePossiblyVisible(int x0, int y0, int x1, int y1)
        {
            if (!((-1 < x0) || (-1 < x1)) || !((-1 < y0) || (-1 < y1)))
            {
                return false;
            }

            int maxX = _rect.X + _rect.Width - 1;
            int maxY = _rect.Y + _rect.Height - 1;

            if (!((x0 <= maxX) || (x1 <= maxX)) || !((y0 <= maxY) || (y1 <= maxY)))
            {
                return false;
            }

            return true;
        }

        private void DrawLineCore(int x0, int y0, int x1, int y1, byte index)
        {
            int dx = x1 - x0;
            int dy = y1 - y0;

            int stepX = (dx != 0) ? ((dx > 0) ? 1 : -1) : 0;
            int stepY = (dy != 0) ? ((dy > 0) ? 1 : -1) : 0;

            uint absDx = (uint)((dx < 0) ? -dx : dx);
            uint absDy = (uint)((dy < 0) ? -dy : dy);

            int twiceAbsDx = (int)(absDx * 2);
            int twiceAbsDy = (int)(absDy * 2);

            Draw_SetPixel(x0, y0, index);

            if (absDx < absDy)
            {
                int err = twiceAbsDx - (int)absDy;

                while (y1 != y0)
                {
                    int sub = (-1 < err) ? twiceAbsDy : 0;
                    int addX = (-1 < err) ? stepX : 0;

                    x0 += addX;
                    y0 += stepY;

                    err = (err + twiceAbsDx) - sub;

                    Draw_SetPixel(x0, y0, index);
                }
            }
            else
            {
                int err = twiceAbsDy - (int)absDx;

                while (x1 != x0)
                {
                    int sub = (-1 < err) ? twiceAbsDx : 0;
                    int addY = (-1 < err) ? stepY : 0;

                    y0 += addY;
                    x0 += stepX;

                    err = (err + twiceAbsDy) - sub;

                    Draw_SetPixel(x0, y0, index);
                }
            }
        }

        private void DrawLineCore(int x0, int y0, int x1, int y1, ushort value)
        {
            int dx = x1 - x0;
            int dy = y1 - y0;

            int stepX = (dx != 0) ? ((dx > 0) ? 1 : -1) : 0;
            int stepY = (dy != 0) ? ((dy > 0) ? 1 : -1) : 0;

            int absDx = (dx < 0) ? -dx : dx;
            int absDy = (dy < 0) ? -dy : dy;

            int twiceAbsDx = absDx * 2;
            int twiceAbsDy = absDy * 2;

            if (absDx < absDy)
            {
                int err = twiceAbsDx - absDy;

                while (true)
                {
                    if (IsInsideRect(x0, y0))
                    {
                        Draw_SetPixel(x0, y0, value);
                    }

                    if (y1 == y0)
                    {
                        return;
                    }

                    int sub = (-1 < err) ? twiceAbsDy : 0;
                    int addX = (-1 < err) ? stepX : 0;

                    x0 += addX;
                    y0 += stepY;

                    err = (err + twiceAbsDx) - sub;
                }
            }
            else
            {
                int err = twiceAbsDy - absDx;

                while (true)
                {
                    if (IsInsideRect(x0, y0))
                    {
                        Draw_SetPixel(x0, y0, value);
                    }

                    if (x1 == x0)
                    {
                        return;
                    }

                    int sub = (-1 < err) ? twiceAbsDx : 0;
                    int addY = (-1 < err) ? stepY : 0;

                    y0 += addY;
                    x0 += stepX;

                    err = (err + twiceAbsDy) - sub;
                }
            }
        }

        private void DrawLineCore(int x0, int y0, int x1, int y1, uint value)
        {
            int dx = x1 - x0;
            int dy = y1 - y0;

            int stepX = (dx != 0) ? ((dx > 0) ? 1 : -1) : 0;
            int stepY = (dy != 0) ? ((dy > 0) ? 1 : -1) : 0;

            int absDx = (dx < 0) ? -dx : dx;
            int absDy = (dy < 0) ? -dy : dy;

            int twiceAbsDx = absDx * 2;
            int twiceAbsDy = absDy * 2;

            if (absDx < absDy)
            {
                int err = twiceAbsDx - absDy;

                while (true)
                {
                    if (IsInsideRect(x0, y0))
                    {
                        Draw_SetPixel(x0, y0, value);
                    }

                    if (y1 == y0)
                    {
                        return;
                    }

                    int sub = (-1 < err) ? twiceAbsDy : 0;
                    int addX = (-1 < err) ? stepX : 0;

                    x0 += addX;
                    y0 += stepY;

                    err = (err + twiceAbsDx) - sub;
                }
            }
            else
            {
                int err = twiceAbsDy - absDx;

                while (true)
                {
                    if (IsInsideRect(x0, y0))
                    {
                        Draw_SetPixel(x0, y0, value);
                    }

                    if (x1 == x0)
                    {
                        return;
                    }

                    int sub = (-1 < err) ? twiceAbsDx : 0;
                    int addY = (-1 < err) ? stepY : 0;

                    y0 += addY;
                    x0 += stepX;

                    err = (err + twiceAbsDy) - sub;
                }
            }
        }

        private bool IsInsideRect(int x, int y)
        {
            if (x < _rect.X || y < _rect.Y)
            {
                return false;
            }

            if (x > (_rect.X + _rect.Width - 1))
            {
                return false;
            }

            if (y > (_rect.Y + _rect.Height - 1))
            {
                return false;
            }

            return true;
        }

        private void DrawStippleCore(int x0, int y0, int xEndExclusive, int yEndExclusive, byte parity, byte index)
        {
            for (int y = y0; y < yEndExclusive; y++)
            {
                int x = x0;

                if (((xEndExclusive - x) & 1) != 0)
                {
                    if (((x & 1) == 0) == (parity == 0))
                    {
                        Draw_SetPixel(x, y, index);
                    }

                    x++;
                }

                while (x != xEndExclusive)
                {
                    if (((x & 1) == 0) == (parity == 0))
                    {
                        Draw_SetPixel(x, y, index);
                    }
                    else
                    {
                        Draw_SetPixel(x + 1, y, index);
                    }

                    x += 2;
                }

                parity ^= 1;
            }
        }

        private void DrawStippleCore(int x0, int y0, int xEndExclusive, int yEndExclusive, byte parity, ushort value)
        {
            for (int y = y0; y < yEndExclusive; y++)
            {
                int x = x0;

                if (((xEndExclusive - x) & 1) != 0)
                {
                    if (((x & 1) == 0) == (parity == 0))
                    {
                        Draw_SetPixel(x, y, value);
                    }

                    x++;
                }

                while (x != xEndExclusive)
                {
                    if (((x & 1) == 0) == (parity == 0))
                    {
                        Draw_SetPixel(x, y, value);
                    }
                    else
                    {
                        Draw_SetPixel(x + 1, y, value);
                    }

                    x += 2;
                }

                parity ^= 1;
            }
        }

        private void DrawStippleCore(int x0, int y0, int xEndExclusive, int yEndExclusive, byte parity, uint value)
        {
            for (int y = y0; y < yEndExclusive; y++)
            {
                int x = x0;

                if (((xEndExclusive - x) & 1) != 0)
                {
                    if (((x & 1) == 0) == (parity == 0))
                    {
                        Draw_SetPixel(x, y, value);
                    }

                    x++;
                }

                while (x != xEndExclusive)
                {
                    if (((x & 1) == 0) == (parity == 0))
                    {
                        Draw_SetPixel(x, y, value);
                    }
                    else
                    {
                        Draw_SetPixel(x + 1, y, value);
                    }

                    x += 2;
                }

                parity ^= 1;
            }
        }

        #endregion

        #region NXBasics::CBitmap::Draw_StippledLine()
        // NXBasics::CBitmap::Draw_StippledLine(int, int, int, int, unsigned char, unsigned int) const
        internal void Draw_StippledLine(int x0, int y0, int x1, int y1, byte index, uint period)
        {
            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (!((-1 < x0) || (-1 < x1)) || !((-1 < y0) || (-1 < y1)))
            {
                return;
            }

            int clipRight = _rect.X + _rect.Width - 1;
            int clipBottom = _rect.Y + _rect.Height - 1;

            if (!((x0 <= clipRight) || (x1 <= clipRight)) || !((y0 <= clipBottom) || (y1 <= clipBottom)))
            {
                return;
            }

            int dy = y1 - y0;
            int stepY = (dy != 0) ? ((0 < dy) ? 1 : -1) : 0;

            int dx = x1 - x0;
            int stepX = (dx != 0) ? ((0 < dx) ? 1 : -1) : 0;

            uint absDx = (uint)((dx < 0) ? -dx : dx);
            uint absDy = (uint)((dy < 0) ? -dy : dy);

            int twiceAbsDx = (int)(absDx * 2);
            int twiceAbsDy = (int)(absDy * 2);

            uint counter = 0;
            bool skip = false;

            if (period == 0)
            {
                skip = false;
            }

            if (absDx < absDy)
            {
                int err = twiceAbsDx - (int)absDy;

                while (true)
                {
                    if (!skip)
                    {
                        Draw_SetPixel(x0, y0, index);
                    }

                    if (y1 == y0)
                    {
                        break;
                    }

                    if (period != 0)
                    {
                        counter++;
                        if (counter == period)
                        {
                            counter = 0;
                            skip = !skip;
                        }
                    }

                    int sub = 0;
                    if (-1 < err) sub = twiceAbsDy;

                    int addX = 0;
                    if (-1 < err) addX = stepX;

                    x0 += addX;
                    y0 += stepY;
                    err = (err + twiceAbsDx) - sub;
                }
            }
            else
            {
                int err = twiceAbsDy - (int)absDx;

                while (true)
                {
                    if (!skip)
                    {
                        Draw_SetPixel(x0, y0, index);
                    }

                    if (x1 == x0)
                    {
                        break;
                    }

                    if (period != 0)
                    {
                        counter++;
                        if (counter == period)
                        {
                            counter = 0;
                            skip = !skip;
                        }
                    }

                    int sub = 0;
                    if (-1 < err) sub = twiceAbsDx;

                    int addY = 0;
                    if (-1 < err) addY = stepY;

                    y0 += addY;
                    x0 += stepX;
                    err = (err + twiceAbsDy) - sub;
                }
            }
        }

        // NXBasics::CBitmap::Draw_StippledLine(int, int, int, int, unsigned short, unsigned int) const
        internal void Draw_StippledLine(int x0, int y0, int x1, int y1, ushort value, uint period)
        {
            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (!((-1 < x0) || (-1 < x1)) || !((-1 < y0) || (-1 < y1)))
            {
                return;
            }

            int clipRight = _rect.X + _rect.Width - 1;
            int clipBottom = _rect.Y + _rect.Height - 1;

            if (!((x0 <= clipRight) || (x1 <= clipRight)) || !((y0 <= clipBottom) || (y1 <= clipBottom)))
            {
                return;
            }

            int dy = y1 - y0;
            int stepY = (dy != 0) ? ((0 < dy) ? 1 : -1) : 0;

            int dx = x1 - x0;
            int stepX = (dx != 0) ? ((0 < dx) ? 1 : -1) : 0;

            int absDx = (dx < 0) ? -dx : dx;
            int absDy = (dy < 0) ? -dy : dy;

            int twiceAbsDx = absDx * 2;
            int twiceAbsDy = absDy * 2;

            uint counter = 0;
            bool skip = false;

            if (period == 0)
            {
                skip = false;
            }

            if (absDx < absDy)
            {
                int err = twiceAbsDx - absDy;

                while (true)
                {
                    if (!skip)
                    {
                        if ((_rect.X <= x0) && (x0 <= clipRight) && (_rect.Y <= y0) && (y0 <= clipBottom) && (_bpp == 0x10))
                        {
                            Draw_SetPixel(x0, y0, value);
                        }
                    }

                    if (y1 == y0)
                    {
                        break;
                    }

                    if (period != 0)
                    {
                        counter++;
                        if (counter == period)
                        {
                            counter = 0;
                            skip = !skip;
                        }
                    }

                    int sub = 0;
                    if (-1 < err) sub = twiceAbsDy;

                    int addX = 0;
                    if (-1 < err) addX = stepX;

                    x0 += addX;
                    y0 += stepY;
                    err = (err + twiceAbsDx) - sub;
                }

                return;
            }
            else
            {
                int err = twiceAbsDy - absDx;
                long x = x0;

                while (true)
                {
                    if (!skip)
                    {
                        if ((_rect.X <= x) && (x <= clipRight) && (_rect.Y <= y0) && (y0 <= clipBottom) && (_bpp == 0x10))
                        {
                            Draw_SetPixel((int)x, y0, value);
                        }
                    }

                    if (x1 == x)
                    {
                        break;
                    }

                    if (period != 0)
                    {
                        counter++;
                        if (counter == period)
                        {
                            counter = 0;
                            skip = !skip;
                        }
                    }

                    int sub = 0;
                    if (-1 < err) sub = twiceAbsDx;

                    int addY = 0;
                    if (-1 < err) addY = stepY;

                    y0 += addY;
                    x += stepX;
                    err = (err + twiceAbsDy) - sub;
                }
            }
        }

        // NXBasics::CBitmap::Draw_StippledLine(int, int, int, int, unsigned int, unsigned int) const
        internal void Draw_StippledLine(int x0, int y0, int x1, int y1, uint value, uint period)
        {
            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (!((-1 < x0) || (-1 < x1)) || !((-1 < y0) || (-1 < y1)))
            {
                return;
            }

            int clipRight = _rect.X + _rect.Width - 1;
            int clipBottom = _rect.Y + _rect.Height - 1;

            if (!((x0 <= clipRight) || (x1 <= clipRight)) || !((y0 <= clipBottom) || (y1 <= clipBottom)))
            {
                return;
            }

            int dy = y1 - y0;
            int stepY = (dy != 0) ? ((0 < dy) ? 1 : -1) : 0;

            int dx = x1 - x0;
            int stepX = (dx != 0) ? ((0 < dx) ? 1 : -1) : 0;

            int absDx = (dx < 0) ? -dx : dx;
            int absDy = (dy < 0) ? -dy : dy;

            int twiceAbsDx = absDx * 2;
            int twiceAbsDy = absDy * 2;

            uint counter = 0;
            bool skip = false;

            if (period == 0)
            {
                skip = false;
            }

            if (absDx < absDy)
            {
                int err = twiceAbsDx - absDy;

                while (true)
                {
                    if (!skip)
                    {
                        if ((_rect.X <= x0) && (x0 <= clipRight) && (_rect.Y <= y0) && (y0 <= clipBottom) && (_bpp == 0x20))
                        {
                            Draw_SetPixel(x0, y0, value);
                        }
                    }

                    if (y1 == y0)
                    {
                        break;
                    }

                    if (period != 0)
                    {
                        counter++;
                        if (counter == period)
                        {
                            counter = 0;
                            skip = !skip;
                        }
                    }

                    int sub = 0;
                    if (-1 < err) sub = twiceAbsDy;

                    int addX = 0;
                    if (-1 < err) addX = stepX;

                    x0 += addX;
                    y0 += stepY;
                    err = (err + twiceAbsDx) - sub;
                }

                return;
            }
            else
            {
                int err = twiceAbsDy - absDx;
                long x = x0;

                while (true)
                {
                    if (!skip)
                    {
                        if ((_rect.X <= x) && (x <= clipRight) && (_rect.Y <= y0) && (y0 <= clipBottom) && (_bpp == 0x20))
                        {
                            Draw_SetPixel((int)x, y0, value);
                        }
                    }

                    if (x1 == x)
                    {
                        break;
                    }

                    if (period != 0)
                    {
                        counter++;
                        if (counter == period)
                        {
                            counter = 0;
                            skip = !skip;
                        }
                    }

                    int sub = 0;
                    if (-1 < err) sub = twiceAbsDx;

                    int addY = 0;
                    if (-1 < err) addY = stepY;

                    y0 += addY;
                    x += stepX;
                    err = (err + twiceAbsDy) - sub;
                }
            }
        }

        // NXBasics::CBitmap::Draw_StippledLine(int, int, int, int, NXBasics::SColorRGB const&, unsigned int) const
        internal void Draw_StippledLine(int x0, int y0, int x1, int y1, in SColorRGB color, uint period)
        {
            if (_bpp == 0x20)
            {
                uint v = CXBSystemManager.sTrueColorCreatorPtr.GetTrueColorWord(in color);
                Draw_StippledLine(x0, y0, x1, y1, v, period);
                return;
            }

            if (_bpp == 0x10)
            {
                ushort v = CXBSystemManager.sHighColorCreatorPtr.GetHighColorWord(color.R, color.G, color.B);
                Draw_StippledLine(x0, y0, x1, y1, v, period);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                byte idx = palette.FindMatchingColor(color);
                Draw_StippledLine(x0, y0, x1, y1, idx, period);
            }
        }
        #endregion

        #region NXBasics::CBitmap::Draw_Circle...()
        // NXBasics::CBitmap::Draw_Circle(int, int, unsigned int, unsigned char) const
        internal void Draw_Circle(int cx, int cy, uint radius, byte index)
        {
            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (radius > int.MaxValue)
            {
                return;
            }

            int r = (int)radius;
            if (r < 0)
            {
                return;
            }

            int d = 3 - (2 * r);
            int x = 0;
            int y = r;

            while (x <= y)
            {
                Draw_SetPixel(cx + x, cy + y, index);
                Draw_SetPixel(cx - x, cy + y, index);
                Draw_SetPixel(cx + x, cy - y, index);
                Draw_SetPixel(cx - x, cy - y, index);

                Draw_SetPixel(cx + y, cy + x, index);
                Draw_SetPixel(cx - y, cy + x, index);
                Draw_SetPixel(cx + y, cy - x, index);
                Draw_SetPixel(cx - y, cy - x, index);

                if (d < 0)
                {
                    d += (4 * x) + 6;
                }
                else
                {
                    d += (4 * (x - y)) + 10;
                    y--;
                }

                x++;
            }
        }

        // NXBasics::CBitmap::Draw_Circle(int, int, unsigned int, unsigned short) const
        internal void Draw_Circle(int cx, int cy, uint radius, ushort value)
        {
            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (radius > int.MaxValue)
            {
                return;
            }

            int r = (int)radius;
            if (r < 0)
            {
                return;
            }

            int d = 3 - (2 * r);
            int x = 0;
            int y = r;

            while (x <= y)
            {
                Draw_SetPixel(cx + x, cy + y, value);
                Draw_SetPixel(cx - x, cy + y, value);
                Draw_SetPixel(cx + x, cy - y, value);
                Draw_SetPixel(cx - x, cy - y, value);

                Draw_SetPixel(cx + y, cy + x, value);
                Draw_SetPixel(cx - y, cy + x, value);
                Draw_SetPixel(cx + y, cy - x, value);
                Draw_SetPixel(cx - y, cy - x, value);

                if (d < 0)
                {
                    d += (4 * x) + 6;
                }
                else
                {
                    d += (4 * (x - y)) + 10;
                    y--;
                }

                x++;
            }
        }

        // NXBasics::CBitmap::Draw_Circle(int, int, unsigned int, unsigned int) const
        internal void Draw_Circle(int cx, int cy, uint radius, uint value)
        {
            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (radius > int.MaxValue)
            {
                return;
            }

            int r = (int)radius;
            if (r < 0)
            {
                return;
            }

            int d = 3 - (2 * r);
            int x = 0;
            int y = r;

            while (x <= y)
            {
                Draw_SetPixel(cx + x, cy + y, value);
                Draw_SetPixel(cx - x, cy + y, value);
                Draw_SetPixel(cx + x, cy - y, value);
                Draw_SetPixel(cx - x, cy - y, value);

                Draw_SetPixel(cx + y, cy + x, value);
                Draw_SetPixel(cx - y, cy + x, value);
                Draw_SetPixel(cx + y, cy - x, value);
                Draw_SetPixel(cx - y, cy - x, value);

                if (d < 0)
                {
                    d += (4 * x) + 6;
                }
                else
                {
                    d += (4 * (x - y)) + 10;
                    y--;
                }

                x++;
            }
        }

        // NXBasics::CBitmap::Draw_Circle(int, int, unsigned int, NXBasics::SColorRGB const&) const
        internal void Draw_Circle(int cx, int cy, uint radius, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                uint v = CXBSystemManager.sTrueColorCreatorPtr.GetTrueColorWord(in color);
                Draw_Circle(cx, cy, radius, v);
                return;
            }

            if (_bpp == 0x10)
            {
                ushort v = CXBSystemManager.sHighColorCreatorPtr.GetHighColorWord(color.R, color.G, color.B);
                Draw_Circle(cx, cy, radius, v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                byte idx = pal.FindMatchingColor(color);
                Draw_Circle(cx, cy, radius, idx);
            }
        }

        // NXBasics::CBitmap::Draw_FilledCircle(int, int, unsigned int, unsigned char) const
        internal void Draw_FilledCircle(int cx, int cy, uint radius, byte index)
        {
            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (radius > int.MaxValue)
            {
                return;
            }

            int r = (int)radius;
            if (r < 0)
            {
                return;
            }

            int d = 3 - (2 * r);
            int x = 0;
            int y = r;

            while (x <= y)
            {
                // Horizontal spans for the two Y-levels for current x/y
                SRectangle span;

                span = new SRectangle(cx - x, cy + y, x * 2 + 1, 1);
                Draw_Box(ref span, index);

                span = new SRectangle(cx - x, cy - y, x * 2 + 1, 1);
                Draw_Box(ref span, index);

                span = new SRectangle(cx - y, cy + x, y * 2 + 1, 1);
                Draw_Box(ref span, index);

                span = new SRectangle(cx - y, cy - x, y * 2 + 1, 1);
                Draw_Box(ref span, index);

                if (d < 0)
                {
                    d += (4 * x) + 6;
                }
                else
                {
                    d += (4 * (x - y)) + 10;
                    y--;
                }

                x++;
            }
        }

        // NXBasics::CBitmap::Draw_FilledCircle(int, int, unsigned int, unsigned short) const
        internal void Draw_FilledCircle(int cx, int cy, uint radius, ushort value)
        {
            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (radius > int.MaxValue)
            {
                return;
            }

            int r = (int)radius;
            if (r < 0)
            {
                return;
            }

            int d = 3 - (2 * r);
            int x = 0;
            int y = r;

            while (x <= y)
            {
                Draw_HorizontalLine(cx - x, cy + y, (uint)(x * 2 + 1), value);
                Draw_HorizontalLine(cx - x, cy - y, (uint)(x * 2 + 1), value);

                Draw_HorizontalLine(cx - y, cy + x, (uint)(y * 2 + 1), value);
                Draw_HorizontalLine(cx - y, cy - x, (uint)(y * 2 + 1), value);

                if (d < 0)
                {
                    d += (4 * x) + 6;
                }
                else
                {
                    d += (4 * (x - y)) + 10;
                    y--;
                }

                x++;
            }
        }

        // NXBasics::CBitmap::Draw_FilledCircle(int, int, unsigned int, unsigned int) const
        internal void Draw_FilledCircle(int cx, int cy, uint radius, uint value)
        {
            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (radius > int.MaxValue)
            {
                return;
            }

            int r = (int)radius;
            if (r < 0)
            {
                return;
            }

            int d = 3 - (2 * r);
            int x = 0;
            int y = r;

            while (x <= y)
            {
                Draw_HorizontalLine(cx - x, cy + y, (uint)(x * 2 + 1), value);
                Draw_HorizontalLine(cx - x, cy - y, (uint)(x * 2 + 1), value);

                Draw_HorizontalLine(cx - y, cy + x, (uint)(y * 2 + 1), value);
                Draw_HorizontalLine(cx - y, cy - x, (uint)(y * 2 + 1), value);

                if (d < 0)
                {
                    d += (4 * x) + 6;
                }
                else
                {
                    d += (4 * (x - y)) + 10;
                    y--;
                }

                x++;
            }
        }

        // NXBasics::CBitmap::Draw_FilledCircle(int, int, unsigned int, NXBasics::SColorRGB const&) const
        internal void Draw_FilledCircle(int cx, int cy, uint radius, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                uint v = CXBSystemManager.sTrueColorCreatorPtr.GetTrueColorWord(in color);
                Draw_FilledCircle(cx, cy, radius, v);
                return;
            }

            if (_bpp == 0x10)
            {
                ushort v = CXBSystemManager.sHighColorCreatorPtr.GetHighColorWord(color.R, color.G, color.B);
                Draw_FilledCircle(cx, cy, radius, v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette pal = _palette ?? CXBSystemManager.sPalettePtr;
                byte idx = pal.FindMatchingColor(color);
                Draw_FilledCircle(cx, cy, radius, idx);
            }
        }
        #endregion

        #region NXBasics::CBitmap::Text_Print()
        // NXBasics::CBitmap::Text_Print(NXBasics::CFont const*, int, int, char const*) const
        internal void Text_Print(CFont font, int x, int y, string text)
        {
            if (font == null || text == null)
            {
                return;
            }

            Text_PrintCore(font, x, y, text, static (f, bmp, ch, cx, cy) => f.PrintCharacter(bmp, (byte)ch, cx, cy));
        }

        // NXBasics::CBitmap::Text_PrintV(NXBasics::CFont const*, int, int, char const*, ...) const
        internal void Text_PrintV(CFont font, int x, int y, string format, params object[] args)
        {
            if (font == null || format == null)
            {
                return;
            }

            string composed = PrintfFormat(format, args);
            Text_Print(font, x, y, composed);
        }

        // NXBasics::CBitmap::Text_Print(NXBasics::CFont const&, NXBasics::CPalette&, int, int, char const*) const
        internal void Text_Print(CFont font, CPalette palette, int x, int y, string text)
        {
            if (font == null || palette == null || text == null)
            {
                return;
            }

            Text_PrintCore(font, x, y, text, (f, bmp, ch, cx, cy) => f.PrintCharacter(palette, bmp, (byte)ch, cx, cy));
        }

        // NXBasics::CBitmap::Text_PrintV(NXBasics::CFont const&, NXBasics::CPalette&, int, int, char const*, ...) const
        internal void Text_PrintV(CFont font, CPalette palette, int x, int y, string format, params object[] args)
        {
            if (font == null || palette == null || format == null)
            {
                return;
            }

            string composed = PrintfFormat(format, args);
            Text_Print(font, palette, x, y, composed);
        }

        // NXBasics::CBitmap::Text_Print(NXBasics::CFont const&, NXBasics::SColorRGB const&, int, int, char const*) const
        internal void Text_Print(CFont font, in SColorRGB color, int x, int y, string text)
        {
            if (font == null || text == null)
            {
                return;
            }

            SColorRGB colorCopy = color;
            Text_PrintCore(font, x, y, text, (f, bmp, ch, cx, cy) => f.PrintCharacter(colorCopy, bmp, (byte)ch, cx, cy));
        }

        // NXBasics::CBitmap::Text_PrintV(NXBasics::CFont const&, NXBasics::SColorRGB const&, int, int, char const*, ...) const
        internal void Text_PrintV(CFont font, in SColorRGB color, int x, int y, string format, params object[] args)
        {
            if (font == null || format == null)
            {
                return;
            }

            string composed = PrintfFormat(format, args);
            Text_Print(font, in color, x, y, composed);
        }

        // -------- shared core --------

        private delegate void PrintCharDelegate(CFont font, CBitmap bmp, char ch, int cx, int cy);

        private void Text_PrintCore(CFont font, int x, int y, string text, PrintCharDelegate printer)
        {
            int cursorX = x;
            int cursorY = y;

            int i = 0;
            while (i < text.Length)
            {
                char ch = text[i];

                if (ch == '\0')
                {
                    return;
                }

                if (ch == '\t' || ch == ' ' || ch == '_')
                {
                    int advance = _fixedXDistance != 0 ? _fixedXDistance : font.GetCharacterWidth((byte)'i');
                    cursorX += advance;
                    i++;
                    continue;
                }

                if (ch == '\n' || ch == '\r')
                {
                    if (ch == '\r')
                    {
                        i = ((i + 1) < text.Length && text[i + 1] == '\n') ? (i + 2) : (i + 1);
                    }
                    else
                    {
                        i = ((i + 1) < text.Length && text[i + 1] == '\r') ? (i + 2) : (i + 1);
                    }

                    cursorY += font.GetPixelHeight("i") + 2;
                    cursorX = x;
                    continue;
                }

                if (ch > (char)0x1F)
                {
                    byte ascii = ToByteOrQuestionMark(ch);

                    int charWidth = font.GetCharacterWidth(ascii);

                    if (_fixedXDistance == 0)
                    {
                        printer(font, this, ch, cursorX, cursorY);
                        cursorX += charWidth;
                    }
                    else
                    {
                        int centeredX = cursorX + ((_fixedXDistance - charWidth) / 2);
                        printer(font, this, ch, centeredX, cursorY);
                        cursorX += _fixedXDistance;
                    }
                }

                i++;
            }
        }

        private static byte ToByteOrQuestionMark(char ch)
        {
            if (ch <= (char)0xFF)
            {
                return (byte)ch;
            }

            return (byte)'?';
        }

        // -------- printf-style formatting (replaces fake DexterString.VSPrintf) --------

        private static string PrintfFormat(string format, object[] args)
        {
            if (format == null)
            {
                return string.Empty;
            }

            if (args == null || args.Length == 0)
            {
                return ReplaceDoublePercent(format);
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder(format.Length + 32);
            int argIndex = 0;

            for (int i = 0; i < format.Length; i++)
            {
                char ch = format[i];
                if (ch != '%')
                {
                    sb.Append(ch);
                    continue;
                }

                if (i + 1 < format.Length && format[i + 1] == '%')
                {
                    sb.Append('%');
                    i++;
                    continue;
                }

                bool leftAlign = false;
                bool zeroPad = false;

                int j = i + 1;
                bool flagsDone = false;
                while (!flagsDone && j < format.Length)
                {
                    char f = format[j];
                    switch (f)
                    {
                        case '-':
                            leftAlign = true;
                            j++;
                            break;
                        case '0':
                            zeroPad = true;
                            j++;
                            break;
                        default:
                            flagsDone = true;
                            break;
                    }
                }

                int width = 0;
                while (j < format.Length && format[j] >= '0' && format[j] <= '9')
                {
                    width = (width * 10) + (format[j] - '0');
                    j++;
                }

                int precision = -1;
                if (j < format.Length && format[j] == '.')
                {
                    j++;
                    precision = 0;
                    while (j < format.Length && format[j] >= '0' && format[j] <= '9')
                    {
                        precision = (precision * 10) + (format[j] - '0');
                        j++;
                    }
                }

                if (j >= format.Length)
                {
                    sb.Append('%');
                    break;
                }

                char spec = format[j];
                i = j;

                object value = argIndex < args.Length ? args[argIndex] : null;
                if (argIndex < args.Length)
                {
                    argIndex++;
                }

                string rendered = RenderPrintfValue(spec, value, precision);

                if (width > 0 && rendered.Length < width)
                {
                    int padCount = width - rendered.Length;
                    char padChar = (!leftAlign && zeroPad) ? '0' : ' ';

                    if (leftAlign)
                    {
                        sb.Append(rendered);
                        sb.Append(padChar, padCount);
                    }
                    else
                    {
                        if (padChar == '0' && rendered.Length > 0 && (rendered[0] == '+' || rendered[0] == '-') && IsNumericSpec(spec))
                        {
                            sb.Append(rendered[0]);
                            sb.Append('0', padCount);
                            sb.Append(rendered.AsSpan(1));
                        }
                        else
                        {
                            sb.Append(padChar, padCount);
                            sb.Append(rendered);
                        }
                    }
                }
                else
                {
                    sb.Append(rendered);
                }
            }

            return sb.ToString();
        }

        private static string RenderPrintfValue(char spec, object value, int precision)
        {
            switch (spec)
            {
                case 's':
                    return value?.ToString() ?? "(null)";

                case 'c':
                    if (value == null)
                    {
                        return "\0";
                    }
                    if (value is char c)
                    {
                        return c.ToString();
                    }
                    return ((char)ConvertToInt64(value)).ToString();

                case 'd':
                case 'i':
                    return ConvertToInt64(value).ToString(System.Globalization.CultureInfo.InvariantCulture);

                case 'u':
                    return ConvertToUInt64(value).ToString(System.Globalization.CultureInfo.InvariantCulture);

                case 'x':
                    return ConvertToUInt64(value).ToString("x", System.Globalization.CultureInfo.InvariantCulture);

                case 'X':
                    return ConvertToUInt64(value).ToString("X", System.Globalization.CultureInfo.InvariantCulture);

                case 'f':
                    {
                        double d = ConvertToDouble(value);
                        if (precision >= 0)
                        {
                            return d.ToString("F" + precision.ToString(System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture);
                        }
                        return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }

                default:
                    return "%" + spec;
            }
        }

        private static bool IsNumericSpec(char spec)
        {
            return spec == 'd' || spec == 'i' || spec == 'u' || spec == 'x' || spec == 'X' || spec == 'f';
        }

        private static long ConvertToInt64(object value)
        {
            if (value == null)
            {
                return 0;
            }

            if (value is sbyte sb) return sb;
            if (value is short s) return s;
            if (value is int i) return i;
            if (value is long l) return l;

            if (value is byte b) return b;
            if (value is ushort us) return us;
            if (value is uint ui) return ui;
            if (value is ulong ul) return unchecked((long)ul);

            if (value is char c) return c;

            return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static ulong ConvertToUInt64(object value)
        {
            if (value == null)
            {
                return 0;
            }

            if (value is byte b) return b;
            if (value is ushort us) return us;
            if (value is uint ui) return ui;
            if (value is ulong ul) return ul;

            if (value is sbyte sb) return unchecked((ulong)sb);
            if (value is short s) return unchecked((ulong)s);
            if (value is int i) return unchecked((ulong)i);
            if (value is long l) return unchecked((ulong)l);

            if (value is char c) return c;

            return Convert.ToUInt64(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static double ConvertToDouble(object value)
        {
            if (value == null)
            {
                return 0.0;
            }

            if (value is float f) return f;
            if (value is double d) return d;
            if (value is decimal m) return (double)m;

            return Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string ReplaceDoublePercent(string format)
        {
            if (format.IndexOf("%%", StringComparison.Ordinal) < 0)
            {
                return format;
            }

            return format.Replace("%%", "%", StringComparison.Ordinal);
        }

        #endregion

        #region NXBasics::CBitmap::Text_GetPixelWidth() Text_SetFixedXDistance()
        // NXBasics::CBitmap::Text_GetPixelWidth(NXBasics::CFont const*, char const*) const
        internal uint Text_GetPixelWidth(CFont font, string text)
        {
            if (font == null || text == null)
            {
                return 0;
            }

            int cursorX = 0;
            int maxX = 0;

            int i = 0;
            while (i < text.Length)
            {
                char ch = text[i];

                if (ch == '\0')
                {
                    break;
                }

                if (ch == '\t' || ch == ' ' || ch == '_')
                {
                    int advance = _fixedXDistance != 0 ? _fixedXDistance : font.GetCharacterWidth((byte)'i');
                    cursorX += advance;

                    if (cursorX > maxX)
                    {
                        maxX = cursorX;
                    }

                    i++;
                    continue;
                }

                if (ch == '\n' || ch == '\r')
                {
                    if (cursorX > maxX)
                    {
                        maxX = cursorX;
                    }

                    cursorX = 0;

                    if (ch == '\r')
                    {
                        i = ((i + 1) < text.Length && text[i + 1] == '\n') ? (i + 2) : (i + 1);
                    }
                    else
                    {
                        i = ((i + 1) < text.Length && text[i + 1] == '\r') ? (i + 2) : (i + 1);
                    }

                    continue;
                }

                if (ch > (char)0x1F)
                {
                    int advance = _fixedXDistance != 0 ? _fixedXDistance : font.GetCharacterWidth((byte)ch);
                    cursorX += advance;

                    if (cursorX > maxX)
                    {
                        maxX = cursorX;
                    }
                }

                i++;
            }

            if (cursorX > maxX)
            {
                maxX = cursorX;
            }

            if (maxX < 0)
            {
                return 0;
            }

            return (uint)maxX;
        }

        // NXBasics::CBitmap::Text_SetFixedXDistance(int)
        internal void Text_SetFixedXDistance(int fixedXDistance)
        {
            _fixedXDistance = fixedXDistance;
        }
        #endregion

        #region NXBasics::CBitmap::ZoomIn()
        // NXBasics::CBitmap::ZoomIn() const
        internal void ZoomIn()
        {
            ZoomIn(64);
        }

        // NXBasics::CBitmap::ZoomIn(unsigned int) const
        internal void ZoomIn(uint factor)
        {
            if (factor >= 0x100)
            {
                return;
            }

            int width = _rect.Width;
            int height = _rect.Height;

            if (width <= 1 || height <= 1)
            {
                return;
            }

            int halfWidth = width / 2;
            int halfHeight = height / 2;

            if (halfWidth <= 0 || halfHeight <= 0)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffset, out int pitchPixels, out int bytesPerPixel))
            {
                return;
            }

            int pitch = pitchPixels > 0 ? pitchPixels : width;

            // Snapshot the full plane (including pitch padding), so writes don't affect subsequent reads.
            int srcRowBytes = checked(pitch * bytesPerPixel);
            int srcSizeBytes = checked(srcRowBytes * height);

            byte[] src = new byte[srcSizeBytes];

            for (int y = 0; y < height; y++)
            {
                int srcDst = y * srcRowBytes;
                int srcSrc = checked(baseOffset + (y * srcRowBytes));
                Buffer.BlockCopy(buffer, srcSrc, src, srcDst, srcRowBytes);
            }

            int topY = 0;
            int bottomY = (halfHeight * 2) - 1;
            int yDist = halfHeight;

            while (yDist > 1)
            {
                int mappedY = (int)(((uint)yDist * factor) >> 8);
                int srcTopY = halfHeight - mappedY;
                int srcBottomY = (halfHeight + mappedY) - 1;

                int leftX = 0;
                int rightX = (halfWidth * 2) - 1;
                int xDist = halfWidth;

                uint xAcc = (uint)halfWidth * factor;

                while (xDist > 1)
                {
                    int mappedX = (int)(xAcc >> 8);
                    int srcLeftX = halfWidth - mappedX;
                    int srcRightX = (halfWidth + mappedX) - 1;

                    int dstTL = checked(baseOffset + (((topY * pitch) + leftX) * bytesPerPixel));
                    int dstTR = checked(baseOffset + (((topY * pitch) + rightX) * bytesPerPixel));
                    int dstBL = checked(baseOffset + (((bottomY * pitch) + leftX) * bytesPerPixel));
                    int dstBR = checked(baseOffset + (((bottomY * pitch) + rightX) * bytesPerPixel));

                    int srcTL = checked(((srcTopY * pitch) + srcLeftX) * bytesPerPixel);
                    int srcTR = checked(((srcTopY * pitch) + srcRightX) * bytesPerPixel);
                    int srcBL = checked(((srcBottomY * pitch) + srcLeftX) * bytesPerPixel);
                    int srcBR = checked(((srcBottomY * pitch) + srcRightX) * bytesPerPixel);

                    Buffer.BlockCopy(src, srcTL, buffer, dstTL, bytesPerPixel);
                    Buffer.BlockCopy(src, srcTR, buffer, dstTR, bytesPerPixel);
                    Buffer.BlockCopy(src, srcBL, buffer, dstBL, bytesPerPixel);
                    Buffer.BlockCopy(src, srcBR, buffer, dstBR, bytesPerPixel);

                    leftX++;
                    rightX--;
                    xDist--;
                    xAcc -= factor;
                }

                topY++;
                bottomY--;
                yDist--;
            }
        }
        #endregion

        #region NXBasics::CBitmap::GetBoundingRectangle()
        // NXBasics::CBitmap::GetBoundingRectangle(unsigned char, NXBasics::SRectangle&) const
        internal bool GetBoundingRectangle(byte transparentIndex, out SRectangle rect)
        {
            rect = new SRectangle(0, 0, 0, 0);

            if (_bpp != 0x08)
            {
                return false;
            }

            if (_pixels8 == null)
            {
                return false;
            }

            int width = _rect.Width;
            int height = _rect.Height;

            if (width <= 0 || height <= 0)
            {
                return false;
            }

            int stride = StridePixels;
            if (stride <= 0)
            {
                stride = width;
            }

            int minX = 2000;
            int minY = 2000;
            int maxX = -2000;
            int maxY = -2000;

            bool found = false;

            for (int y = 0; y < height; y++)
            {
                int rowBase = y * stride;

                for (int x = 0; x < width; x++)
                {
                    byte value = _pixels8[rowBase + x];
                    if (value == transparentIndex)
                    {
                        continue;
                    }

                    if (x <= minX) minX = x;
                    if (y <= minY) minY = y;
                    if (x >= maxX) maxX = x;
                    if (y >= maxY) maxY = y;

                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            rect = new SRectangle(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
            return true;
        }
        #endregion

        #region NXBasics::CBitmap::Remap()
        // NXBasics::CBitmap::Remap(NXBasics::CRemapTable const&, NXBasics::SRectangle) const
        internal void Remap(CRemapTable remapTable, SRectangle rect)
        {
            if (_bpp == 0x20)
            {
                if (_pixels32 == null)
                {
                    return;
                }

                if (!rect.IsTouching(in _rect))
                {
                    return;
                }

                rect.CutInside(in _rect);

                if (rect.Width <= 0 || rect.Height <= 0)
                {
                    return;
                }

                int stride = StridePixels;
                if (stride <= 0)
                {
                    stride = _rect.Width;
                }

                int yEndExclusive = rect.Y + rect.Height;
                int xEndExclusive = rect.X + rect.Width;

                for (int y = rect.Y; y < yEndExclusive; y++)
                {
                    int rowBase = y * stride;

                    for (int x = rect.X; x < xEndExclusive; x++)
                    {
                        int i = rowBase + x;
                        uint value = _pixels32[i];
                        value = (value >> 1) & 0x007F7F7FU;
                        _pixels32[i] = value;
                    }
                }

                return;
            }

            if (_bpp == 0x10)
            {
                if (_pixels16 == null)
                {
                    return;
                }

                if (!rect.IsTouching(in _rect))
                {
                    return;
                }

                rect.CutInside(in _rect);

                if (rect.Width <= 0 || rect.Height <= 0)
                {
                    return;
                }

                CHighColorCreator creator = CXBSystemManager.sHighColorCreatorPtr;
                if (creator == null)
                {
                    return;
                }

                ushort mask = creator.GetHighColorWord(0x7F, 0x7F, 0x7F);

                int stride = StridePixels;
                if (stride <= 0)
                {
                    stride = _rect.Width;
                }

                int yEndExclusive = rect.Y + rect.Height;
                int xEndExclusive = rect.X + rect.Width;

                for (int y = rect.Y; y < yEndExclusive; y++)
                {
                    int rowBase = y * stride;

                    for (int x = rect.X; x < xEndExclusive; x++)
                    {
                        int i = rowBase + x;
                        ushort value = _pixels16[i];
                        value = (ushort)(((uint)value >> 1) & mask);
                        _pixels16[i] = value;
                    }
                }

                return;
            }

            if (_bpp == 0x08)
            {
                if (_pixels8 == null)
                {
                    return;
                }

                if (remapTable == null)
                {
                    return;
                }

                byte[] table = remapTable.Table256Bytes;
                if (table == null || table.Length < 256)
                {
                    return;
                }

                if (!rect.IsTouching(in _rect))
                {
                    return;
                }

                rect.CutInside(in _rect);

                if (rect.Width <= 0 || rect.Height <= 0)
                {
                    return;
                }

                int stride = StridePixels;
                if (stride <= 0)
                {
                    stride = _rect.Width;
                }

                int yEndExclusive = rect.Y + rect.Height;
                int xEndExclusive = rect.X + rect.Width;

                for (int y = rect.Y; y < yEndExclusive; y++)
                {
                    int rowBase = y * stride;

                    for (int x = rect.X; x < xEndExclusive; x++)
                    {
                        int i = rowBase + x;
                        _pixels8[i] = table[_pixels8[i]];
                    }
                }
            }
        }

        // NXBasics::CBitmap::Remap(NXBasics::CRemapTable const&, int, int, int, int) const
        internal void Remap(CRemapTable remapTable, int x, int y, int width, int height)
        {
            SRectangle rect = new(x, y, width, height);
            Remap(remapTable, rect);
        }

        internal bool HasPixelBuffer()
        {
            return _bpp switch
            {
                (byte)BitmapFormat.Indexed8 => _pixels8 != null,
                (byte)BitmapFormat.HighColor16 => _pixels16 != null,
                (byte)BitmapFormat.TrueColor32 => _pixels32 != null,
                _ => false
            };
        }
        #endregion

        #region NXBasics::CBitmap::FilterColor() and ReplaceColor()
        // NXBasics::CBitmap::FilterColor(unsigned char, unsigned char) const
        internal void FilterColor(byte fromColor, byte toColor)
        {
            if (_bpp != (byte)BitmapFormat.Indexed8 || _pixels8 == null)
            {
                return;
            }

            int width = Width;
            int height = Height;
            int stride = StridePixels;

            for (int y = 0; y < height; y++)
            {
                int rowBase = y * stride;

                for (int x = 0; x < width; x++)
                {
                    int i = rowBase + x;

                    if (_pixels8[i] != fromColor)
                    {
                        _pixels8[i] = toColor;
                    }
                }
            }
        }

        // NXBasics::CBitmap::ReplaceColor(unsigned char, unsigned char) const
        internal void ReplaceColor(byte fromColor, byte toColor)
        {
            if (_bpp != (byte)BitmapFormat.Indexed8 || _pixels8 == null)
            {
                return;
            }

            int width = Width;
            int height = Height;
            int stride = StridePixels;

            for (int y = 0; y < height; y++)
            {
                int rowBase = y * stride;

                for (int x = 0; x < width; x++)
                {
                    int i = rowBase + x;

                    if (_pixels8[i] == fromColor)
                    {
                        _pixels8[i] = toColor;
                    }
                }
            }
        }
        #endregion

        #region NXBasics::CBitmap::Tool_Darken()
        // NXBasics::CBitmap::GetUnclippedPixelPtr(int, int) const
        internal int GetUnclippedPixelIndex(int x, int y)
        {
            return (y * StridePixels) + x;
        }

        // NXBasics::CBitmap::Tool_UseSourceAsMaskAndDarken(NXBasics::CBitmap const&, int, int, unsigned char) const
        internal void Tool_UseSourceAsMaskAndDarken(CBitmap destination, int offsetX, int offsetY, byte transparentMaskColor)
        {
            if (destination == null)
            {
                return;
            }

            if (_bpp != (byte)BitmapFormat.Indexed8)
            {
                return;
            }

            if (_pixels8 == null)
            {
                return;
            }

            if (!destination.HasPixelBuffer())
            {
                return;
            }

            SRectangle dstBounds = destination._rect;
            SRectangle srcOverDst = new(offsetX, offsetY, Width, Height);

            if (!srcOverDst.IsTouching(dstBounds))
            {
                return;
            }

            srcOverDst.CutInside(dstBounds);

            if (srcOverDst.Width <= 0 || srcOverDst.Height <= 0)
            {
                return;
            }

            int maskStartX = srcOverDst.X - offsetX;
            int maskStartY = srcOverDst.Y - offsetY;

            int maskStride = StridePixels;
            int dstStride = destination.StridePixels;

            if (destination._bpp == (byte)BitmapFormat.TrueColor32)
            {
                if (destination._pixels32 == null)
                {
                    return;
                }

                // Robust per-channel anti-bleed mask (better than hardcoding 0x007F7F7F).
                CTrueColorCreator trueColorCreator = CXBSystemManager.sTrueColorCreatorPtr;

                uint rMask = 0x00FF0000U;
                uint gMask = 0x0000FF00U;
                uint bMask = 0x000000FFU;

                if (trueColorCreator != null && trueColorCreator.IsEnabled)
                {
                    rMask = trueColorCreator.RMask;
                    gMask = trueColorCreator.GMask;
                    bMask = trueColorCreator.BMask;
                }

                uint channelMask = ((rMask >> 1) & rMask) | ((gMask >> 1) & gMask) | ((bMask >> 1) & bMask);

                for (int row = 0; row < srcOverDst.Height; row++)
                {
                    int maskRowBase = (maskStartY + row) * maskStride + maskStartX;
                    int dstRowBase = (srcOverDst.Y + row) * dstStride + srcOverDst.X;

                    for (int col = 0; col < srcOverDst.Width; col++)
                    {
                        if (_pixels8[maskRowBase + col] != transparentMaskColor)
                        {
                            uint value = destination._pixels32[dstRowBase + col];
                            destination._pixels32[dstRowBase + col] = (value >> 1) & channelMask;
                        }
                    }
                }

                return;
            }

            if (destination._bpp == (byte)BitmapFormat.HighColor16)
            {
                if (destination._pixels16 == null)
                {
                    return;
                }

                // No interface: use the concrete creator used elsewhere in your codebase.
                CHighColorCreator highColorCreator = CXBSystemManager.sHighColorCreatorPtr;

                if (highColorCreator == null || !highColorCreator.IsEnabled)
                {
                    return;
                }

                // Keep original intent: build a per-format mask from (0x7F,0x7F,0x7F)
                // so that (value >> 1) & mask does not bleed between channels in the packed format.
                ushort mask = highColorCreator.GetHighColorWord(0x7F, 0x7F, 0x7F);

                for (int row = 0; row < srcOverDst.Height; row++)
                {
                    int maskRowBase = (maskStartY + row) * maskStride + maskStartX;
                    int dstRowBase = (srcOverDst.Y + row) * dstStride + srcOverDst.X;

                    for (int col = 0; col < srcOverDst.Width; col++)
                    {
                        if (_pixels8[maskRowBase + col] != transparentMaskColor)
                        {
                            ushort value = destination._pixels16[dstRowBase + col];
                            destination._pixels16[dstRowBase + col] = (ushort)(((uint)value >> 1) & mask);
                        }
                    }
                }
            }
        }

        // NXBasics::CBitmap::Tool_Darken() const
        internal void Tool_Darken()
        {
            SRectangle rect = _rect;
            Tool_Darken(ref rect);
        }

        // NXBasics::CBitmap::Tool_Darken(NXBasics::SRectangle) const
        internal void Tool_Darken(ref SRectangle rect)
        {
            // Call the overload that derives correct behavior per bpp internally.
            Tool_Darken(in rect, 0, 0, 0);
        }

        // NXBasics::CBitmap::Tool_Darken(NXBasics::SRectangle, unsigned char, unsigned char, unsigned char) const
        internal void Tool_Darken(in SRectangle rect, uint rMask, uint gMask, uint bMask)
        {
            if (_bpp == (byte)BitmapFormat.TrueColor32)
            {
                if (_pixels32 == null)
                {
                    return;
                }

                // Prefer masks from the true-color creator if available.
                CTrueColorCreator trueColorCreator = CXBSystemManager.sTrueColorCreatorPtr;

                uint rm = rMask;
                uint gm = gMask;
                uint bm = bMask;

                if (trueColorCreator != null && trueColorCreator.IsEnabled)
                {
                    rm = trueColorCreator.RMask;
                    gm = trueColorCreator.GMask;
                    bm = trueColorCreator.BMask;
                }

                // Prevent cross-channel bit bleeding when shifting the whole pixel right by 1.
                uint channelMask = ((rm >> 1) & rm) | ((gm >> 1) & gm) | ((bm >> 1) & bm);

                int stride = StridePixels;
                int startIndex = (rect.Y * stride) + rect.X;

                for (int y = 0; y < rect.Height; y++)
                {
                    int rowIndex = startIndex + (y * stride);

                    for (int x = 0; x < rect.Width; x++)
                    {
                        uint value = _pixels32[rowIndex + x];
                        _pixels32[rowIndex + x] = (value >> 1) & channelMask;
                    }
                }

                return;
            }
        }
        #endregion

        #region PlotCirclePoints16/32()
        //private void PlotCirclePoints16(int cx, int cy, int r, int xPos, int yNeg, ushort value)
        //{
        //    if (_ptr8 == 0)
        //    {
        //        return;
        //    }

        //    int xA = cx;
        //    int xB = cx + xPos;

        //    int yTop = cy + r;
        //    int yBottom = cy - r;

        //    if ((_rect.X <= xA) && (xA <= _rect.Right) && (_rect.Y <= yTop) && (yTop <= _rect.Bottom) && (_bpp == 0x10))
        //    {
        //        DexterMemory.WriteUInt16(_ptr16 + (nint)(((xA + (yTop * _pitchPixels)) * 2)), value);
        //    }

        //    if ((_rect.X <= xB) && (xB <= _rect.Right) && (_rect.Y <= yTop) && (yTop <= _rect.Bottom) && (_bpp == 0x10))
        //    {
        //        DexterMemory.WriteUInt16(_ptr16 + (nint)(((xB + (yTop * _pitchPixels)) * 2)), value);
        //    }

        //    if ((_rect.X <= xA) && (xA <= _rect.Right) && (_rect.Y <= yBottom) && (yBottom <= _rect.Bottom) && (_bpp == 0x10))
        //    {
        //        DexterMemory.WriteUInt16(_ptr16 + (nint)(((xA + (yBottom * _pitchPixels)) * 2)), value);
        //    }

        //    if ((_rect.X <= xB) && (xB <= _rect.Right) && (_rect.Y <= yBottom) && (yBottom <= _rect.Bottom) && (_bpp == 0x10))
        //    {
        //        DexterMemory.WriteUInt16(_ptr16 + (nint)(((xB + (yBottom * _pitchPixels)) * 2)), value);
        //    }

        //    int xLeft = cx - r;
        //    int xRight = cx + r;

        //    int yA = cy + xPos;
        //    int yB = cy + yNeg;

        //    if ((_rect.X <= xLeft) && (xLeft <= _rect.Right) && (_rect.Y <= yA) && (yA <= _rect.Bottom) && (_bpp == 0x10))
        //    {
        //        DexterMemory.WriteUInt16(_ptr16 + (nint)(((xLeft + (yA * _pitchPixels)) * 2)), value);
        //    }

        //    if ((_rect.X <= xRight) && (xRight <= _rect.Right) && (_rect.Y <= yA) && (yA <= _rect.Bottom) && (_bpp == 0x10))
        //    {
        //        DexterMemory.WriteUInt16(_ptr16 + (nint)(((xRight + (yA * _pitchPixels)) * 2)), value);
        //    }

        //    if ((_rect.X <= xLeft) && (xLeft <= _rect.Right) && (_rect.Y <= yB) && (yB <= _rect.Bottom) && (_bpp == 0x10))
        //    {
        //        DexterMemory.WriteUInt16(_ptr16 + (nint)(((xLeft + (yB * _pitchPixels)) * 2)), value);
        //    }

        //    if ((_rect.X <= xRight) && (xRight <= _rect.Right) && (_rect.Y <= yB) && (yB <= _rect.Bottom) && (_bpp == 0x10))
        //    {
        //        DexterMemory.WriteUInt16(_ptr16 + (nint)(((xRight + (yB * _pitchPixels)) * 2)), value);
        //    }
        //}

        //private void PlotCirclePoints32(int cx, int cy, int r, int xPos, int yNeg, uint value)
        //{
        //    if (_ptr8 == 0)
        //    {
        //        return;
        //    }

        //    int xA = cx;
        //    int xB = cx + xPos;

        //    int yTop = cy + r;
        //    int yBottom = cy - r;

        //    if ((_rect.X <= xA) && (xA <= _rect.Right) && (_rect.Y <= yTop) && (yTop <= _rect.Bottom) && (_bpp == 0x20))
        //    {
        //        DexterMemory.WriteUInt32(_ptr32 + (nint)(((xA + (yTop * _pitchPixels)) * 4)), value);
        //    }

        //    if ((_rect.X <= xB) && (xB <= _rect.Right) && (_rect.Y <= yTop) && (yTop <= _rect.Bottom) && (_bpp == 0x20))
        //    {
        //        DexterMemory.WriteUInt32(_ptr32 + (nint)(((xB + (yTop * _pitchPixels)) * 4)), value);
        //    }

        //    if ((_rect.X <= xA) && (xA <= _rect.Right) && (_rect.Y <= yBottom) && (yBottom <= _rect.Bottom) && (_bpp == 0x20))
        //    {
        //        DexterMemory.WriteUInt32(_ptr32 + (nint)(((xA + (yBottom * _pitchPixels)) * 4)), value);
        //    }

        //    if ((_rect.X <= xB) && (xB <= _rect.Right) && (_rect.Y <= yBottom) && (yBottom <= _rect.Bottom) && (_bpp == 0x20))
        //    {
        //        DexterMemory.WriteUInt32(_ptr32 + (nint)(((xB + (yBottom * _pitchPixels)) * 4)), value);
        //    }

        //    int xLeft = cx - r;
        //    int xRight = cx + r;

        //    int yA = cy + xPos;
        //    int yB = cy + yNeg;

        //    if ((_rect.X <= xLeft) && (xLeft <= _rect.Right) && (_rect.Y <= yA) && (yA <= _rect.Bottom) && (_bpp == 0x20))
        //    {
        //        DexterMemory.WriteUInt32(_ptr32 + (nint)(((xLeft + (yA * _pitchPixels)) * 4)), value);
        //    }

        //    if ((_rect.X <= xRight) && (xRight <= _rect.Right) && (_rect.Y <= yA) && (yA <= _rect.Bottom) && (_bpp == 0x20))
        //    {
        //        DexterMemory.WriteUInt32(_ptr32 + (nint)(((xRight + (yA * _pitchPixels)) * 4)), value);
        //    }

        //    if ((_rect.X <= xLeft) && (xLeft <= _rect.Right) && (_rect.Y <= yB) && (yB <= _rect.Bottom) && (_bpp == 0x20))
        //    {
        //        DexterMemory.WriteUInt32(_ptr32 + (nint)(((xLeft + (yB * _pitchPixels)) * 4)), value);
        //    }

        //    if ((_rect.X <= xRight) && (xRight <= _rect.Right) && (_rect.Y <= yB) && (yB <= _rect.Bottom) && (_bpp == 0x20))
        //    {
        //        DexterMemory.WriteUInt32(_ptr32 + (nint)(((xRight + (yB * _pitchPixels)) * 4)), value);
        //    }
        //}
        #endregion

        #region SEEMS UNUSED - Tool_InitializeAsVirtualBitmap() || Storable_GetId() || IsPointInside() || ConvertBitsToTDepth() || ConstructVirtual() || GetHighColorWord() || Fill() || Storable_SaveData() || GetUnclippedPixelPtr() || WriteInt32LE()
        // NXBasics::CBitmap::Tool_InitializeAsVirtualBitmap(NXBasics::CBitmap const&, NXBasics::SRectangle const&)
        /*        internal void Tool_InitializeAsVirtualBitmap(CBitmap source, in SRectangle sourceRect)
                {
                    // Mirrors: memset(this+8, 0, 0x70), set tdepth = 8, bounds init, then construct virtual view.
                    ResetCoreFields();

                    _bpp = (byte)BitmapFormat.Indexed8;
                    _rect = new SRectangle(0, 0, 0, 0);

                    ConstructVirtual(source, sourceRect);
                }

                // NXBasics::CBitmap::Storable_GetId() const
        internal static ulong Storable_GetId()
        {
            return 0x3F3UL;
        }

                // NXBasics::CBitmap::IsPointInside(NXBasics::SPoint const&) const
        internal bool IsPointInside(in SPoint point)
        {
            int x = point.X;
            int y = point.Y;

            if (_rect.X <= x && x < (_rect.X + _rect.Width) && _rect.Y <= y)
            {
                return y < (_rect.Y + _rect.Height);
            }

            return false;
        }

                        // NXBasics::CBitmap::ConvertBitsToTDepth(unsigned int)
                internal static byte ConvertBitsToTDepth(uint bitsPerPixel)
                {
                    byte result = 8;

                    if (bitsPerPixel == 0x20)
                    {
                        result = 0x20;
                    }

                    if (bitsPerPixel == 0x10)
                    {
                        result = 0x10;
                    }

                    return result;
                }

                private void ConstructVirtual(CBitmap source, in SRectangle sourceRect)
                {
                    if (source == null)
                    {
                        return;
                    }

                    SRectangle rect = sourceRect;
                    rect.Validate();

                    if (!rect.IsTouching(source._rect))
                    {
                        return;
                    }

                    rect.CutInside(source._rect);

                    if (rect.Width <= 0 || rect.Height <= 0)
                    {
                        return;
                    }

                    // The original sets tdepth to 8 before calling into the constructor.
                    // In practice, virtual bitmaps in this engine are used as 8bpp views.
                    if (source._bpp != (byte)BitmapFormat.Indexed8 || source._pixels8 == null)
                    {
                        return;
                    }

                    // Virtual view: share the same backing buffer, but with an origin offset.
                    // We store the full buffer and an origin; drawing ops that use _pixels8 must respect _virtualOriginIndex.
                    // If the pipeline already supports "base offset", keep this; otherwise, we can slice/copy.
                    _pixels8 = source._pixels8;
                    _virtualOriginIndex = (rect.Y * source.StridePixels) + rect.X;

                    _bpp = (byte)BitmapFormat.Indexed8;
                    Width = rect.Width;
                    Height = rect.Height;
                    StridePixels = source.StridePixels;

                    _palettePtr = source._palettePtr;

                    _rect = new SRectangle(0, 0, rect.Width, rect.Height);
                }
        
                // NXBasics::CBitmap::GetHighColorWord(unsigned char) const
        internal void Fill(ushort highColor)
        {
            if (_bpp != 0x10)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel))
            {
                return;
            }

            FillBlockWordLE(buffer, baseOffsetBytes, _rect.Width, _rect.Height, pitchPixels, highColor);
        }

        // NXBasics::CBitmap::GetTrueColorWord(unsigned char) const
        internal void Fill(uint trueColor)
        {
            if (_bpp != 0x20)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel))
            {
                return;
            }

            FillBlockLongLE(buffer, baseOffsetBytes, _rect.Width, _rect.Height, pitchPixels, trueColor);
        }

                // NXBasics::CBitmap::Storable_SaveData(NXBasics::CFile&)
        internal void Storable_SaveData(CFile file)
        {
            ArgumentNullException.ThrowIfNull(file);

            // write bpp (1 byte)
            byte[] one = [_bpp];
            file.Write(one, 1);

            // write rect as 16 bytes: X, Y, Width, Height (little-endian int32)
            byte[] rectBytes = new byte[16];

            WriteInt32LE(rectBytes, 0, _rect.X);
            WriteInt32LE(rectBytes, 4, _rect.Y);
            WriteInt32LE(rectBytes, 8, _rect.Width);
            WriteInt32LE(rectBytes, 12, _rect.Height);

            file.Write(rectBytes, rectBytes.Length);

            // save memoryOwner as storable (or null-storable)
            if (_memoryOwner != null)
            {
                _memoryOwner.Storable_Save(file);
            }
            else
            {
                CStorable.Storable_SaveNull(file);
            }
        }

        private static void WriteInt32LE(byte[] buffer, int offset, int value)
        {
            uint u = unchecked((uint)value);
            buffer[offset + 0] = (byte)(u & 0xFF);
            buffer[offset + 1] = (byte)((u >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((u >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((u >> 24) & 0xFF);
        }
         
         */
        #endregion
    }
}