using OpenVikings.NC2GuiToolsBase;
using OpenVikings.NXBasics.Structs;

namespace OpenVikings.NXBasics
{
    internal sealed class CBitmap : IDisposable
    {
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
        internal int Width { get; private set; }
        internal int Height { get; private set; }
        internal int StridePixels { get; private set; }

        private object _palettePtr;

        // Managed backing buffers (you likely already have these in your reimplementation)
        private byte[] _pixels8;
        private ushort[] _pixels16;
        private uint[] _pixels32;

        // Based on your offsets, CBitmap has an embedded SRectangle at +0x0C.
        // In the reimplementation, keep it as a field.
        private SRectangle _bounds;

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
            // Managed-safe reset instead of raw memset(this+8, 0, 0x70)

            _bpp = 0x08;

            _pixels8 = null;
            _pixels16 = null;
            _pixels32 = null;

            Width = 0;
            Height = 0;
            StridePixels = 0;

            _palettePtr = null;

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

        internal CBitmap(CFile file, uint param2) : this()
        {
            // NXBasics::CBitmap::CBitmap(NXBasics::CFile&, unsigned int)
            // param2 is unused in the dump chunk (kept for signature match)
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

            L_SetRawMemoryPtr();

            _pitchPixels = _rect.Width;
            _strideBytes = _bytesPerPixel * _pitchPixels;
            _isVirtual = 0x00;
        }

        // NXBasics::CBitmap::L_ConstructNonVirtual(NXBasics::SRectangle const&, unsigned char)
        internal void L_ConstructNonVirtual(in SRectangle rect, byte bpp)
        {
            int pixelCount = rect.Height * rect.Width;

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

            _pitchPixels = rect.Width;
            _strideBytes = _bytesPerPixel * _pitchPixels;

            _rect.SetVariables(0, 0, rect.Width, rect.Height);

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
            // NXBasics::CBitmap::L_ConstructExternalMemory(...)

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

        // NXBasics::CBitmap::~CBitmap()
        public void Dispose()
        {
            _memoryOwner?.Dispose();
            _memoryOwner = null;
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

        // NXBasics::CBitmap::L_SetRawMemoryPtr()
        internal void L_SetRawMemoryPtr()
        {
            if (_bpp == 0x20) _bytesPerPixel = 4;
            else if (_bpp == 0x10) _bytesPerPixel = 2;
            else _bytesPerPixel = 1;

            if (_memoryOwner == null)
            {
                return;
            }

            int expectedMin = checked(_strideBytes * _rect.Height);
            if (_memoryOwner.Size < (uint)expectedMin)
            {
                // Choose policy: throw (best for porting), or just leave it.
                throw new InvalidOperationException("CBitmap: memory owner buffer is smaller than expected for current layout.");
            }
        }

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


        // NXBasics::CBitmap::L_FindMatchingColor(NXBasics::SColorRGB const&) const
        private bool TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel)
        {
            bytesPerPixel = _bytesPerPixel;

            // Owned memory
            if (_memoryOwner != null)
            {
                byte[]? arr = _memoryOwner.BufferArray;
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

            // External memory
            if (_externalBuffer != null)
            {
                buffer = _externalBuffer;
                baseOffsetBytes = 0;
                pitchPixels = _pitchPixels;
                return true;
            }

            // Virtual bitmap (view into parent)
            if (_isVirtual != 0x00 && _virtualParent != null)
            {
                if (!_virtualParent.TryGetPixelBuffer(out buffer, out int parentBase, out int parentPitch, out int parentBpp))
                {
                    baseOffsetBytes = 0;
                    pitchPixels = 0;
                    bytesPerPixel = _bytesPerPixel;
                    return false;
                }

                // inherit addressing from parent
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


        // NXBasics::CBitmap::GetHighColorTablePtr() const
        internal ushort[] GetHighColorTablePtr()
        {
            return (_palette ?? CXBSystemManager.sPalettePtr).GetHighColorTablePtr();
        }

        internal uint[] GetTrueColorTablePtr()
        {
            return (_palette ?? CXBSystemManager.sPalettePtr).GetTrueColorTablePtr();
        }

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

/*

        // NXBasics::CBitmap::CopyIntoBitmap_HalfSize_ColorKeyed(NXBasics::CBitmap const&, int, int, unsigned char) const
        internal void CopyIntoBitmap_HalfSize_ColorKeyed(CBitmap destination, int dstX, int dstY, byte colorKey)
        {
            if ((_bpp == 0x08) && (_ptr8 != 0) && (destination._ptr8 != 0))
            {
                uint srcXSkip = 0;

                SRectangle dstBounds = new SRectangle(0, 0, (uint)destination._rect.Width, (uint)destination._rect.Height);
                SRectangle placing = new SRectangle(dstX, dstY, (uint)(_rect.Width / 2), (uint)(_rect.Height / 2));

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

                if (SRectangle.IsInside(ref placing, ref dstBounds))
                {
                    int srcPitch = _pitchPixels;
                    nint srcPtr = _ptr8 + (nint)(((srcPitch * ySkip + (int)srcXSkip) * 2));

                    if (destination._bpp == 0x20)
                    {
                        CPalette pal = _palette;
                        if (pal == null) pal = CXBSystemManager.sPalettePtr;

                        nint tablePtr = CPalette.GetTrueColorTablePtr(pal);
                        if (tablePtr != 0 && ySkip < placing.Height)
                        {
                            uint dstPitch = (uint)destination._pitchPixels;
                            nint dstPtr = destination._ptr32 + (nint)(((placing.Y * (int)dstPitch) + placing.X) * 4);

                            int xStart = 0;
                            if (0 < startX) xStart = startX;

                            do
                            {
                                ulong x = srcXSkip;
                                int srcCol = (xStart * 2) + (startX * -2);

                                if ((int)srcXSkip < placing.Width)
                                {
                                    do
                                    {
                                        byte idx = DexterMemory.ReadUInt8(srcPtr + srcCol);
                                        if (idx != colorKey)
                                        {
                                            uint color = DexterMemory.ReadUInt32(tablePtr + (nint)(idx * 4));
                                            DexterMemory.WriteUInt32(dstPtr + (nint)(x * 4), color);
                                        }

                                        x = x + 1;
                                        srcCol = srcCol + 2;
                                    } while ((int)x < placing.Width);
                                }

                                srcPtr = srcPtr + (nint)(srcPitch * 2);
                                dstPtr = dstPtr + (nint)(dstPitch * 4);
                                ySkip = ySkip + 1;
                            } while (ySkip < placing.Height);
                        }
                    }
                    else if (destination._bpp == 0x10)
                    {
                        CPalette pal = _palette;
                        if (pal == null) pal = CXBSystemManager.sPalettePtr;

                        nint tablePtr = CPalette.GetHighColorTablePtr(pal);
                        if (tablePtr != 0 && ySkip < placing.Height)
                        {
                            uint dstPitch = (uint)destination._pitchPixels;
                            nint dstPtr = destination._ptr16 + (nint)(((placing.Y * (int)dstPitch) + placing.X) * 2);

                            int xStart = 0;
                            if (0 < startX) xStart = startX;

                            do
                            {
                                ulong x = srcXSkip;
                                int srcCol = (xStart * 2) + (startX * -2);

                                if ((int)srcXSkip < placing.Width)
                                {
                                    do
                                    {
                                        byte idx = DexterMemory.ReadUInt8(srcPtr + srcCol);
                                        if (idx != colorKey)
                                        {
                                            ushort w = DexterMemory.ReadUInt16(tablePtr + (nint)(idx * 2));
                                            DexterMemory.WriteUInt16(dstPtr + (nint)(x * 2), w);
                                        }

                                        x = x + 1;
                                        srcCol = srcCol + 2;
                                    } while ((int)x < placing.Width);
                                }

                                srcPtr = srcPtr + (nint)(srcPitch * 2);
                                dstPtr = dstPtr + (nint)(dstPitch * 2);
                                ySkip = ySkip + 1;
                            } while (ySkip < placing.Height);
                        }
                    }
                    else if (destination._bpp == 0x08)
                    {
                        if (ySkip < placing.Height)
                        {
                            uint dstPitch = (uint)destination._pitchPixels;
                            nint dstPtr = destination._ptr8 + (nint)((placing.Y * (int)dstPitch) + placing.X);

                            int xStart = 0;
                            if (0 < startX) xStart = startX;

                            do
                            {
                                uint x = srcXSkip;
                                int srcCol = (xStart * 2) + (startX * -2);

                                if ((int)srcXSkip < placing.Width)
                                {
                                    do
                                    {
                                        byte idx = DexterMemory.ReadUInt8(srcPtr + srcCol);
                                        if (idx != colorKey)
                                        {
                                            DexterMemory.WriteUInt8(dstPtr + (int)x, idx);
                                        }

                                        x = x + 1;
                                        srcCol = srcCol + 2;
                                    } while ((int)x < placing.Width);
                                }

                                srcPtr = srcPtr + (nint)(srcPitch * 2);
                                dstPtr = dstPtr + (nint)dstPitch;
                                ySkip = ySkip + 1;
                            } while (ySkip < placing.Height);
                        }
                    }
                }
            }
        }

        // NXBasics::CBitmap::CopyIntoBitmap_HalfSize_ColorKeyed_Remap(NXBasics::CBitmap const&, int, int, unsigned char, unsigned char const*) const
        internal void CopyIntoBitmap_HalfSize_ColorKeyed_Remap(CBitmap destination, int dstX, int dstY, byte colorKey, byte[] remap)
        {
            if ((_bpp == 0x08) && (_ptr8 != 0) && (destination._ptr8 != 0))
            {
                uint srcXSkip = 0;

                SRectangle dstBounds = new SRectangle(0, 0, (uint)destination._rect.Width, (uint)destination._rect.Height);
                SRectangle placing = new SRectangle(dstX, dstY, (uint)(_rect.Width / 2), (uint)(_rect.Height / 2));

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

                if (SRectangle.IsInside(ref placing, ref dstBounds))
                {
                    int srcPitch = _pitchPixels;
                    nint srcRowPtr = _ptr8 + (nint)(((srcPitch * ySkip + (int)srcXSkip) * 2));

                    nint srcRowPtrBase = srcRowPtr;

                    if (destination._bpp == 0x20)
                    {
                        CPalette pal = _palette;
                        if (pal == null) pal = CXBSystemManager.sPalettePtr;

                        nint tablePtr = CPalette.GetTrueColorTablePtr(pal);
                        if ((tablePtr != 0) && (ySkip < placing.Height))
                        {
                            uint dstPitch = (uint)destination._pitchPixels;
                            nint dstRowPtr = destination._ptr32 + (nint)(((placing.Y * (int)dstPitch) + placing.X) * 4);

                            int xStart = 0;
                            if (0 < originalPlacingX) xStart = originalPlacingX;

                            int row = ySkip;
                            while (row < placing.Height)
                            {
                                ulong x = srcXSkip;
                                int srcCol = (xStart * 2) + (originalPlacingX * -2);

                                if ((int)srcXSkip < placing.Width)
                                {
                                    do
                                    {
                                        byte idx = DexterMemory.ReadUInt8(srcRowPtrBase + srcCol);
                                        if (idx != colorKey)
                                        {
                                            byte mapped = remap[idx];
                                            uint color = DexterMemory.ReadUInt32(tablePtr + (nint)(mapped * 4));
                                            DexterMemory.WriteUInt32(dstRowPtr + (nint)(x * 4), color);
                                        }

                                        x = x + 1;
                                        srcCol = srcCol + 2;
                                    } while ((int)x < placing.Width);
                                }

                                srcRowPtrBase = srcRowPtrBase + (nint)(srcPitch * 2);
                                dstRowPtr = dstRowPtr + (nint)(dstPitch * 4);
                                row++;
                            }
                        }
                    }
                    else if (destination._bpp == 0x10)
                    {
                        CPalette pal = _palette;
                        if (pal == null) pal = CXBSystemManager.sPalettePtr;

                        nint tablePtr = CPalette.GetHighColorTablePtr(pal);
                        if ((tablePtr != 0) && (ySkip < placing.Height))
                        {
                            uint dstPitch = (uint)destination._pitchPixels;
                            nint dstRowPtr = destination._ptr16 + (nint)(((placing.Y * (int)dstPitch) + placing.X) * 2);

                            int xStart = 0;
                            if (0 < originalPlacingX) xStart = originalPlacingX;

                            int row = ySkip;
                            while (row < placing.Height)
                            {
                                ulong x = srcXSkip;
                                int srcCol = (xStart * 2) + (originalPlacingX * -2);

                                if ((int)srcXSkip < placing.Width)
                                {
                                    do
                                    {
                                        byte idx = DexterMemory.ReadUInt8(srcRowPtrBase + srcCol);
                                        if (idx != colorKey)
                                        {
                                            byte mapped = remap[idx];
                                            ushort w = DexterMemory.ReadUInt16(tablePtr + (nint)(mapped * 2));
                                            DexterMemory.WriteUInt16(dstRowPtr + (nint)(x * 2), w);
                                        }

                                        x = x + 1;
                                        srcCol = srcCol + 2;
                                    } while ((int)x < placing.Width);
                                }

                                srcRowPtrBase = srcRowPtrBase + (nint)(srcPitch * 2);
                                dstRowPtr = dstRowPtr + (nint)(dstPitch * 2);
                                row++;
                            }
                        }
                    }
                    else if (destination._bpp == 0x08)
                    {
                        if (ySkip < placing.Height)
                        {
                            uint dstPitch = (uint)destination._pitchPixels;
                            nint dstRowPtr = destination._ptr8 + (nint)((placing.Y * (int)dstPitch) + placing.X);

                            int xStart = 0;
                            if (0 < originalPlacingX) xStart = originalPlacingX;

                            int row = ySkip;
                            while (row < placing.Height)
                            {
                                ulong x = srcXSkip;
                                int srcCol = (xStart * 2) + (originalPlacingX * -2);

                                if ((int)srcXSkip < placing.Width)
                                {
                                    do
                                    {
                                        byte idx = DexterMemory.ReadUInt8(srcRowPtrBase + srcCol);
                                        if (idx != colorKey)
                                        {
                                            DexterMemory.WriteUInt8(dstRowPtr + (nint)x, remap[idx]);
                                        }

                                        x = x + 1;
                                        srcCol = srcCol + 2;
                                    } while ((int)x < placing.Width);
                                }

                                srcRowPtrBase = srcRowPtrBase + (nint)(srcPitch * 2);
                                dstRowPtr = dstRowPtr + (nint)dstPitch;
                                row++;
                            }
                        }
                    }
                }
            }
        }

        // NXBasics::CBitmap::CopyIntoBitmap_HalfSize_ColorKeyed_RemapTwice(NXBasics::CBitmap const&, int, int, unsigned char, unsigned char const*, unsigned char const*) const
        internal void CopyIntoBitmap_HalfSize_ColorKeyed_RemapTwice(CBitmap destination, int dstX, int dstY, byte colorKey, byte[] remapA, byte[] remapB)
        {
            if ((_bpp == 0x08) && (_ptr8 != 0) && (destination._ptr8 != 0))
            {
                uint srcXSkip = 0;

                SRectangle dstBounds = new SRectangle(0, 0, (uint)destination._rect.Width, (uint)destination._rect.Height);
                SRectangle.MakeSizeLongAlligned(ref dstBounds);

                SRectangle placing = new SRectangle(dstX & ~1, dstY & ~1, (uint)(_rect.Width / 2), (uint)(_rect.Height / 2));
                SRectangle.MakeSizeLongAlligned(ref placing);

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

                if (SRectangle.IsInside(ref placing, ref dstBounds))
                {
                    int srcPitch = _pitchPixels;
                    nint srcRowPtr = _ptr8 + (nint)(((srcPitch * ySkip + (int)srcXSkip) * 2));

                    if (destination._bpp == 0x20)
                    {
                        CPalette pal = _palette;
                        if (pal == null) pal = CXBSystemManager.sPalettePtr;

                        nint tablePtr = CPalette.GetTrueColorTablePtr(pal);
                        if ((tablePtr != 0) && (ySkip < placing.Height))
                        {
                            uint dstPitch = (uint)destination._pitchPixels;
                            nint dstRowPtr = destination._ptr32 + (nint)(((placing.Y * (int)dstPitch) + placing.X) * 4);

                            int xStart = 0;
                            if (0 < originalPlacingX) xStart = originalPlacingX;

                            int row = ySkip;
                            while (row < placing.Height)
                            {
                                ulong x = srcXSkip;
                                int srcCol = (xStart * 2) + (originalPlacingX * -2);

                                if ((int)srcXSkip < placing.Width)
                                {
                                    do
                                    {
                                        byte idx = DexterMemory.ReadUInt8(srcRowPtr + srcCol);
                                        if (idx != colorKey)
                                        {
                                            byte mapped = remapB[remapA[idx]];
                                            uint color = DexterMemory.ReadUInt32(tablePtr + (nint)(mapped * 4));
                                            DexterMemory.WriteUInt32(dstRowPtr + (nint)(x * 4), color);
                                        }

                                        x = x + 1;
                                        srcCol = srcCol + 2;
                                    } while ((int)x < placing.Width);
                                }

                                srcRowPtr = srcRowPtr + (nint)(srcPitch * 2);
                                dstRowPtr = dstRowPtr + (nint)(dstPitch * 4);
                                row++;
                            }
                        }
                    }
                    else if (destination._bpp == 0x10)
                    {
                        CPalette pal = _palette;
                        if (pal == null) pal = CXBSystemManager.sPalettePtr;

                        nint tablePtr = CPalette.GetHighColorTablePtr(pal);
                        if ((tablePtr != 0) && (ySkip < placing.Height))
                        {
                            uint dstPitch = (uint)destination._pitchPixels;
                            nint dstRowPtr = destination._ptr16 + (nint)(((placing.Y * (int)dstPitch) + placing.X) * 2);

                            int xStart = 0;
                            if (0 < originalPlacingX) xStart = originalPlacingX;

                            int row = ySkip;
                            while (row < placing.Height)
                            {
                                ulong x = srcXSkip;
                                int srcCol = (xStart * 2) + (originalPlacingX * -2);

                                if ((int)srcXSkip < placing.Width)
                                {
                                    do
                                    {
                                        byte idx = DexterMemory.ReadUInt8(srcRowPtr + srcCol);
                                        if (idx != colorKey)
                                        {
                                            byte mapped = remapB[remapA[idx]];
                                            ushort w = DexterMemory.ReadUInt16(tablePtr + (nint)(mapped * 2));
                                            DexterMemory.WriteUInt16(dstRowPtr + (nint)(x * 2), w);
                                        }

                                        x = x + 1;
                                        srcCol = srcCol + 2;
                                    } while ((int)x < placing.Width);
                                }

                                srcRowPtr = srcRowPtr + (nint)(srcPitch * 2);
                                dstRowPtr = dstRowPtr + (nint)(dstPitch * 2);
                                row++;
                            }
                        }
                    }
                    else if (destination._bpp == 0x08)
                    {
                        if (ySkip < placing.Height)
                        {
                            uint dstPitch = (uint)destination._pitchPixels;
                            nint dstRowPtr = destination._ptr8 + (nint)((placing.Y * (int)dstPitch) + placing.X);

                            int xStart = 0;
                            if (0 < originalPlacingX) xStart = originalPlacingX;

                            int row = ySkip;
                            while (row < placing.Height)
                            {
                                ulong x = srcXSkip;
                                int srcCol = (xStart * 2) + (originalPlacingX * -2);

                                if ((int)srcXSkip < placing.Width)
                                {
                                    do
                                    {
                                        byte idx = DexterMemory.ReadUInt8(srcRowPtr + srcCol);
                                        if (idx != colorKey)
                                        {
                                            DexterMemory.WriteUInt8(dstRowPtr + (nint)x, remapB[remapA[idx]]);
                                        }

                                        x = x + 1;
                                        srcCol = srcCol + 2;
                                    } while ((int)x < placing.Width);
                                }

                                srcRowPtr = srcRowPtr + (nint)(srcPitch * 2);
                                dstRowPtr = dstRowPtr + (nint)dstPitch;
                                row++;
                            }
                        }
                    }
                }
            }
        }

        // NXBasics::CBitmap::CopyIntoBitmap_HalfSize_Optimized(NXBasics::CBitmap const&) const
        internal void CopyIntoBitmap_HalfSize_Optimized(CBitmap destination)
        {
            uint srcW = (uint)_rect.Width;
            int dstW = destination._rect.Width;

            if (srcW == (uint)(dstW * 2))
            {
                int dstH = destination._rect.Height;

                if (((_rect.Height == dstH * 2) && (_bpp == destination._bpp)) &&
                    ((_bpp == 0x20) || (_bpp == 0x10)) &&
                    (((_rect.Height & 1) == 0) && ((srcW & 0x3f) == 0)))
                {
                    uint pairs = (uint)(dstW / 2);
                    uint srcRowAdvancePixels = (uint)(_pitchPixels * 2) - srcW;
                    int dstPitch = destination._pitchPixels;

                    if (_bpp == 0x10)
                    {
                        if (0 < dstH)
                        {
                            nint dstPtr = destination._ptr16;
                            nint srcPtr = _ptr16;

                            int y = dstH;
                            do
                            {
                                if (1 < dstW)
                                {
                                    uint remaining = pairs;

                                    if ((remaining & 3) != 0)
                                    {
                                        uint done = 0;
                                        do
                                        {
                                            ushort a = DexterMemory.ReadUInt16(srcPtr + 0);
                                            ushort b = DexterMemory.ReadUInt16(srcPtr + 4);

                                            DexterMemory.WriteUInt16(dstPtr + 0, a);
                                            DexterMemory.WriteUInt16(dstPtr + 2, b);

                                            dstPtr = dstPtr + 4;
                                            srcPtr = srcPtr + 8;
                                            done++;
                                        } while ((pairs & 3) != done);

                                        remaining = pairs - done;
                                    }

                                    if (2 < (int)(pairs - 1))
                                    {
                                        int loops = (int)remaining + 4;
                                        while (4 < loops)
                                        {
                                            ushort p0 = DexterMemory.ReadUInt16(srcPtr + 0);
                                            ushort p1 = DexterMemory.ReadUInt16(srcPtr + 4);
                                            ushort p2 = DexterMemory.ReadUInt16(srcPtr + 8);
                                            ushort p3 = DexterMemory.ReadUInt16(srcPtr + 12);
                                            ushort p4 = DexterMemory.ReadUInt16(srcPtr + 16);
                                            ushort p5 = DexterMemory.ReadUInt16(srcPtr + 20);
                                            ushort p6 = DexterMemory.ReadUInt16(srcPtr + 24);
                                            ushort p7 = DexterMemory.ReadUInt16(srcPtr + 28);

                                            DexterMemory.WriteUInt16(dstPtr + 0, p0);
                                            DexterMemory.WriteUInt16(dstPtr + 2, p1);
                                            DexterMemory.WriteUInt16(dstPtr + 4, p2);
                                            DexterMemory.WriteUInt16(dstPtr + 6, p3);
                                            DexterMemory.WriteUInt16(dstPtr + 8, p4);
                                            DexterMemory.WriteUInt16(dstPtr + 10, p5);
                                            DexterMemory.WriteUInt16(dstPtr + 12, p6);
                                            DexterMemory.WriteUInt16(dstPtr + 14, p7);

                                            dstPtr = dstPtr + 16;
                                            srcPtr = srcPtr + 32;
                                            loops -= 4;
                                        }
                                    }
                                }

                                srcPtr = srcPtr + (nint)(srcRowAdvancePixels * 2);
                                dstPtr = dstPtr + (nint)((dstPitch - dstW) * 2);

                                y--;
                            } while (1 < y);
                        }
                    }
                    else
                    {
                        if (0 < dstH)
                        {
                            nint dstPtr = destination._ptr32;
                            nint srcPtr = _ptr32;

                            int y = dstH;
                            do
                            {
                                if (1 < dstW)
                                {
                                    uint remaining = pairs;

                                    if ((remaining & 3) != 0)
                                    {
                                        uint done = 0;
                                        do
                                        {
                                            uint a = DexterMemory.ReadUInt32(srcPtr + 0);
                                            uint b = DexterMemory.ReadUInt32(srcPtr + 8);

                                            DexterMemory.WriteUInt32(dstPtr + 0, a);
                                            DexterMemory.WriteUInt32(dstPtr + 4, b);

                                            dstPtr = dstPtr + 8;
                                            srcPtr = srcPtr + 16;
                                            done++;
                                        } while ((pairs & 3) != done);

                                        remaining = pairs - done;
                                    }

                                    if (2 < (int)(pairs - 1))
                                    {
                                        int loops = (int)remaining + 4;
                                        while (4 < loops)
                                        {
                                            uint p0 = DexterMemory.ReadUInt32(srcPtr + 0);
                                            uint p1 = DexterMemory.ReadUInt32(srcPtr + 8);
                                            uint p2 = DexterMemory.ReadUInt32(srcPtr + 16);
                                            uint p3 = DexterMemory.ReadUInt32(srcPtr + 24);
                                            uint p4 = DexterMemory.ReadUInt32(srcPtr + 32);
                                            uint p5 = DexterMemory.ReadUInt32(srcPtr + 40);
                                            uint p6 = DexterMemory.ReadUInt32(srcPtr + 48);
                                            uint p7 = DexterMemory.ReadUInt32(srcPtr + 56);

                                            DexterMemory.WriteUInt32(dstPtr + 0, p0);
                                            DexterMemory.WriteUInt32(dstPtr + 4, p1);
                                            DexterMemory.WriteUInt32(dstPtr + 8, p2);
                                            DexterMemory.WriteUInt32(dstPtr + 12, p3);
                                            DexterMemory.WriteUInt32(dstPtr + 16, p4);
                                            DexterMemory.WriteUInt32(dstPtr + 20, p5);
                                            DexterMemory.WriteUInt32(dstPtr + 24, p6);
                                            DexterMemory.WriteUInt32(dstPtr + 28, p7);

                                            dstPtr = dstPtr + 32;
                                            srcPtr = srcPtr + 64;
                                            loops -= 4;
                                        }
                                    }
                                }

                                srcPtr = srcPtr + (nint)(srcRowAdvancePixels * 4);
                                dstPtr = dstPtr + (nint)((dstPitch - dstW) * 4);

                                y--;
                            } while (1 < y);
                        }
                    }

                    return;
                }
            }

            CopyIntoBitmap_HalfSize(destination, 0, 0);
        }

        // NXBasics::CBitmap::CopyIntoBitmap_FitIn(NXBasics::CBitmap const&) const
        internal void CopyIntoBitmap_FitIn(CBitmap destination)
        {
            if ((_ptr8 == 0) || (destination._ptr8 == 0))
            {
                return;
            }

            uint dstW = (uint)destination._rect.Width;
            uint dstH = (uint)destination._rect.Height;

            int srcBpp = _bpp;

            if (srcBpp == 0x20)
            {
                if ((destination._bpp == 0x20) && (0 < (int)dstH))
                {
                    uint y = 0;
                    while (y < dstH)
                    {
                        if (0 < (int)dstW)
                        {
                            uint x = 0;
                            while (x < dstW)
                            {
                                int srcX = (_rect.Width * (int)x) / (int)dstW;
                                int srcY = (_rect.Height * (int)y) / (int)dstH;

                                uint pixel = 0;
                                if ((_ptr32 != 0) && (_rect.X <= srcX) && (srcX <= _rect.Right) &&
                                    (_rect.Y <= srcY) && (srcY <= _rect.Bottom))
                                {
                                    pixel = DexterMemory.ReadUInt32(_ptr32 + (nint)(((srcX + (srcY * _pitchPixels)) * 4)));
                                }

                                DexterMemory.WriteUInt32(destination._ptr32 + (nint)(((destination._pitchPixels * (int)y + (int)x) * 4)), pixel);
                                x++;
                            }
                        }

                        y++;
                    }
                }

                return;
            }

            if (srcBpp == 0x10)
            {
                if ((destination._bpp == 0x10) && (0 < (int)dstH))
                {
                    uint y = 0;
                    while (y < dstH)
                    {
                        if (0 < (int)dstW)
                        {
                            int acc = 0;
                            uint x = 0;
                            while (x < dstW)
                            {
                                int srcX = acc / (int)dstW;
                                int srcY = (_rect.Height * (int)y) / (int)dstH;

                                ushort pixel = 0;
                                if ((_ptr16 != 0) && (_rect.X <= srcX) && (srcX <= _rect.Right) &&
                                    (_rect.Y <= srcY) && (srcY <= _rect.Bottom))
                                {
                                    pixel = DexterMemory.ReadUInt16(_ptr16 + (nint)(((srcX + (srcY * _pitchPixels)) * 2)));
                                }

                                DexterMemory.WriteUInt16(destination._ptr16 + (nint)(((destination._pitchPixels * (int)y + (int)x) * 2)), pixel);

                                x++;
                                acc += _rect.Width;
                            }
                        }

                        y++;
                    }
                }

                return;
            }

            if (srcBpp == 0x08)
            {
                int dstBpp = destination._bpp;

                if (dstBpp == 0x20)
                {
                    CPalette pal = _palette;
                    if (pal == null) pal = CXBSystemManager.sPalettePtr;

                    nint tablePtr = CPalette.GetTrueColorTablePtr(pal);
                    if ((tablePtr != 0) && (0 < (int)dstH))
                    {
                        uint y = 0;
                        while (y < dstH)
                        {
                            if (0 < (int)dstW)
                            {
                                uint x = 0;
                                while (x < dstW)
                                {
                                    int srcX = (_rect.Width * (int)x) / (int)dstW;
                                    int srcY = (_rect.Height * (int)y) / (int)dstH;

                                    byte idx = Draw_GetPixel(srcX, srcY);
                                    uint color = DexterMemory.ReadUInt32(tablePtr + (nint)(idx * 4));

                                    DexterMemory.WriteUInt32(destination._ptr32 + (nint)(((destination._pitchPixels * (int)y + (int)x) * 4)), color);
                                    x++;
                                }
                            }

                            y++;
                        }
                    }

                    return;
                }

                if (dstBpp == 0x10)
                {
                    CPalette pal = _palette;
                    if (pal == null) pal = CXBSystemManager.sPalettePtr;

                    nint tablePtr = CPalette.GetHighColorTablePtr(pal);
                    if ((tablePtr != 0) && (0 < (int)dstH))
                    {
                        uint y = 0;
                        while (y < dstH)
                        {
                            if (0 < (int)dstW)
                            {
                                uint srcRowOffset = (uint)(((_rect.Height * (int)y) / (int)dstH) * _pitchPixels);

                                nint dstRowPtr = destination._ptr16 + (nint)((destination._pitchPixels * (int)y) * 2);

                                uint x = 0;
                                while (x < dstW)
                                {
                                    int srcX = (_rect.Width * (int)x) / (int)dstW;
                                    byte idx = DexterMemory.ReadUInt8(_ptr8 + (nint)(srcRowOffset + (uint)srcX));

                                    ushort color = DexterMemory.ReadUInt16(tablePtr + (nint)(idx * 2));
                                    DexterMemory.WriteUInt16(dstRowPtr + (nint)(x * 2), color);

                                    x++;
                                }
                            }

                            y++;
                        }
                    }

                    return;
                }

                if ((dstBpp == 0x08) && (0 < (int)dstH))
                {
                    uint y = 0;
                    while (y < dstH)
                    {
                        if (0 < (int)dstW)
                        {
                            uint srcRowOffset = (uint)(((_rect.Height * (int)y) / (int)dstH) * _pitchPixels);
                            nint dstRowPtr = destination._ptr8 + (nint)(destination._pitchPixels * (int)y);

                            uint x = 0;
                            while (x < dstW)
                            {
                                int srcX = (_rect.Width * (int)x) / (int)dstW;
                                byte idx = DexterMemory.ReadUInt8(_ptr8 + (nint)(srcRowOffset + (uint)srcX));
                                DexterMemory.WriteUInt8(dstRowPtr + (nint)x, idx);
                                x++;
                            }
                        }

                        y++;
                    }
                }
            }
        }

        // NXBasics::CBitmap::Draw_GetPixel(int, int) const
        internal byte Draw_GetPixel(int x, int y)
        {
            if ((_ptr8 == 0) || (x < _rect.X) || (_rect.Right < x) ||
                (y < _rect.Y) || (_rect.Bottom < y) || (_bpp != 0x08))
            {
                return 0;
            }

            return DexterMemory.ReadUInt8(_ptr8 + (nint)(x + (y * _pitchPixels)));
        }

        // NXBasics::CBitmap::CopyOutOfBitmap(NXBasics::CBitmap const&, int, int) const
        internal void CopyOutOfBitmap(CBitmap source, int srcX, int srcY)
        {
            if ((_bpp == 0x08) && (source._bpp == 0x08) && (_ptr8 != 0) && (source._ptr8 != 0))
            {
                SRectangle r = new SRectangle(srcX, srcY, (uint)_rect.Width, (uint)_rect.Height);

                if (SRectangle.IsTouching(ref r, ref source._rect))
                {
                    SRectangle.CutInside(ref r, ref source._rect);

                    nint srcPtr = source._ptr8 + (nint)(r.X + (r.Y * source._rect.Width));
                    nint dstPtr = _ptr8;

                    XB_Tool_Byte_CopyBlock(
                        srcPtr,
                        dstPtr,
                        r.Width,
                        r.Height,
                        source._rect.Width,
                        _pitchPixels
                    );
                }
            }
        }

        // NXBasics::CBitmap::Draw_SetPixel(int, int, unsigned char) const
        internal void Draw_SetPixel(int x, int y, byte index)
        {
            if ((_ptr8 != 0) && (_rect.X <= x) && (x <= _rect.Right) && (_rect.Y <= y) && (y <= _rect.Bottom))
            {
                if (_bpp == 0x20)
                {
                    CPalette pal = _palette;
                    if (pal == null) pal = CXBSystemManager.sPalettePtr;

                    uint color = CPalette.GetTrueColorWord(pal, (uint)index);
                    DexterMemory.WriteUInt32(_ptr32 + (nint)(((x + (y * _pitchPixels)) * 4)), color);
                }
                else if (_bpp == 0x10)
                {
                    CPalette pal = _palette;
                    if (pal == null) pal = CXBSystemManager.sPalettePtr;

                    ushort color = CPalette.GetHighColorWord(pal, (uint)index);
                    DexterMemory.WriteUInt16(_ptr16 + (nint)(((x + (y * _pitchPixels)) * 2)), color);
                }
                else if (_bpp == 0x08)
                {
                    DexterMemory.WriteUInt8(_ptr8 + (nint)(x + (y * _pitchPixels)), index);
                }
            }
        }

        // NXBasics::CBitmap::IsPointInside(int, int) const
        internal bool IsPointInside(int x, int y)
        {
            if ((_rect.X <= x) && (x <= _rect.Right) && (_rect.Y <= y))
            {
                return y <= _rect.Bottom;
            }

            return false;
        }

        // NXBasics::CBitmap::Draw_SetPixel(int, int, unsigned short) const
        internal void Draw_SetPixel(int x, int y, ushort value)
        {
            if ((_ptr8 != 0) && (_rect.X <= x) && (x <= _rect.Right) &&
                (_rect.Y <= y) && (y <= _rect.Bottom) && (_bpp == 0x10))
            {
                DexterMemory.WriteUInt16(_ptr16 + (nint)(((x + (y * _pitchPixels)) * 2)), value);
            }
        }

        // NXBasics::CBitmap::Draw_SetPixel(int, int, unsigned int) const
        internal void Draw_SetPixel(int x, int y, uint value)
        {
            if ((_ptr8 != 0) && (_rect.X <= x) && (x <= _rect.Right) &&
                (_rect.Y <= y) && (y <= _rect.Bottom) && (_bpp == 0x20))
            {
                DexterMemory.WriteUInt32(_ptr32 + (nint)(((x + (y * _pitchPixels)) * 4)), value);
            }
        }

        // NXBasics::CBitmap::Draw_SetPixel(int, int, NXBasics::SColorRGB const&) const
        internal void Draw_SetPixel(int x, int y, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                uint v = CTrueColorCreator.GetTrueColorWord(CXBSystemManager.sTrueColorCreatorPtr, in color);
                if ((_ptr8 != 0) && (_rect.X <= x) && (x <= _rect.Right) &&
                    (_rect.Y <= y) && (y <= _rect.Bottom) && (_bpp == 0x20))
                {
                    DexterMemory.WriteUInt32(_ptr32 + (nint)(((x + (y * _pitchPixels)) * 4)), v);
                }

                return;
            }

            if (_bpp == 0x10)
            {
                ushort v = CHighColorCreator.GetHighColorWord(CXBSystemManager.sHighColorCreatorPtr, in color);
                if ((_ptr8 != 0) && (_rect.X <= x) && (x <= _rect.Right) &&
                    (_rect.Y <= y) && (y <= _rect.Bottom) && (_bpp == 0x10))
                {
                    DexterMemory.WriteUInt16(_ptr16 + (nint)(((x + (y * _pitchPixels)) * 2)), v);
                }

                return;
            }

            if (_bpp == 0x08)
            {
                CPalette pal = _palette;
                pal ??= CXBSystemManager.sPalettePtr;

                byte idx = CPalette.FindMatchingColor(pal, in color);
                Draw_SetPixel(x, y, idx);
            }
        }

        // NXBasics::CBitmap::Draw_SetPixelUnclipped(int, int, NXBasics::SColorRGB const&) const
        internal void Draw_SetPixelUnclipped(int x, int y, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                uint v = CTrueColorCreator.GetTrueColorWord(CXBSystemManager.sTrueColorCreatorPtr, in color);
                DexterMemory.WriteUInt32(_ptr32 + (nint)(((x + (y * _pitchPixels)) * 4)), v);
                return;
            }

            if (_bpp == 0x10)
            {
                ushort v = CHighColorCreator.GetHighColorWord(CXBSystemManager.sHighColorCreatorPtr, in color);
                DexterMemory.WriteUInt16(_ptr16 + (nint)(((x + (y * _pitchPixels)) * 2)), v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette pal = _palette;
                if (pal == null) pal = CXBSystemManager.sPalettePtr;

                byte idx = CPalette.FindMatchingColor(pal, in color);
                DexterMemory.WriteUInt8(_ptr8 + (nint)(x + (y * _pitchPixels)), idx);
            }
        }

        // NXBasics::CBitmap::Draw_Box(NXBasics::SRectangle, unsigned char) const
        internal void Draw_Box(ref SRectangle rect, byte index)
        {
            if (_ptr8 == 0)
            {
                return;
            }

            SRectangle.Validate(ref rect);

            if (SRectangle.IsTouching(ref rect, ref _rect))
            {
                SRectangle.CutInside(ref rect, ref _rect);

                if (_bpp == 0x20)
                {
                    CPalette pal = _palette;
                    if (pal == null) pal = CXBSystemManager.sPalettePtr;

                    uint color = CPalette.GetTrueColorWord(pal, (uint)index);
                    nint dstPtr = _ptr32 + (nint)(((rect.Y * _pitchPixels) + rect.X) * 4);

                    XB_Tool_Long_FillBlock(dstPtr, rect.Width, rect.Height, _pitchPixels, color);
                    return;
                }

                if (_bpp == 0x10)
                {
                    CPalette pal = _palette;
                    if (pal == null) pal = CXBSystemManager.sPalettePtr;

                    ushort color = CPalette.GetHighColorWord(pal, (uint)index);
                    nint dstPtr = _ptr16 + (nint)(((rect.Y * _pitchPixels) + rect.X) * 2);

                    XB_Tool_Word_FillBlock(dstPtr, rect.Width, rect.Height, _pitchPixels, color);
                    return;
                }

                if (_bpp == 0x08)
                {
                    nint dstPtr = _ptr8 + (nint)((rect.Y * _pitchPixels) + rect.X);
                    XB_Tool_Byte_FillBlock(dstPtr, rect.Width, rect.Height, _pitchPixels, index);
                    return;
                }
            }
        }

        // NXBasics::CBitmap::Draw_Box(NXBasics::SRectangle, unsigned short) const
        internal void Draw_Box(ref SRectangle rect, ushort value)
        {
            SRectangle.Validate(ref rect);

            if ((_ptr8 != 0) && (_bpp == 0x10))
            {
                if (SRectangle.IsTouching(ref rect, ref _rect))
                {
                    SRectangle.CutInside(ref rect, ref _rect);

                    nint dstPtr = _ptr16 + (nint)(((rect.Y * _pitchPixels) + rect.X) * 2);
                    XB_Tool_Word_FillBlock(dstPtr, rect.Width, rect.Height, _pitchPixels, value);
                    return;
                }
            }
        }

        // NXBasics::CBitmap::Draw_Box(NXBasics::SRectangle, unsigned int) const
        internal void Draw_Box(ref SRectangle rect, uint value)
        {
            SRectangle.Validate(ref rect);

            if ((_ptr8 != 0) && (_bpp == 0x20))
            {
                if (SRectangle.IsTouching(ref rect, ref _rect))
                {
                    SRectangle.CutInside(ref rect, ref _rect);

                    nint dstPtr = _ptr32 + (nint)(((rect.Y * _pitchPixels) + rect.X) * 4);
                    XB_Tool_Long_FillBlock(dstPtr, rect.Width, rect.Height, _pitchPixels, value);
                    return;
                }
            }
        }

        // NXBasics::CBitmap::Draw_Box(NXBasics::SRectangle const&, NXBasics::SColorRGB const&) const
        internal void Draw_Box(in SRectangle input, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                SRectangle r = new SRectangle(in input);
                uint v = CTrueColorCreator.GetTrueColorWord(CXBSystemManager.sTrueColorCreatorPtr, in color);

                SRectangle.Validate(ref r);

                if ((_ptr8 != 0) && (_bpp == 0x20))
                {
                    if (SRectangle.IsTouching(ref r, ref _rect))
                    {
                        SRectangle.CutInside(ref r, ref _rect);

                        nint dstPtr = _ptr32 + (nint)(((r.Y * _pitchPixels) + r.X) * 4);
                        XB_Tool_Long_FillBlock(dstPtr, r.Width, r.Height, _pitchPixels, v);
                    }
                }

                return;
            }

            if (_bpp == 0x10)
            {
                SRectangle r = new SRectangle(in input);
                ushort v = CHighColorCreator.GetHighColorWord(CXBSystemManager.sHighColorCreatorPtr, in color);

                SRectangle.Validate(ref r);

                if ((_ptr8 != 0) && (_bpp == 0x10))
                {
                    if (SRectangle.IsTouching(ref r, ref _rect))
                    {
                        SRectangle.CutInside(ref r, ref _rect);

                        nint dstPtr = _ptr16 + (nint)(((r.Y * _pitchPixels) + r.X) * 2);
                        XB_Tool_Word_FillBlock(dstPtr, r.Width, r.Height, _pitchPixels, v);
                    }
                }

                return;
            }

            if (_bpp == 0x08)
            {
                SRectangle r = new(in input);

                CPalette pal = _palette;
                pal ??= CXBSystemManager.sPalettePtr;

                byte idx = CPalette.FindMatchingColor(pal, in color);
                Draw_Box(ref r, idx);
            }
        }

        // NXBasics::CBitmap::Draw_Rectangle(NXBasics::SRectangle, unsigned char) const
        internal void Draw_Rectangle(ref SRectangle rect, byte index)
        {
            SRectangle.Validate(ref rect);

            if (_ptr8 == 0)
            {
                return;
            }

            if (!SRectangle.IsTouching(ref rect, ref _rect))
            {
                return;
            }

            SRectangle top = new SRectangle(rect.X, rect.Y, rect.Width, 1);
            SRectangle tmp = new SRectangle(ref top);
            Draw_Box(ref tmp, index);

            SRectangle left = new SRectangle(rect.X, rect.Y, 1, rect.Height);
            tmp = new SRectangle(ref left);
            Draw_Box(ref tmp, index);

            SRectangle bottom = new SRectangle(rect.X, rect.Y + rect.Height - 1, rect.Width, 1);
            tmp = new SRectangle(ref bottom);
            Draw_Box(ref tmp, index);

            SRectangle right = new SRectangle(rect.X + rect.Width - 1, rect.Y, 1, rect.Height);
            tmp = new SRectangle(ref right);
            Draw_Box(ref tmp, index);
        }

        // NXBasics::CBitmap::Draw_HorizontalLine(int, int, unsigned int, unsigned char) const
        internal void Draw_HorizontalLine(int x, int y, uint width, byte index)
        {
            SRectangle r = new SRectangle(x, y, width, 1);
            SRectangle tmp = new SRectangle(ref r);
            Draw_Box(ref tmp, index);
        }

        // NXBasics::CBitmap::Draw_VerticalLine(int, int, unsigned int, unsigned char) const
        internal void Draw_VerticalLine(int x, int y, uint height, byte index)
        {
            SRectangle r = new SRectangle(x, y, 1, height);
            SRectangle tmp = new SRectangle(ref r);
            Draw_Box(ref tmp, index);
        }

        // NXBasics::CBitmap::Draw_Rectangle(NXBasics::SRectangle, unsigned short) const
        internal void Draw_Rectangle(ref SRectangle rect, ushort value)
        {
            SRectangle.Validate(ref rect);

            if (_ptr8 == 0)
            {
                return;
            }

            if (!SRectangle.IsTouching(ref rect, ref _rect))
            {
                return;
            }

            Draw_HorizontalLine(rect.X, rect.Y, rect.Width, value);
            Draw_VerticalLine(rect.X, rect.Y, rect.Height, value);
            Draw_HorizontalLine(rect.X, rect.Y + rect.Height - 1, rect.Width, value);
            Draw_VerticalLine(rect.X + rect.Width - 1, rect.Y, rect.Height, value);
        }

        // NXBasics::CBitmap::Draw_HorizontalLine(int, int, unsigned int, unsigned short) const
        internal void Draw_HorizontalLine(int x, int y, uint width, ushort value)
        {
            SRectangle r = new SRectangle(x, y, width, 1);
            SRectangle tmp = new SRectangle(ref r);

            SRectangle.Validate(ref tmp);

            if ((_ptr8 != 0) && (_bpp == 0x10))
            {
                if (SRectangle.IsTouching(ref tmp, ref _rect))
                {
                    SRectangle.CutInside(ref tmp, ref _rect);

                    nint dstPtr = _ptr16 + (nint)(((tmp.Y * _pitchPixels) + tmp.X) * 2);
                    XB_Tool_Word_FillBlock(dstPtr, tmp.Width, tmp.Height, _pitchPixels, value);
                }
            }
        }

        // NXBasics::CBitmap::Draw_VerticalLine(int, int, unsigned int, unsigned short) const
        internal void Draw_VerticalLine(int x, int y, uint height, ushort value)
        {
            SRectangle r = new SRectangle(x, y, 1, height);
            SRectangle tmp = new SRectangle(ref r);

            SRectangle.Validate(ref tmp);

            if ((_ptr8 != 0) && (_bpp == 0x10))
            {
                if (SRectangle.IsTouching(ref tmp, ref _rect))
                {
                    SRectangle.CutInside(ref tmp, ref _rect);

                    nint dstPtr = _ptr16 + (nint)(((tmp.Y * _pitchPixels) + tmp.X) * 2);
                    XB_Tool_Word_FillBlock(dstPtr, tmp.Width, tmp.Height, _pitchPixels, value);
                }
            }
        }

        // NXBasics::CBitmap::Draw_Rectangle(NXBasics::SRectangle, unsigned int) const
        internal void Draw_Rectangle(ref SRectangle rect, uint value)
        {
            SRectangle.Validate(ref rect);

            if (_ptr8 == 0)
            {
                return;
            }

            if (!SRectangle.IsTouching(ref rect, ref _rect))
            {
                return;
            }

            Draw_HorizontalLine(rect.X, rect.Y, rect.Width, value);
            Draw_VerticalLine(rect.X, rect.Y, rect.Height, value);
            Draw_HorizontalLine(rect.X, rect.Y + rect.Height - 1, rect.Width, value);
            Draw_VerticalLine(rect.X + rect.Width - 1, rect.Y, rect.Height, value);
        }

        // NXBasics::CBitmap::Draw_HorizontalLine(int, int, unsigned int, unsigned int) const
        internal void Draw_HorizontalLine(int x, int y, uint width, uint value)
        {
            SRectangle r = new SRectangle(x, y, width, 1);
            SRectangle tmp = new SRectangle(ref r);

            SRectangle.Validate(ref tmp);

            if ((_ptr8 != 0) && (_bpp == 0x20))
            {
                if (SRectangle.IsTouching(ref tmp, ref _rect))
                {
                    SRectangle.CutInside(ref tmp, ref _rect);

                    nint dstPtr = _ptr32 + (nint)(((tmp.Y * _pitchPixels) + tmp.X) * 4);
                    XB_Tool_Long_FillBlock(dstPtr, tmp.Width, tmp.Height, _pitchPixels, value);
                }
            }
        }

        // NXBasics::CBitmap::Draw_VerticalLine(int, int, unsigned int, unsigned int) const
        internal void Draw_VerticalLine(int x, int y, uint height, uint value)
        {
            SRectangle r = new SRectangle(x, y, 1, height);
            SRectangle tmp = new SRectangle(ref r);

            SRectangle.Validate(ref tmp);

            if ((_ptr8 != 0) && (_bpp == 0x20))
            {
                if (SRectangle.IsTouching(ref tmp, ref _rect))
                {
                    SRectangle.CutInside(ref tmp, ref _rect);

                    nint dstPtr = _ptr32 + (nint)(((tmp.Y * _pitchPixels) + tmp.X) * 4);
                    XB_Tool_Long_FillBlock(dstPtr, tmp.Width, tmp.Height, _pitchPixels, value);
                }
            }
        }

        // NXBasics::CBitmap::Draw_Rectangle(NXBasics::SRectangle const&, NXBasics::SColorRGB const&) const
        internal void Draw_Rectangle(in SRectangle rect, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                SRectangle r = new SRectangle(in rect);
                uint v = CTrueColorCreator.GetTrueColorWord(CXBSystemManager.sTrueColorCreatorPtr, in color);
                Draw_Rectangle(ref r, v);
                return;
            }

            if (_bpp == 0x10)
            {
                SRectangle r = new SRectangle(in rect);
                ushort v = CHighColorCreator.GetHighColorWord(CXBSystemManager.sHighColorCreatorPtr, in color);
                Draw_Rectangle(ref r, v);
                return;
            }

            if (_bpp == 0x08)
            {
                SRectangle r = new SRectangle(in rect);

                CPalette pal = _palette;
                if (pal == null) pal = CXBSystemManager.sPalettePtr;

                byte idx = CPalette.FindMatchingColor(pal, in color);
                Draw_Rectangle(ref r, idx);
            }
        }

        // NXBasics::CBitmap::Draw_StippleBox(NXBasics::SRectangle, unsigned char) const
        internal void Draw_StippleBox(ref SRectangle rect, byte index)
        {
            SRectangle.Validate(ref rect);

            if (_ptr8 == 0)
            {
                return;
            }

            if (!SRectangle.IsTouching(ref rect, ref _rect))
            {
                return;
            }

            SRectangle.CutInside(ref rect, ref _rect);

            int x0 = rect.X;
            int y0 = rect.Y;
            int x1 = x0 + (int)rect.Width - 1;
            int y1 = y0 + (int)rect.Height - 1;

            int xEndExclusive = x0 + (int)rect.Width;
            int yEndExclusive = y0 + (int)rect.Height;

            byte parity = (byte)(x0 & 1);

            if (_bpp == 0x20)
            {
                CPalette pal = _palette;
                if (pal == null) pal = CXBSystemManager.sPalettePtr;

                uint color = CPalette.GetTrueColorWord(pal, (uint)index);

                int y = y0;
                while (y <= y1)
                {
                    int startX = x0;
                    int endX = xEndExclusive;

                    if (startX <= x1)
                    {
                        int x = startX;

                        if (((endX - x) & 1) != 0)
                        {
                            if (((x & 1) == 0) == (parity == 0))
                            {
                                Draw_SetPixel(x, y, color);
                            }

                            x++;
                        }

                        while (xEndExclusive != x)
                        {
                            if (((x & 1) == 0) == (parity == 0))
                            {
                                Draw_SetPixel(x, y, color);
                            }
                            else
                            {
                                Draw_SetPixel(x + 1, y, color);
                            }

                            x += 2;
                        }
                    }

                    parity ^= 1;
                    y++;
                }

                return;
            }

            if (_bpp == 0x10)
            {
                CPalette pal = _palette;
                if (pal == null) pal = CXBSystemManager.sPalettePtr;

                ushort color = CPalette.GetHighColorWord(pal, (uint)index);

                int y = y0;
                while (y <= y1)
                {
                    int x = x0;

                    if (x <= x1)
                    {
                        if (((xEndExclusive - x) & 1) != 0)
                        {
                            if (((x & 1) == 0) == (parity == 0))
                            {
                                Draw_SetPixel(x, y, color);
                            }

                            x++;
                        }

                        while (xEndExclusive != x)
                        {
                            if (((x & 1) == 0) == (parity == 0))
                            {
                                Draw_SetPixel(x, y, color);
                            }
                            else
                            {
                                Draw_SetPixel(x + 1, y, color);
                            }

                            x += 2;
                        }
                    }

                    parity ^= 1;
                    y++;
                }

                return;
            }

            if (_bpp == 0x08)
            {
                int y = y0;
                while (y < yEndExclusive)
                {
                    nint rowPtr = _ptr8 + (nint)(_pitchPixels * y);

                    int x = x0;

                    if (parity == 0)
                    {
                        if (((xEndExclusive - x) & 1) != 0)
                        {
                            if ((x & 1) == 0)
                            {
                                DexterMemory.WriteUInt8(rowPtr + x, index);
                            }

                            x++;
                        }

                        while (xEndExclusive != x)
                        {
                            if ((x & 1) == 0)
                            {
                                DexterMemory.WriteUInt8(rowPtr + x, index);
                            }
                            else
                            {
                                DexterMemory.WriteUInt8(rowPtr + x + 1, index);
                            }

                            x += 2;
                        }
                    }
                    else
                    {
                        if (((xEndExclusive - x) & 1) != 0)
                        {
                            if ((x & 1) != 0)
                            {
                                DexterMemory.WriteUInt8(rowPtr + x, index);
                            }

                            x++;
                        }

                        while (xEndExclusive != x)
                        {
                            if ((x & 1) == 0)
                            {
                                DexterMemory.WriteUInt8(rowPtr + x + 1, index);
                            }
                            else
                            {
                                DexterMemory.WriteUInt8(rowPtr + x, index);
                            }

                            x += 2;
                        }
                    }

                    parity ^= 1;
                    y++;
                }
            }
        }

        // NXBasics::CBitmap::Draw_HorizontalLine(int, int, unsigned int, NXBasics::SColorRGB const&) const
        internal void Draw_HorizontalLine(int x, int y, uint width, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                uint v = CTrueColorCreator.GetTrueColorWord(CXBSystemManager.sTrueColorCreatorPtr, in color);
                Draw_HorizontalLine(x, y, width, v);
                return;
            }

            if (_bpp == 0x10)
            {
                ushort v = CHighColorCreator.GetHighColorWord(CXBSystemManager.sHighColorCreatorPtr, in color);
                Draw_HorizontalLine(x, y, width, v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette pal = _palette;
                if (pal == null) pal = CXBSystemManager.sPalettePtr;

                byte idx = CPalette.FindMatchingColor(pal, in color);
                SRectangle r = new SRectangle(x, y, width, 1);
                SRectangle tmp = new SRectangle(ref r);
                Draw_Box(ref tmp, idx);
            }
        }

        // NXBasics::CBitmap::Draw_VerticalLine(int, int, unsigned int, NXBasics::SColorRGB const&) const
        internal void Draw_VerticalLine(int x, int y, uint height, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                uint v = CTrueColorCreator.GetTrueColorWord(CXBSystemManager.sTrueColorCreatorPtr, in color);
                Draw_VerticalLine(x, y, height, v);
                return;
            }

            if (_bpp == 0x10)
            {
                ushort v = CHighColorCreator.GetHighColorWord(CXBSystemManager.sHighColorCreatorPtr, in color);
                Draw_VerticalLine(x, y, height, v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette pal = _palette;
                if (pal == null) pal = CXBSystemManager.sPalettePtr;

                byte idx = CPalette.FindMatchingColor(pal, in color);
                SRectangle r = new SRectangle(x, y, 1, height);
                SRectangle tmp = new SRectangle(ref r);
                Draw_Box(ref tmp, idx);
            }
        }

        // NXBasics::CBitmap::Draw_Line(int, int, int, int, unsigned char) const
        internal void Draw_Line(int x0, int y0, int x1, int y1, byte index)
        {
            if (_ptr8 == 0)
            {
                return;
            }

            if (!((-1 < x0) || (-1 < x1)) || !((-1 < y0) || (-1 < y1)))
            {
                return;
            }

            if (!((x0 <= _rect.Right) || (x1 <= _rect.Right)) || !((y0 <= _rect.Bottom) || (y1 <= _rect.Bottom)))
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

            Draw_SetPixel(x0, y0, index);

            if (absDx < absDy)
            {
                if (y1 != y0)
                {
                    int err = twiceAbsDx - (int)absDy;

                    while (y1 != y0)
                    {
                        int sub = 0;
                        if (-1 < err) sub = twiceAbsDy;

                        int addX = 0;
                        if (-1 < err) addX = stepX;

                        x0 += addX;
                        y0 += stepY;

                        err = (err + twiceAbsDx) - sub;

                        Draw_SetPixel(x0, y0, index);
                    }
                }
            }
            else
            {
                if (x1 != x0)
                {
                    int err = twiceAbsDy - (int)absDx;

                    while (x1 != x0)
                    {
                        int sub = 0;
                        if (-1 < err) sub = twiceAbsDx;

                        int addY = 0;
                        if (-1 < err) addY = stepY;

                        y0 += addY;
                        x0 += stepX;

                        err = (err + twiceAbsDy) - sub;

                        Draw_SetPixel(x0, y0, index);
                    }
                }
            }
        }

        // NXBasics::CBitmap::Draw_Line(int, int, int, int, unsigned short) const
        internal void Draw_Line(int x0, int y0, int x1, int y1, ushort value)
        {
            if (_ptr8 == 0)
            {
                return;
            }

            if (!((-1 < x0) || (-1 < x1)) || !((-1 < y0) || (-1 < y1)))
            {
                return;
            }

            if (!((x0 <= _rect.Right) || (x1 <= _rect.Right)) || !((y0 <= _rect.Bottom) || (y1 <= _rect.Bottom)))
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

            if (absDx < absDy)
            {
                int err = twiceAbsDx - absDy;
                int clipLeft = _rect.X;

                while (true)
                {
                    if ((_rect.X <= x0) && (x0 <= _rect.Right) && (_rect.Y <= y0) && (y0 <= _rect.Bottom) && (_bpp == 0x10))
                    {
                        DexterMemory.WriteUInt16(_ptr16 + (nint)(((x0 + (y0 * _pitchPixels)) * 2)), value);
                    }

                    if (y1 == y0)
                    {
                        return;
                    }

                    do
                    {
                        if (y1 == y0)
                        {
                            return;
                        }

                        int sub = 0;
                        if (-1 < err) sub = twiceAbsDy;

                        int addX = 0;
                        if (-1 < err) addX = stepX;

                        x0 += addX;
                        y0 += stepY;
                        err = (err + twiceAbsDx) - sub;
                    } while (x0 < clipLeft);
                }
            }
            else
            {
                int err = twiceAbsDy - absDx;
                long x = x0;

                while (true)
                {
                    if ((_rect.X <= x) && (x <= _rect.Right) && (_rect.Y <= y0) && (y0 <= _rect.Bottom) && (_bpp == 0x10))
                    {
                        DexterMemory.WriteUInt16(_ptr16 + (nint)((((int)x + (y0 * _pitchPixels)) * 2)), value);
                    }

                    if (x1 == x)
                    {
                        break;
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

        // NXBasics::CBitmap::Draw_Line(int, int, int, int, unsigned int) const
        internal void Draw_Line(int x0, int y0, int x1, int y1, uint value)
        {
            if (_ptr8 == 0)
            {
                return;
            }

            if (!((-1 < x0) || (-1 < x1)) || !((-1 < y0) || (-1 < y1)))
            {
                return;
            }

            if (!((x0 <= _rect.Right) || (x1 <= _rect.Right)) || !((y0 <= _rect.Bottom) || (y1 <= _rect.Bottom)))
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

            if (absDx < absDy)
            {
                int err = twiceAbsDx - absDy;
                int clipLeft = _rect.X;

                while (true)
                {
                    if ((x0 <= _rect.Right) && (_rect.Y <= y0) && (y0 <= _rect.Bottom) && (_bpp == 0x20) && (_rect.X <= x0))
                    {
                        DexterMemory.WriteUInt32(_ptr32 + (nint)(((x0 + (y0 * _pitchPixels)) * 4)), value);
                    }

                    if (y1 == y0)
                    {
                        return;
                    }

                    do
                    {
                        if (y1 == y0)
                        {
                            return;
                        }

                        int sub = 0;
                        if (-1 < err) sub = twiceAbsDy;

                        int addX = 0;
                        if (-1 < err) addX = stepX;

                        x0 += addX;
                        y0 += stepY;
                        err = (err + twiceAbsDx) - sub;
                    } while (x0 < clipLeft);
                }
            }
            else
            {
                int err = twiceAbsDy - absDx;
                long x = x0;

                while (true)
                {
                    if ((_rect.X <= x) && (x <= _rect.Right) && (_rect.Y <= y0) && (y0 <= _rect.Bottom) && (_bpp == 0x20))
                    {
                        DexterMemory.WriteUInt32(_ptr32 + (nint)((((int)x + (y0 * _pitchPixels)) * 4)), value);
                    }

                    if (x1 == x)
                    {
                        break;
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

        // NXBasics::CBitmap::Draw_Line(int, int, int, int, NXBasics::SColorRGB const&) const
        internal void Draw_Line(int x0, int y0, int x1, int y1, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                uint v = CTrueColorCreator.GetTrueColorWord(CXBSystemManager.sTrueColorCreatorPtr, in color);
                Draw_Line(x0, y0, x1, y1, v);
                return;
            }

            if (_bpp == 0x10)
            {
                ushort v = CHighColorCreator.GetHighColorWord(CXBSystemManager.sHighColorCreatorPtr, in color);
                Draw_Line(x0, y0, x1, y1, v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette pal = _palette;
                if (pal == null) pal = CXBSystemManager.sPalettePtr;

                byte idx = CPalette.FindMatchingColor(pal, in color);
                Draw_Line(x0, y0, x1, y1, idx);
            }
        }

        // NXBasics::CBitmap::Draw_StippledLine(int, int, int, int, unsigned char, unsigned int) const
        internal void Draw_StippledLine(int x0, int y0, int x1, int y1, byte index, uint period)
        {
            if (_ptr8 == 0)
            {
                return;
            }

            if (!((-1 < x0) || (-1 < x1)) || !((-1 < y0) || (-1 < y1)))
            {
                return;
            }

            if (!((x0 <= _rect.Right) || (x1 <= _rect.Right)) || !((y0 <= _rect.Bottom) || (y1 <= _rect.Bottom)))
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

                    counter++;
                    if (counter == period)
                    {
                        counter = 0;
                        skip = !skip;
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

                    counter++;
                    if (counter == period)
                    {
                        counter = 0;
                        skip = !skip;
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
            if (_ptr8 == 0)
            {
                return;
            }

            if (!((-1 < x0) || (-1 < x1)) || !((-1 < y0) || (-1 < y1)))
            {
                return;
            }

            if (!((x0 <= _rect.Right) || (x1 <= _rect.Right)) || !((y0 <= _rect.Bottom) || (y1 <= _rect.Bottom)))
            {
                return;
            }

            int clipRight = _rect.Right;
            int clipBottom = _rect.Bottom;

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

            if (absDx < absDy)
            {
                int err = twiceAbsDx - absDy;

                while (true)
                {
                    if (!skip)
                    {
                        if ((_rect.X <= x0) && (x0 <= clipRight) && (_rect.Y <= y0) && (y0 <= clipBottom) && (_bpp == 0x10))
                        {
                            DexterMemory.WriteUInt16(_ptr16 + (nint)(((x0 + (y0 * _pitchPixels)) * 2)), value);
                        }
                    }

                    if (y1 == y0)
                    {
                        break;
                    }

                    counter++;
                    if (counter == period)
                    {
                        counter = 0;
                        skip = !skip;
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
                            DexterMemory.WriteUInt16(_ptr16 + (nint)((((int)x + (y0 * _pitchPixels)) * 2)), value);
                        }
                    }

                    if (x1 == x)
                    {
                        break;
                    }

                    counter++;
                    if (counter == period)
                    {
                        counter = 0;
                        skip = !skip;
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
            if (_ptr8 == 0)
            {
                return;
            }

            if (!((-1 < x0) || (-1 < x1)) || !((-1 < y0) || (-1 < y1)))
            {
                return;
            }

            if (!((x0 <= _rect.Right) || (x1 <= _rect.Right)) || !((y0 <= _rect.Bottom) || (y1 <= _rect.Bottom)))
            {
                return;
            }

            int clipRight = _rect.Right;
            int clipBottom = _rect.Bottom;

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

            if (absDx < absDy)
            {
                int err = twiceAbsDx - absDy;

                while (true)
                {
                    if (!skip)
                    {
                        if ((_rect.X <= x0) && (x0 <= clipRight) && (_rect.Y <= y0) && (y0 <= clipBottom) && (_bpp == 0x20))
                        {
                            DexterMemory.WriteUInt32(_ptr32 + (nint)(((x0 + (y0 * _pitchPixels)) * 4)), value);
                        }
                    }

                    if (y1 == y0)
                    {
                        break;
                    }

                    counter++;
                    if (counter == period)
                    {
                        counter = 0;
                        skip = !skip;
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
                            DexterMemory.WriteUInt32(_ptr32 + (nint)((((int)x + (y0 * _pitchPixels)) * 4)), value);
                        }
                    }

                    if (x1 == x)
                    {
                        break;
                    }

                    counter++;
                    if (counter == period)
                    {
                        counter = 0;
                        skip = !skip;
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
                uint v = CTrueColorCreator.GetTrueColorWord(CXBSystemManager.sTrueColorCreatorPtr, in color);
                Draw_StippledLine(x0, y0, x1, y1, v, period);
                return;
            }

            if (_bpp == 0x10)
            {
                ushort v = CHighColorCreator.GetHighColorWord(CXBSystemManager.sHighColorCreatorPtr, in color);
                Draw_StippledLine(x0, y0, x1, y1, v, period);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette pal = _palette;
                if (pal == null) pal = CXBSystemManager.sPalettePtr;

                byte idx = CPalette.FindMatchingColor(pal, in color);
                Draw_StippledLine(x0, y0, x1, y1, idx, period);
            }
        }

        // NXBasics::CBitmap::Draw_Circle(int, int, unsigned int, unsigned char) const
        internal void Draw_Circle(int cx, int cy, uint radius, byte index)
        {
            if ((int)radius < 0)
            {
                return;
            }

            int decision = (int)(3 - (radius * 2));
            int xOff = 0;
            int yOff = 0;

            int r = (int)radius;

            do
            {
                Draw_SetPixel(cx + yOff, cy + r, index);
                Draw_SetPixel(cx + xOff, cy + r, index);
                Draw_SetPixel(cx + yOff, cy - r, index);
                Draw_SetPixel(cx + xOff, cy - r, index);

                Draw_SetPixel(cx - r, cy + xOff, index);
                Draw_SetPixel(cx + r, cy + xOff, index);
                Draw_SetPixel(cx - r, cy + yOff, index);
                Draw_SetPixel(cx + r, cy + yOff, index);

                int add = (int)(4 - (radius * 4));
                int nextR = r + ((decision < 0) ? 0 : -1);
                if (decision < 0)
                {
                    add = 0;
                }

                decision = decision + (xOff * 4) + add + 2;

                yOff -= 1;
                r = nextR;

                xOff += 1;
            }
            while (xOff < r);
        }

        // NXBasics::CBitmap::Draw_Circle(int, int, unsigned int, unsigned short) const
        internal void Draw_Circle(int cx, int cy, uint radius, ushort value)
        {
            if ((int)radius < 0)
            {
                return;
            }

            if (_ptr8 == 0)
            {
                return;
            }

            int decision = (int)(3 - (radius * 2));
            int step = 2;

            int yNeg = 0;
            int xPos = 0;

            long leftX = cx;

            int r = (int)radius;

            while (true)
            {
                PlotCirclePoints16(cx, cy, r, xPos, yNeg, value);

                int add = (int)(4 - (radius * 4));
                int nextR = r + ((decision < 0) ? 0 : -1);
                if (decision < 0)
                {
                    add = 0;
                }

                decision = add + decision + step;
                step += 4;

                leftX -= 1;
                yNeg -= 1;

                bool cont = xPos < nextR;
                xPos += 1;
                r = nextR;

                if (!cont)
                {
                    break;
                }
            }
        }

        // NXBasics::CBitmap::Draw_Circle(int, int, unsigned int, unsigned int) const
        internal void Draw_Circle(int cx, int cy, uint radius, uint value)
        {
            if ((int)radius < 0)
            {
                return;
            }

            if (_ptr8 == 0)
            {
                return;
            }

            int decision = (int)(3 - (radius * 2));
            int step = 2;

            int yNeg = 0;
            int xPos = 0;

            int r = (int)radius;

            while (true)
            {
                PlotCirclePoints32(cx, cy, r, xPos, yNeg, value);

                int add = (int)(4 - (radius * 4));
                int nextR = r + ((decision < 0) ? 0 : -1);
                if (decision < 0)
                {
                    add = 0;
                }

                decision = add + decision + step;
                step += 4;

                yNeg -= 1;

                bool cont = xPos < nextR;
                xPos += 1;
                r = nextR;

                if (!cont)
                {
                    break;
                }
            }
        }

        // NXBasics::CBitmap::Draw_Circle(int, int, unsigned int, NXBasics::SColorRGB const&) const
        internal void Draw_Circle(int cx, int cy, uint radius, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                uint v = CTrueColorCreator.GetTrueColorWord(CXBSystemManager.sTrueColorCreatorPtr, in color);
                Draw_Circle(cx, cy, radius, v);
                return;
            }

            if (_bpp == 0x10)
            {
                ushort v = CHighColorCreator.GetHighColorWord(CXBSystemManager.sHighColorCreatorPtr, in color);
                Draw_Circle(cx, cy, radius, v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette pal = _palette;
                if (pal == null) pal = CXBSystemManager.sPalettePtr;

                byte idx = CPalette.FindMatchingColor(pal, in color);
                Draw_Circle(cx, cy, radius, idx);
            }
        }

        // NXBasics::CBitmap::Draw_FilledCircle(int, int, unsigned int, unsigned char) const
        internal void Draw_FilledCircle(int cx, int cy, uint radius, byte index)
        {
            if ((int)radius < 0)
            {
                return;
            }

            int decision = (int)(3 - (radius * 2));
            int spanWidth = 0;

            int i = 0;
            int yOffsetNeg = -1;

            int leftX = cx;
            int topY = cy;

            uint colorIndex = index;

            int r = (int)radius;

            while (true)
            {
                int y = r + cy;
                SRectangle a = new SRectangle(leftX, y, (uint)spanWidth, 1);
                SRectangle tmp = new SRectangle(ref a);
                Draw_Box(ref tmp, (byte)colorIndex);

                y = cy - r;
                a = new SRectangle(leftX, y, (uint)spanWidth, 1);
                tmp = new SRectangle(ref a);
                Draw_Box(ref tmp, (byte)colorIndex);

                int xLeft = cx - r;
                int yLine = yOffsetNeg + cy + 1;
                a = new SRectangle(xLeft, yLine, (uint)(r * 2), 1);
                tmp = new SRectangle(ref a);
                Draw_Box(ref tmp, (byte)colorIndex);

                a = new SRectangle(xLeft, topY, (uint)(r * 2), 1);
                tmp = new SRectangle(ref a);
                Draw_Box(ref tmp, (byte)colorIndex);

                int add = (int)(4 - (radius * 4));
                int nextR = r + ((decision < 0) ? 0 : -1);
                if (decision < 0)
                {
                    add = 0;
                }

                decision = (i * 4) + add + decision + 2;
                i++;

                yOffsetNeg++;
                topY -= 1;
                leftX -= 1;
                spanWidth += 2;

                bool cont = yOffsetNeg < nextR;
                r = nextR;

                if (!cont)
                {
                    break;
                }
            }
        }

        // NXBasics::CBitmap::Draw_FilledCircle(int, int, unsigned int, unsigned short) const
        internal void Draw_FilledCircle(int cx, int cy, uint radius, ushort value)
        {
            if ((int)radius < 0)
            {
                return;
            }

            int decision = (int)(3 - (radius * 2));
            int yOffsetNeg = -1;

            uint spanWidth = 0;
            int i = 0;

            int leftX = cx;
            int topY = cy;

            int r = (int)radius;

            while (true)
            {
                Draw_HorizontalLine(leftX, r + cy, spanWidth, value);
                Draw_HorizontalLine(leftX, cy - r, spanWidth, value);

                Draw_HorizontalLine(cx - r, cy + yOffsetNeg + 1, (uint)(r * 2), value);
                Draw_HorizontalLine(cx - r, topY, (uint)(r * 2), value);

                int add = (int)(-(r * 4) + 4);
                int nextR = r + ((decision < 0) ? 0 : -1);
                if (decision < 0)
                {
                    add = 0;
                }

                decision = add + decision + (i * 4) + 2;
                i++;

                yOffsetNeg++;
                topY -= 1;
                leftX -= 1;

                spanWidth += 2;

                bool cont = yOffsetNeg < nextR;
                r = nextR;

                if (!cont)
                {
                    break;
                }
            }
        }

        // NXBasics::CBitmap::Draw_FilledCircle(int, int, unsigned int, unsigned int) const
        internal void Draw_FilledCircle(int cx, int cy, uint radius, uint value)
        {
            if ((int)radius < 0)
            {
                return;
            }

            int decision = (int)(3 - (radius * 2));
            int yOffsetNeg = -1;

            uint spanWidth = 0;
            int i = 0;

            int leftX = cx;
            int topY = cy;

            int r = (int)radius;

            while (true)
            {
                Draw_HorizontalLine(leftX, r + cy, spanWidth, value);
                Draw_HorizontalLine(leftX, cy - r, spanWidth, value);

                Draw_HorizontalLine(cx - r, cy + yOffsetNeg + 1, (uint)(r * 2), value);
                Draw_HorizontalLine(cx - r, topY, (uint)(r * 2), value);

                int add = (int)(-(r * 4) + 4);
                int nextR = r + ((decision < 0) ? 0 : -1);
                if (decision < 0)
                {
                    add = 0;
                }

                decision = add + decision + (i * 4) + 2;
                i++;

                yOffsetNeg++;
                topY -= 1;
                leftX -= 1;

                spanWidth += 2;

                bool cont = yOffsetNeg < nextR;
                r = nextR;

                if (!cont)
                {
                    break;
                }
            }
        }

        // NXBasics::CBitmap::Draw_FilledCircle(int, int, unsigned int, NXBasics::SColorRGB const&) const
        internal void Draw_FilledCircle(int cx, int cy, uint radius, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                uint v = CTrueColorCreator.GetTrueColorWord(CXBSystemManager.sTrueColorCreatorPtr, in color);
                Draw_FilledCircle(cx, cy, radius, v);
                return;
            }

            if (_bpp == 0x10)
            {
                ushort v = CHighColorCreator.GetHighColorWord(CXBSystemManager.sHighColorCreatorPtr, in color);
                Draw_FilledCircle(cx, cy, radius, v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette pal = _palette;
                if (pal == null) pal = CXBSystemManager.sPalettePtr;

                byte idx = CPalette.FindMatchingColor(pal, in color);
                Draw_FilledCircle(cx, cy, radius, idx);
            }
        }

        private void PlotCirclePoints16(int cx, int cy, int r, int xPos, int yNeg, ushort value)
        {
            if (_ptr8 == 0)
            {
                return;
            }

            int xA = cx;
            int xB = cx + xPos;

            int yTop = cy + r;
            int yBottom = cy - r;

            if ((_rect.X <= xA) && (xA <= _rect.Right) && (_rect.Y <= yTop) && (yTop <= _rect.Bottom) && (_bpp == 0x10))
            {
                DexterMemory.WriteUInt16(_ptr16 + (nint)(((xA + (yTop * _pitchPixels)) * 2)), value);
            }

            if ((_rect.X <= xB) && (xB <= _rect.Right) && (_rect.Y <= yTop) && (yTop <= _rect.Bottom) && (_bpp == 0x10))
            {
                DexterMemory.WriteUInt16(_ptr16 + (nint)(((xB + (yTop * _pitchPixels)) * 2)), value);
            }

            if ((_rect.X <= xA) && (xA <= _rect.Right) && (_rect.Y <= yBottom) && (yBottom <= _rect.Bottom) && (_bpp == 0x10))
            {
                DexterMemory.WriteUInt16(_ptr16 + (nint)(((xA + (yBottom * _pitchPixels)) * 2)), value);
            }

            if ((_rect.X <= xB) && (xB <= _rect.Right) && (_rect.Y <= yBottom) && (yBottom <= _rect.Bottom) && (_bpp == 0x10))
            {
                DexterMemory.WriteUInt16(_ptr16 + (nint)(((xB + (yBottom * _pitchPixels)) * 2)), value);
            }

            int xLeft = cx - r;
            int xRight = cx + r;

            int yA = cy + xPos;
            int yB = cy + yNeg;

            if ((_rect.X <= xLeft) && (xLeft <= _rect.Right) && (_rect.Y <= yA) && (yA <= _rect.Bottom) && (_bpp == 0x10))
            {
                DexterMemory.WriteUInt16(_ptr16 + (nint)(((xLeft + (yA * _pitchPixels)) * 2)), value);
            }

            if ((_rect.X <= xRight) && (xRight <= _rect.Right) && (_rect.Y <= yA) && (yA <= _rect.Bottom) && (_bpp == 0x10))
            {
                DexterMemory.WriteUInt16(_ptr16 + (nint)(((xRight + (yA * _pitchPixels)) * 2)), value);
            }

            if ((_rect.X <= xLeft) && (xLeft <= _rect.Right) && (_rect.Y <= yB) && (yB <= _rect.Bottom) && (_bpp == 0x10))
            {
                DexterMemory.WriteUInt16(_ptr16 + (nint)(((xLeft + (yB * _pitchPixels)) * 2)), value);
            }

            if ((_rect.X <= xRight) && (xRight <= _rect.Right) && (_rect.Y <= yB) && (yB <= _rect.Bottom) && (_bpp == 0x10))
            {
                DexterMemory.WriteUInt16(_ptr16 + (nint)(((xRight + (yB * _pitchPixels)) * 2)), value);
            }
        }

        private void PlotCirclePoints32(int cx, int cy, int r, int xPos, int yNeg, uint value)
        {
            if (_ptr8 == 0)
            {
                return;
            }

            int xA = cx;
            int xB = cx + xPos;

            int yTop = cy + r;
            int yBottom = cy - r;

            if ((_rect.X <= xA) && (xA <= _rect.Right) && (_rect.Y <= yTop) && (yTop <= _rect.Bottom) && (_bpp == 0x20))
            {
                DexterMemory.WriteUInt32(_ptr32 + (nint)(((xA + (yTop * _pitchPixels)) * 4)), value);
            }

            if ((_rect.X <= xB) && (xB <= _rect.Right) && (_rect.Y <= yTop) && (yTop <= _rect.Bottom) && (_bpp == 0x20))
            {
                DexterMemory.WriteUInt32(_ptr32 + (nint)(((xB + (yTop * _pitchPixels)) * 4)), value);
            }

            if ((_rect.X <= xA) && (xA <= _rect.Right) && (_rect.Y <= yBottom) && (yBottom <= _rect.Bottom) && (_bpp == 0x20))
            {
                DexterMemory.WriteUInt32(_ptr32 + (nint)(((xA + (yBottom * _pitchPixels)) * 4)), value);
            }

            if ((_rect.X <= xB) && (xB <= _rect.Right) && (_rect.Y <= yBottom) && (yBottom <= _rect.Bottom) && (_bpp == 0x20))
            {
                DexterMemory.WriteUInt32(_ptr32 + (nint)(((xB + (yBottom * _pitchPixels)) * 4)), value);
            }

            int xLeft = cx - r;
            int xRight = cx + r;

            int yA = cy + xPos;
            int yB = cy + yNeg;

            if ((_rect.X <= xLeft) && (xLeft <= _rect.Right) && (_rect.Y <= yA) && (yA <= _rect.Bottom) && (_bpp == 0x20))
            {
                DexterMemory.WriteUInt32(_ptr32 + (nint)(((xLeft + (yA * _pitchPixels)) * 4)), value);
            }

            if ((_rect.X <= xRight) && (xRight <= _rect.Right) && (_rect.Y <= yA) && (yA <= _rect.Bottom) && (_bpp == 0x20))
            {
                DexterMemory.WriteUInt32(_ptr32 + (nint)(((xRight + (yA * _pitchPixels)) * 4)), value);
            }

            if ((_rect.X <= xLeft) && (xLeft <= _rect.Right) && (_rect.Y <= yB) && (yB <= _rect.Bottom) && (_bpp == 0x20))
            {
                DexterMemory.WriteUInt32(_ptr32 + (nint)(((xLeft + (yB * _pitchPixels)) * 4)), value);
            }

            if ((_rect.X <= xRight) && (xRight <= _rect.Right) && (_rect.Y <= yB) && (yB <= _rect.Bottom) && (_bpp == 0x20))
            {
                DexterMemory.WriteUInt32(_ptr32 + (nint)(((xRight + (yB * _pitchPixels)) * 4)), value);
            }
        }

        // NXBasics::CBitmap::Text_Print(NXBasics::CFont const*, int, int, char const*) const
        internal void Text_Print(CFont font, int x, int y, string text)
        {
            if (text == null || font == null)
            {
                return;
            }

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
                    int advance;
                    if (_fixedXDistance != 0)
                    {
                        advance = _fixedXDistance;
                    }
                    else
                    {
                        advance = font.GetCharacterWidth('i');
                    }

                    cursorX += advance;
                    i++;
                    continue;
                }

                if (ch == '\n' || ch == '\r')
                {
                    if (ch == '\r')
                    {
                        if ((i + 1) < text.Length && text[i + 1] == '\n')
                        {
                            i += 2;
                        }
                        else
                        {
                            i += 1;
                        }
                    }
                    else
                    {
                        if ((i + 1) < text.Length && text[i + 1] == '\r')
                        {
                            i += 2;
                        }
                        else
                        {
                            i += 1;
                        }
                    }

                    cursorY += font.LineHeight + 2;
                    cursorX = x;
                    continue;
                }

                if (ch > (char)0x1F)
                {
                    int charWidth = font.GetCharacterWidth(ch);

                    if (_fixedXDistance == 0)
                    {
                        font.PrintCharacter(this, ch, cursorX, cursorY);
                        cursorX += charWidth;
                    }
                    else
                    {
                        int centeredX = cursorX + ((_fixedXDistance - charWidth) / 2);
                        font.PrintCharacter(this, ch, centeredX, cursorY);
                        cursorX += _fixedXDistance;
                    }
                }

                i++;
            }
        }

        // NXBasics::CBitmap::Text_PrintV(NXBasics::CFont const*, int, int, char const*, ...) const
        // Note: original is printf-style; keep this as a thin wrapper around your own formatter.
        internal void Text_PrintV(CFont font, int x, int y, string format, params object[] args)
        {
            if (format == null)
            {
                return;
            }

            string composed = DexterString.VSPrintf(format, args);
            Text_Print(font, x, y, composed);
        }

        // NXBasics::CBitmap::Text_Print(NXBasics::CFont const&, NXBasics::CPalette&, int, int, char const*) const
        internal void Text_Print(CFont font, CPalette palette, int x, int y, string text)
        {
            if (text == null || font == null)
            {
                return;
            }

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
                    int advance;
                    if (_fixedXDistance != 0)
                    {
                        advance = _fixedXDistance;
                    }
                    else
                    {
                        advance = font.GetCharacterWidth('i');
                    }

                    cursorX += advance;
                    i++;
                    continue;
                }

                if (ch == '\n' || ch == '\r')
                {
                    if (ch == '\r')
                    {
                        if ((i + 1) < text.Length && text[i + 1] == '\n')
                        {
                            i += 2;
                        }
                        else
                        {
                            i += 1;
                        }
                    }
                    else
                    {
                        if ((i + 1) < text.Length && text[i + 1] == '\r')
                        {
                            i += 2;
                        }
                        else
                        {
                            i += 1;
                        }
                    }

                    cursorY += font.LineHeight + 2;
                    cursorX = x;
                    continue;
                }

                if (ch > (char)0x1F)
                {
                    int charWidth = font.GetCharacterWidth(ch);

                    if (_fixedXDistance == 0)
                    {
                        font.PrintCharacter(palette, this, ch, cursorX, cursorY);
                        cursorX += charWidth;
                    }
                    else
                    {
                        int centeredX = cursorX + ((_fixedXDistance - charWidth) / 2);
                        font.PrintCharacter(palette, this, ch, centeredX, cursorY);
                        cursorX += _fixedXDistance;
                    }
                }

                i++;
            }
        }

        // NXBasics::CBitmap::Text_PrintV(NXBasics::CFont const&, NXBasics::CPalette&, int, int, char const*, ...) const
        internal void Text_PrintV(CFont font, CPalette palette, int x, int y, string format, params object[] args)
        {
            if (format == null)
            {
                return;
            }

            string composed = DexterString.VSPrintf(format, args);
            Text_Print(font, palette, x, y, composed);
        }

        // NXBasics::CBitmap::Text_Print(NXBasics::CFont const&, NXBasics::SColorRGB const&, int, int, char const*) const
        internal void Text_Print(CFont font, in SColorRGB color, int x, int y, string text)
        {
            if (text == null || font == null)
            {
                return;
            }

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
                    int advance;
                    if (_fixedXDistance != 0)
                    {
                        advance = _fixedXDistance;
                    }
                    else
                    {
                        advance = font.GetCharacterWidth('i');
                    }

                    cursorX += advance;
                    i++;
                    continue;
                }

                if (ch == '\n' || ch == '\r')
                {
                    if (ch == '\r')
                    {
                        if ((i + 1) < text.Length && text[i + 1] == '\n')
                        {
                            i += 2;
                        }
                        else
                        {
                            i += 1;
                        }
                    }
                    else
                    {
                        if ((i + 1) < text.Length && text[i + 1] == '\r')
                        {
                            i += 2;
                        }
                        else
                        {
                            i += 1;
                        }
                    }

                    cursorY += font.LineHeight + 2;
                    cursorX = x;
                    continue;
                }

                if (ch > (char)0x1F)
                {
                    int charWidth = font.GetCharacterWidth(ch);

                    if (_fixedXDistance == 0)
                    {
                        font.PrintCharacter(in color, this, ch, cursorX, cursorY);
                        cursorX += charWidth;
                    }
                    else
                    {
                        int centeredX = cursorX + ((_fixedXDistance - charWidth) / 2);
                        font.PrintCharacter(in color, this, ch, centeredX, cursorY);
                        cursorX += _fixedXDistance;
                    }
                }

                i++;
            }
        }

        // NXBasics::CBitmap::Text_PrintV(NXBasics::CFont const&, NXBasics::SColorRGB const&, int, int, char const*, ...) const
        internal void Text_PrintV(CFont font, in SColorRGB color, int x, int y, string format, params object[] args)
        {
            if (format == null)
            {
                return;
            }

            string composed = DexterString.VSPrintf(format, args);
            Text_Print(font, in color, x, y, composed);
        }

        // NXBasics::CBitmap::Text_GetPixelWidth(NXBasics::CFont const*, char const*) const
        internal uint Text_GetPixelWidth(CFont font, string text)
        {
            if (font == null)
            {
                return 0;
            }

            if (_fixedXDistance == 0)
            {
                return font.GetPixelWidth(text);
            }

            int len = DexterString.StringLength(text);
            long width = (long)len * (long)_fixedXDistance;
            if (width < 0)
            {
                return 0;
            }

            return (uint)width;
        }

        // NXBasics::CBitmap::Text_SetFixedXDistance(int)
        internal void Text_SetFixedXDistance(int fixedXDistance)
        {
            _fixedXDistance = fixedXDistance;
        }

        // NXBasics::CBitmap::SetPalettePtr(NXBasics::CPalette*)
        internal void SetPalettePtr(CPalette palette)
        {
            _palette = palette;
        }

        // NXBasics::CBitmap::ZoomIn() const
        //
        // The original no-arg ZoomIn() is a fixed zoom step.
        // Based on the decomp it behaves like a "center zoom" with an effective factor of ~1/4.
        // In the parameterized version, that corresponds well to factor = 64 (64/256 = 0.25).
        internal void ZoomIn()
        {
            ZoomIn(64);
        }

        // NXBasics::CBitmap::ZoomIn(unsigned int) const
        //
        // param_1 in [0..255]. 255 ~= almost no zoom (source close to edges), smaller values zoom further into the center.
        // The implementation matches the symmetry-based mapping seen in the decomp:
        // - Work from outer rows/cols towards the center
        // - Map destination positions to source positions around the center using (distance * factor) >> 8
        internal void ZoomIn(uint factor)
        {
            if (factor >= 0x100)
            {
                return;
            }

            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            if (Height <= 1)
            {
                return;
            }

            int halfWidth = Width / 2;
            int halfHeight = Height / 2;

            if (halfWidth <= 0 || halfHeight <= 0)
            {
                return;
            }

            int stride = StridePixels;
            if (stride <= 0)
            {
                stride = Width;
            }

            if (Format == BitmapFormat.TrueColor32)
            {
                if (_pixels32 == null)
                {
                    return;
                }

                uint[] src = (uint[])_pixels32.Clone();

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

                        _pixels32[topY * stride + leftX] = src[srcTopY * stride + srcLeftX];
                        _pixels32[topY * stride + rightX] = src[srcTopY * stride + srcRightX];
                        _pixels32[bottomY * stride + leftX] = src[srcBottomY * stride + srcLeftX];
                        _pixels32[bottomY * stride + rightX] = src[srcBottomY * stride + srcRightX];

                        leftX++;
                        rightX--;
                        xDist--;
                        xAcc -= factor;
                    }

                    topY++;
                    bottomY--;
                    yDist--;
                }

                return;
            }

            if (Format == BitmapFormat.HighColor16)
            {
                if (_pixels16 == null)
                {
                    return;
                }

                ushort[] src = (ushort[])_pixels16.Clone();

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

                        _pixels16[topY * stride + leftX] = src[srcTopY * stride + srcLeftX];
                        _pixels16[topY * stride + rightX] = src[srcTopY * stride + srcRightX];
                        _pixels16[bottomY * stride + leftX] = src[srcBottomY * stride + srcLeftX];
                        _pixels16[bottomY * stride + rightX] = src[srcBottomY * stride + srcRightX];

                        leftX++;
                        rightX--;
                        xDist--;
                        xAcc -= factor;
                    }

                    topY++;
                    bottomY--;
                    yDist--;
                }

                return;
            }

            if (Format == BitmapFormat.Indexed8)
            {
                if (_pixels8 == null)
                {
                    return;
                }

                byte[] src = (byte[])_pixels8.Clone();

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

                        _pixels8[topY * stride + leftX] = src[srcTopY * stride + srcLeftX];
                        _pixels8[topY * stride + rightX] = src[srcTopY * stride + srcRightX];
                        _pixels8[bottomY * stride + leftX] = src[srcBottomY * stride + srcLeftX];
                        _pixels8[bottomY * stride + rightX] = src[srcBottomY * stride + srcRightX];

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
        }

        // NXBasics::CBitmap::IsPointInside(NXBasics::SPoint const&) const
        //
        // The original just calls SPoint::IsInside(point, (SRectangle*)(this+0x0C)).
        // It does not return in the decomp, but the logical meaning is a boolean test.
        // In C#, we expose it as a bool.
        internal bool IsPointInside(in SPoint point)
        {
            return point.IsInside(_bounds);
        }

        // NXBasics::CBitmap::GetBoundingRectangle(unsigned char, NXBasics::SRectangle&) const
        //
        // Only works for 8bpp (format 0x08). It scans the whole bitmap and finds the min/max
        // extents of all pixels that are != transparentIndex. If none found -> returns false and rect=0.
        internal bool GetBoundingRectangle(byte transparentIndex, out SRectangle rect)
        {
            rect = new SRectangle(0, 0, 0, 0);

            if (Format != BitmapFormat.Indexed8)
            {
                return false;
            }

            if (Height <= 0 || Width <= 0)
            {
                return false;
            }

            if (_pixels8 == null)
            {
                return false;
            }

            int minX = 2000;
            int minY = 2000;
            int maxX = -2000;
            int maxY = -2000;

            bool found = false;

            int stride = StridePixels;
            if (stride <= 0)
            {
                stride = Width;
            }

            for (int y = 0; y < Height; y++)
            {
                int rowBase = y * stride;

                for (int x = 0; x < Width; x++)
                {
                    byte value = _pixels8[rowBase + x];
                    if (value == transparentIndex)
                    {
                        continue;
                    }

                    if (x <= minX)
                    {
                        minX = x;
                    }

                    if (y <= minY)
                    {
                        minY = y;
                    }

                    if (x >= maxX)
                    {
                        maxX = x;
                    }

                    if (y >= maxY)
                    {
                        maxY = y;
                    }

                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            rect.SetVariables(
                minX,
                minY,
                (maxX - minX) + 1,
                (maxY - minY) + 1);

            return true;
        }

        // NXBasics::CBitmap::Remap(NXBasics::CRemapTable const&, NXBasics::SRectangle) const
        //
        // Notes from the decomp:
        // - Requires pixel buffer (this+0x30 != 0).
        // - Cuts rect to bitmap bounds.
        // - 0x20: does a "darken" pass: pixel = (pixel >> 1) & 0x007F7F7F.
        // - 0x10: does a "darken" pass: pixel = (pixel >> 1) & mask(0x7F,0x7F,0x7F) from HighColorCreator.
        // - 0x08: uses remap table to remap indices in the rectangle.
        internal void Remap(CRemapTable remapTable, SRectangle rect)
        {
            // Equivalent to: if (*(long*)(this+0x30) == 0) return;
            if (!HasPixelBuffer())
            {
                return;
            }

            if (!rect.IsTouching(_bounds))
            {
                return;
            }

            rect.CutInside(_bounds);

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            if (Format == BitmapFormat.TrueColor32)
            {
                // Matches: *(uint*) = *(uint*) >> 1 & 0x7f7f7f
                int stride = StridePixels;
                int yStart = rect.Y;
                int yEndExclusive = rect.Y + rect.Height;

                int xStart = rect.X;
                int xEndExclusive = rect.X + rect.Width;

                for (int y = yStart; y < yEndExclusive; y++)
                {
                    int rowBase = y * stride;

                    for (int x = xStart; x < xEndExclusive; x++)
                    {
                        int index = rowBase + x;
                        uint value = _pixels32[index];
                        value = (value >> 1) & 0x007F7F7FU;
                        _pixels32[index] = value;
                    }
                }

                return;
            }

            if (Format == BitmapFormat.HighColor16)
            {
                IHighColorCreator highColorCreator = CXBSystemManager.HighColorCreator;
                if (highColorCreator == null)
                {
                    return;
                }

                ushort mask = highColorCreator.GetHighColorWord(0x7F, 0x7F, 0x7F);

                int stride = StridePixels;
                int yStart = rect.Y;
                int yEndExclusive = rect.Y + rect.Height;

                int xStart = rect.X;
                int xEndExclusive = rect.X + rect.Width;

                for (int y = yStart; y < yEndExclusive; y++)
                {
                    int rowBase = y * stride;

                    for (int x = xStart; x < xEndExclusive; x++)
                    {
                        int index = rowBase + x;
                        ushort value = _pixels16[index];
                        value = (ushort)(((uint)value >> 1) & mask);
                        _pixels16[index] = value;
                    }
                }

                return;
            }

            if (Format == BitmapFormat.Indexed8)
            {
                if (remapTable == null || remapTable.Table256 == null || remapTable.Table256.Length < 256)
                {
                    return;
                }

                byte[] table = remapTable.Table256;

                int stride = StridePixels;
                int yStart = rect.Y;
                int yEndExclusive = rect.Y + rect.Height;

                int xStart = rect.X;
                int width = rect.Width;

                // Equivalent to XB_Tool_Byte_RemapBlock(
                //   dst = pixels + rect.Y*stride + rect.X,
                //   table = remapTable + 8,
                //   width, height,
                //   rowSkip = stride - width);
                for (int y = yStart; y < yEndExclusive; y++)
                {
                    int rowBase = y * stride + xStart;

                    for (int x = 0; x < width; x++)
                    {
                        int index = rowBase + x;
                        byte src = _pixels8[index];
                        _pixels8[index] = table[src];
                    }
                }

                return;
            }
        }

        // NXBasics::CBitmap::Remap(NXBasics::CRemapTable const&, int, int, int, int) const
        internal void Remap(CRemapTable remapTable, int x, int y, int width, int height)
        {
            SRectangle rect = new(x, y, width, height);
            Remap(remapTable, rect);
        }

        private bool HasPixelBuffer()
        {
            if (Format == BitmapFormat.Indexed8)
            {
                return _pixels8 != null;
            }

            if (Format == BitmapFormat.HighColor16)
            {
                return _pixels16 != null;
            }

            if (Format == BitmapFormat.TrueColor32)
            {
                return _pixels32 != null;
            }

            return false;
        }

        // NXBasics::CBitmap::FilterColor(unsigned char, unsigned char) const
        //
        // Indexed8 only:
        // - For every pixel != fromColor, write toColor.
        // (So it "keeps" fromColor and turns everything else into toColor.)
        internal void FilterColor(byte fromColor, byte toColor)
        {
            if (Format != BitmapFormat.Indexed8)
            {
                return;
            }

            if (_pixels8 == null)
            {
                return;
            }

            int height = Height;
            int width = Width;
            int stride = StridePixels;

            if (height <= 0 || width <= 0)
            {
                return;
            }

            for (int y = 0; y < height; y++)
            {
                int rowBase = y * stride;

                for (int x = 0; x < width; x++)
                {
                    int index = rowBase + x;

                    if (_pixels8[index] != fromColor)
                    {
                        _pixels8[index] = toColor;
                    }
                }
            }
        }

        // NXBasics::CBitmap::ReplaceColor(unsigned char, unsigned char) const
        //
        // Indexed8 only:
        // - For every pixel == fromColor, write toColor.
        internal void ReplaceColor(byte fromColor, byte toColor)
        {
            if (Format != BitmapFormat.Indexed8)
            {
                return;
            }

            if (_pixels8 == null)
            {
                return;
            }

            int height = Height;
            int width = Width;
            int stride = StridePixels;

            if (height <= 0 || width <= 0)
            {
                return;
            }

            for (int y = 0; y < height; y++)
            {
                int rowBase = y * stride;

                for (int x = 0; x < width; x++)
                {
                    int index = rowBase + x;

                    if (_pixels8[index] == fromColor)
                    {
                        _pixels8[index] = toColor;
                    }
                }
            }
        }

        // NXBasics::CBitmap::GetUnclippedPixelPtr(int, int) const
        //
        // Returns a "pointer-like" location into the underlying buffer.
        // In managed code we return a byte offset into a backing byte[] that represents the pixel plane.
        // If you keep separate arrays per bpp (byte/ushort/uint), returning an index is more useful than an IntPtr.
        internal int GetUnclippedPixelPtr(int x, int y)
        {
            int stride = StridePixels;

            if (Format == BitmapFormat.TrueColor32)
            {
                return (y * stride) + x; // index into _pixels32
            }

            if (Format == BitmapFormat.HighColor16)
            {
                return (y * stride) + x; // index into _pixels16
            }

            if (Format == BitmapFormat.Indexed8)
            {
                return (y * stride) + x; // index into _pixels8
            }

            return 0;
        }

        // NXBasics::CBitmap::Tool_UseSourceAsMaskAndDarken(NXBasics::CBitmap const&, int, int, unsigned char) const
        //
        // Decomp behavior:
        // - this must be Indexed8 and have pixels (mask bitmap).
        // - destination bitmap (source in name / param_1 in decomp) must have pixels and be either 32-bit or 16-bit.
        // - Use this bitmap as a mask, positioned at (offsetX, offsetY) over destination.
        // - For each overlapped pixel: if maskPixel != transparentMaskColor => darken destination pixel.
        //
        // Darken:
        // - 32-bit: dst = (dst >> 1) & 0x007F7F7F
        // - 16-bit: dst = (dst >> 1) & mask(0x7F,0x7F,0x7F) using HighColorCreator
        internal void Tool_UseSourceAsMaskAndDarken(CBitmap destination, int offsetX, int offsetY, byte transparentMaskColor)
        {
            if (destination == null)
            {
                return;
            }

            if (Format != BitmapFormat.Indexed8)
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

            // Source rect is the destination canvas in decomp: (offsetX, offsetY, this.Width, this.Height)
            // Then it gets cut to destination bounds.
            SRectangle dstBounds = destination._bounds;
            SRectangle srcOverDst = new SRectangle(offsetX, offsetY, Width, Height);

            if (!srcOverDst.IsTouching(dstBounds))
            {
                return;
            }

            srcOverDst.CutInside(dstBounds);

            if (srcOverDst.Width <= 0 || srcOverDst.Height <= 0)
            {
                return;
            }

            // Compute where to start reading in the mask:
            // If the overlap starts at (srcOverDst.X, srcOverDst.Y) in destination space,
            // the corresponding mask coordinate is:
            // maskX = srcOverDst.X - offsetX
            // maskY = srcOverDst.Y - offsetY
            int maskStartX = srcOverDst.X - offsetX;
            int maskStartY = srcOverDst.Y - offsetY;

            int maskStride = StridePixels;
            int dstStride = destination.StridePixels;

            if (destination.Format == BitmapFormat.TrueColor32)
            {
                if (destination._pixels32 == null)
                {
                    return;
                }

                for (int row = 0; row < srcOverDst.Height; row++)
                {
                    int maskRowBase = (maskStartY + row) * maskStride + maskStartX;
                    int dstRowBase = (srcOverDst.Y + row) * dstStride + srcOverDst.X;

                    for (int col = 0; col < srcOverDst.Width; col++)
                    {
                        if (_pixels8[maskRowBase + col] != transparentMaskColor)
                        {
                            uint value = destination._pixels32[dstRowBase + col];
                            destination._pixels32[dstRowBase + col] = (value >> 1) & 0x007F7F7FU;
                        }
                    }
                }

                return;
            }

            if (destination.Format == BitmapFormat.HighColor16)
            {
                if (destination._pixels16 == null)
                {
                    return;
                }

                IHighColorCreator highColorCreator = CXBSystemManager.HighColorCreator;
                if (highColorCreator == null)
                {
                    return;
                }

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
            SRectangle rect = _bounds;
            Tool_Darken(ref rect);
        }

        // NXBasics::CBitmap::Tool_Darken(NXBasics::SRectangle) const
        internal void Tool_Darken(ref SRectangle rect)
        {
            Tool_Darken(ref rect, 0x7F, 0x7F, 0x7F);
        }

        // NXBasics::CBitmap::Tool_Darken(NXBasics::SRectangle, unsigned char, unsigned char, unsigned char) const
        internal void Tool_Darken(ref SRectangle rect, byte rMask, byte gMask, byte bMask)
        {
            if (!HasPixelBuffer())
            {
                return;
            }

            rect.Validate();

            if (!rect.IsTouching(_bounds))
            {
                return;
            }

            rect.CutInside(_bounds);

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            if (Format == BitmapFormat.TrueColor32)
            {
                if (_pixels32 == null)
                {
                    return;
                }

                ITrueColorCreator trueColorCreator = CXBSystemManager.TrueColorCreator;
                uint channelMask = 0x007F7F7FU;

                if (trueColorCreator != null)
                {
                    channelMask = trueColorCreator.GetTrueColorWord(rMask, gMask, bMask);
                }

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

            if (Format == BitmapFormat.HighColor16)
            {
                if (_pixels16 == null)
                {
                    return;
                }

                IHighColorCreator highColorCreator = CXBSystemManager.HighColorCreator;
                if (highColorCreator == null)
                {
                    return;
                }

                ushort channelMask = highColorCreator.GetHighColorWord(rMask, gMask, bMask);

                int stride = StridePixels;
                int startIndex = (rect.Y * stride) + rect.X;

                for (int y = 0; y < rect.Height; y++)
                {
                    int rowIndex = startIndex + (y * stride);

                    for (int x = 0; x < rect.Width; x++)
                    {
                        ushort value = _pixels16[rowIndex + x];
                        _pixels16[rowIndex + x] = (ushort)(((uint)value >> 1) & channelMask);
                    }
                }
            }
        }

        // NXBasics::CBitmap::Tool_InitializeAsVirtualBitmap(NXBasics::CBitmap const&, NXBasics::SRectangle const&)
        internal void Tool_InitializeAsVirtualBitmap(CBitmap source, in SRectangle sourceRect)
        {
            // Mirrors: memset(this+8, 0, 0x70), set tdepth = 8, bounds init, then construct virtual view.
            ResetCoreFields();

            Format = BitmapFormat.Indexed8;
            _bounds = new SRectangle(0, 0, 0, 0);

            ConstructVirtual(source, sourceRect);
        }

        private void ResetCoreFields()
        {
            // Keep this limited to fields that exist in our managed implementation.
            // The native code nukes a 0x70-byte chunk; here we reset what matters.
            _pixels8 = null;
            _pixels16 = null;
            _pixels32 = null;

            Width = 0;
            Height = 0;
            StridePixels = 0;

            _palettePtr = null;

            _bounds = new SRectangle(0, 0, 0, 0);
        }

        private void ConstructVirtual(CBitmap source, in SRectangle sourceRect)
        {
            if (source == null)
            {
                return;
            }

            SRectangle rect = sourceRect;
            rect.Validate();

            if (!rect.IsTouching(source._bounds))
            {
                return;
            }

            rect.CutInside(source._bounds);

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            // The original sets tdepth to 8 before calling into the constructor.
            // In practice, virtual bitmaps in this engine are used as 8bpp views.
            if (source.Format != BitmapFormat.Indexed8 || source._pixels8 == null)
            {
                return;
            }

            // Virtual view: share the same backing buffer, but with an origin offset.
            // We store the full buffer and an origin; drawing ops that use _pixels8 must respect _virtualOriginIndex.
            // If your pipeline already supports "base offset", keep this; otherwise, you can slice/copy.
            _pixels8 = source._pixels8;
            _virtualOriginIndex = (rect.Y * source.StridePixels) + rect.X;

            Format = BitmapFormat.Indexed8;
            Width = rect.Width;
            Height = rect.Height;
            StridePixels = source.StridePixels;

            _palettePtr = source._palettePtr;

            _bounds = new SRectangle(0, 0, rect.Width, rect.Height);
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

        // NXBasics::CBitmap::Storable_GetId() const
        internal static ulong Storable_GetId()
        {
            return 0x3F3UL;
        }

        // Notes:
        // - Tool_InitializeAsVirtualBitmap above assumes virtual bitmaps are 8bpp views (matches the decomp setting tdepth=0x8).
        // - If you need virtual views for 16/32-bit too, we can extend ConstructVirtual to branch on source.Format
        //   and store _virtualOriginIndex for _pixels16/_pixels32 accordingly.

        */
    }
}