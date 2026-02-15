using OpenVikings.NC2GuiToolsBase;
using OpenVikings.NXBasics.Structs;

namespace OpenVikings.NXBasics
{
    internal sealed class CBitmap : CStorable, IDisposable
    {
        #region Initialization and layout notes

        // --- Fields (layout-oriented, based on offsets in your dump) ---

        // Byte at +0x08 in the original object (written as 1 byte).
        private byte _bpp;

        // Block at +0x0C (0x10 bytes) written verbatim by Storable_SaveData.
        private readonly byte[] _storableBlock0C;

        // Storable at +0x28 in the original (saved via CStorable::Storable_Save).
        private readonly CStorable _pixelData;

        // +0x0C.. (SRectangle at +0x0C)
        private SRectangle _rect;

        // +0x28
        private CMemory? _memoryOwner; // owned memory (or null when view/external)

        // +0x48 / +0x4C / +0x50
        private int _pitchPixels;   // "width" or inherited pitch for virtual
        private int _bytesPerPixel; // 1/2/4
        private int _strideBytes;   // bytesPerPixel * pitchPixels

        // +0x54
        private byte _isVirtual; // 0/1 (byte in dump)

        // +0x58 / +0x5C / +0x60
        private int _virtualSrcX;
        private int _virtualSrcY;
        private CBitmap? _virtualParent;

        // +0x68
        private CPalette? _palette;
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

        private object? _palettePtr;

        // Managed backing buffers (you likely already have these in your reimplementation)
        private byte[]? _pixels8;
        private ushort[]? _pixels16;
        private uint[]? _pixels32;

        // --- Ctors / init ---

        // Ensure _storableBlock0C is initialized in every ctor.
        // Example:
        // internal CBitmap(CFile file) { _storableBlock0C = new byte[0x10]; ...; _pixelData = ...; }

        internal override uint Storable_GetId()
        {
            return 0x3F3u;
        }

        internal override void Storable_SaveData(CFile file)
        {
            // Mirrors:
            // CFile::Write(file, this + 8, 1);
            // CFile::Write(file, this + 0x0C, 0x10);
            // CStorable::Storable_Save(*(CStorable **)(this + 0x28), file);

            file.WriteByte(_bpp);

            if (_storableBlock0C.Length != 0x10)
            {
                throw new InvalidOperationException("CBitmap storable header block must be 0x10 bytes.");
            }

            file.Write(_storableBlock0C, 0, 0x10);

            _pixelData.Storable_Save(file);
        }

        internal byte[]? Pixels8 => _pixels8;

        internal BitmapFormat Format
        {
            get { return (BitmapFormat)_bpp; }
        }

        internal byte BitsPerPixel
        {
            get { return _bpp; }
        }


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
            _bpp = (byte)BitmapFormat.Indexed8;

            _memoryOwner = null;
            _externalBuffer = null;

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
            byte[] bppBuffer = new byte[1];
            int read = file.Read(bppBuffer, 1);
            if (read != 1)
            {
                throw new InvalidOperationException("Unexpected end of file while reading bpp.");
            }

            _bpp = bppBuffer[0];

            int x = unchecked((int)file.ReadLong());
            int y = unchecked((int)file.ReadLong());
            int w = unchecked((int)file.ReadLong());
            int h = unchecked((int)file.ReadLong());

            if (w < 0 || h < 0)
            {
                throw new InvalidOperationException($"Invalid bitmap size {w}x{h} read from file.");
            }

            // Keep SRectangle internal derived values consistent.
            _rect.SetVariables(x, y, w, h);

            CStorable loaded = NXBasicsApi.XB_Storable_LoadObject(file);
            if (loaded is not CMemory memory)
            {
                throw new InvalidOperationException($"Expected CMemory for bitmap backing store, got '{loaded.GetType().Name}'.");
            }

            _memoryOwner = memory;

            _pitchPixels = _rect.Width;

            if (_bpp == 0x20)
            {
                _bytesPerPixel = 4;
            }
            else if (_bpp == 0x10)
            {
                _bytesPerPixel = 2;
            }
            else
            {
                _bytesPerPixel = 1;
            }

            _strideBytes = checked(_bytesPerPixel * _pitchPixels);

            _isVirtual = 0x00;
            _virtualSrcX = 0;
            _virtualSrcY = 0;
            _virtualParent = null;

            L_SetRawMemoryPtr();
        }


        // NXBasics::CBitmap::~CBitmap()
        public void Dispose()
        {
            CMemory? owner = _memoryOwner;
            _memoryOwner = null;

            owner?.Dispose();

            _externalBuffer = null;
            _virtualParent = null;

            _pixels8 = null;
            _pixels16 = null;
            _pixels32 = null;
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

            L_SetRawMemoryPtr();
        }

        // NXBasics::CBitmap::L_ConstructVirtual(NXBasics::CBitmap const&, NXBasics::SRectangle const&)
        internal void L_ConstructVirtual(CBitmap source, in SRectangle requestedRect)
        {
            _isVirtual = 0x01;

            _memoryOwner = null;
            _externalBuffer = null;

            _virtualSrcX = 0;
            _virtualSrcY = 0;
            _virtualParent = null;

            _bpp = source._bpp;

            // Equivalent to: if (*(long*)(parent+0x30) != 0) ...
            // In managed code, "has pixel memory" means: parent can resolve a backing buffer view.
            if (!source.TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            SRectangle local = requestedRect;

            if (!local.IsTouching(source._rect))
            {
                return;
            }

            local.CutInside(source._rect);

            if (local.Width <= 0 || local.Height <= 0)
            {
                return;
            }

            _virtualSrcX = local.X;
            _virtualSrcY = local.Y;
            _virtualParent = source;

            // C++: SetVariables(thisRect, 0,0, local.w, local.h) + manual derived fields
            // Managed: SetVariables must keep derived values consistent.
            _rect.SetVariables(0, 0, local.Width, local.Height);

            // Inherit pitch/bytesPerPixel/stride from parent (C++ copies 0x48/0x4C/0x50).
            _pitchPixels = source._pitchPixels;
            _bytesPerPixel = source._bytesPerPixel;
            _strideBytes = source._strideBytes;

            L_SetRawMemoryPtr();
        }

        // NXBasics::CBitmap::L_ConstructExternalMemory(...)
        // NOTE: externalPtr here is "pointer-like"; map this to your actual raw memory system.
        // Expect: you have a way to pin/obtain nint from it. We call it ExternalMemory.GetRawPtr(externalPtr).
        internal void L_ConstructExternalMemory(uint width, uint height, byte bpp, byte[] externalPtr, uint pitchPixels)
        {
            _bpp = bpp;

            _memoryOwner = null;
            _externalBuffer = externalPtr;

            if (_bpp == 0x20)
            {
                _bytesPerPixel = 4;
            }
            else if (_bpp == 0x10)
            {
                _bytesPerPixel = 2;
            }
            else
            {
                _bytesPerPixel = 1;
            }

            _pitchPixels = checked((int)pitchPixels);
            _strideBytes = checked(_bytesPerPixel * _pitchPixels);

            _rect.SetVariables(0, 0, checked((int)width), checked((int)height));

            _isVirtual = 0x00;
            _virtualSrcX = 0;
            _virtualSrcY = 0;
            _virtualParent = null;

            L_SetRawMemoryPtr();
        }

        // NXBasics::CBitmap::L_SetRawMemoryPtr()
        internal void L_SetRawMemoryPtr()
        {
            if (_bpp == (byte)BitmapFormat.TrueColor32)
            {
                _bytesPerPixel = 4;
            }
            else if (_bpp == (byte)BitmapFormat.HighColor16)
            {
                _bytesPerPixel = 2;
            }
            else
            {
                _bytesPerPixel = 1;
                _bpp = (byte)BitmapFormat.Indexed8;
            }

            _strideBytes = checked(_bytesPerPixel * _pitchPixels);

            if (!TryGetPixelBuffer(out byte[]? buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel))
            {
                _pixels8 = null;
                _pixels16 = null;
                _pixels32 = null;
                return;
            }

            // Ensure internal layout matches the resolved buffer view.
            if (bytesPerPixel != _bytesPerPixel || pitchPixels != _pitchPixels)
            {
                _bytesPerPixel = bytesPerPixel;
                _pitchPixels = pitchPixels;
                _strideBytes = checked(_bytesPerPixel * _pitchPixels);
            }

            int requiredBytes = checked(_strideBytes * _rect.Height);
            int availableBytes = buffer.Length - baseOffsetBytes;

            if (availableBytes < requiredBytes)
            {
                throw new InvalidOperationException("CBitmap: backing buffer is smaller than expected for current layout.");
            }

            // Managed convenience views:
            // Only 8-bit can be represented as byte[] without unsafe.
            if (_bytesPerPixel == 1 && baseOffsetBytes == 0)
            {
                _pixels8 = buffer;
            }
            else
            {
                _pixels8 = null;
            }

            _pixels16 = null;
            _pixels32 = null;
        }

        // NXBasics::CBitmap::L_FindMatchingColor(NXBasics::SColorRGB const&) const
        internal bool TryGetPixelBuffer(out byte[]? buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel)
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
        internal ushort[] GetHighColorTablePtr()
        {
            CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;

            ushort[] table = palette.GetHighColorTablePtr();
            return table ?? throw new InvalidOperationException("HighColor table pointer is null.");
        }

        internal uint[] GetTrueColorTablePtr()
        {
            CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;

            uint[] table = palette.GetTrueColorTablePtr();
            return table ?? throw new InvalidOperationException("TrueColor table pointer is null.");
        }

        internal void SetPalettePtr(CPalette? palette)
        {
            _palette = palette;
        }

        #endregion

        #region NXBasics::CBitmap::Draw_GetPixel
        // NXBasics::CBitmap::Draw_GetPixel_Word(int, int) const
        internal ushort Draw_GetPixel_Word(int x, int y)
        {
            if (_bpp != (byte)BitmapFormat.HighColor16)
            {
                return 0;
            }

            if (!TryGetPixelBuffer(out byte[]? buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel))
            {
                return 0;
            }

            if (buffer == null || bytesPerPixel != 2)
            {
                return 0;
            }

            if ((uint)x >= (uint)_rect.Width || (uint)y >= (uint)_rect.Height)
            {
                return 0;
            }

            int pixelIndex = checked(y * pitchPixels + x);
            int byteIndex = checked(baseOffsetBytes + pixelIndex * 2);

            if ((uint)(byteIndex + 1) >= (uint)buffer.Length)
            {
                return 0;
            }

            return (ushort)(buffer[byteIndex + 0] | (buffer[byteIndex + 1] << 8));
        }

        // NXBasics::CBitmap::Draw_GetPixel_Long(int, int) const
        internal uint Draw_GetPixel_Long(int x, int y)
        {
            if (_bpp != (byte)BitmapFormat.TrueColor32)
            {
                return 0;
            }

            if (!TryGetPixelBuffer(out byte[]? buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel))
            {
                return 0;
            }

            if (buffer == null || bytesPerPixel != 4)
            {
                return 0;
            }

            if ((uint)x >= (uint)_rect.Width || (uint)y >= (uint)_rect.Height)
            {
                return 0;
            }

            int pixelIndex = checked(y * pitchPixels + x);
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
            if (_bpp != (byte)BitmapFormat.Indexed8)
            {
                return 0;
            }

            if (!TryGetPixelBuffer(out byte[]? buffer, out int baseOffsetBytes, out int pitchPixels, out int bytesPerPixel))
            {
                return 0;
            }

            if (buffer == null || bytesPerPixel != 1)
            {
                return 0;
            }

            if ((uint)x >= (uint)_rect.Width || (uint)y >= (uint)_rect.Height)
            {
                return 0;
            }

            int index = checked(baseOffsetBytes + y * pitchPixels + x);
            if ((uint)index >= (uint)buffer.Length)
            {
                return 0;
            }

            return buffer[index];
        }
        #endregion

        #region NXBasics::CBitmap::CopyIntoBitmap

        // NXBasics::CBitmap::CopyIntoBitmap(NXBasics::CBitmap const&, int, int) const
        internal void CopyIntoBitmap(CBitmap destination, int dstX, int dstY)
        {
            if (destination == null)
            {
                return;
            }

            // Special-case whole bitmap conversions (original checks rectangle equality only and ignores dstX/dstY).
            if (_bpp == (byte)BitmapFormat.HighColor16 && destination._bpp == (byte)BitmapFormat.TrueColor32)
            {
                if (RectEquals(in _rect, in destination._rect) && _rect.Height > 0)
                {
                    Convert16To32Whole(destination);
                }

                return;
            }

            if (_bpp == (byte)BitmapFormat.TrueColor32 && destination._bpp == (byte)BitmapFormat.HighColor16)
            {
                if (RectEquals(in _rect, in destination._rect) && _rect.Height > 0)
                {
                    Convert32To16Whole(destination);
                }

                return;
            }

            if (!TryGetPixelBuffer(out byte[]? srcBuf, out int srcBase, out int srcPitchPixels, out int srcBytesPerPixel))
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[]? dstBuf, out int dstBase, out int dstPitchPixels, out int dstBytesPerPixel))
            {
                return;
            }

            if (srcBuf == null || dstBuf == null)
            {
                return;
            }

            // Destination bounds: (0,0,width,height)
            SRectangle dstBounds = new();
            dstBounds.SetVariables(0, 0, destination._rect.Width, destination._rect.Height);

            // Placing rectangle where THIS bitmap would be drawn into destination.
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

            // Source start inside THIS bitmap (relative to placement).
            int srcX = placing.X - dstX;
            int srcY = placing.Y - dstY;

            // Destination start.
            int dstStartX = placing.X;
            int dstStartY = placing.Y;

            if (_bpp == (byte)BitmapFormat.TrueColor32 && destination._bpp == (byte)BitmapFormat.TrueColor32)
            {
                if (srcBytesPerPixel != 4 || dstBytesPerPixel != 4)
                {
                    return;
                }

                int srcOffset = checked(srcBase + checked((srcY * srcPitchPixels + srcX) * 4));
                int dstOffset = checked(dstBase + checked((dstStartY * dstPitchPixels + dstStartX) * 4));
                CopyBlockRaw(srcBuf, srcOffset, srcPitchPixels, dstBuf, dstOffset, dstPitchPixels, copyWidth, copyHeight, 4);
                return;
            }

            if (_bpp == (byte)BitmapFormat.HighColor16 && destination._bpp == (byte)BitmapFormat.HighColor16)
            {
                if (srcBytesPerPixel != 2 || dstBytesPerPixel != 2)
                {
                    return;
                }

                int srcOffset = checked(srcBase + checked((srcY * srcPitchPixels + srcX) * 2));
                int dstOffset = checked(dstBase + checked((dstStartY * dstPitchPixels + dstStartX) * 2));
                CopyBlockRaw(srcBuf, srcOffset, srcPitchPixels, dstBuf, dstOffset, dstPitchPixels, copyWidth, copyHeight, 2);
                return;
            }

            if (_bpp == (byte)BitmapFormat.Indexed8 && destination._bpp == (byte)BitmapFormat.Indexed8)
            {
                if (srcBytesPerPixel != 1 || dstBytesPerPixel != 1)
                {
                    return;
                }

                int srcOffset = checked(srcBase + (srcY * srcPitchPixels + srcX));
                int dstOffset = checked(dstBase + (dstStartY * dstPitchPixels + dstStartX));
                CopyBlockRaw(srcBuf, srcOffset, srcPitchPixels, dstBuf, dstOffset, dstPitchPixels, copyWidth, copyHeight, 1);
                return;
            }

            if (_bpp == (byte)BitmapFormat.Indexed8 && destination._bpp == (byte)BitmapFormat.TrueColor32)
            {
                if (srcBytesPerPixel != 1 || dstBytesPerPixel != 4)
                {
                    return;
                }

                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                uint[] table32 = palette.GetTrueColorTablePtr();
                if (table32 == null)
                {
                    return;
                }

                Convert8To32Block(srcBuf, srcBase, srcPitchPixels, srcX, srcY, dstBuf, dstBase, dstPitchPixels, dstStartX, dstStartY, copyWidth, copyHeight, table32);
                return;
            }

            if (_bpp == (byte)BitmapFormat.Indexed8 && destination._bpp == (byte)BitmapFormat.HighColor16)
            {
                if (srcBytesPerPixel != 1 || dstBytesPerPixel != 2)
                {
                    return;
                }

                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                ushort[] table16 = palette.GetHighColorTablePtr();
                if (table16 == null)
                {
                    return;
                }

                Convert8To16Block(srcBuf, srcBase, srcPitchPixels, srcX, srcY, dstBuf, dstBase, dstPitchPixels, dstStartX, dstStartY, copyWidth, copyHeight, table16);
                return;
            }
        }

        private static bool RectEquals(in SRectangle a, in SRectangle b)
        {
            return a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
        }

        private static void CopyBlockRaw(byte[] src, int srcOffsetBytes, int srcPitchPixels, byte[] dst, int dstOffsetBytes, int dstPitchPixels, int widthPixels, int heightPixels, int bytesPerPixel)
        {
            if (widthPixels <= 0 || heightPixels <= 0)
            {
                return;
            }

            if (bytesPerPixel != 1 && bytesPerPixel != 2 && bytesPerPixel != 4)
            {
                return;
            }

            int srcRowStrideBytes = checked(srcPitchPixels * bytesPerPixel);
            int dstRowStrideBytes = checked(dstPitchPixels * bytesPerPixel);
            int rowBytes = checked(widthPixels * bytesPerPixel);

            for (int y = 0; y < heightPixels; y++)
            {
                int s = checked(srcOffsetBytes + y * srcRowStrideBytes);
                int d = checked(dstOffsetBytes + y * dstRowStrideBytes);

                if ((uint)s >= (uint)src.Length || (uint)d >= (uint)dst.Length)
                {
                    return;
                }

                if (s + rowBytes > src.Length || d + rowBytes > dst.Length)
                {
                    return;
                }

                Buffer.BlockCopy(src, s, dst, d, rowBytes);
            }
        }

        private static void Convert8To16Block(byte[] src, int srcBase, int srcPitchPixels, int srcX, int srcY, byte[] dst, int dstBase, int dstPitchPixels, int dstX, int dstY, int width, int height, ushort[] table16)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitchPixels + srcX);
                int dstRow = checked(dstBase + checked(((dstY + y) * dstPitchPixels + dstX) * 2));

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];
                    ushort v = table16[idx];

                    int di = dstRow + x * 2;
                    dst[di + 0] = (byte)(v & 0xFF);
                    dst[di + 1] = (byte)((v >> 8) & 0xFF);
                }
            }
        }

        private static void Convert8To32Block(byte[] src, int srcBase, int srcPitchPixels, int srcX, int srcY, byte[] dst, int dstBase, int dstPitchPixels, int dstX, int dstY, int width, int height, uint[] table32)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitchPixels + srcX);
                int dstRow = checked(dstBase + checked(((dstY + y) * dstPitchPixels + dstX) * 4));

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];
                    uint v = table32[idx];

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
            if (destination == null)
            {
                return;
            }

            if (_bpp != (byte)BitmapFormat.HighColor16 || destination._bpp != (byte)BitmapFormat.TrueColor32)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[]? srcBuf, out int srcBase, out int srcPitchBytes, out int srcBytesPerPixel))
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[]? dstBuf, out int dstBase, out int dstPitchBytes, out int dstBytesPerPixel))
            {
                return;
            }

            if (srcBuf == null || dstBuf == null || srcBytesPerPixel != 2 || dstBytesPerPixel != 4)
            {
                return;
            }

            CHighColorCreator? highColorCreator = CXBSystemManager.sHighColorCreatorPtr;
            CTrueColorCreator? trueColorCreator = CXBSystemManager.sTrueColorCreatorPtr;

            if (highColorCreator == null || trueColorCreator == null)
            {
                return;
            }

            if (!highColorCreator.IsEnabled || !trueColorCreator.IsEnabled)
            {
                return;
            }

            uint[] table = highColorCreator.GetTrueColorConvertTablePtr(trueColorCreator);
            if (table.Length != 65536)
            {
                return;
            }

            int width = _rect.Width;
            int height = _rect.Height;

            if (destination._rect.Width < width)
            {
                width = destination._rect.Width;
            }

            if (destination._rect.Height < height)
            {
                height = destination._rect.Height;
            }

            int srcStrideBytes = srcPitchBytes;
            int dstStrideBytes = dstPitchBytes;

            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + y * srcStrideBytes);
                int dstRow = checked(dstBase + y * dstStrideBytes);

                for (int x = 0; x < width; x++)
                {
                    int si = checked(srcRow + x * 2);

                    ushort w = (ushort)(srcBuf[si + 0] | (srcBuf[si + 1] << 8));
                    uint color = table[w];

                    int di = checked(dstRow + x * 4);

                    // Destination is a raw 32-bit word (layout is defined by CTrueColorCreator masks).
                    dstBuf[di + 0] = (byte)(color & 0xFFu);
                    dstBuf[di + 1] = (byte)((color >> 8) & 0xFFu);
                    dstBuf[di + 2] = (byte)((color >> 16) & 0xFFu);
                    dstBuf[di + 3] = (byte)((color >> 24) & 0xFFu);
                }
            }
        }

        private void Convert32To16Whole(CBitmap destination)
        {
            if (_bpp != (byte)BitmapFormat.TrueColor32 || destination._bpp != (byte)BitmapFormat.HighColor16)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[]? srcBuf, out int srcBase, out int srcPitchPixels, out int srcBytesPerPixel))
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[]? dstBuf, out int dstBase, out int dstPitchPixels, out int dstBytesPerPixel))
            {
                return;
            }

            if (srcBuf == null || dstBuf == null || srcBytesPerPixel != 4 || dstBytesPerPixel != 2)
            {
                return;
            }

            CHighColorCreator highColorCreator = CXBSystemManager.sHighColorCreatorPtr;

            int width = _rect.Width;
            int height = _rect.Height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    uint c = ReadPixel32(srcBuf, srcBase, checked(srcPitchPixels * 4), x, y);

                    // Matches original call-site: GetHighColorWord(r,g,b) with r=(c>>16), g=(c>>8), b=(c>>0).
                    byte r = (byte)((c >> 16) & 0xFF);
                    byte g = (byte)((c >> 8) & 0xFF);
                    byte b = (byte)(c & 0xFF);

                    ushort w = highColorCreator.GetHighColorWord(r, g, b);
                    WritePixel16(dstBuf, dstBase, checked(dstPitchPixels * 2), x, y, w);
                }
            }
        }

        // --- 8-bit mapped blits (single implementation; duplicates removed) ---

        internal void CopyIntoBitmap_ColorKeyed(CBitmap destination, int dstX, int dstY, byte colorKey)
        {
            CopyIntoBitmap_8BitMappedCore(destination, dstX, dstY, true, colorKey, null, null);
        }

        internal void CopyIntoBitmap_ColorKeyed_Remap(CBitmap destination, int dstX, int dstY, byte colorKey, byte[] remap)
        {
            if (remap == null || remap.Length < 256)
            {
                return;
            }

            CopyIntoBitmap_8BitMappedCore(destination, dstX, dstY, true, colorKey, remap, null);
        }

        internal void CopyIntoBitmap_ColorKeyed_RemapTwice(CBitmap destination, int dstX, int dstY, byte colorKey, byte[] remapA, byte[] remapB)
        {
            if (remapA == null || remapA.Length < 256)
            {
                return;
            }

            if (remapB == null || remapB.Length < 256)
            {
                return;
            }

            CopyIntoBitmap_8BitMappedCore(destination, dstX, dstY, true, colorKey, remapA, remapB);
        }

        private void CopyIntoBitmap_8BitMappedCore(CBitmap destination, int dstX, int dstY, bool useColorKey, byte colorKey, byte[]? remapA, byte[]? remapB)
        {
            if (destination == null)
            {
                return;
            }

            if (_bpp != 0x08)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[]? srcBuf, out int srcBase, out int srcPitchPixels, out int srcBytesPerPixel))
            {
                return;
            }

            if (srcBuf == null || srcBytesPerPixel != 1)
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[]? dstBuf, out int dstBase, out int dstPitchPixels, out int dstBytesPerPixel))
            {
                return;
            }

            if (dstBuf == null)
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

            if (destination._bpp == 0x08)
            {
                if (dstBytesPerPixel != 1)
                {
                    return;
                }

                Blit8_MappedTo8(srcBuf, srcBase, srcPitchPixels, srcX, srcY,
                                dstBuf, dstBase, dstPitchPixels, dstStartX, dstStartY,
                                copyWidth, copyHeight, colorKey, useColorKey, remapA, remapB);
                return;
            }

            CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;

            if (destination._bpp == 0x10)
            {
                if (dstBytesPerPixel != 2)
                {
                    return;
                }

                ushort[] table16 = palette.GetHighColorTablePtr();
                if (table16 == null)
                {
                    return;
                }

                Blit8_MappedTo16(srcBuf, srcBase, srcPitchPixels, srcX, srcY,
                                 dstBuf, dstBase, dstPitchPixels, dstStartX, dstStartY,
                                 copyWidth, copyHeight, colorKey, useColorKey, remapA, remapB, table16);
                return;
            }

            if (destination._bpp == 0x20)
            {
                if (dstBytesPerPixel != 4)
                {
                    return;
                }

                uint[] table32 = palette.GetTrueColorTablePtr();
                if (table32 == null)
                {
                    return;
                }

                Blit8_MappedTo32(srcBuf, srcBase, srcPitchPixels, srcX, srcY,
                                 dstBuf, dstBase, dstPitchPixels, dstStartX, dstStartY,
                                 copyWidth, copyHeight, colorKey, useColorKey, remapA, remapB, table32);
            }
        }

        private static void Blit8_MappedTo8(byte[] src, int srcBase, int srcPitchPixels, int srcX, int srcY, byte[] dst, int dstBase, int dstPitchPixels, int dstX, int dstY, int width, int height, byte colorKey, bool useColorKey, byte[]? remapA, byte[]? remapB)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitchPixels + srcX);
                int dstRow = checked(dstBase + (dstY + y) * dstPitchPixels + dstX);

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];

                    if (useColorKey && idx == colorKey)
                    {
                        continue;
                    }

                    idx = ApplyRemap(idx, remapA, remapB);
                    dst[dstRow + x] = idx;
                }
            }
        }

        private static void Blit8_MappedTo16(byte[] src, int srcBase, int srcPitchPixels, int srcX, int srcY, byte[] dst, int dstBase, int dstPitchPixels, int dstX, int dstY, int width, int height, byte colorKey, bool useColorKey, byte[]? remapA, byte[]? remapB, ushort[] table16)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitchPixels + srcX);
                int dstRow = checked(dstBase + checked(((dstY + y) * dstPitchPixels + dstX) * 2));

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];

                    if (useColorKey && idx == colorKey)
                    {
                        continue;
                    }

                    idx = ApplyRemap(idx, remapA, remapB);
                    ushort v = table16[idx];

                    int di = dstRow + x * 2;
                    dst[di + 0] = (byte)(v & 0xFF);
                    dst[di + 1] = (byte)((v >> 8) & 0xFF);
                }
            }
        }

        private static void Blit8_MappedTo32(byte[] src, int srcBase, int srcPitchPixels, int srcX, int srcY, byte[] dst, int dstBase, int dstPitchPixels, int dstX, int dstY, int width, int height, byte colorKey, bool useColorKey, byte[]? remapA, byte[]? remapB, uint[] table32)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = checked(srcBase + (srcY + y) * srcPitchPixels + srcX);
                int dstRow = checked(dstBase + checked(((dstY + y) * dstPitchPixels + dstX) * 4));

                for (int x = 0; x < width; x++)
                {
                    byte idx = src[srcRow + x];

                    if (useColorKey && idx == colorKey)
                    {
                        continue;
                    }

                    idx = ApplyRemap(idx, remapA, remapB);
                    uint v = table32[idx];

                    int di = dstRow + x * 4;
                    dst[di + 0] = (byte)(v & 0xFF);
                    dst[di + 1] = (byte)((v >> 8) & 0xFF);
                    dst[di + 2] = (byte)((v >> 16) & 0xFF);
                    dst[di + 3] = (byte)((v >> 24) & 0xFF);
                }
            }
        }

        private static byte ApplyRemap(byte index, byte[]? remapA, byte[]? remapB)
        {
            if (remapA != null)
            {
                index = remapA[index];
            }

            if (remapB != null)
            {
                index = remapB[index];
            }

            return index;
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

            if (!TryGetPixelBuffer(out byte[]? srcBuf, out int srcBase, out int srcPitchPixels, out int srcBytesPerPixel))
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[]? dstBuf, out int dstBase, out int dstPitchPixels, out int dstBytesPerPixel))
            {
                return;
            }

            if (srcBuf == null || dstBuf == null)
            {
                return;
            }

            if (srcBytesPerPixel != dstBytesPerPixel)
            {
                return;
            }

            SRectangle dstBounds = new();
            dstBounds.SetVariables(0, 0, destination._rect.Width, destination._rect.Height);

            SRectangle placing = new();
            placing.SetVariables(dstX, dstY, _rect.Width / 2, _rect.Height / 2);

            if (!placing.IsTouching(dstBounds))
            {
                return;
            }

            SRectangle clipped = placing;
            clipped.CutInside(dstBounds);

            if (clipped.Width <= 0 || clipped.Height <= 0)
            {
                return;
            }

            // How much of the intended placement was clipped away on the left/top
            int clippedLeft = clipped.X - placing.X;
            int clippedTop = clipped.Y - placing.Y;

            // Source skip is *twice* that because of half-size sampling.
            int srcXSkip = checked(clippedLeft * 2);
            int srcYSkip = checked(clippedTop * 2);

            int bytesPerPixel = srcBytesPerPixel;

            if (_bpp == 0x20 && bytesPerPixel == 4)
            {
                CopyHalf_Generic(srcBuf, srcBase, srcPitchPixels, srcXSkip, srcYSkip, dstBuf, dstBase, dstPitchPixels, clipped.X, clipped.Y, clipped.Width, clipped.Height, 4);
                return;
            }

            if (_bpp == 0x10 && bytesPerPixel == 2)
            {
                CopyHalf_Generic(srcBuf, srcBase, srcPitchPixels, srcXSkip, srcYSkip, dstBuf, dstBase, dstPitchPixels, clipped.X, clipped.Y, clipped.Width, clipped.Height, 2);
                return;
            }

            if (_bpp == 0x08 && bytesPerPixel == 1)
            {
                CopyHalf_Generic(srcBuf, srcBase, srcPitchPixels, srcXSkip, srcYSkip, dstBuf, dstBase, dstPitchPixels, clipped.X, clipped.Y, clipped.Width, clipped.Height, 1);
            }
        }

        private static void CopyHalf_Generic(byte[] src, int srcBase, int srcPitchPixels, int srcXSkip, int srcYSkip, byte[] dst, int dstBase, int dstPitchPixels, int dstX, int dstY, int width, int height, int bytesPerPixel)
        {
            int srcStrideBytes = checked(srcPitchPixels * bytesPerPixel);
            int dstStrideBytes = checked(dstPitchPixels * bytesPerPixel);

            for (int y = 0; y < height; y++)
            {
                int sy = checked(srcYSkip + (y * 2));
                int srcRow = checked(srcBase + sy * srcStrideBytes);

                int dstRow = checked(dstBase + (dstY + y) * dstStrideBytes + dstX * bytesPerPixel);

                for (int x = 0; x < width; x++)
                {
                    int sx = checked(srcXSkip + (x * 2));
                    int si = checked(srcRow + sx * bytesPerPixel);
                    int di = checked(dstRow + x * bytesPerPixel);

                    if (bytesPerPixel == 1)
                    {
                        dst[di] = src[si];
                    }
                    else if (bytesPerPixel == 2)
                    {
                        dst[di + 0] = src[si + 0];
                        dst[di + 1] = src[si + 1];
                    }
                    else
                    {
                        dst[di + 0] = src[si + 0];
                        dst[di + 1] = src[si + 1];
                        dst[di + 2] = src[si + 2];
                        dst[di + 3] = src[si + 3];
                    }
                }
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

            if (!TryGetPixelBuffer(out byte[]? srcBuf, out int srcBase, out int srcPitchPixels, out int srcBytesPerPixel))
            {
                return;
            }

            if (srcBuf == null || srcBytesPerPixel != 1)
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[]? dstBuf, out int dstBase, out int dstPitchPixels, out int dstBytesPerPixel))
            {
                return;
            }

            if (dstBuf == null)
            {
                return;
            }

            uint srcXSkip = 0;

            SRectangle dstBounds = new();
            dstBounds.SetVariables(0, 0, destination._rect.Width, destination._rect.Height);

            SRectangle placing = new();
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

            int srcRow = checked(srcBase + checked((srcPitchPixels * ySkip + (int)srcXSkip) * 2));

            if (destination._bpp == 0x20)
            {
                if (dstBytesPerPixel != 4)
                {
                    return;
                }

                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                uint[] table32 = palette.GetTrueColorTablePtr();
                if (table32 == null)
                {
                    return;
                }

                int dstRow = checked(dstBase + checked(((placing.Y * dstPitchPixels) + placing.X) * 4));

                int row = ySkip;
                while (row < placing.Height)
                {
                    int srcCol = (xStart * 2) - (startX * 2);
                    int dx = (int)srcXSkip;

                    while (dx < placing.Width)
                    {
                        byte idx = srcBuf[checked(srcRow + srcCol)];
                        if (idx != colorKey)
                        {
                            uint color = table32[idx];
                            int di = checked(dstRow + checked(dx * 4));
                            dstBuf[di + 0] = (byte)(color & 0xFF);
                            dstBuf[di + 1] = (byte)((color >> 8) & 0xFF);
                            dstBuf[di + 2] = (byte)((color >> 16) & 0xFF);
                            dstBuf[di + 3] = (byte)((color >> 24) & 0xFF);
                        }

                        dx++;
                        srcCol += 2;
                    }

                    srcRow = checked(srcRow + (srcPitchPixels * 2));
                    dstRow = checked(dstRow + checked(dstPitchPixels * 4));
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

                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                ushort[] table16 = palette.GetHighColorTablePtr();
                if (table16 == null)
                {
                    return;
                }

                int dstRow = checked(dstBase + checked(((placing.Y * dstPitchPixels) + placing.X) * 2));

                int row = ySkip;
                while (row < placing.Height)
                {
                    int srcCol = (xStart * 2) - (startX * 2);
                    int dx = (int)srcXSkip;

                    while (dx < placing.Width)
                    {
                        byte idx = srcBuf[checked(srcRow + srcCol)];
                        if (idx != colorKey)
                        {
                            ushort w = table16[idx];
                            int di = checked(dstRow + checked(dx * 2));
                            dstBuf[di + 0] = (byte)(w & 0xFF);
                            dstBuf[di + 1] = (byte)((w >> 8) & 0xFF);
                        }

                        dx++;
                        srcCol += 2;
                    }

                    srcRow = checked(srcRow + (srcPitchPixels * 2));
                    dstRow = checked(dstRow + checked(dstPitchPixels * 2));
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

                int dstRow = checked(dstBase + (placing.Y * dstPitchPixels) + placing.X);

                int row = ySkip;
                while (row < placing.Height)
                {
                    int srcCol = (xStart * 2) - (startX * 2);
                    int dx = (int)srcXSkip;

                    while (dx < placing.Width)
                    {
                        byte idx = srcBuf[checked(srcRow + srcCol)];
                        if (idx != colorKey)
                        {
                            dstBuf[checked(dstRow + dx)] = idx;
                        }

                        dx++;
                        srcCol += 2;
                    }

                    srcRow = checked(srcRow + (srcPitchPixels * 2));
                    dstRow = checked(dstRow + dstPitchPixels);
                    row++;
                }
            }
        }

        // HalfSize remap wrappers
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
            if (destination == null)
            {
                return;
            }

            if (_bpp != 0x08)
            {
                return;
            }

            if (remapA == null || remapA.Length < 256)
            {
                return;
            }

            if (remapB != null && remapB.Length < 256)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[]? srcBuf, out int srcBase, out int srcPitchPixels, out int srcBytesPerPixel))
            {
                return;
            }

            if (srcBuf == null || srcBytesPerPixel != 1)
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[]? dstBuf, out int dstBase, out int dstPitchPixels, out int dstBytesPerPixel))
            {
                return;
            }

            if (dstBuf == null)
            {
                return;
            }

            uint srcXSkip = 0;

            SRectangle dstBounds = new(0, 0, destination._rect.Width, destination._rect.Height);

            SRectangle placing;
            if (longAlign)
            {
                int ax = dstX & ~1;
                int ay = dstY & ~1;

                placing = new SRectangle(ax, ay, _rect.Width / 2, _rect.Height / 2);

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

            int xStart = 0;
            if (0 < originalPlacingX)
            {
                xStart = originalPlacingX;
            }

            int srcColStart = (xStart * 2) - (originalPlacingX * 2);

            byte Map(byte idx)
            {
                if (remapB == null)
                {
                    return remapA[idx];
                }

                return remapB[remapA[idx]];
            }

            int srcRow = checked(srcBase + checked((srcPitchPixels * ySkip + (int)srcXSkip) * 2));

            if (dstBytesPerPixel == 4)
            {
                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                uint[] table32 = palette.GetTrueColorTablePtr();
                if (table32 == null)
                {
                    return;
                }

                int dstRow = checked(dstBase + checked(((placing.Y * dstPitchPixels) + placing.X) * 4));

                int row = ySkip;
                while (row < placing.Height)
                {
                    int srcCol = srcColStart;
                    int dx = (int)srcXSkip;

                    while (dx < placing.Width)
                    {
                        byte idx = srcBuf[checked(srcRow + srcCol)];
                        if (idx != colorKey)
                        {
                            uint color = table32[Map(idx)];
                            int di = checked(dstRow + checked(dx * 4));
                            dstBuf[di + 0] = (byte)(color & 0xFF);
                            dstBuf[di + 1] = (byte)((color >> 8) & 0xFF);
                            dstBuf[di + 2] = (byte)((color >> 16) & 0xFF);
                            dstBuf[di + 3] = (byte)((color >> 24) & 0xFF);
                        }

                        dx++;
                        srcCol += 2;
                    }

                    srcRow = checked(srcRow + (srcPitchPixels * 2));
                    dstRow = checked(dstRow + checked(dstPitchPixels * 4));
                    row++;
                }

                return;
            }

            if (dstBytesPerPixel == 2)
            {
                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                ushort[] table16 = palette.GetHighColorTablePtr();
                if (table16 == null)
                {
                    return;
                }

                int dstRow = checked(dstBase + checked(((placing.Y * dstPitchPixels) + placing.X) * 2));

                int row = ySkip;
                while (row < placing.Height)
                {
                    int srcCol = srcColStart;
                    int dx = (int)srcXSkip;

                    while (dx < placing.Width)
                    {
                        byte idx = srcBuf[checked(srcRow + srcCol)];
                        if (idx != colorKey)
                        {
                            ushort w = table16[Map(idx)];
                            int di = checked(dstRow + checked(dx * 2));
                            dstBuf[di + 0] = (byte)(w & 0xFF);
                            dstBuf[di + 1] = (byte)((w >> 8) & 0xFF);
                        }

                        dx++;
                        srcCol += 2;
                    }

                    srcRow = checked(srcRow + (srcPitchPixels * 2));
                    dstRow = checked(dstRow + checked(dstPitchPixels * 2));
                    row++;
                }

                return;
            }

            if (dstBytesPerPixel == 1)
            {
                int dstRow = checked(dstBase + (placing.Y * dstPitchPixels) + placing.X);

                int row = ySkip;
                while (row < placing.Height)
                {
                    int srcCol = srcColStart;
                    int dx = (int)srcXSkip;

                    while (dx < placing.Width)
                    {
                        byte idx = srcBuf[checked(srcRow + srcCol)];
                        if (idx != colorKey)
                        {
                            dstBuf[checked(dstRow + dx)] = Map(idx);
                        }

                        dx++;
                        srcCol += 2;
                    }

                    srcRow = checked(srcRow + (srcPitchPixels * 2));
                    dstRow = checked(dstRow + dstPitchPixels);
                    row++;
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

            if (srcW != dstW * 2 || srcH != dstH * 2)
            {
                CopyIntoBitmap_HalfSize(destination, 0, 0);
                return;
            }

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

            // Matches original constraints.
            if (((srcH & 1) != 0) || ((srcW & 0x3F) != 0))
            {
                CopyIntoBitmap_HalfSize(destination, 0, 0);
                return;
            }

            if (!TryGetPixelBuffer(out byte[]? srcBuf, out int srcBase, out int srcPitchPixels, out int srcBytesPerPixel))
            {
                CopyIntoBitmap_HalfSize(destination, 0, 0);
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[]? dstBuf, out int dstBase, out int dstPitchPixels, out int dstBytesPerPixel))
            {
                CopyIntoBitmap_HalfSize(destination, 0, 0);
                return;
            }

            if (srcBuf == null || dstBuf == null)
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

            int srcRowStrideBytes = checked(srcPitchPixels * bytesPerPixel);
            int dstRowStrideBytes = checked(dstPitchPixels * bytesPerPixel);

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

            if (!TryGetPixelBuffer(out byte[]? srcBuf, out int srcBase, out int srcPitchPixels, out int srcBytesPerPixel))
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[]? dstBuf, out int dstBase, out int dstPitchPixels, out int dstBytesPerPixel))
            {
                return;
            }

            if (srcBuf == null || dstBuf == null)
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
                if (destination._bpp != 0x20 || srcBytesPerPixel != 4 || dstBytesPerPixel != 4)
                {
                    return;
                }

                int srcRowStrideBytes = checked(srcPitchPixels * 4);
                int dstRowStrideBytes = checked(dstPitchPixels * 4);

                for (int y = 0; y < dstH; y++)
                {
                    int sy = (srcH * y) / dstH;

                    for (int x = 0; x < dstW; x++)
                    {
                        int sx = (srcW * x) / dstW;
                        uint pixel = ReadPixel32(srcBuf, srcBase, srcRowStrideBytes, sx, sy);
                        WritePixel32(dstBuf, dstBase, dstRowStrideBytes, x, y, pixel);
                    }
                }

                return;
            }

            if (_bpp == 0x10)
            {
                if (destination._bpp != 0x10 || srcBytesPerPixel != 2 || dstBytesPerPixel != 2)
                {
                    return;
                }

                int srcRowStrideBytes = checked(srcPitchPixels * 2);
                int dstRowStrideBytes = checked(dstPitchPixels * 2);

                for (int y = 0; y < dstH; y++)
                {
                    int sy = (srcH * y) / dstH;

                    for (int x = 0; x < dstW; x++)
                    {
                        int sx = (srcW * x) / dstW;
                        ushort pixel = ReadPixel16(srcBuf, srcBase, srcRowStrideBytes, sx, sy);
                        WritePixel16(dstBuf, dstBase, dstRowStrideBytes, x, y, pixel);
                    }
                }

                return;
            }

            if (_bpp != 0x08 || srcBytesPerPixel != 1)
            {
                return;
            }

            if (destination._bpp == 0x20)
            {
                if (dstBytesPerPixel != 4)
                {
                    return;
                }

                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                uint[] table32 = palette.GetTrueColorTablePtr();
                if (table32 == null)
                {
                    return;
                }

                int srcRowStrideBytes = srcPitchPixels;
                int dstRowStrideBytes = checked(dstPitchPixels * 4);

                for (int y = 0; y < dstH; y++)
                {
                    int sy = (srcH * y) / dstH;
                    int srcRow = checked(srcBase + sy * srcRowStrideBytes);

                    for (int x = 0; x < dstW; x++)
                    {
                        int sx = (srcW * x) / dstW;
                        int si = srcRow + sx;

                        if ((uint)si >= (uint)srcBuf.Length)
                        {
                            WritePixel32(dstBuf, dstBase, dstRowStrideBytes, x, y, 0);
                            continue;
                        }

                        uint color = table32[srcBuf[si]];
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

                CPalette palette = _palette ?? CXBSystemManager.sPalettePtr;
                ushort[] table16 = palette.GetHighColorTablePtr();
                if (table16 == null)
                {
                    return;
                }

                int srcRowStrideBytes = srcPitchPixels;
                int dstRowStrideBytes = checked(dstPitchPixels * 2);

                for (int y = 0; y < dstH; y++)
                {
                    int sy = (srcH * y) / dstH;
                    int srcRow = checked(srcBase + sy * srcRowStrideBytes);

                    for (int x = 0; x < dstW; x++)
                    {
                        int sx = (srcW * x) / dstW;
                        int si = srcRow + sx;

                        if ((uint)si >= (uint)srcBuf.Length)
                        {
                            WritePixel16(dstBuf, dstBase, dstRowStrideBytes, x, y, 0);
                            continue;
                        }

                        ushort color = table16[srcBuf[si]];
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

                int srcRowStrideBytes = srcPitchPixels;
                int dstRowStrideBytes = dstPitchPixels;

                for (int y = 0; y < dstH; y++)
                {
                    int sy = (srcH * y) / dstH;
                    int srcRow = checked(srcBase + sy * srcRowStrideBytes);
                    int dstRow = checked(dstBase + y * dstRowStrideBytes);

                    for (int x = 0; x < dstW; x++)
                    {
                        int sx = (srcW * x) / dstW;
                        int si = srcRow + sx;
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

        internal static void WritePixel32(byte[] buffer, int baseOffsetBytes, int rowStrideBytes, int x, int y, uint value)
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

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchBytes, out int bytesPerPixel))
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

                CPalette? pal = _palette ?? CXBSystemManager.sPalettePtr;
                if (pal == null)
                {
                    return;
                }

                uint color = pal.GetTrueColorWord(index);

                int bi = checked(baseOffsetBytes + relY * pitchBytes + relX * 4);
                if ((uint)(bi + 3) >= (uint)buffer.Length)
                {
                    return;
                }

                buffer[bi + 0] = (byte)(color & 0xFFu);
                buffer[bi + 1] = (byte)((color >> 8) & 0xFFu);
                buffer[bi + 2] = (byte)((color >> 16) & 0xFFu);
                buffer[bi + 3] = (byte)((color >> 24) & 0xFFu);
                return;
            }

            if (_bpp == 0x10)
            {
                if (bytesPerPixel != 2)
                {
                    return;
                }

                CPalette? pal = _palette ?? CXBSystemManager.sPalettePtr;
                if (pal == null)
                {
                    return;
                }

                ushort color = pal.GetHighColorWord(index);

                int bi = checked(baseOffsetBytes + relY * pitchBytes + relX * 2);
                if ((uint)(bi + 1) >= (uint)buffer.Length)
                {
                    return;
                }

                buffer[bi + 0] = (byte)(color & 0xFFu);
                buffer[bi + 1] = (byte)((color >> 8) & 0xFFu);
                return;
            }

            if (_bpp == 0x08)
            {
                if (bytesPerPixel != 1)
                {
                    return;
                }

                int bi = checked(baseOffsetBytes + relY * pitchBytes + relX);
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

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchBytes, out int bytesPerPixel))
            {
                return;
            }

            if (bytesPerPixel != 2)
            {
                return;
            }

            int relX = x - _rect.X;
            int relY = y - _rect.Y;

            int bi = checked(baseOffsetBytes + relY * pitchBytes + relX * 2);
            if ((uint)(bi + 1) >= (uint)buffer.Length)
            {
                return;
            }

            buffer[bi + 0] = (byte)(value & 0xFFu);
            buffer[bi + 1] = (byte)((value >> 8) & 0xFFu);
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

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchBytes, out int bytesPerPixel))
            {
                return;
            }

            if (bytesPerPixel != 4)
            {
                return;
            }

            int relX = x - _rect.X;
            int relY = y - _rect.Y;

            int bi = checked(baseOffsetBytes + relY * pitchBytes + relX * 4);
            if ((uint)(bi + 3) >= (uint)buffer.Length)
            {
                return;
            }

            buffer[bi + 0] = (byte)(value & 0xFFu);
            buffer[bi + 1] = (byte)((value >> 8) & 0xFFu);
            buffer[bi + 2] = (byte)((value >> 16) & 0xFFu);
            buffer[bi + 3] = (byte)((value >> 24) & 0xFFu);
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
                CTrueColorCreator? creator = CXBSystemManager.sTrueColorCreatorPtr;
                if (creator == null || !creator.IsEnabled)
                {
                    return;
                }

                uint v = creator.GetTrueColorWord(in color);
                Draw_SetPixel(x, y, v);
                return;
            }

            if (_bpp == 0x10)
            {
                CHighColorCreator? creator = CXBSystemManager.sHighColorCreatorPtr;
                if (creator == null || !creator.IsEnabled)
                {
                    return;
                }

                ushort v = creator.GetHighColorWord(color.R, color.G, color.B);
                Draw_SetPixel(x, y, v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette? pal = _palette ?? CXBSystemManager.sPalettePtr;
                if (pal == null)
                {
                    return;
                }

                byte idx = pal.FindMatchingColor(color.R, color.G, color.B);
                Draw_SetPixel(x, y, idx);
            }
        }

        // NXBasics::CBitmap::Draw_SetPixelUnclipped(int, int, NXBasics::SColorRGB const&) const
        internal void Draw_SetPixelUnclipped(int x, int y, in SColorRGB color)
        {
            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchBytes, out int bytesPerPixel))
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

                CTrueColorCreator? creator = CXBSystemManager.sTrueColorCreatorPtr;
                if (creator == null || !creator.IsEnabled)
                {
                    return;
                }

                uint v = creator.GetTrueColorWord(in color);

                int bi = checked(baseOffsetBytes + relY * pitchBytes + relX * 4);
                if ((uint)(bi + 3) >= (uint)buffer.Length)
                {
                    return;
                }

                buffer[bi + 0] = (byte)(v & 0xFFu);
                buffer[bi + 1] = (byte)((v >> 8) & 0xFFu);
                buffer[bi + 2] = (byte)((v >> 16) & 0xFFu);
                buffer[bi + 3] = (byte)((v >> 24) & 0xFFu);
                return;
            }

            if (_bpp == 0x10)
            {
                if (bytesPerPixel != 2)
                {
                    return;
                }

                CHighColorCreator? creator = CXBSystemManager.sHighColorCreatorPtr;
                if (creator == null || !creator.IsEnabled)
                {
                    return;
                }

                ushort v = creator.GetHighColorWord(color.R, color.G, color.B);

                int bi = checked(baseOffsetBytes + relY * pitchBytes + relX * 2);
                if ((uint)(bi + 1) >= (uint)buffer.Length)
                {
                    return;
                }

                buffer[bi + 0] = (byte)(v & 0xFFu);
                buffer[bi + 1] = (byte)((v >> 8) & 0xFFu);
                return;
            }

            if (_bpp == 0x08)
            {
                if (bytesPerPixel != 1)
                {
                    return;
                }

                CPalette? pal = _palette ?? CXBSystemManager.sPalettePtr;
                if (pal == null)
                {
                    return;
                }

                byte idx = pal.FindMatchingColor(color.R, color.G, color.B);

                int bi = checked(baseOffsetBytes + relY * pitchBytes + relX);
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
            if (x < _rect.X || y < _rect.Y)
            {
                return false;
            }

            int relX = x - _rect.X;
            int relY = y - _rect.Y;

            return (uint)relX < (uint)_rect.Width && (uint)relY < (uint)_rect.Height;
        }
        #endregion

        #region NXBasics::CBitmap::Draw_Box()

        // NXBasics::CBitmap::Draw_Box(NXBasics::SRectangle, unsigned char) const
        internal void Draw_Box(ref SRectangle rect, byte index)
        {
            rect.Validate();

            if (!rect.IsTouching(_rect))
            {
                return;
            }

            rect.CutInside(_rect);

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchBytes, out int bytesPerPixel))
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

                CPalette? pal = _palette ?? CXBSystemManager.sPalettePtr;
                if (pal == null)
                {
                    return;
                }

                uint color = pal.GetTrueColorWord(index);

                int start = checked(baseOffsetBytes + relY * pitchBytes + relX * 4);
                FillBlock32(buffer, start, pitchBytes, rect.Width, rect.Height, color);
                return;
            }

            if (_bpp == 0x10)
            {
                if (bytesPerPixel != 2)
                {
                    return;
                }

                CPalette? pal = _palette ?? CXBSystemManager.sPalettePtr;
                if (pal == null)
                {
                    return;
                }

                ushort color = pal.GetHighColorWord(index);

                int start = checked(baseOffsetBytes + relY * pitchBytes + relX * 2);
                FillBlock16(buffer, start, pitchBytes, rect.Width, rect.Height, color);
                return;
            }

            if (_bpp == 0x08)
            {
                if (bytesPerPixel != 1)
                {
                    return;
                }

                int start = checked(baseOffsetBytes + relY * pitchBytes + relX);
                FillBlock8(buffer, start, pitchBytes, rect.Width, rect.Height, index);
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

            if (!rect.IsTouching(_rect))
            {
                return;
            }

            rect.CutInside(_rect);

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchBytes, out int bytesPerPixel))
            {
                return;
            }

            if (bytesPerPixel != 2)
            {
                return;
            }

            int relX = rect.X - _rect.X;
            int relY = rect.Y - _rect.Y;

            int start = checked(baseOffsetBytes + relY * pitchBytes + relX * 2);
            FillBlock16(buffer, start, pitchBytes, rect.Width, rect.Height, value);
        }

        // NXBasics::CBitmap::Draw_Box(NXBasics::SRectangle, unsigned int) const
        internal void Draw_Box(ref SRectangle rect, uint value)
        {
            rect.Validate();

            if (_bpp != 0x20)
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

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffsetBytes, out int pitchBytes, out int bytesPerPixel))
            {
                return;
            }

            if (bytesPerPixel != 4)
            {
                return;
            }

            int relX = rect.X - _rect.X;
            int relY = rect.Y - _rect.Y;

            int start = checked(baseOffsetBytes + relY * pitchBytes + relX * 4);
            FillBlock32(buffer, start, pitchBytes, rect.Width, rect.Height, value);
        }

        // NXBasics::CBitmap::Draw_Box(NXBasics::SRectangle const&, NXBasics::SColorRGB const&) const
        internal void Draw_Box(in SRectangle input, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                CTrueColorCreator? creator = CXBSystemManager.sTrueColorCreatorPtr;
                if (creator == null || !creator.IsEnabled)
                {
                    return;
                }

                SRectangle r = input;
                uint v = creator.GetTrueColorWord(in color);
                Draw_Box(ref r, v);
                return;
            }

            if (_bpp == 0x10)
            {
                CHighColorCreator? creator = CXBSystemManager.sHighColorCreatorPtr;
                if (creator == null || !creator.IsEnabled)
                {
                    return;
                }

                SRectangle r = input;
                ushort v = creator.GetHighColorWord(color.R, color.G, color.B);
                Draw_Box(ref r, v);
                return;
            }

            if (_bpp == 0x08)
            {
                SRectangle r = input;

                CPalette? pal = _palette ?? CXBSystemManager.sPalettePtr;
                if (pal == null)
                {
                    return;
                }

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

            if (!rect.IsTouching(_rect))
            {
                return;
            }

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            SRectangle top = new(rect.X, rect.Y, rect.Width, 1);
            Draw_Box(ref top, index);

            SRectangle left = new(rect.X, rect.Y, 1, rect.Height);
            Draw_Box(ref left, index);

            if (rect.Height > 1)
            {
                SRectangle bottom = new(rect.X, checked(rect.Y + rect.Height - 1), rect.Width, 1);
                Draw_Box(ref bottom, index);
            }

            if (rect.Width > 1)
            {
                SRectangle right = new(checked(rect.X + rect.Width - 1), rect.Y, 1, rect.Height);
                Draw_Box(ref right, index);
            }
        }

        // NXBasics::CBitmap::Draw_Rectangle(NXBasics::SRectangle const&, NXBasics::SColorRGB const&) const
        internal void Draw_Rectangle(in SRectangle rect, in SColorRGB color)
        {
            SRectangle r = rect;

            if (_bpp == 0x20)
            {
                CTrueColorCreator? trueColorCreator = CXBSystemManager.sTrueColorCreatorPtr;
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
                CHighColorCreator? highColorCreator = CXBSystemManager.sHighColorCreatorPtr;
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
                CPalette? pal = _palette ?? CXBSystemManager.sPalettePtr;
                if (pal == null)
                {
                    return;
                }

                byte idx = pal.FindMatchingColor(color.R, color.G, color.B);
                Draw_Rectangle(ref r, idx);
            }
        }

        // NXBasics::CBitmap::Draw_Rectangle(NXBasics::SRectangle, unsigned short) const
        internal void Draw_Rectangle(ref SRectangle rect, ushort value)
        {
            rect.Validate();

            if (_bpp != 0x10)
            {
                return;
            }

            if (!rect.IsTouching(_rect))
            {
                return;
            }

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            Draw_HorizontalLine(rect.X, rect.Y, (uint)rect.Width, value);
            Draw_VerticalLine(rect.X, rect.Y, (uint)rect.Height, value);

            if (rect.Height > 1)
            {
                Draw_HorizontalLine(rect.X, checked(rect.Y + rect.Height - 1), (uint)rect.Width, value);
            }

            if (rect.Width > 1)
            {
                Draw_VerticalLine(checked(rect.X + rect.Width - 1), rect.Y, (uint)rect.Height, value);
            }
        }

        // NXBasics::CBitmap::Draw_Rectangle(NXBasics::SRectangle, unsigned int) const
        internal void Draw_Rectangle(ref SRectangle rect, uint value)
        {
            rect.Validate();

            if (_bpp != 0x20)
            {
                return;
            }

            if (!rect.IsTouching(_rect))
            {
                return;
            }

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            Draw_HorizontalLine(rect.X, rect.Y, (uint)rect.Width, value);
            Draw_VerticalLine(rect.X, rect.Y, (uint)rect.Height, value);

            if (rect.Height > 1)
            {
                Draw_HorizontalLine(rect.X, checked(rect.Y + rect.Height - 1), (uint)rect.Width, value);
            }

            if (rect.Width > 1)
            {
                Draw_VerticalLine(checked(rect.X + rect.Width - 1), rect.Y, (uint)rect.Height, value);
            }
        }

        #endregion

        #region FillBlock8/16/32
        private static void FillBlock8(byte[] buffer, int startIndex, int rowStrideBytes, int widthPixels, int heightPixels, byte value)
        {
            if (buffer == null)
            {
                return;
            }

            if (widthPixels <= 0 || heightPixels <= 0 || rowStrideBytes <= 0)
            {
                return;
            }

            int rowBytes = widthPixels;

            for (int y = 0; y < heightPixels; y++)
            {
                int rowStart = startIndex + y * rowStrideBytes;
                if ((uint)rowStart >= (uint)buffer.Length)
                {
                    continue;
                }

                int avail = buffer.Length - rowStart;
                int count = rowBytes;
                if (count > avail)
                {
                    count = avail;
                }

                buffer.AsSpan(rowStart, count).Fill(value);
            }
        }

        private static void FillBlock16(byte[] buffer, int startIndex, int rowStrideBytes, int widthPixels, int heightPixels, ushort value)
        {
            if (buffer == null)
            {
                return;
            }

            if (widthPixels <= 0 || heightPixels <= 0 || rowStrideBytes <= 0)
            {
                return;
            }

            int rowBytes = widthPixels * 2;

            byte lo = (byte)(value & 0xFFu);
            byte hi = (byte)((value >> 8) & 0xFFu);

            for (int y = 0; y < heightPixels; y++)
            {
                int rowStart = startIndex + y * rowStrideBytes;
                if ((uint)rowStart >= (uint)buffer.Length)
                {
                    continue;
                }

                int avail = buffer.Length - rowStart;
                int count = rowBytes;
                if (count > avail)
                {
                    count = avail;
                }

                count &= ~1;
                if (count <= 0)
                {
                    continue;
                }

                int end = rowStart + count;
                for (int i = rowStart; i < end; i += 2)
                {
                    buffer[i + 0] = lo;
                    buffer[i + 1] = hi;
                }
            }
        }

        private static void FillBlock32(byte[] buffer, int startIndex, int rowStrideBytes, int widthPixels, int heightPixels, uint value)
        {
            if (buffer == null)
            {
                return;
            }

            if (widthPixels <= 0 || heightPixels <= 0 || rowStrideBytes <= 0)
            {
                return;
            }

            int rowBytes = widthPixels * 4;

            byte b0 = (byte)(value & 0xFFu);
            byte b1 = (byte)((value >> 8) & 0xFFu);
            byte b2 = (byte)((value >> 16) & 0xFFu);
            byte b3 = (byte)((value >> 24) & 0xFFu);

            for (int y = 0; y < heightPixels; y++)
            {
                int rowStart = startIndex + y * rowStrideBytes;
                if ((uint)rowStart >= (uint)buffer.Length)
                {
                    continue;
                }

                int avail = buffer.Length - rowStart;
                int count = rowBytes;
                if (count > avail)
                {
                    count = avail;
                }

                count &= ~3;
                if (count <= 0)
                {
                    continue;
                }

                int end = rowStart + count;
                for (int i = rowStart; i < end; i += 4)
                {
                    buffer[i + 0] = b0;
                    buffer[i + 1] = b1;
                    buffer[i + 2] = b2;
                    buffer[i + 3] = b3;
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
            Draw_Box(ref tmp, value);
        }

        // NXBasics::CBitmap::Draw_StippleBox(NXBasics::SRectangle, unsigned char) const
        internal void Draw_StippleBox(ref SRectangle rect, byte index)
        {
            rect.Validate();

            if (!rect.IsTouching(_rect))
            {
                return;
            }

            rect.CutInside(_rect);

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            int x0 = rect.X;
            int y0 = rect.Y;

            int xEndExclusive = checked(x0 + rect.Width);
            int yEndExclusive = checked(y0 + rect.Height);

            if (_bpp == 0x20)
            {
                CPalette? palette = _palette ?? CXBSystemManager.sPalettePtr;
                if (palette == null)
                {
                    return;
                }

                uint color = palette.GetTrueColorWord(index);

                byte rowParity = (byte)(x0 & 1);
                for (int y = y0; y < yEndExclusive; y++)
                {
                    int parity = rowParity;
                    for (int x = x0; x < xEndExclusive; x++)
                    {
                        if (((x & 1) == 0) == (parity == 0))
                        {
                            Draw_SetPixel(x, y, color);
                        }
                    }

                    rowParity ^= 1;
                }

                return;
            }

            if (_bpp == 0x10)
            {
                CPalette? palette = _palette ?? CXBSystemManager.sPalettePtr;
                if (palette == null)
                {
                    return;
                }

                ushort color = palette.GetHighColorWord(index);

                byte rowParity = (byte)(x0 & 1);
                for (int y = y0; y < yEndExclusive; y++)
                {
                    int parity = rowParity;
                    for (int x = x0; x < xEndExclusive; x++)
                    {
                        if (((x & 1) == 0) == (parity == 0))
                        {
                            Draw_SetPixel(x, y, color);
                        }
                    }

                    rowParity ^= 1;
                }

                return;
            }

            if (_bpp == 0x08)
            {
                byte rowParity = (byte)(x0 & 1);
                for (int y = y0; y < yEndExclusive; y++)
                {
                    int parity = rowParity;
                    for (int x = x0; x < xEndExclusive; x++)
                    {
                        if (((x & 1) == 0) == (parity == 0))
                        {
                            Draw_SetPixel(x, y, index);
                        }
                    }

                    rowParity ^= 1;
                }
            }
        }

        // NXBasics::CBitmap::Draw_HorizontalLine(int, int, unsigned int, NXBasics::SColorRGB const&) const
        internal void Draw_HorizontalLine(int x, int y, uint width, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                CTrueColorCreator? creator = CXBSystemManager.sTrueColorCreatorPtr;
                if (creator == null || !creator.IsEnabled)
                {
                    return;
                }

                uint v = creator.GetTrueColorWord(in color);
                Draw_HorizontalLine(x, y, width, v);
                return;
            }

            if (_bpp == 0x10)
            {
                CHighColorCreator? creator = CXBSystemManager.sHighColorCreatorPtr;
                if (creator == null || !creator.IsEnabled)
                {
                    return;
                }

                ushort v = creator.GetHighColorWord(color.R, color.G, color.B);
                Draw_HorizontalLine(x, y, width, v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette? palette = _palette ?? CXBSystemManager.sPalettePtr;
                if (palette == null)
                {
                    return;
                }

                byte idx = palette.FindMatchingColor(color.R, color.G, color.B);
                Draw_HorizontalLine(x, y, width, idx);
            }
        }

        // NXBasics::CBitmap::Draw_VerticalLine(int, int, unsigned int, NXBasics::SColorRGB const&) const
        internal void Draw_VerticalLine(int x, int y, uint height, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                CTrueColorCreator? creator = CXBSystemManager.sTrueColorCreatorPtr;
                if (creator == null || !creator.IsEnabled)
                {
                    return;
                }

                uint v = creator.GetTrueColorWord(in color);
                Draw_VerticalLine(x, y, height, v);
                return;
            }

            if (_bpp == 0x10)
            {
                CHighColorCreator? creator = CXBSystemManager.sHighColorCreatorPtr;
                if (creator == null || !creator.IsEnabled)
                {
                    return;
                }

                ushort v = creator.GetHighColorWord(color.R, color.G, color.B);
                Draw_VerticalLine(x, y, height, v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette? palette = _palette ?? CXBSystemManager.sPalettePtr;
                if (palette == null)
                {
                    return;
                }

                byte idx = palette.FindMatchingColor(color.R, color.G, color.B);
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

            int dx = x1 - x0;
            int dy = y1 - y0;

            int stepX = dx != 0 ? (dx > 0 ? 1 : -1) : 0;
            int stepY = dy != 0 ? (dy > 0 ? 1 : -1) : 0;

            int absDx = dx < 0 ? -dx : dx;
            int absDy = dy < 0 ? -dy : dy;

            int twiceAbsDx = absDx * 2;
            int twiceAbsDy = absDy * 2;

            Draw_SetPixel(x0, y0, index);

            if (absDx < absDy)
            {
                int err = twiceAbsDx - absDy;

                while (y0 != y1)
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
                int err = twiceAbsDy - absDx;

                while (x0 != x1)
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

            int dx = x1 - x0;
            int dy = y1 - y0;

            int stepX = dx != 0 ? (dx > 0 ? 1 : -1) : 0;
            int stepY = dy != 0 ? (dy > 0 ? 1 : -1) : 0;

            int absDx = dx < 0 ? -dx : dx;
            int absDy = dy < 0 ? -dy : dy;

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

                    if (y0 == y1)
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

                    if (x0 == x1)
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

            int dx = x1 - x0;
            int dy = y1 - y0;

            int stepX = dx != 0 ? (dx > 0 ? 1 : -1) : 0;
            int stepY = dy != 0 ? (dy > 0 ? 1 : -1) : 0;

            int absDx = dx < 0 ? -dx : dx;
            int absDy = dy < 0 ? -dy : dy;

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

                    if (y0 == y1)
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

                    if (x0 == x1)
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

        // NXBasics::CBitmap::Draw_Line(int, int, int, int, NXBasics::SColorRGB const&) const
        internal void Draw_Line(int x0, int y0, int x1, int y1, in SColorRGB color)
        {
            if (_bpp == 0x20)
            {
                CTrueColorCreator? creator = CXBSystemManager.sTrueColorCreatorPtr;
                if (creator == null || !creator.IsEnabled)
                {
                    return;
                }

                uint v = creator.GetTrueColorWord(in color);
                Draw_Line(x0, y0, x1, y1, v);
                return;
            }

            if (_bpp == 0x10)
            {
                CHighColorCreator? creator = CXBSystemManager.sHighColorCreatorPtr;
                if (creator == null || !creator.IsEnabled)
                {
                    return;
                }

                ushort v = creator.GetHighColorWord(color.R, color.G, color.B);
                Draw_Line(x0, y0, x1, y1, v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette? palette = _palette ?? CXBSystemManager.sPalettePtr;
                if (palette == null)
                {
                    return;
                }

                byte idx = palette.FindMatchingColor(color.R, color.G, color.B);
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

            if (rect.Width <= 0 || rect.Height <= 0)
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
            int left = _rect.X;
            int top = _rect.Y;
            int rightExclusive = checked(left + _rect.Width);
            int bottomExclusive = checked(top + _rect.Height);

            if (x0 < left && x1 < left)
            {
                return false;
            }

            if (y0 < top && y1 < top)
            {
                return false;
            }

            if (x0 >= rightExclusive && x1 >= rightExclusive)
            {
                return false;
            }

            if (y0 >= bottomExclusive && y1 >= bottomExclusive)
            {
                return false;
            }

            return true;
        }

        private bool IsInsideRect(int x, int y)
        {
            if (x < _rect.X || y < _rect.Y)
            {
                return false;
            }

            int relX = x - _rect.X;
            int relY = y - _rect.Y;

            return (uint)relX < (uint)_rect.Width && (uint)relY < (uint)_rect.Height;
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

            if (!IsLinePossiblyVisible(x0, y0, x1, y1))
            {
                return;
            }

            int dx = x1 - x0;
            int dy = y1 - y0;

            int stepX = dx != 0 ? (dx > 0 ? 1 : -1) : 0;
            int stepY = dy != 0 ? (dy > 0 ? 1 : -1) : 0;

            int absDx = dx < 0 ? -dx : dx;
            int absDy = dy < 0 ? -dy : dy;

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
                        Draw_SetPixel(x0, y0, index);
                    }

                    if (y0 == y1)
                    {
                        return;
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
                    if (!skip)
                    {
                        Draw_SetPixel(x0, y0, index);
                    }

                    if (x0 == x1)
                    {
                        return;
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

                    int sub = (-1 < err) ? twiceAbsDx : 0;
                    int addY = (-1 < err) ? stepY : 0;

                    y0 += addY;
                    x0 += stepX;

                    err = (err + twiceAbsDy) - sub;
                }
            }
        }

        // NXBasics::CBitmap::Draw_StippledLine(int, int, int, int, unsigned short, unsigned int) const
        internal void Draw_StippledLine(int x0, int y0, int x1, int y1, ushort value, uint period)
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

            int dx = x1 - x0;
            int dy = y1 - y0;

            int stepX = dx != 0 ? (dx > 0 ? 1 : -1) : 0;
            int stepY = dy != 0 ? (dy > 0 ? 1 : -1) : 0;

            int absDx = dx < 0 ? -dx : dx;
            int absDy = dy < 0 ? -dy : dy;

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
                        if (IsInsideRect(x0, y0))
                        {
                            Draw_SetPixel(x0, y0, value);
                        }
                    }

                    if (y0 == y1)
                    {
                        return;
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
                    if (!skip)
                    {
                        if (IsInsideRect(x0, y0))
                        {
                            Draw_SetPixel(x0, y0, value);
                        }
                    }

                    if (x0 == x1)
                    {
                        return;
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

                    int sub = (-1 < err) ? twiceAbsDx : 0;
                    int addY = (-1 < err) ? stepY : 0;

                    y0 += addY;
                    x0 += stepX;

                    err = (err + twiceAbsDy) - sub;
                }
            }
        }

        // NXBasics::CBitmap::Draw_StippledLine(int, int, int, int, unsigned int, unsigned int) const
        internal void Draw_StippledLine(int x0, int y0, int x1, int y1, uint value, uint period)
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

            int dx = x1 - x0;
            int dy = y1 - y0;

            int stepX = dx != 0 ? (dx > 0 ? 1 : -1) : 0;
            int stepY = dy != 0 ? (dy > 0 ? 1 : -1) : 0;

            int absDx = dx < 0 ? -dx : dx;
            int absDy = dy < 0 ? -dy : dy;

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
                        if (IsInsideRect(x0, y0))
                        {
                            Draw_SetPixel(x0, y0, value);
                        }
                    }

                    if (y0 == y1)
                    {
                        return;
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
                    if (!skip)
                    {
                        if (IsInsideRect(x0, y0))
                        {
                            Draw_SetPixel(x0, y0, value);
                        }
                    }

                    if (x0 == x1)
                    {
                        return;
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

                    int sub = (-1 < err) ? twiceAbsDx : 0;
                    int addY = (-1 < err) ? stepY : 0;

                    y0 += addY;
                    x0 += stepX;

                    err = (err + twiceAbsDy) - sub;
                }
            }
        }

        // NXBasics::CBitmap::Draw_StippledLine(int, int, int, int, NXBasics::SColorRGB const&, unsigned int) const
        internal void Draw_StippledLine(int x0, int y0, int x1, int y1, in SColorRGB color, uint period)
        {
            if (_bpp == 0x20)
            {
                CTrueColorCreator? creator = CXBSystemManager.sTrueColorCreatorPtr;
                if (creator == null || !creator.IsEnabled)
                {
                    return;
                }

                uint v = creator.GetTrueColorWord(in color);
                Draw_StippledLine(x0, y0, x1, y1, v, period);
                return;
            }

            if (_bpp == 0x10)
            {
                CHighColorCreator? creator = CXBSystemManager.sHighColorCreatorPtr;
                if (creator == null || !creator.IsEnabled)
                {
                    return;
                }

                ushort v = creator.GetHighColorWord(color.R, color.G, color.B);
                Draw_StippledLine(x0, y0, x1, y1, v, period);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette? palette = _palette ?? CXBSystemManager.sPalettePtr;
                if (palette == null)
                {
                    return;
                }

                byte idx = palette.FindMatchingColor(color.R, color.G, color.B);
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
            if (_bpp != 0x10)
            {
                return;
            }

            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (radius > int.MaxValue)
            {
                return;
            }

            int r = (int)radius;

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
            if (_bpp != 0x20)
            {
                return;
            }

            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (radius > int.MaxValue)
            {
                return;
            }

            int r = (int)radius;

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
                CTrueColorCreator? creator = CXBSystemManager.sTrueColorCreatorPtr;
                if (creator == null || !creator.IsEnabled)
                {
                    return;
                }

                uint v = creator.GetTrueColorWord(in color);
                Draw_Circle(cx, cy, radius, v);
                return;
            }

            if (_bpp == 0x10)
            {
                CHighColorCreator? creator = CXBSystemManager.sHighColorCreatorPtr;
                if (creator == null || !creator.IsEnabled)
                {
                    return;
                }

                ushort v = creator.GetHighColorWord(color.R, color.G, color.B);
                Draw_Circle(cx, cy, radius, v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette? pal = _palette ?? CXBSystemManager.sPalettePtr;
                if (pal == null)
                {
                    return;
                }

                byte idx = pal.FindMatchingColor(color.R, color.G, color.B);
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

            int d = 3 - (2 * r);
            int x = 0;
            int y = r;

            while (x <= y)
            {
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
            if (_bpp != 0x10)
            {
                return;
            }

            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (radius > int.MaxValue)
            {
                return;
            }

            int r = (int)radius;

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
            if (_bpp != 0x20)
            {
                return;
            }

            if (!TryGetPixelBuffer(out _, out _, out _, out _))
            {
                return;
            }

            if (radius > int.MaxValue)
            {
                return;
            }

            int r = (int)radius;

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
                CTrueColorCreator? creator = CXBSystemManager.sTrueColorCreatorPtr;
                if (creator == null || !creator.IsEnabled)
                {
                    return;
                }

                uint v = creator.GetTrueColorWord(in color);
                Draw_FilledCircle(cx, cy, radius, v);
                return;
            }

            if (_bpp == 0x10)
            {
                CHighColorCreator? creator = CXBSystemManager.sHighColorCreatorPtr;
                if (creator == null || !creator.IsEnabled)
                {
                    return;
                }

                ushort v = creator.GetHighColorWord(color.R, color.G, color.B);
                Draw_FilledCircle(cx, cy, radius, v);
                return;
            }

            if (_bpp == 0x08)
            {
                CPalette? pal = _palette ?? CXBSystemManager.sPalettePtr;
                if (pal == null)
                {
                    return;
                }

                byte idx = pal.FindMatchingColor(color.R, color.G, color.B);
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

            Text_PrintCore(font, x, y, text, static (f, bmp, ascii, cx, cy) => f.PrintCharacter(bmp, ascii, cx, cy));
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

            Text_PrintCore(font, x, y, text, (f, bmp, ascii, cx, cy) => f.PrintCharacter(palette, bmp, ascii, cx, cy));
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
            Text_PrintCore(font, x, y, text, (f, bmp, ascii, cx, cy) => f.PrintCharacter(colorCopy, bmp, ascii, cx, cy));
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

        private delegate void PrintCharDelegate(CFont font, CBitmap bmp, byte ascii, int cx, int cy);

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

                    cursorY += font.GetCharacterHeight((byte)'i') + 2;
                    cursorX = x;
                    continue;
                }

                if (ch > (char)0x1F)
                {
                    byte ascii = ToByteOrQuestionMark(ch);

                    int charWidth = font.GetCharacterWidth(ascii);

                    if (_fixedXDistance == 0)
                    {
                        printer(font, this, ascii, cursorX, cursorY);
                        cursorX += charWidth;
                    }
                    else
                    {
                        int centeredX = cursorX + ((_fixedXDistance - charWidth) / 2);
                        printer(font, this, ascii, centeredX, cursorY);
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

        // -------- printf-style formatting --------

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

            System.Text.StringBuilder sb = new(format.Length + 32);
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

            int zeroIndex = text.IndexOf('\0');
            int effectiveLength = zeroIndex >= 0 ? zeroIndex : text.Length;

            if (_fixedXDistance == 0)
            {
                // Matches: CFont::GetPixelWidth(font, param_2)
                // If text contains '\0', only the prefix is relevant.
                string effectiveText = effectiveLength == text.Length ? text : text[..effectiveLength];
                int width = font.GetPixelWidth(effectiveText);

                if (width <= 0)
                {
                    return 0;
                }

                return (uint)width;
            }

            if (_fixedXDistance <= 0 || effectiveLength <= 0)
            {
                return 0;
            }

            long product = effectiveLength * _fixedXDistance;
            if (product <= 0)
            {
                return 0;
            }

            // The original returns ulong but effectively uses a 32-bit product; clamp for safety in managed code.
            if (product > uint.MaxValue)
            {
                return uint.MaxValue;
            }

            return (uint)product;
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

            if (!TryGetPixelBuffer(out byte[]? buffer, out int baseOffset, out int pitchPixels, out int bytesPerPixel))
            {
                return;
            }

            if (buffer == null || bytesPerPixel <= 0)
            {
                return;
            }

            int pitch = pitchPixels > 0 ? pitchPixels : width;

            int halfWidth = width / 2;
            int halfHeight = height / 2;

            if (halfWidth <= 0 || halfHeight <= 0)
            {
                return;
            }

            // Matches dump behavior: only uses a 2*halfHeight area (odd last row/col is effectively ignored).
            int effectiveWidth = halfWidth * 2;
            int effectiveHeight = halfHeight * 2;

            if (effectiveWidth <= 1 || effectiveHeight <= 1)
            {
                return;
            }

            int topY = 0;
            int bottomY = effectiveHeight - 1;

            int yDist = halfHeight;
            while (yDist > 1)
            {
                int mappedY = (int)(((uint)yDist * factor) >> 8);
                int srcTopY = halfHeight - mappedY;
                int srcBottomY = (halfHeight + mappedY) - 1;

                if ((uint)srcTopY >= (uint)effectiveHeight || (uint)srcBottomY >= (uint)effectiveHeight)
                {
                    topY++;
                    bottomY--;
                    yDist--;
                    continue;
                }

                uint xAcc = (uint)halfWidth * factor;

                int leftX = 0;
                int rightX = effectiveWidth - 1;

                int xCount = halfWidth;
                while (xCount > 1)
                {
                    int mappedX = (int)(xAcc >> 8);

                    int srcLeftX = halfWidth - mappedX;
                    int srcRightX = (halfWidth + mappedX) - 1;

                    if ((uint)srcLeftX < (uint)effectiveWidth && (uint)srcRightX < (uint)effectiveWidth)
                    {
                        CopyPixel(buffer, baseOffset, pitch, bytesPerPixel, srcTopY, srcLeftX, topY, leftX);
                        CopyPixel(buffer, baseOffset, pitch, bytesPerPixel, srcTopY, srcRightX, topY, rightX);
                        CopyPixel(buffer, baseOffset, pitch, bytesPerPixel, srcBottomY, srcLeftX, bottomY, leftX);
                        CopyPixel(buffer, baseOffset, pitch, bytesPerPixel, srcBottomY, srcRightX, bottomY, rightX);
                    }

                    leftX++;
                    rightX--;
                    xCount--;

                    xAcc -= factor;
                }

                topY++;
                bottomY--;
                yDist--;
            }
        }

        private static void CopyPixel(byte[] buffer, int baseOffset, int pitchPixels, int bytesPerPixel, int srcY, int srcX, int dstY, int dstX)
        {
            int srcIndex = checked(baseOffset + ((srcY * pitchPixels) + srcX) * bytesPerPixel);
            int dstIndex = checked(baseOffset + ((dstY * pitchPixels) + dstX) * bytesPerPixel);

            // Copy exactly one pixel (1/2/4 bytes)
            Buffer.BlockCopy(buffer, srcIndex, buffer, dstIndex, bytesPerPixel);
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

            int width = _rect.Width;
            int height = _rect.Height;

            if (width <= 0 || height <= 0)
            {
                return false;
            }

            if (!TryGetPixelBuffer(out byte[]? buffer, out int baseOffset, out int pitchPixels, out int bytesPerPixel))
            {
                return false;
            }

            if (buffer == null || bytesPerPixel != 1)
            {
                return false;
            }

            int stride = pitchPixels > 0 ? pitchPixels : width;

            int minX = 2000;
            int minY = 2000;
            int maxX = -2000;
            int maxY = -2000;

            bool found = false;

            for (int y = 0; y < height; y++)
            {
                int rowBase = checked(baseOffset + (y * stride));

                for (int x = 0; x < width; x++)
                {
                    byte value = buffer[rowBase + x];
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
            if (!rect.IsTouching(in _rect))
            {
                return;
            }

            rect.CutInside(in _rect);
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffset, out int pitchPixels, out int bytesPerPixel))
            {
                return;
            }

            if (buffer == null || bytesPerPixel <= 0)
            {
                return;
            }

            int pitch = pitchPixels > 0 ? pitchPixels : _rect.Width;

            if (_bpp == 0x20)
            {
                // Matches dump: *(uint*) = (*(uint*) >> 1) & 0x7f7f7f
                uint mask = 0x007F7F7FU;

                int yEndExclusive = rect.Y + rect.Height;
                int xEndExclusive = rect.X + rect.Width;

                for (int y = rect.Y; y < yEndExclusive; y++)
                {
                    int rowBaseBytes = checked(baseOffset + (y * pitch + rect.X) * bytesPerPixel);

                    for (int x = rect.X; x < xEndExclusive; x++)
                    {
                        int pixelOffset = checked(rowBaseBytes + (x - rect.X) * bytesPerPixel);

                        uint value = ReadUInt32LittleEndian(buffer, pixelOffset);
                        value = (value >> 1) & mask;
                        WriteUInt32LittleEndian(buffer, pixelOffset, value);
                    }
                }

                return;
            }

            if (_bpp == 0x10)
            {
                CHighColorCreator creator = CXBSystemManager.sHighColorCreatorPtr;
                if (creator == null)
                {
                    return;
                }

                ushort mask = creator.GetHighColorWord(0x7F, 0x7F, 0x7F);

                int yEndExclusive = rect.Y + rect.Height;
                int xEndExclusive = rect.X + rect.Width;

                for (int y = rect.Y; y < yEndExclusive; y++)
                {
                    int rowBaseBytes = checked(baseOffset + (y * pitch + rect.X) * bytesPerPixel);

                    for (int x = rect.X; x < xEndExclusive; x++)
                    {
                        int pixelOffset = checked(rowBaseBytes + (x - rect.X) * bytesPerPixel);

                        ushort value = ReadUInt16LittleEndian(buffer, pixelOffset);
                        value = (ushort)(((uint)value >> 1) & mask);
                        WriteUInt16LittleEndian(buffer, pixelOffset, value);
                    }
                }

                return;
            }

            if (_bpp == 0x08)
            {
                if (remapTable == null)
                {
                    return;
                }

                byte[] table = remapTable.Table256Bytes;
                if (table == null || table.Length < 256)
                {
                    return;
                }

                int yEndExclusive = rect.Y + rect.Height;
                int xEndExclusive = rect.X + rect.Width;

                for (int y = rect.Y; y < yEndExclusive; y++)
                {
                    int rowBase = checked(baseOffset + (y * pitch) + rect.X);

                    for (int x = rect.X; x < xEndExclusive; x++)
                    {
                        int i = rowBase + (x - rect.X);
                        buffer[i] = table[buffer[i]];
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

        private static ushort ReadUInt16LittleEndian(byte[] buffer, int offset)
        {
            return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
        }

        private static void WriteUInt16LittleEndian(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static uint ReadUInt32LittleEndian(byte[] buffer, int offset)
        {
            return (uint)(
                buffer[offset] |
                (buffer[offset + 1] << 8) |
                (buffer[offset + 2] << 16) |
                (buffer[offset + 3] << 24));
        }

        private static void WriteUInt32LittleEndian(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
        #endregion

        #region NXBasics::CBitmap::FilterColor() and ReplaceColor()
        // NXBasics::CBitmap::FilterColor(unsigned char, unsigned char) const
        internal void FilterColor(byte fromColor, byte toColor)
        {
            if (_bpp != (byte)BitmapFormat.Indexed8)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[]? buffer, out int baseOffset, out int pitchPixels, out int bytesPerPixel))
            {
                return;
            }

            if (buffer == null || bytesPerPixel != 1)
            {
                return;
            }

            int width = Width;
            int height = Height;

            if (width <= 0 || height <= 0)
            {
                return;
            }

            int stride = pitchPixels > 0 ? pitchPixels : width;

            for (int y = 0; y < height; y++)
            {
                int rowBase = checked(baseOffset + (y * stride));

                for (int x = 0; x < width; x++)
                {
                    int i = rowBase + x;

                    if (buffer[i] != fromColor)
                    {
                        buffer[i] = toColor;
                    }
                }
            }
        }

        // NXBasics::CBitmap::ReplaceColor(unsigned char, unsigned char) const
        internal void ReplaceColor(byte fromColor, byte toColor)
        {
            if (_bpp != (byte)BitmapFormat.Indexed8)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[]? buffer, out int baseOffset, out int pitchPixels, out int bytesPerPixel))
            {
                return;
            }

            if (buffer == null || bytesPerPixel != 1)
            {
                return;
            }

            int width = Width;
            int height = Height;

            if (width <= 0 || height <= 0)
            {
                return;
            }

            int stride = pitchPixels > 0 ? pitchPixels : width;

            for (int y = 0; y < height; y++)
            {
                int rowBase = checked(baseOffset + (y * stride));

                for (int x = 0; x < width; x++)
                {
                    int i = rowBase + x;

                    if (buffer[i] == fromColor)
                    {
                        buffer[i] = toColor;
                    }
                }
            }
        }
        #endregion

        #region NXBasics::CBitmap::Tool_Darken()
        // NXBasics::CBitmap::GetUnclippedPixelPtr(int, int) const
        internal int GetUnclippedPixelIndex(int x, int y)
        {
            int stride = StridePixels;
            if (stride <= 0)
            {
                stride = Width;
            }

            return (y * stride) + x;
        }

        // NXBasics::CBitmap::Tool_UseSourceAsMaskAndDarken(NXBasics::CBitmap const&, int, int, unsigned char) const
        internal void Tool_UseSourceAsMaskAndDarken(CBitmap destination, int offsetX, int offsetY, byte transparentMaskColor)
        {
            if (destination == null)
            {
                return;
            }

            // Dump: source must be 8bpp and both bitmaps must have pixel pointers.
            if (_bpp != (byte)BitmapFormat.Indexed8)
            {
                return;
            }

            if (!TryGetPixelBuffer(out byte[] srcBuf, out int srcBase, out int srcPitchPixels, out int srcBytesPerPixel))
            {
                return;
            }

            if (srcBuf == null || srcBytesPerPixel != 1)
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[] dstBuf, out int dstBase, out int dstPitchPixels, out int dstBytesPerPixel))
            {
                return;
            }

            if (dstBuf == null || dstBytesPerPixel <= 0)
            {
                return;
            }

            SRectangle dstBounds = destination._rect;
            SRectangle srcOverDst = new(offsetX, offsetY, Width, Height);

            if (!srcOverDst.IsTouching(in dstBounds))
            {
                return;
            }

            srcOverDst.CutInside(in dstBounds);

            if (srcOverDst.Width <= 0 || srcOverDst.Height <= 0)
            {
                return;
            }

            int srcPitch = srcPitchPixels > 0 ? srcPitchPixels : Width;
            int dstPitch = dstPitchPixels > 0 ? dstPitchPixels : destination.Width;

            int maskStartX = srcOverDst.X - offsetX;
            int maskStartY = srcOverDst.Y - offsetY;

            if (dstBytesPerPixel == 4 && destination._bpp == (byte)BitmapFormat.TrueColor32)
            {
                // Dump uses constant 0x007F7F7F
                const uint channelMask = 0x007F7F7FU;

                for (int row = 0; row < srcOverDst.Height; row++)
                {
                    int srcRowBase = checked(srcBase + ((maskStartY + row) * srcPitch) + maskStartX);
                    int dstRowBaseBytes = checked(dstBase + (((srcOverDst.Y + row) * dstPitch) + srcOverDst.X) * 4);

                    for (int col = 0; col < srcOverDst.Width; col++)
                    {
                        if (srcBuf[srcRowBase + col] == transparentMaskColor)
                        {
                            continue;
                        }

                        int dstOff = checked(dstRowBaseBytes + (col * 4));
                        uint value = ReadUInt32LittleEndian(dstBuf, dstOff);
                        value = (value >> 1) & channelMask;
                        WriteUInt32LittleEndian(dstBuf, dstOff, value);
                    }
                }

                return;
            }

            if (dstBytesPerPixel == 2 && destination._bpp == (byte)BitmapFormat.HighColor16)
            {
                // Dump checks only pointer != null, not "IsEnabled"
                CHighColorCreator highColorCreator = CXBSystemManager.sHighColorCreatorPtr;
                if (highColorCreator == null)
                {
                    return;
                }

                ushort mask = highColorCreator.GetHighColorWord(0x7F, 0x7F, 0x7F);

                for (int row = 0; row < srcOverDst.Height; row++)
                {
                    int srcRowBase = checked(srcBase + ((maskStartY + row) * srcPitch) + maskStartX);
                    int dstRowBaseBytes = checked(dstBase + (((srcOverDst.Y + row) * dstPitch) + srcOverDst.X) * 2);

                    for (int col = 0; col < srcOverDst.Width; col++)
                    {
                        if (srcBuf[srcRowBase + col] == transparentMaskColor)
                        {
                            continue;
                        }

                        int dstOff = checked(dstRowBaseBytes + (col * 2));
                        ushort value = ReadUInt16LittleEndian(dstBuf, dstOff);
                        value = (ushort)(((uint)value >> 1) & mask);
                        WriteUInt16LittleEndian(dstBuf, dstOff, value);
                    }
                }
            }
        }

        // NXBasics::CBitmap::Tool_Darken() const
        internal void Tool_Darken()
        {
            SRectangle rect = _rect;
            Tool_Darken(rect);
        }

        // NXBasics::CBitmap::Tool_Darken(NXBasics::SRectangle) const
        // NXBasics::CBitmap::Tool_Darken(NXBasics::SRectangle) const
        internal void Tool_Darken(SRectangle rect)
        {
            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffset, out int pitchPixels, out int bytesPerPixel))
            {
                return;
            }

            if (buffer == null || bytesPerPixel <= 0)
            {
                return;
            }

            rect.Validate();

            if (!rect.IsTouching(in _rect))
            {
                return;
            }

            rect.CutInside(in _rect);

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            if (_bpp == (byte)BitmapFormat.TrueColor32 && bytesPerPixel == 4)
            {
                // Dump: mask = GetTrueColorWord(..., 0x7f,0x7f,0x7f)
                CTrueColorCreator trueColorCreator = CXBSystemManager.sTrueColorCreatorPtr;
                uint mask = trueColorCreator != null ? trueColorCreator.GetTrueColorWord(0x7F, 0x7F, 0x7F) : 0x007F7F7FU;

                int pitch = pitchPixels > 0 ? pitchPixels : Width;

                for (int y = 0; y < rect.Height; y++)
                {
                    int rowBaseBytes = checked(baseOffset + (((rect.Y + y) * pitch) + rect.X) * 4);

                    for (int x = 0; x < rect.Width; x++)
                    {
                        int off = checked(rowBaseBytes + (x * 4));
                        uint value = ReadUInt32LittleEndian(buffer, off);
                        value = (value >> 1) & mask;
                        WriteUInt32LittleEndian(buffer, off, value);
                    }
                }

                return;
            }

            if (_bpp == (byte)BitmapFormat.HighColor16 && bytesPerPixel == 2)
            {
                CHighColorCreator highColorCreator = CXBSystemManager.sHighColorCreatorPtr;
                if (highColorCreator == null)
                {
                    return;
                }

                ushort mask = highColorCreator.GetHighColorWord(0x7F, 0x7F, 0x7F);
                int pitch = pitchPixels > 0 ? pitchPixels : Width;

                for (int y = 0; y < rect.Height; y++)
                {
                    int rowBaseBytes = checked(baseOffset + (((rect.Y + y) * pitch) + rect.X) * 2);

                    for (int x = 0; x < rect.Width; x++)
                    {
                        int off = checked(rowBaseBytes + (x * 2));
                        ushort value = ReadUInt16LittleEndian(buffer, off);
                        value = (ushort)(((uint)value >> 1) & mask);
                        WriteUInt16LittleEndian(buffer, off, value);
                    }
                }
            }
        }

        // NXBasics::CBitmap::Tool_Darken(NXBasics::SRectangle, unsigned char, unsigned char, unsigned char) const
        // NXBasics::CBitmap::Tool_Darken(NXBasics::SRectangle, unsigned char, unsigned char, unsigned char) const
        internal void Tool_Darken(in SRectangle rect, uint rMask, uint gMask, uint bMask)
        {
            if (!TryGetPixelBuffer(out byte[] buffer, out int baseOffset, out int pitchPixels, out int bytesPerPixel))
            {
                return;
            }

            if (buffer == null || bytesPerPixel <= 0)
            {
                return;
            }

            SRectangle work = rect;
            work.Validate();

            if (!work.IsTouching(in _rect))
            {
                return;
            }

            work.CutInside(in _rect);

            if (work.Width <= 0 || work.Height <= 0)
            {
                return;
            }

            byte r = (byte)(rMask & 0xFF);
            byte g = (byte)(gMask & 0xFF);
            byte b = (byte)(bMask & 0xFF);

            int pitch = pitchPixels > 0 ? pitchPixels : Width;

            if (_bpp == (byte)BitmapFormat.TrueColor32 && bytesPerPixel == 4)
            {
                CTrueColorCreator trueColorCreator = CXBSystemManager.sTrueColorCreatorPtr;
                uint mask = trueColorCreator != null ? trueColorCreator.GetTrueColorWord(r, g, b) : 0x007F7F7FU;

                for (int y = 0; y < work.Height; y++)
                {
                    int rowBaseBytes = checked(baseOffset + (((work.Y + y) * pitch) + work.X) * 4);

                    for (int x = 0; x < work.Width; x++)
                    {
                        int off = checked(rowBaseBytes + (x * 4));
                        uint value = ReadUInt32LittleEndian(buffer, off);
                        value = (value >> 1) & mask;
                        WriteUInt32LittleEndian(buffer, off, value);
                    }
                }

                return;
            }

            if (_bpp == (byte)BitmapFormat.HighColor16 && bytesPerPixel == 2)
            {
                CHighColorCreator highColorCreator = CXBSystemManager.sHighColorCreatorPtr;
                if (highColorCreator == null)
                {
                    return;
                }

                ushort mask = highColorCreator.GetHighColorWord(r, g, b);

                for (int y = 0; y < work.Height; y++)
                {
                    int rowBaseBytes = checked(baseOffset + (((work.Y + y) * pitch) + work.X) * 2);

                    for (int x = 0; x < work.Width; x++)
                    {
                        int off = checked(rowBaseBytes + (x * 2));
                        ushort value = ReadUInt16LittleEndian(buffer, off);
                        value = (ushort)(((uint)value >> 1) & mask);
                        WriteUInt16LittleEndian(buffer, off, value);
                    }
                }
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