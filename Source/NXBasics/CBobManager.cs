using OpenVikings.NC2GuiToolsBase;
using OpenVikings.NXBasics.Structs;

namespace OpenVikings.NXBasics
{
    // NXBasics::CBobManager
    // Managed, pointer-free reimplementation based on the decompiled C++.
    //
    // Notes:
    // - The original uses three CMemory blocks:
    //   (1) BobData array (type + rectangle + misc)
    //   (2) Packed line byte stream (RLE-like)
    //   (3) Line control array (uint per scanline; contains packed offset + optional flags)
    //
    // - This C# version keeps the exact same conceptual model, but uses managed arrays.
    // - Many print variants in the original share the same packed-line decoder.
    internal sealed class CBobManager : CStorable, IDisposable
    {
        // Original hard safety limit observed in AddBob8BitDouble(): 0x186a1
        private const uint MaxBobIndexPlusOne = 0x186A1;

        // Packed-line control encoding as seen in IsBobHit():
        // offset = value & 0x003F_FFFF (22 bits)
        // xMin   = value >> 22         (10 bits)
        private const uint PackedOffsetMask = 0x003FFFFF;
        private const int PackedXShift = 22;
        private const uint PackedLineFlagStride = 0x00400000; // used in double-byte bobs (per the generator)

        private readonly StaticVars _staticVars;

        private bool _disposed;

        // this + 0x08
        private uint _firstBobId;

        // this + 0x0C
        private uint _bobCount;

        // this + 0x10
        private uint _packedLineDataUsedBytes;

        // this + 0x14
        private uint _lineControlCount;

        // this + 0x18..0x20 (counters in generator; kept for fidelity/debug)
        private uint _generatedNonEmptyLines;
        private uint _generatedEmptyLines;
        private uint _generatedPackedLines;

        // this[0x28] in the dump (bool)
        private bool _memoryOptimization;

        // Raw pointers in C++:
        // this + 0x30 -> bob data base
        // this + 0x38 -> packed line data base
        // this + 0x40 -> line control base
        //
        // Managed equivalents:
        private SBobData[] _bobData;
        private byte[] _packedLineData;
        private uint[] _lineControl;

        internal CBobManager(uint firstBobId, bool memoryOptimization)
        {
            _staticVars = new StaticVars();
            SystemInitObject();

            _firstBobId = firstBobId;
            _memoryOptimization = memoryOptimization;

            _bobData = [];
            _packedLineData = [];
            _lineControl = [];
        }

        internal CBobManager(CFile file)
        {
            _staticVars = new StaticVars();
            SystemInitObject();

            // Layout written by Storable_SaveData(): 0x1C bytes starting at this+8 in C++:
            // 0x08 firstBobId
            // 0x0C bobCount
            // 0x10 packedLineDataUsedBytes
            // 0x14 lineControlCount
            // 0x18 generatedNonEmptyLines
            // 0x1C generatedEmptyLines
            // 0x20 generatedPackedLines
            _firstBobId = file.ReadLong();
            _bobCount = file.ReadLong();
            _packedLineDataUsedBytes = file.ReadLong();
            _lineControlCount = file.ReadLong();
            _generatedNonEmptyLines = file.ReadLong();
            _generatedEmptyLines = file.ReadLong();
            _generatedPackedLines = file.ReadLong();

            // In the C++ ctor, objects are only loaded if bobCount != 0.
            // The file stores three storables (CMemory blocks).
            if (_bobCount != 0)
            {
                // These are expected to be CMemory objects in the original.
                CMemory? bobMem = NXBasicsApi.XB_Storable_LoadObject(file) as CMemory;
                CMemory? packedMem = NXBasicsApi.XB_Storable_LoadObject(file) as CMemory;
                CMemory? lineMem = NXBasicsApi.XB_Storable_LoadObject(file) as CMemory;

                // Convert to managed arrays (no raw pointer exposure).
                _bobData = ReadBobDataFromMemory(bobMem, _bobCount);
                _packedLineData = ReadBytesFromMemory(packedMem);
                _lineControl = ReadUInt32ArrayFromMemory(lineMem, _lineControlCount);

                // Endian conversions exist in the dump (ConvertLongLSB). Keep behavior:
                // - bobData fields (type + rectangle ints) are uint32/int32 and need endian swap if file is LSB-encoded.
                // - lineControl entries are uint32.
                // This project typically stores little-endian; if DexterEndian is present it should already be correct.
                // Still, the conversion hooks are kept here as no-ops unless needed by the platform layer.
                NormalizeEndianAfterLoad();
            }
            else
            {
                _bobData = [];
                _packedLineData = [];
                _lineControl = [];
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _bobData = [];
            _packedLineData = [];
            _lineControl = [];

            SystemInitObject();
        }

        internal uint GetFirstBobId()
        {
            return _firstBobId;
        }

        internal uint GetNumberOfBobs()
        {
            return _bobCount;
        }

        internal bool DoesBobExists(uint bobId)
        {
            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return false;
            }

            return _bobData[index].Type != 0;
        }

        internal int GetBobType(uint bobId)
        {
            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return 0;
            }

            return _bobData[index].Type;
        }

        internal SRectangle? GetBobAreaRectanglePtr(uint bobId)
        {
            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return null;
            }

            if (_bobData[index].Type == 0)
            {
                return null;
            }

            // In the C++ this returns (base + 4) into the record; effectively the rectangle.
            return _bobData[index].Area;
        }

        internal bool IsBobHit(uint bobId, in SPoint point)
        {
            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return false;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return false;
            }

            if (!point.IsInside(in bob.Area))
            {
                return false;
            }

            // Line-control is indexed by absolute Y (bounds.Y + localY) in the generator.
            // Therefore the hit-test must use the same addressing scheme.
            int relativeY = point.Y - bob.Area.Y;
            if (relativeY < 0 || relativeY >= bob.Area.Height)
            {
                return false;
            }

            int ctrlIndex = bob.Area.Y + relativeY;
            if (ctrlIndex < 0 || (uint)ctrlIndex >= _lineControlCount)
            {
                return false;
            }

            uint ctrl = _lineControl[ctrlIndex];
            if (ctrl == 0xFFFFFFFFu)
            {
                return false;
            }

            int relativeX = point.X - bob.Area.X;

            uint xMin = ctrl >> PackedXShift;
            if (relativeX < (int)xMin)
            {
                return false;
            }

            uint packedOffset = ctrl & PackedOffsetMask;
            if (packedOffset >= (uint)_packedLineData.Length)
            {
                return false;
            }

            // Type-specific hit test:
            // - Type 1: 8-bit bob (single byte per pixel in packed raw segments)
            // - Type 4: double-byte bob (two bytes per pixel in raw segments)
            if (bob.Type == 1)
            {
                return HitTestPackedLine_8Bit(packedOffset, xMin, relativeX);
            }

            if (bob.Type == 4)
            {
                return HitTestPackedLine_DoubleByte(packedOffset, xMin, relativeX);
            }

            // Other bob types exist; default: treat packed data as opaque (no hit).
            return false;
        }

        // NXBasics::CBobManager::Generate_SetMemoryOptimizationFlag(bool)
        internal void Generate_SetMemoryOptimizationFlag(bool enabled)
        {
            _memoryOptimization = enabled;
        }

        // NXBasics::CBobManager::l_Generate_FinalizePackedLineDataBuffer()
        internal void Generate_FinalizePackedLineDataBuffer()
        {
            Generate_ResizePackedLineDataBuffer(_packedLineDataUsedBytes);
        }

        // NXBasics::CBobManager::AddBob8BitDouble(...)
        // In the original, this is a specialized generator for Type=4 double-byte bobs.
        internal bool AddBob8BitDouble(uint bobId, CBitmap sourceA, byte colorKeyA, int offsetX, int offsetY, CBitmap sourceB, byte fallbackB)
        {
            if (sourceA == null || sourceB == null)
            {
                return false;
            }

            if (sourceA.BitsPerPixel != 8 || sourceB.BitsPerPixel != 8)
            {
                return false;
            }

            if (!sourceA.GetBoundingRectangle(colorKeyA, out SRectangle bounds))
            {
                return false;
            }

            uint index = bobId - _firstBobId;
            if (index >= MaxBobIndexPlusOne)
            {
                return false;
            }

            if (index >= _bobCount)
            {
                Generate_AllocateBobDataStructureArrayBuffer(index + 1);
            }

            if (_bobData[index].Type != 0)
            {
                // Already exists; mirror original behavior: do not overwrite silently.
                return false;
            }

            // Initialize bob record.
            SBobData bob = new()
            {
                Type = 4,
                Area = bounds
            };
            bob.Area.MovePosition(-offsetX, -offsetY);

            // Prepare per-line control offsets for this bob's height.
            uint height = (uint)bounds.Height;
            uint width = (uint)bounds.Width;
            if (height == 0 || width == 0)
            {
                // Empty bounds -> treat as not added.
                return false;
            }

            Generate_AllocateLineControlArrayBuffer(Math.Max(_lineControlCount, (uint)(bounds.Y + bounds.Height + 1)));

            // Temporary buffer per line (original used 5000 bytes).
            byte[] tempLine = new byte[5000];

            // Crop bitmaps to bounds (original creates CBitmap sub-bitmaps).
            CBitmap croppedA = new(sourceA, bounds);
            CBitmap croppedB = new(sourceB, bounds);

            // For each scanline, pack and append to global packed buffer.
            for (uint y = 0; y < height; y++)
            {
                _generatedNonEmptyLines++;

                int packedLen = Generate_PackLine_Double8BitBob(tempLine, croppedA, 0, y, colorKeyA, croppedB, fallbackB);
                if (packedLen < 0)
                {
                    return false;
                }

                // The original encodes a per-x "flag stride" in the high bits (uVar15) when skipping pixels outside sourceB.
                // That behavior is preserved inside Generate_PackLine_Double8BitBob.

                if (_memoryOptimization)
                {
                    uint foundAt = XB_Tool_FindMemoryInMemory(_packedLineData, _packedLineDataUsedBytes, tempLine, (uint)packedLen);
                    if (foundAt != 0xFFFFFFFFu)
                    {
                        _lineControl[bounds.Y + (int)y] = foundAt;
                        _generatedPackedLines++;
                        continue;
                    }
                }

                uint dstOffset = _packedLineDataUsedBytes;
                if (dstOffset + (uint)packedLen >= 0x400000u)
                {
                    // Same hard cap as in the original generator path.
                    return false;
                }

                Generate_AllocatePackedLineDataBuffer(dstOffset + (uint)packedLen);
                Buffer.BlockCopy(tempLine, 0, _packedLineData, (int)dstOffset, packedLen);

                _lineControl[(int)(bounds.Y + y)] = dstOffset;
                _packedLineDataUsedBytes += (uint)packedLen;
                _generatedPackedLines++;
            }

            // Store bob.
            _bobData[index] = bob;
            return true;
        }

        // NXBasics::CBobManager::AddBob(unsigned int, TBobType, CBitmap const&, unsigned char, int, int)
        internal bool AddBob(uint bobId, TBobType type, CBitmap source, byte colorKey, int offsetX, int offsetY)
        {
            if (source == null)
            {
                return false;
            }

            if (!source.GetBoundingRectangle(colorKey, out SRectangle bounds))
            {
                return false;
            }

            uint index = bobId - _firstBobId;
            if (index >= MaxBobIndexPlusOne)
            {
                return false;
            }

            if (index >= _bobCount)
            {
                Generate_AllocateBobDataStructureArrayBuffer(index + 1);
            }

            if (_bobData[index].Type != 0)
            {
                return false;
            }

            SBobData bob = new()
            {
                Type = (int)type,
                Area = bounds
            };
            bob.Area.MovePosition(-offsetX, -offsetY);

            uint height = (uint)bounds.Height;
            uint width = (uint)bounds.Width;
            if (height == 0 || width == 0)
            {
                return false;
            }

            Generate_AllocateLineControlArrayBuffer(Math.Max(_lineControlCount, (uint)(bounds.Y + bounds.Height + 1)));

            byte[] tempLine = new byte[5000];
            CBitmap cropped = new(source, bounds);

            for (uint y = 0; y < height; y++)
            {
                int packedLen;
                if (type == TBobType.Bob8Bit)
                {
                    packedLen = Generate_PackLine_8BitBob(tempLine, cropped, 0, y, colorKey);
                }
                else if (type == TBobType.Bob1Bit)
                {
                    packedLen = Generate_PackLine_1BitBob(tempLine, cropped, 0, y, colorKey);
                }
                else if (type == TBobType.TimeMask)
                {
                    packedLen = Generate_PackLine_TimeMaskBob(tempLine, cropped, 0, y, colorKey);
                }
                else
                {
                    // Fallback: treat as 8-bit bob packer.
                    packedLen = Generate_PackLine_8BitBob(tempLine, cropped, 0, y, colorKey);
                }

                if (packedLen < 0)
                {
                    return false;
                }

                if (packedLen == 1 && tempLine[0] == 0)
                {
                    _lineControl[(int)(bounds.Y + y)] = 0xFFFFFFFFu;
                    _generatedEmptyLines++;
                    continue;
                }

                if (_memoryOptimization)
                {
                    uint foundAt = XB_Tool_FindMemoryInMemory(_packedLineData, _packedLineDataUsedBytes, tempLine, (uint)packedLen);
                    if (foundAt != 0xFFFFFFFFu)
                    {
                        _lineControl[(int)(bounds.Y + y)] = foundAt;
                        _generatedPackedLines++;
                        continue;
                    }
                }

                uint dstOffset = _packedLineDataUsedBytes;
                if (dstOffset + (uint)packedLen >= 0x400000u)
                {
                    return false;
                }

                Generate_AllocatePackedLineDataBuffer(dstOffset + (uint)packedLen);
                Buffer.BlockCopy(tempLine, 0, _packedLineData, (int)dstOffset, packedLen);

                _lineControl[(int)(bounds.Y + y)] = dstOffset;
                _packedLineDataUsedBytes += (uint)packedLen;
                _generatedPackedLines++;
            }

            _bobData[index] = bob;
            return true;
        }

        // NXBasics::CBobManager::PrintBob(unsigned int, CBitmap const&, int, int, CPalette*) const
        internal void PrintBob(uint bobId, CBitmap destination, int dstX, int dstY, CPalette? palette)
        {
            if (destination == null)
            {
                return;
            }

            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return;
            }

            if (bob.Type == 1)
            {
                PrintBob_8BitCore(bob, destination, dstX, dstY, palette, null);
                return;
            }

            if (bob.Type == 4)
            {
                PrintBob_DoubleByteCore(bob, destination, dstX, dstY, palette, null);
                return;
            }

            if (bob.Type == 2)
            {
                // 1-bit bob (mask) -> draw as solid using palette entry 0xFF by convention if available.
                PrintBob_1BitCore(bob, destination, dstX, dstY, palette, TBobPrintHighColorMaskMode.Normal);
                return;
            }

            if (bob.Type == 3)
            {
                // TimeMask bob: draw as 8-bit indices (or as mask in specialized methods).
                PrintBob_8BitCore(bob, destination, dstX, dstY, palette, null);
                return;
            }

            // Fallback
            PrintBob_8BitCore(bob, destination, dstX, dstY, palette, null);
        }

        // NXBasics::CBobManager::PrintBob_Remapped(...)
        internal void PrintBob_Remapped(uint bobId, CBitmap destination, int dstX, int dstY, CPalette? palette, byte[] remap)
        {
            if (destination == null || remap == null || remap.Length < 256)
            {
                return;
            }

            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return;
            }

            if (bob.Type == 1)
            {
                PrintBob_8BitCore(bob, destination, dstX, dstY, palette, remap);
                return;
            }

            if (bob.Type == 4)
            {
                PrintBob_DoubleByteCore(bob, destination, dstX, dstY, palette, remap);
                return;
            }
        }

        // NXBasics::CBobManager::PrintBob_1Bit_HighColor(...)
        internal void PrintBob_1Bit_HighColor(uint bobId, CBitmap destination, int dstX, int dstY, TBobPrintHighColorMaskMode mode)
        {
            if (destination == null)
            {
                return;
            }

            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return;
            }

            PrintBob_1BitCore(bob, destination, dstX, dstY, null, mode);
        }

        // NXBasics::CBobManager::PrintBob_1Bit_TrueColor(...)
        internal void PrintBob_1Bit_TrueColor(uint bobId, CBitmap destination, int dstX, int dstY, TBobPrintHighColorMaskMode mode)
        {
            if (destination == null)
            {
                return;
            }

            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return;
            }

            PrintBob_1BitCore(bob, destination, dstX, dstY, null, mode);
        }

        // -------------------------
        // Storage
        // -------------------------

        // NXBasics::CBobManager::Storable_GetId() const
        internal static ulong Storable_GetId_Static()
        {
            return 0x3F4;
        }

        internal override uint Storable_GetId()
        {
            return 0x3F4U;
        }

        // NXBasics::CBobManager::Storable_SaveData(CFile&)
        internal override void Storable_SaveData(CFile file)
        {
            Generate_ResizePackedLineDataBuffer(_packedLineDataUsedBytes);

            file.WriteLong(_firstBobId);
            file.WriteLong(_bobCount);
            file.WriteLong(_packedLineDataUsedBytes);
            file.WriteLong(_lineControlCount);
            file.WriteLong(_generatedNonEmptyLines);
            file.WriteLong(_generatedEmptyLines);
            file.WriteLong(_generatedPackedLines);

            if (_bobCount == 0)
            {
                return;
            }

            CMemory bobMem = WriteBobDataToMemory(_bobData, _bobCount);

            // --- Packed data ---
            CMemory packedMem = new(_packedLineDataUsedBytes);
            byte[]? packedBuf = packedMem.BufferArray;
            if (packedBuf != null && _packedLineDataUsedBytes != 0)
            {
                Buffer.BlockCopy(_packedLineData, 0, packedBuf, 0, checked((int)_packedLineDataUsedBytes));
            }

            // --- Line control (uint[] -> little endian bytes) ---
            CMemory lineMem = new(_lineControlCount * 4u);
            byte[]? lineBuf = lineMem.BufferArray;
            if (lineBuf != null)
            {
                for (uint i = 0; i < _lineControlCount; i++)
                {
                    WriteUInt32LSB(lineBuf, checked((int)i) * 4, _lineControl[i]);
                }
            }

            bobMem.Storable_Save(file);
            packedMem.Storable_Save(file);
            lineMem.Storable_Save(file);
        }

        // -------------------------
        // Generator buffers (managed equivalents)
        // -------------------------

        private void SystemInitObject()
        {
            _firstBobId = 0;
            _bobCount = 0;
            _packedLineDataUsedBytes = 0;
            _lineControlCount = 0;
            _generatedNonEmptyLines = 0;
            _generatedEmptyLines = 0;
            _generatedPackedLines = 0;
            _memoryOptimization = false;

            StaticVars.Reset();
        }

        private void Generate_AllocateBobDataStructureArrayBuffer(uint newCount)
        {
            if (_bobCount == newCount)
            {
                return;
            }

            SBobData[] newArr = new SBobData[newCount];
            if (_bobData.Length != 0)
            {
                uint copyCount = Math.Min((uint)_bobData.Length, newCount);
                Array.Copy(_bobData, 0, newArr, 0, (int)copyCount);
            }

            _bobData = newArr;
            _bobCount = newCount;
        }

        private void Generate_AllocatePackedLineDataBuffer(uint requiredBytes)
        {
            _packedLineDataUsedBytes = requiredBytes;

            if (_packedLineData.Length != 0 && requiredBytes <= (uint)_packedLineData.Length)
            {
                return;
            }

            Generate_ResizePackedLineDataBuffer(requiredBytes + 50000u);
        }

        private void Generate_ResizePackedLineDataBuffer(uint newSize)
        {
            if (_packedLineData.Length == 0)
            {
                _packedLineData = new byte[newSize];
                return;
            }

            if ((uint)_packedLineData.Length == newSize)
            {
                return;
            }

            byte[] newBuf = new byte[newSize];
            uint copyLen = Math.Min((uint)_packedLineData.Length, newSize);
            if (copyLen != 0)
            {
                Buffer.BlockCopy(_packedLineData, 0, newBuf, 0, (int)copyLen);
            }

            _packedLineData = newBuf;
        }

        private void Generate_AllocateLineControlArrayBuffer(uint newCount)
        {
            if (_lineControlCount == newCount)
            {
                return;
            }

            uint[] newArr = new uint[newCount];
            if (_lineControl.Length != 0)
            {
                uint copyCount = Math.Min((uint)_lineControl.Length, newCount);
                Array.Copy(_lineControl, 0, newArr, 0, (int)copyCount);
            }

            _lineControl = newArr;
            _lineControlCount = newCount;
        }

        // -------------------------
        // Packed line generators
        // -------------------------

        // l_Generate_PackLine_8BitBob()
        private static int Generate_PackLine_8BitBob(byte[] dst, CBitmap bmp, uint startX, uint y, byte colorKey)
        {
            return Generate_PackLine_DoPack(dst, bmp, startX, y, colorKey, false);
        }

        // l_Generate_PackLine_DoPack(..., bool longAlign)
        private static int Generate_PackLine_DoPack(byte[] dst, CBitmap bmp, uint startX, uint y, byte colorKey, bool longAlign)
        {
            // Encoding:
            // - Positive count (0..0x7F): raw run follows (count bytes)
            // - Skip run via high bit (0x80..0xFF): skip count = b & 0x7F
            // - Terminator: 0
            int width = bmp.Width;
            if (width <= 0)
            {
                dst[0] = 0;
                return 1;
            }

            int outPos = 0;
            int x = (int)startX;

            int key = colorKey;
            int iy = (int)y;

            while (true)
            {
                // Raw run until first colorKey (or end)
                int rawStart = x;
                while (x < width && bmp.GetUnclippedPixelIndex(x, iy) != key)
                {
                    x++;
                }

                int rawLen = x - rawStart;
                while (rawLen > 0)
                {
                    int chunk = rawLen > 0x7F ? 0x7F : rawLen;
                    dst[outPos++] = (byte)chunk;

                    for (int i = 0; i < chunk; i++)
                    {
                        int pix = bmp.GetUnclippedPixelIndex(rawStart + i, iy);
                        dst[outPos++] = unchecked((byte)pix);
                    }

                    rawStart += chunk;
                    rawLen -= chunk;
                }

                if (x >= width)
                {
                    dst[outPos++] = 0;
                    return outPos;
                }

                // Skip run (colorKey) until first non-colorKey (or end)
                int skipStart = x;
                while (x < width && bmp.GetUnclippedPixelIndex(x, iy) == key)
                {
                    x++;
                }

                int skipLen = x - skipStart;

                while (skipLen > 0)
                {
                    int chunk = skipLen > 0x7F ? 0x7F : skipLen;
                    dst[outPos++] = (byte)(0x80 | (byte)chunk);
                    skipLen -= chunk;
                }

                if (x >= width)
                {
                    dst[outPos++] = 0;
                    return outPos;
                }

                if (longAlign)
                {
                    // Optional alignment behavior exists in other parts of the engine.
                    // Keep as no-op unless the original logic requires padding here.
                }
            }
        }

        // l_Generate_PackLine_1BitBob()
        // l_Generate_PackLine_1BitBob()
        private static int Generate_PackLine_1BitBob(byte[] dst, CBitmap bmp, uint startX, uint y, byte colorKey)
        {
            // For 1-bit bobs, treat "non-colorKey" as set bits, and store raw bytes as 0/1.
            // This keeps the same RLE structure so print/hit logic stays consistent.
            int width = bmp.Width;
            int outPos = 0;
            int x = (int)startX;

            int key = colorKey;

            while (true)
            {
                int rawStart = x;
                while (x < width && bmp.GetUnclippedPixelIndex(x, (int)y) != key)
                {
                    x++;
                }

                int rawLen = x - rawStart;
                while (rawLen > 0)
                {
                    int chunk = rawLen > 0x7F ? 0x7F : rawLen;
                    dst[outPos++] = (byte)chunk;

                    for (int i = 0; i < chunk; i++)
                    {
                        int pix = bmp.GetUnclippedPixelIndex(rawStart + i, (int)y);
                        dst[outPos++] = pix != key ? (byte)1 : (byte)0;
                    }

                    rawStart += chunk;
                    rawLen -= chunk;
                }

                if (x >= width)
                {
                    dst[outPos++] = 0;
                    return outPos;
                }

                int skipStart = x;
                while (x < width && bmp.GetUnclippedPixelIndex(x, (int)y) == key)
                {
                    x++;
                }

                int skipLen = x - skipStart;
                while (skipLen > 0)
                {
                    int chunk = skipLen > 0x7F ? 0x7F : skipLen;
                    dst[outPos++] = (byte)(0x80 | (byte)chunk);
                    skipLen -= chunk;
                }

                if (x >= width)
                {
                    dst[outPos++] = 0;
                    return outPos;
                }
            }
        }

        // l_Generate_PackLine_TimeMaskBob()
        private static int Generate_PackLine_TimeMaskBob(byte[] dst, CBitmap bmp, uint startX, uint y, byte colorKey)
        {
            // TimeMask behaves like 8-bit bob, but pixel values are interpreted differently by some print modes.
            return Generate_PackLine_DoPack(dst, bmp, startX, y, colorKey, false);
        }

        // l_Generate_PackLine_Double8BitBob()
        private static int Generate_PackLine_Double8BitBob(byte[] dst, CBitmap bmpA, uint startX, uint y, byte colorKeyA, CBitmap bmpB, byte fallbackB)
        {
            // Packed format for double-byte bobs:
            // raw run: [count][A0][B0][A1][B1]...
            // skip run: [0x80|count]
            // terminator: 0
            int width = bmpA.Width;
            int outPos = 0;
            int x = (int)startX;

            int keyA = colorKeyA;
            int iy = (int)y;

            while (true)
            {
                // Raw run until first colorKeyA (or end)
                int rawStart = x;
                while (x < width && bmpA.GetUnclippedPixelIndex(x, iy) != keyA)
                {
                    x++;
                }

                int rawLen = x - rawStart;
                while (rawLen > 0)
                {
                    int chunk = rawLen > 0x7F ? 0x7F : rawLen;
                    dst[outPos++] = (byte)chunk;

                    for (int i = 0; i < chunk; i++)
                    {
                        int px = rawStart + i;

                        int aIndex = bmpA.GetUnclippedPixelIndex(px, iy);
                        byte a = unchecked((byte)aIndex);

                        byte b = fallbackB;
                        if (bmpB.IsPointInside(px, iy))
                        {
                            int bIndex = bmpB.GetUnclippedPixelIndex(px, iy);
                            b = unchecked((byte)bIndex);
                        }

                        dst[outPos++] = a;
                        dst[outPos++] = b;
                    }

                    rawStart += chunk;
                    rawLen -= chunk;
                }

                if (x >= width)
                {
                    dst[outPos++] = 0;
                    return outPos;
                }

                // Skip run of colorKeyA
                int skipStart = x;
                while (x < width && bmpA.GetUnclippedPixelIndex(x, iy) == keyA)
                {
                    x++;
                }

                int skipLen = x - skipStart;
                while (skipLen > 0)
                {
                    int chunk = skipLen > 0x7F ? 0x7F : skipLen;
                    dst[outPos++] = (byte)(0x80 | (byte)chunk);
                    skipLen -= chunk;
                }

                if (x >= width)
                {
                    dst[outPos++] = 0;
                    return outPos;
                }
            }
        }

        // -------------------------
        // Packed line hit tests
        // -------------------------

        private bool HitTestPackedLine_8Bit(uint packedOffset, uint xMin, int relativeX)
        {
            int x = (int)xMin;
            int targetX = relativeX;

            int pos = (int)packedOffset;
            if (pos < 0 || pos >= _packedLineData.Length)
            {
                return false;
            }

            byte b = _packedLineData[pos];
            if (b == 0)
            {
                return false;
            }

            while (b != 0)
            {
                pos++;

                int count = b & 0x7F;
                if ((b & 0x80) == 0)
                {
                    // Raw bytes: [count] data bytes
                    if (x <= targetX && targetX < x + count)
                    {
                        return true;
                    }

                    pos += count;
                }

                x += count;

                if (pos < 0 || pos >= _packedLineData.Length)
                {
                    return false;
                }

                b = _packedLineData[pos];
            }

            return false;
        }

        private bool HitTestPackedLine_DoubleByte(uint packedOffset, uint xMin, int relativeX)
        {
            int x = (int)xMin;
            int targetX = relativeX;

            int pos = (int)packedOffset;
            if (pos < 0 || pos >= _packedLineData.Length)
            {
                return false;
            }

            byte b = _packedLineData[pos];
            if (b == 0)
            {
                return false;
            }

            while (b != 0)
            {
                pos++;

                int count = b & 0x7F;
                if ((b & 0x80) == 0)
                {
                    if (x <= targetX && targetX < x + count)
                    {
                        return true;
                    }

                    // Skip 2 bytes per pixel
                    pos += count * 2;
                }

                x += count;

                if (pos < 0 || pos >= _packedLineData.Length)
                {
                    return false;
                }

                b = _packedLineData[pos];
            }

            return false;
        }

        // -------------------------
        // Packed line printers (core)
        // -------------------------

        private void PrintBob_8BitCore(in SBobData bob, CBitmap destination, int dstX, int dstY, CPalette? palette, byte[]? remap)
        {
            // 8-bit bob prints:
            // - If destination is 8bpp: write indices (with optional remap)
            // - If destination is 16/32bpp and palette available: expand using palette tables
            // - If no palette for hi/true: write nothing (safe)
            int height = bob.Area.Height;
            if (height <= 0)
            {
                return;
            }

            int baseLine = bob.Area.Y;
            int x0 = dstX + bob.Area.X;
            int y0 = dstY + bob.Area.Y;

            for (int line = 0; line < height; line++)
            {
                int ctrlIndex = baseLine + line;
                if ((uint)ctrlIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[ctrlIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                uint xMin = ctrl >> PackedXShift;
                uint offset = ctrl & PackedOffsetMask;

                PrintPackedLine_8Bit(destination, x0, y0 + line, (int)xMin, offset, palette, remap);
            }
        }

        private void PrintBob_DoubleByteCore(in SBobData bob, CBitmap destination, int dstX, int dstY, CPalette? palette, byte[]? remap)
        {
            int height = bob.Area.Height;
            if (height <= 0)
            {
                return;
            }

            int baseLine = bob.Area.Y;
            int x0 = dstX + bob.Area.X;
            int y0 = dstY + bob.Area.Y;

            for (int line = 0; line < height; line++)
            {
                int ctrlIndex = baseLine + line;
                if ((uint)ctrlIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[ctrlIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                uint xMin = ctrl >> PackedXShift;
                uint offset = ctrl & PackedOffsetMask;

                PrintPackedLine_DoubleByte(destination, x0, y0 + line, (int)xMin, offset, palette, remap);
            }
        }

        private void PrintBob_1BitCore(in SBobData bob, CBitmap destination, int dstX, int dstY, CPalette? palette, TBobPrintHighColorMaskMode mode)
        {
            // For mask modes, the original supports multiple blending/highcolor/truecolor paths.
            // This managed version draws mask pixels as:
            // - 8bpp destination: write 0xFF
            // - 16/32bpp destination: use palette[0xFF] if palette exists; otherwise use max white.
            int height = bob.Area.Height;
            if (height <= 0)
            {
                return;
            }

            int baseLine = bob.Area.Y;
            int x0 = dstX + bob.Area.X;
            int y0 = dstY + bob.Area.Y;

            for (int line = 0; line < height; line++)
            {
                int ctrlIndex = baseLine + line;
                if ((uint)ctrlIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[ctrlIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                uint xMin = ctrl >> PackedXShift;
                uint offset = ctrl & PackedOffsetMask;

                PrintPackedLine_1BitMask(destination, x0, y0 + line, (int)xMin, offset, palette, mode);
            }
        }

        private void PrintPackedLine_8Bit(CBitmap dst, int dstBaseX, int dstY, int xMin, uint packedOffset, CPalette? palette, byte[]? remap)
        {
            int pos = (int)packedOffset;
            if (pos < 0 || pos >= _packedLineData.Length)
            {
                return;
            }

            int x = xMin;
            byte b = _packedLineData[pos];

            while (b != 0)
            {
                pos++;
                int count = b & 0x7F;

                if ((b & 0x80) == 0)
                {
                    // Raw run: 'count' source indices follow.
                    for (int i = 0; i < count; i++)
                    {
                        if (pos >= _packedLineData.Length)
                        {
                            return;
                        }

                        byte src = _packedLineData[pos++];
                        if (remap != null)
                        {
                            src = remap[src];
                        }

                        // No base-class changes: map to existing CBitmap methods.
                        if (dst.BitsPerPixel == 8)
                        {
                            dst.Draw_SetPixel(dstBaseX + x + i, dstY, src);
                        }
                        else if (dst.BitsPerPixel == 16)
                        {
                            if (palette == null)
                            {
                                continue;
                            }

                            ushort[] table = palette.GetHighColorTablePtr();
                            dst.Draw_SetPixel(dstBaseX + x + i, dstY, table[src]);
                        }
                        else if (dst.BitsPerPixel == 32)
                        {
                            if (palette == null)
                            {
                                continue;
                            }

                            uint[] table = palette.GetTrueColorTablePtr();
                            dst.Draw_SetPixel(dstBaseX + x + i, dstY, table[src]);
                        }
                    }
                }
                else
                {
                    // Skip run: advance X by 'count' pixels.
                }

                x += count;

                if (pos >= _packedLineData.Length)
                {
                    return;
                }

                b = _packedLineData[pos];
            }
        }

        private void PrintPackedLine_DoubleByte(CBitmap dst, int dstBaseX, int dstY, int xMin, uint packedOffset, CPalette? palette, byte[]? remap)
        {
            int pos = (int)packedOffset;
            if (pos < 0 || pos >= _packedLineData.Length)
            {
                return;
            }

            int x = xMin;
            byte b = _packedLineData[pos];

            while (b != 0)
            {
                pos++;
                int count = b & 0x7F;

                if ((b & 0x80) == 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (pos + 1 >= _packedLineData.Length)
                        {
                            return;
                        }

                        byte a = _packedLineData[pos++];
                        pos++; // Skip second byte (B)

                        if (remap != null)
                        {
                            a = remap[a];
                        }

                        if (dst.BitsPerPixel == 8)
                        {
                            dst.Draw_SetPixel(dstBaseX + x + i, dstY, a);
                        }
                        else if (dst.BitsPerPixel == 16)
                        {
                            if (palette == null)
                            {
                                continue;
                            }

                            ushort[] table = palette.GetHighColorTablePtr();
                            dst.Draw_SetPixel(dstBaseX + x + i, dstY, table[a]);
                        }
                        else if (dst.BitsPerPixel == 32)
                        {
                            if (palette == null)
                            {
                                continue;
                            }

                            uint[] table = palette.GetTrueColorTablePtr();
                            dst.Draw_SetPixel(dstBaseX + x + i, dstY, table[a]);
                        }
                    }
                }

                x += count;

                if (pos >= _packedLineData.Length)
                {
                    return;
                }

                b = _packedLineData[pos];
            }
        }

        private void PrintPackedLine_1BitMask(CBitmap dst, int dstBaseX, int dstY, int xMin, uint packedOffset, CPalette? palette, TBobPrintHighColorMaskMode mode)
        {
            int pos = (int)packedOffset;
            if (pos < 0 || pos >= _packedLineData.Length)
            {
                return;
            }

            int x = xMin;
            byte b = _packedLineData[pos];

            while (b != 0)
            {
                pos++;
                int count = b & 0x7F;

                if ((b & 0x80) == 0)
                {
                    // Raw run: 'count' mask bytes follow (stored as 0/1).
                    for (int i = 0; i < count; i++)
                    {
                        if (pos >= _packedLineData.Length)
                        {
                            return;
                        }

                        bool draw = _packedLineData[pos++] != 0;
                        if (mode == TBobPrintHighColorMaskMode.Invert)
                        {
                            draw = !draw;
                        }

                        if (!draw)
                        {
                            continue;
                        }

                        int px = dstBaseX + x + i;

                        if (dst.BitsPerPixel == 8)
                        {
                            dst.Draw_SetPixel(px, dstY, 0xFF);
                        }
                        else if (dst.BitsPerPixel == 16)
                        {
                            if (palette == null)
                            {
                                continue;
                            }

                            ushort[] table = palette.GetHighColorTablePtr();
                            dst.Draw_SetPixel(px, dstY, table[0xFF]);
                        }
                        else if (dst.BitsPerPixel == 32)
                        {
                            if (palette == null)
                            {
                                continue;
                            }

                            uint[] table = palette.GetTrueColorTablePtr();
                            dst.Draw_SetPixel(px, dstY, table[0xFF]);
                        }
                    }
                }
                else
                {
                    // Skip run: mask is implicitly 0 for 'count' pixels.
                    // In invert mode, mask=0 becomes draw=1, so draw them.
                    if (mode == TBobPrintHighColorMaskMode.Invert)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            int px = dstBaseX + x + i;

                            if (dst.BitsPerPixel == 8)
                            {
                                dst.Draw_SetPixel(px, dstY, 0xFF);
                            }
                            else if (dst.BitsPerPixel == 16)
                            {
                                if (palette == null)
                                {
                                    continue;
                                }

                                ushort[] table = palette.GetHighColorTablePtr();
                                dst.Draw_SetPixel(px, dstY, table[0xFF]);
                            }
                            else if (dst.BitsPerPixel == 32)
                            {
                                if (palette == null)
                                {
                                    continue;
                                }

                                uint[] table = palette.GetTrueColorTablePtr();
                                dst.Draw_SetPixel(px, dstY, table[0xFF]);
                            }
                        }
                    }
                }

                x += count;

                if (pos >= _packedLineData.Length)
                {
                    return;
                }

                b = _packedLineData[pos];
            }
        }

        // -------------------------
        // Helpers for storage/interop with existing project types
        // -------------------------

        private static SBobData[] ReadBobDataFromMemory(CMemory? mem, uint count)
        {
            if (mem == null || count == 0)
            {
                return [];
            }

            byte[]? buf = mem.BufferArray;
            if (buf == null)
            {
                return [];
            }

            const int RecordSize = 24; // 0x18
            int needed = checked((int)count) * RecordSize;
            if (buf.Length < needed)
            {
                // Corrupt/short file.
                return [];
            }

            SBobData[] arr = new SBobData[count];

            for (uint i = 0; i < count; i++)
            {
                int baseOffset = checked((int)i) * RecordSize;

                int type = ReadInt32LSB(buf, baseOffset + 0);

                int x = ReadInt32LSB(buf, baseOffset + 4);
                int y = ReadInt32LSB(buf, baseOffset + 8);
                int w = ReadInt32LSB(buf, baseOffset + 12);
                int h = ReadInt32LSB(buf, baseOffset + 16);

                uint misc = ReadUInt32LSB(buf, baseOffset + 20);

                arr[i] = new SBobData
                {
                    Type = type,
                    Area = new SRectangle(x, y, w, h),
                    Misc = misc
                };
            }

            return arr;
        }

        private static byte[] ReadBytesFromMemory(CMemory? mem)
        {
            if (mem == null)
            {
                return [];
            }

            byte[]? buf = mem.BufferArray;
            if (buf == null || buf.Length == 0)
            {
                return [];
            }

            byte[] copy = new byte[buf.Length];
            Buffer.BlockCopy(buf, 0, copy, 0, buf.Length);
            return copy;
        }

        private static uint[] ReadUInt32ArrayFromMemory(CMemory? mem, uint count)
        {
            if (mem == null || count == 0)
            {
                return [];
            }

            byte[]? buf = mem.BufferArray;
            if (buf == null)
            {
                return [];
            }

            int needed = checked((int)count) * 4;
            if (buf.Length < needed)
            {
                return [];
            }

            uint[] arr = new uint[count];
            for (uint i = 0; i < count; i++)
            {
                int offset = checked((int)i) * 4;
                arr[i] = ReadUInt32LSB(buf, offset);
            }

            return arr;
        }

        private static CMemory WriteBobDataToMemory(SBobData[] data, uint count)
        {
            const int RecordSize = 24;
            uint bytes = checked(count * RecordSize);

            CMemory mem = new(bytes);

            byte[]? buf = mem.BufferArray;
            if (buf == null)
            {
                return mem;
            }

            for (uint i = 0; i < count; i++)
            {
                int baseOffset = checked((int)i) * RecordSize;
                SBobData d = i < (uint)data.Length ? data[i] : default;

                WriteInt32LSB(buf, baseOffset + 0, d.Type);

                WriteInt32LSB(buf, baseOffset + 4, d.Area.X);
                WriteInt32LSB(buf, baseOffset + 8, d.Area.Y);
                WriteInt32LSB(buf, baseOffset + 12, d.Area.Width);
                WriteInt32LSB(buf, baseOffset + 16, d.Area.Height);

                WriteUInt32LSB(buf, baseOffset + 20, d.Misc);
            }

            return mem;
        }


        private static void NormalizeEndianAfterLoad()
        {
            // If the project layer already reads little-endian, this remains a no-op.
            // Hooks remain so the behavior matches the decompiled intent.
        }

        // Original uses XB_Tool_FindMemoryInMemory(...) to deduplicate packed lines.
        private static uint XB_Tool_FindMemoryInMemory(byte[] haystack, uint haystackLen, byte[] needle, uint needleLen)
        {
            if (needleLen == 0 || needleLen > haystackLen)
            {
                return 0xFFFFFFFFu;
            }

            int limit = (int)(haystackLen - needleLen);
            for (int i = 0; i <= limit; i++)
            {
                bool match = true;
                for (int j = 0; j < (int)needleLen; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return (uint)i;
                }
            }

            return 0xFFFFFFFFu;
        }

        private static int ReadInt32LSB(byte[] buffer, int offset)
        {
            uint u = ReadUInt32LSB(buffer, offset);
            return unchecked((int)u);
        }

        private static uint ReadUInt32LSB(byte[] buffer, int offset)
        {
            // Little-endian
            return buffer[offset] | ((uint)buffer[offset + 1] << 8) | ((uint)buffer[offset + 2] << 16) | ((uint)buffer[offset + 3] << 24);
        }

        private static void WriteInt32LSB(byte[] buffer, int offset, int value)
        {
            WriteUInt32LSB(buffer, offset, unchecked((uint)value));
        }

        private static void WriteUInt32LSB(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        // Add these methods (and helpers) inside CBobManager (same class as the existing PrintBob_* methods).

        // NXBasics::CBobManager::l_System_SetRawPointer()
        // Managed version: nothing to do (no raw pointers). Kept for parity with the original.
        private static void SystemSetRawPointer()
        {
            // In the C++ version, this maps CMemory blocks to raw base pointers.
            // In managed code, the backing arrays are already directly accessible.
        }

        // NXBasics::CBobManager::l_PrintTimeMask_BottomUpScanTimeMask(unsigned int, NXBasics::SBobData const*) const
        private int PrintTimeMask_BottomUpScanTimeMask(uint time, in SBobData bob)
        {
            int height = bob.Area.Height;
            if (height <= 0)
            {
                return 0;
            }

            int baseLine = bob.Area.Y;

            // Scan from bottom to top.
            for (int scanned = 0; scanned < height; scanned++)
            {
                int lineIndex = baseLine + height - 1 - scanned;
                if ((uint)lineIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[lineIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                int pos = (int)(ctrl & PackedOffsetMask);
                if ((uint)pos >= (uint)_packedLineData.Length)
                {
                    continue;
                }

                byte b = _packedLineData[pos];
                if (b == 0)
                {
                    continue;
                }

                // TimeMask packed format (observed in the dump):
                // - Control byte: count (low 7 bits), high bit indicates skip-run
                // - Raw run: 2 bytes per pixel: [valueByte, timeByte]
                // - Skip run: no payload
                //
                // The original returns the number of scanned lines from the bottom until it finds a pixel
                // whose timeByte is greater than the given time (time < timeByte).
                while (b != 0)
                {
                    int count = b & 0x7F;
                    if ((b & 0x80) != 0)
                    {
                        // Skip run
                        pos += 1;
                    }
                    else
                    {
                        int payloadStart = pos + 1;
                        int needed = payloadStart + (count * 2);
                        if (needed > _packedLineData.Length)
                        {
                            break;
                        }

                        for (int i = 0; i < count; i++)
                        {
                            int timeByteIndex = payloadStart + (i * 2) + 1;
                            byte t = _packedLineData[timeByteIndex];
                            if (time < t)
                            {
                                return scanned;
                            }
                        }

                        pos = needed;
                    }

                    if ((uint)pos >= (uint)_packedLineData.Length)
                    {
                        break;
                    }

                    b = _packedLineData[pos];
                }
            }

            return height;
        }

        // NXBasics::CBobManager::l_PrintTimeMask_BottomUpScanTimeMask8Bit(unsigned int, NXBasics::SBobData const*) const
        private int PrintTimeMask_BottomUpScanTimeMask8Bit(uint time, in SBobData bob)
        {
            int height = bob.Area.Height;
            if (height <= 0)
            {
                return 0;
            }

            int baseLine = bob.Area.Y;

            for (int scanned = 0; scanned < height; scanned++)
            {
                int lineIndex = baseLine + height - 1 - scanned;
                if ((uint)lineIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[lineIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                int pos = (int)(ctrl & PackedOffsetMask);
                if ((uint)pos >= (uint)_packedLineData.Length)
                {
                    continue;
                }

                byte b = _packedLineData[pos];
                if (b == 0)
                {
                    continue;
                }

                // 8-bit TimeMask variant:
                // Raw run payload: 1 byte per pixel = timeByte
                while (b != 0)
                {
                    int count = b & 0x7F;
                    if ((b & 0x80) != 0)
                    {
                        // Skip run
                        pos += 1;
                    }
                    else
                    {
                        int payloadStart = pos + 1;
                        int needed = payloadStart + count;
                        if (needed > _packedLineData.Length)
                        {
                            break;
                        }

                        for (int i = 0; i < count; i++)
                        {
                            byte t = _packedLineData[payloadStart + i];
                            if (time < t)
                            {
                                return scanned;
                            }
                        }

                        pos = needed;
                    }

                    if ((uint)pos >= (uint)_packedLineData.Length)
                    {
                        break;
                    }

                    b = _packedLineData[pos];
                }
            }

            return height;
        }

        // NXBasics::CBobManager::PrintBob_Shadow(unsigned int, NXBasics::CBitmap const&, int, int) const
        internal void PrintBob_Shadow(uint bobId, CBitmap destination, int dstX, int dstY)
        {
            if (destination == null)
            {
                return;
            }

            if (destination.BitsPerPixel == 32)
            {
                PrintBob_1Bit_TrueColor(bobId, destination, dstX, dstY, TBobPrintHighColorMaskMode.Normal);
                return;
            }

            if (destination.BitsPerPixel == 16)
            {
                PrintBob_1Bit_HighColor(bobId, destination, dstX, dstY, TBobPrintHighColorMaskMode.Normal);
                return;
            }
        }

        // NXBasics::CBobManager::PrintBob_DoubleByteBobNormal(unsigned int, NXBasics::CBitmap const&, int, int, NXBasics::CPalette*) const
        internal void PrintBob_DoubleByteBobNormal(uint bobId, CBitmap destination, int dstX, int dstY, CPalette? palette)
        {
            if (destination == null)
            {
                return;
            }

            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return;
            }

            // Double-byte bob normal rendering: use the same path as PrintBob_DoubleByteCore
            // (index byte used, second byte ignored).
            PrintBob_DoubleByteCore(bob, destination, dstX, dstY, palette, null);
        }

        // NXBasics::CBobManager::PrintBob_UsingTransparency(unsigned int, NXBasics::CBitmap const&, unsigned int, int, int, NXBasics::CPalette*) const
        internal void PrintBob_UsingTransparency(uint bobId, CBitmap destination, uint alpha, int dstX, int dstY, CPalette? palette)
        {
            if (destination == null)
            {
                return;
            }

            if (alpha >= 256u)
            {
                alpha = 255u;
            }

            if (destination.BitsPerPixel != 16 && destination.BitsPerPixel != 32)
            {
                return;
            }

            if (palette == null)
            {
                return;
            }

            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[]? dstBuf, out int dstBase, out int dstPitch, out int dstBytesPerPixel))
            {
                return;
            }

            if (dstBuf == null)
            {
                return;
            }

            int height = bob.Area.Height;
            if (height <= 0)
            {
                return;
            }

            int baseLine = bob.Area.Y;
            int x0 = dstX + bob.Area.X;
            int y0 = dstY + bob.Area.Y;

            int invA = 256 - (int)alpha;

            if (destination.BitsPerPixel == 32)
            {
                uint[] table = palette.GetTrueColorTablePtr();

                for (int line = 0; line < height; line++)
                {
                    int ctrlIndex = baseLine + line;
                    if ((uint)ctrlIndex >= _lineControlCount)
                    {
                        continue;
                    }

                    uint ctrl = _lineControl[ctrlIndex];
                    if (ctrl == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int pos = (int)(ctrl & PackedOffsetMask);
                    if ((uint)pos >= (uint)_packedLineData.Length)
                    {
                        continue;
                    }

                    int x = (int)(ctrl >> PackedXShift);
                    byte b = _packedLineData[pos];

                    while (b != 0)
                    {
                        pos++;
                        int count = b & 0x7F;

                        if ((b & 0x80) == 0)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                if ((uint)pos >= (uint)_packedLineData.Length)
                                {
                                    return;
                                }

                                byte srcIndex = _packedLineData[pos++];
                                uint srcColor = table[srcIndex];

                                int px = x0 + x + i;
                                int py = y0 + line;

                                int dstOffset = dstBase + (py * dstPitch) + (px * dstBytesPerPixel);
                                if ((uint)(dstOffset + 3) >= (uint)dstBuf.Length)
                                {
                                    continue;
                                }

                                uint dstColor = ReadUInt32LE(dstBuf, dstOffset);
                                uint blended = Blend32(dstColor, srcColor, (int)alpha, invA);
                                WriteUInt32LE(dstBuf, dstOffset, blended);
                            }
                        }

                        x += count;

                        if ((uint)pos >= (uint)_packedLineData.Length)
                        {
                            break;
                        }

                        b = _packedLineData[pos];
                    }
                }

                return;
            }

            // 16bpp
            ushort[] table16 = palette.GetHighColorTablePtr();

            for (int line = 0; line < height; line++)
            {
                int ctrlIndex = baseLine + line;
                if ((uint)ctrlIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[ctrlIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                int pos = (int)(ctrl & PackedOffsetMask);
                if ((uint)pos >= (uint)_packedLineData.Length)
                {
                    continue;
                }

                int x = (int)(ctrl >> PackedXShift);
                byte b = _packedLineData[pos];

                while (b != 0)
                {
                    pos++;
                    int count = b & 0x7F;

                    if ((b & 0x80) == 0)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            if ((uint)pos >= (uint)_packedLineData.Length)
                            {
                                return;
                            }

                            byte srcIndex = _packedLineData[pos++];
                            ushort srcColor = table16[srcIndex];

                            int px = x0 + x + i;
                            int py = y0 + line;

                            int dstOffset = dstBase + (py * dstPitch) + (px * dstBytesPerPixel);
                            if ((uint)(dstOffset + 1) >= (uint)dstBuf.Length)
                            {
                                continue;
                            }

                            ushort dstColor = ReadUInt16LE(dstBuf, dstOffset);
                            ushort blended = Blend565(dstColor, srcColor, (int)alpha, invA);
                            WriteUInt16LE(dstBuf, dstOffset, blended);
                        }
                    }

                    x += count;

                    if ((uint)pos >= (uint)_packedLineData.Length)
                    {
                        break;
                    }

                    b = _packedLineData[pos];
                }
            }
        }

        // NXBasics::CBobManager::PrintBob_UsingTimeMask(unsigned int, unsigned int, NXBasics::CBitmap const&, int, int, NXBasics::CPalette*) const
        internal void PrintBob_UsingTimeMask(uint bobId, uint time, CBitmap destination, int dstX, int dstY, CPalette? palette)
        {
            if (destination == null)
            {
                return;
            }

            if (palette == null)
            {
                return;
            }

            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return;
            }

            int height = bob.Area.Height;
            if (height <= 0)
            {
                return;
            }

            int baseLine = bob.Area.Y;
            int x0 = dstX + bob.Area.X;
            int y0 = dstY + bob.Area.Y;

            for (int line = 0; line < height; line++)
            {
                int ctrlIndex = baseLine + line;
                if ((uint)ctrlIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[ctrlIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                uint packedOffset = ctrl & PackedOffsetMask;
                int xMin = (int)(ctrl >> PackedXShift);

                PrintPackedLine_TimeMaskAsImage(destination, x0, y0 + line, xMin, packedOffset, time, palette, null);
            }
        }

        // NXBasics::CBobManager::PrintBob_UsingCollapseTimeMask(unsigned int, unsigned int, NXBasics::CBitmap const&, int, int, NXBasics::CPalette*) const
        internal int PrintBob_UsingCollapseTimeMask(uint bobId, uint time, CBitmap destination, int dstX, int dstY, CPalette? palette)
        {
            if (destination == null)
            {
                return 0;
            }

            if (palette == null)
            {
                return 0;
            }

            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return 0;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return 0;
            }

            int collapse = PrintTimeMask_BottomUpScanTimeMask(time, bob);

            int height = bob.Area.Height;
            if (height <= 0)
            {
                return collapse;
            }

            int visibleHeight = height - collapse;
            if (visibleHeight <= 0)
            {
                return collapse;
            }

            int baseLine = bob.Area.Y;
            int x0 = dstX + bob.Area.X;
            int y0 = dstY + bob.Area.Y;

            // Draw only the top part (collapse lines removed from the bottom).
            for (int line = 0; line < visibleHeight; line++)
            {
                int ctrlIndex = baseLine + line;
                if ((uint)ctrlIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[ctrlIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                uint packedOffset = ctrl & PackedOffsetMask;
                int xMin = (int)(ctrl >> PackedXShift);

                PrintPackedLine_TimeMaskAsImage(destination, x0, y0 + line, xMin, packedOffset, time, palette, null);
            }

            return collapse;
        }

        // NXBasics::CBobManager::PrintBob_UsingRangedCollapseTimeMask(unsigned int, unsigned int, unsigned int, NXBasics::CBitmap const&, int, int, NXBasics::CPalette*) const
        internal int PrintBob_UsingRangedCollapseTimeMask(uint bobId, uint time, uint maxCollapse, CBitmap destination, int dstX, int dstY, CPalette? palette)
        {
            if (destination == null)
            {
                return 0;
            }

            if (palette == null)
            {
                return 0;
            }

            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return 0;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return 0;
            }

            int collapse = PrintTimeMask_BottomUpScanTimeMask(time, bob);
            if (collapse > (int)maxCollapse)
            {
                collapse = (int)maxCollapse;
            }

            int height = bob.Area.Height;
            if (height <= 0)
            {
                return collapse;
            }

            int visibleHeight = height - collapse;
            if (visibleHeight <= 0)
            {
                return collapse;
            }

            int baseLine = bob.Area.Y;
            int x0 = dstX + bob.Area.X;
            int y0 = dstY + bob.Area.Y;

            for (int line = 0; line < visibleHeight; line++)
            {
                int ctrlIndex = baseLine + line;
                if ((uint)ctrlIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[ctrlIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                uint packedOffset = ctrl & PackedOffsetMask;
                int xMin = (int)(ctrl >> PackedXShift);

                PrintPackedLine_TimeMaskAsImage(destination, x0, y0 + line, xMin, packedOffset, time, palette, null);
            }

            return collapse;
        }

        // NXBasics::CBobManager::PrintBob_Shadow_TimeMask(unsigned int, unsigned int, NXBasics::CBitmap const&, int, int) const
        internal void PrintBob_Shadow_TimeMask(uint bobId, uint time, CBitmap destination, int dstX, int dstY)
        {
            if (destination == null)
            {
                return;
            }

            // Simple shadow: darken destination where the time-mask is visible.
            int shade = 128;

            if (destination.BitsPerPixel == 32)
            {
                PrintBob_Shade_MMX(bobId, destination, dstX, dstY, null, shade, time);
                return;
            }

            if (destination.BitsPerPixel == 16)
            {
                PrintBob_Shade_HighColor(bobId, destination, dstX, dstY, null, shade, time);
                return;
            }
        }

        // NXBasics::CBobManager::PrintBob_UseTimeMaskAsImage(unsigned int, unsigned int, NXBasics::CBitmap const&, int, int) const
        internal void PrintBob_UseTimeMaskAsImage(uint bobId, uint time, CBitmap destination, int dstX, int dstY)
        {
            if (destination == null)
            {
                return;
            }

            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return;
            }

            // Only meaningful for 8bpp destinations (no palette parameter in the original signature).
            if (destination.BitsPerPixel != 8)
            {
                return;
            }

            int height = bob.Area.Height;
            if (height <= 0)
            {
                return;
            }

            int baseLine = bob.Area.Y;
            int x0 = dstX + bob.Area.X;
            int y0 = dstY + bob.Area.Y;

            for (int line = 0; line < height; line++)
            {
                int ctrlIndex = baseLine + line;
                if ((uint)ctrlIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[ctrlIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                uint packedOffset = ctrl & PackedOffsetMask;
                int xMin = (int)(ctrl >> PackedXShift);

                PrintPackedLine_TimeMask8bpp(destination, x0, y0 + line, xMin, packedOffset, time, null);
            }
        }

        // NXBasics::CBobManager::PrintBob_UseTimeMaskAsMask(unsigned int, unsigned int, NXBasics::CBitmap const&, int, int, unsigned char const*) const
        internal void PrintBob_UseTimeMaskAsMask(uint bobId, uint time, CBitmap destination, int dstX, int dstY, byte[] remap)
        {
            if (destination == null)
            {
                return;
            }

            if (destination.BitsPerPixel != 8)
            {
                return;
            }

            if (remap == null || remap.Length < 256)
            {
                return;
            }

            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return;
            }

            int height = bob.Area.Height;
            if (height <= 0)
            {
                return;
            }

            int baseLine = bob.Area.Y;
            int x0 = dstX + bob.Area.X;
            int y0 = dstY + bob.Area.Y;

            for (int line = 0; line < height; line++)
            {
                int ctrlIndex = baseLine + line;
                if ((uint)ctrlIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[ctrlIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                uint packedOffset = ctrl & PackedOffsetMask;
                int xMin = (int)(ctrl >> PackedXShift);

                PrintPackedLine_TimeMask8bpp(destination, x0, y0 + line, xMin, packedOffset, time, remap);
            }
        }

        // NXBasics::CBobManager::PrintBob_UsingEffectMatrix(unsigned int, NXBasics::CBitmap const&, int, int) const
        internal void PrintBob_UsingEffectMatrix(uint bobId, CBitmap destination, int dstX, int dstY)
        {
            if (destination == null)
            {
                return;
            }

            // Managed approximation: per-scanline X shift based on a small fixed table.
            int[] matrix = [0, 1, 0, -1, 0, 2, 0, -2];

            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return;
            }

            int height = bob.Area.Height;
            if (height <= 0)
            {
                return;
            }

            int baseLine = bob.Area.Y;
            int x0 = dstX + bob.Area.X;
            int y0 = dstY + bob.Area.Y;

            for (int line = 0; line < height; line++)
            {
                int shift = matrix[line & 7];

                int ctrlIndex = baseLine + line;
                if ((uint)ctrlIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[ctrlIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                uint xMin = ctrl >> PackedXShift;
                uint offset = ctrl & PackedOffsetMask;

                // Draw as normal 8-bit line (no palette available in original signature); best effort:
                PrintPackedLine_8Bit(destination, x0 + shift, y0 + line, (int)xMin, offset, null, null);
            }
        }

        // NXBasics::CBobManager::PrintBob_UsingHorizontalEffectUnbuffered(unsigned int, NXBasics::CBitmap const&, int, int) const
        internal void PrintBob_UsingHorizontalEffectUnbuffered(uint bobId, CBitmap destination, int dstX, int dstY)
        {
            if (destination == null)
            {
                return;
            }

            // Managed approximation: alternating line wobble.
            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return;
            }

            int height = bob.Area.Height;
            if (height <= 0)
            {
                return;
            }

            int baseLine = bob.Area.Y;
            int x0 = dstX + bob.Area.X;
            int y0 = dstY + bob.Area.Y;

            for (int line = 0; line < height; line++)
            {
                int shift = ((line & 1) == 0) ? -1 : 1;

                int ctrlIndex = baseLine + line;
                if ((uint)ctrlIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[ctrlIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                uint xMin = ctrl >> PackedXShift;
                uint offset = ctrl & PackedOffsetMask;

                PrintPackedLine_8Bit(destination, x0 + shift, y0 + line, (int)xMin, offset, null, null);
            }
        }

        // NXBasics::CBobManager::PrintBob_Glow_AddColor(unsigned int, NXBasics::CBitmap const&, int, int, NXBasics::SColorRGB const&, unsigned int) const
        internal void PrintBob_Glow_AddColor(uint bobId, CBitmap destination, int dstX, int dstY, SColorRGB color, uint strength)
        {
            if (destination == null)
            {
                return;
            }

            if (strength > 255u)
            {
                strength = 255u;
            }

            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[]? dstBuf, out int dstBase, out int dstPitch, out int dstBytesPerPixel))
            {
                return;
            }

            if (dstBuf == null)
            {
                return;
            }

            int addR = (color.R * (int)strength) >> 8;
            int addG = (color.G * (int)strength) >> 8;
            int addB = (color.B * (int)strength) >> 8;

            int height = bob.Area.Height;
            if (height <= 0)
            {
                return;
            }

            int baseLine = bob.Area.Y;
            int x0 = dstX + bob.Area.X;
            int y0 = dstY + bob.Area.Y;

            for (int line = 0; line < height; line++)
            {
                int ctrlIndex = baseLine + line;
                if ((uint)ctrlIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[ctrlIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                int pos = (int)(ctrl & PackedOffsetMask);
                if ((uint)pos >= (uint)_packedLineData.Length)
                {
                    continue;
                }

                int x = (int)(ctrl >> PackedXShift);
                byte b = _packedLineData[pos];

                while (b != 0)
                {
                    pos++;
                    int count = b & 0x7F;

                    if ((b & 0x80) == 0)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            if ((uint)pos >= (uint)_packedLineData.Length)
                            {
                                return;
                            }

                            // Consume source byte (shape only).
                            pos++;

                            int px = x0 + x + i;
                            int py = y0 + line;

                            int dstOffset = dstBase + (py * dstPitch) + (px * dstBytesPerPixel);

                            if (destination.BitsPerPixel == 32)
                            {
                                if ((uint)(dstOffset + 3) >= (uint)dstBuf.Length)
                                {
                                    continue;
                                }

                                uint dstColor = ReadUInt32LE(dstBuf, dstOffset);
                                uint outColor = AddColor32(dstColor, addR, addG, addB);
                                WriteUInt32LE(dstBuf, dstOffset, outColor);
                            }
                            else if (destination.BitsPerPixel == 16)
                            {
                                if ((uint)(dstOffset + 1) >= (uint)dstBuf.Length)
                                {
                                    continue;
                                }

                                ushort dstColor = ReadUInt16LE(dstBuf, dstOffset);
                                ushort outColor = AddColor565(dstColor, addR, addG, addB);
                                WriteUInt16LE(dstBuf, dstOffset, outColor);
                            }
                        }
                    }

                    x += count;

                    if ((uint)pos >= (uint)_packedLineData.Length)
                    {
                        break;
                    }

                    b = _packedLineData[pos];
                }
            }
        }

        // NXBasics::CBobManager::PrintBob_Glow_AddColor_OnlyBobByteRange(unsigned int, NXBasics::CBitmap const&, int, int, NXBasics::SColorRGB const&, unsigned int, unsigned char, unsigned char) const
        internal void PrintBob_Glow_AddColor_OnlyBobByteRange(uint bobId, CBitmap destination, int dstX, int dstY, SColorRGB color, uint strength, byte minIndex, byte maxIndex)
        {
            if (destination == null)
            {
                return;
            }

            if (strength > 255u)
            {
                strength = 255u;
            }

            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[]? dstBuf, out int dstBase, out int dstPitch, out int dstBytesPerPixel))
            {
                return;
            }

            if (dstBuf == null)
            {
                return;
            }

            int addR = (color.R * (int)strength) >> 8;
            int addG = (color.G * (int)strength) >> 8;
            int addB = (color.B * (int)strength) >> 8;

            int height = bob.Area.Height;
            if (height <= 0)
            {
                return;
            }

            int baseLine = bob.Area.Y;
            int x0 = dstX + bob.Area.X;
            int y0 = dstY + bob.Area.Y;

            for (int line = 0; line < height; line++)
            {
                int ctrlIndex = baseLine + line;
                if ((uint)ctrlIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[ctrlIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                int pos = (int)(ctrl & PackedOffsetMask);
                if ((uint)pos >= (uint)_packedLineData.Length)
                {
                    continue;
                }

                int x = (int)(ctrl >> PackedXShift);
                byte b = _packedLineData[pos];

                while (b != 0)
                {
                    pos++;
                    int count = b & 0x7F;

                    if ((b & 0x80) == 0)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            if ((uint)pos >= (uint)_packedLineData.Length)
                            {
                                return;
                            }

                            byte src = _packedLineData[pos++];

                            if (src < minIndex || src > maxIndex)
                            {
                                continue;
                            }

                            int px = x0 + x + i;
                            int py = y0 + line;

                            int dstOffset = dstBase + (py * dstPitch) + (px * dstBytesPerPixel);

                            if (destination.BitsPerPixel == 32)
                            {
                                if ((uint)(dstOffset + 3) >= (uint)dstBuf.Length)
                                {
                                    continue;
                                }

                                uint dstColor = ReadUInt32LE(dstBuf, dstOffset);
                                uint outColor = AddColor32(dstColor, addR, addG, addB);
                                WriteUInt32LE(dstBuf, dstOffset, outColor);
                            }
                            else if (destination.BitsPerPixel == 16)
                            {
                                if ((uint)(dstOffset + 1) >= (uint)dstBuf.Length)
                                {
                                    continue;
                                }

                                ushort dstColor = ReadUInt16LE(dstBuf, dstOffset);
                                ushort outColor = AddColor565(dstColor, addR, addG, addB);
                                WriteUInt16LE(dstBuf, dstOffset, outColor);
                            }
                        }
                    }

                    x += count;

                    if ((uint)pos >= (uint)_packedLineData.Length)
                    {
                        break;
                    }

                    b = _packedLineData[pos];
                }
            }
        }

        // NXBasics::CBobManager::PrintBob_Shade_HighColor(unsigned int, NXBasics::CBitmap const&, int, int, NXBasics::CPalette&, int) const
        internal void PrintBob_Shade_HighColor(uint bobId, CBitmap destination, int dstX, int dstY, CPalette? palette, int shade)
        {
            PrintBob_Shade_HighColor(bobId, destination, dstX, dstY, palette, shade, 0xFFFFFFFFu);
        }

        // Extra helper overload used by Shadow_TimeMask to pass time without inventing another public signature.
        private void PrintBob_Shade_HighColor(uint bobId, CBitmap destination, int dstX, int dstY, CPalette? palette, int shade, uint time)
        {
            if (destination == null)
            {
                return;
            }

            if (destination.BitsPerPixel != 16)
            {
                return;
            }

            if (shade < 0)
            {
                shade = 0;
            }
            if (shade > 255)
            {
                shade = 255;
            }

            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[]? dstBuf, out int dstBase, out int dstPitch, out int dstBytesPerPixel))
            {
                return;
            }

            if (dstBuf == null || dstBytesPerPixel < 2)
            {
                return;
            }

            int height = bob.Area.Height;
            if (height <= 0)
            {
                return;
            }

            int baseLine = bob.Area.Y;
            int x0 = dstX + bob.Area.X;
            int y0 = dstY + bob.Area.Y;

            int factor = 256 - shade;

            for (int line = 0; line < height; line++)
            {
                int ctrlIndex = baseLine + line;
                if ((uint)ctrlIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[ctrlIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                int pos = (int)(ctrl & PackedOffsetMask);
                if ((uint)pos >= (uint)_packedLineData.Length)
                {
                    continue;
                }

                int x = (int)(ctrl >> PackedXShift);
                byte b = _packedLineData[pos];

                while (b != 0)
                {
                    pos++;
                    int count = b & 0x7F;

                    if ((b & 0x80) == 0)
                    {
                        // If this is a TimeMask bob, payload is [value,time] pairs and visibility depends on time.
                        bool isTimeMask = (time != 0xFFFFFFFFu);

                        for (int i = 0; i < count; i++)
                        {
                            if ((uint)pos >= (uint)_packedLineData.Length)
                            {
                                return;
                            }

                            if (!isTimeMask)
                            {
                                // Consume source byte (shape only).
                                pos++;
                                ShadePixel16(dstBuf, dstBase, dstPitch, dstBytesPerPixel, x0 + x + i, y0 + line, factor);
                                continue;
                            }

                            // TimeMask: [valueByte, timeByte]
                            if ((uint)(pos + 1) >= (uint)_packedLineData.Length)
                            {
                                return;
                            }

                            pos++; // value
                            byte t = _packedLineData[pos++];
                            if (t <= (byte)time)
                            {
                                ShadePixel16(dstBuf, dstBase, dstPitch, dstBytesPerPixel, x0 + x + i, y0 + line, factor);
                            }
                        }
                    }

                    x += count;

                    if ((uint)pos >= (uint)_packedLineData.Length)
                    {
                        break;
                    }

                    b = _packedLineData[pos];
                }
            }
        }

        // NXBasics::CBobManager::PrintBob_Shade_MMX(unsigned int, NXBasics::CBitmap const&, int, int, NXBasics::CPalette&, int) const
        internal void PrintBob_Shade_MMX(uint bobId, CBitmap destination, int dstX, int dstY, CPalette? palette, int shade)
        {
            PrintBob_Shade_MMX(bobId, destination, dstX, dstY, palette, shade, 0xFFFFFFFFu);
        }

        private void PrintBob_Shade_MMX(uint bobId, CBitmap destination, int dstX, int dstY, CPalette? palette, int shade, uint time)
        {
            if (destination == null)
            {
                return;
            }

            if (destination.BitsPerPixel != 32)
            {
                return;
            }

            if (shade < 0)
            {
                shade = 0;
            }
            if (shade > 255)
            {
                shade = 255;
            }

            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[]? dstBuf, out int dstBase, out int dstPitch, out int dstBytesPerPixel))
            {
                return;
            }

            if (dstBuf == null || dstBytesPerPixel < 4)
            {
                return;
            }

            int height = bob.Area.Height;
            if (height <= 0)
            {
                return;
            }

            int baseLine = bob.Area.Y;
            int x0 = dstX + bob.Area.X;
            int y0 = dstY + bob.Area.Y;

            int factor = 256 - shade;

            for (int line = 0; line < height; line++)
            {
                int ctrlIndex = baseLine + line;
                if ((uint)ctrlIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[ctrlIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                int pos = (int)(ctrl & PackedOffsetMask);
                if ((uint)pos >= (uint)_packedLineData.Length)
                {
                    continue;
                }

                int x = (int)(ctrl >> PackedXShift);
                byte b = _packedLineData[pos];

                while (b != 0)
                {
                    pos++;
                    int count = b & 0x7F;

                    if ((b & 0x80) == 0)
                    {
                        bool isTimeMask = (time != 0xFFFFFFFFu);

                        for (int i = 0; i < count; i++)
                        {
                            if ((uint)pos >= (uint)_packedLineData.Length)
                            {
                                return;
                            }

                            if (!isTimeMask)
                            {
                                pos++; // shape byte
                                ShadePixel32(dstBuf, dstBase, dstPitch, dstBytesPerPixel, x0 + x + i, y0 + line, factor);
                                continue;
                            }

                            if ((uint)(pos + 1) >= (uint)_packedLineData.Length)
                            {
                                return;
                            }

                            pos++; // value
                            byte t = _packedLineData[pos++];
                            if (t <= (byte)time)
                            {
                                ShadePixel32(dstBuf, dstBase, dstPitch, dstBytesPerPixel, x0 + x + i, y0 + line, factor);
                            }
                        }
                    }

                    x += count;

                    if ((uint)pos >= (uint)_packedLineData.Length)
                    {
                        break;
                    }

                    b = _packedLineData[pos];
                }
            }
        }

        // NXBasics::CBobManager::PrintBob_UsingShadedAlpha(unsigned int, NXBasics::CBitmap const&, int, int, NXBasics::CPalette*, int) const
        internal void PrintBob_UsingShadedAlpha(uint bobId, CBitmap destination, int dstX, int dstY, CPalette? palette, int shade)
        {
            if (destination == null)
            {
                return;
            }

            if (palette == null)
            {
                return;
            }

            if (shade < 0)
            {
                shade = 0;
            }
            if (shade > 255)
            {
                shade = 255;
            }

            if (destination.BitsPerPixel != 16 && destination.BitsPerPixel != 32)
            {
                return;
            }

            // Best-effort interpretation:
            // - Treat the bob as a double-byte bob: [indexByte, alphaByte] per pixel.
            // - Multiply alphaByte by (256 - shade) to get a final alpha.
            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return;
            }

            if (!destination.TryGetPixelBuffer(out byte[]? dstBuf, out int dstBase, out int dstPitch, out int dstBytesPerPixel))
            {
                return;
            }

            if (dstBuf == null)
            {
                return;
            }

            int height = bob.Area.Height;
            if (height <= 0)
            {
                return;
            }

            int baseLine = bob.Area.Y;
            int x0 = dstX + bob.Area.X;
            int y0 = dstY + bob.Area.Y;

            int shadeFactor = 256 - shade;

            if (destination.BitsPerPixel == 32)
            {
                uint[] table = palette.GetTrueColorTablePtr();

                for (int line = 0; line < height; line++)
                {
                    int ctrlIndex = baseLine + line;
                    if ((uint)ctrlIndex >= _lineControlCount)
                    {
                        continue;
                    }

                    uint ctrl = _lineControl[ctrlIndex];
                    if (ctrl == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int pos = (int)(ctrl & PackedOffsetMask);
                    if ((uint)pos >= (uint)_packedLineData.Length)
                    {
                        continue;
                    }

                    int x = (int)(ctrl >> PackedXShift);
                    byte b = _packedLineData[pos];

                    while (b != 0)
                    {
                        pos++;
                        int count = b & 0x7F;

                        if ((b & 0x80) == 0)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                if ((uint)(pos + 1) >= (uint)_packedLineData.Length)
                                {
                                    return;
                                }

                                byte srcIndex = _packedLineData[pos++];
                                byte alphaByte = _packedLineData[pos++];

                                int a = (alphaByte * shadeFactor) >> 8;
                                if (a <= 0)
                                {
                                    continue;
                                }

                                int invA = 256 - a;

                                uint srcColor = table[srcIndex];

                                int px = x0 + x + i;
                                int py = y0 + line;

                                int dstOffset = dstBase + (py * dstPitch) + (px * dstBytesPerPixel);
                                if ((uint)(dstOffset + 3) >= (uint)dstBuf.Length)
                                {
                                    continue;
                                }

                                uint dstColor = ReadUInt32LE(dstBuf, dstOffset);
                                uint blended = Blend32(dstColor, srcColor, a, invA);
                                WriteUInt32LE(dstBuf, dstOffset, blended);
                            }
                        }
                        else
                        {
                            // Skip run
                        }

                        x += count;

                        if ((uint)pos >= (uint)_packedLineData.Length)
                        {
                            break;
                        }

                        b = _packedLineData[pos];
                    }
                }

                return;
            }

            // 16bpp
            ushort[] table16 = palette.GetHighColorTablePtr();

            for (int line = 0; line < height; line++)
            {
                int ctrlIndex = baseLine + line;
                if ((uint)ctrlIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[ctrlIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                int pos = (int)(ctrl & PackedOffsetMask);
                if ((uint)pos >= (uint)_packedLineData.Length)
                {
                    continue;
                }

                int x = (int)(ctrl >> PackedXShift);
                byte b = _packedLineData[pos];

                while (b != 0)
                {
                    pos++;
                    int count = b & 0x7F;

                    if ((b & 0x80) == 0)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            if ((uint)(pos + 1) >= (uint)_packedLineData.Length)
                            {
                                return;
                            }

                            byte srcIndex = _packedLineData[pos++];
                            byte alphaByte = _packedLineData[pos++];

                            int a = (alphaByte * shadeFactor) >> 8;
                            if (a <= 0)
                            {
                                continue;
                            }

                            int invA = 256 - a;

                            ushort srcColor = table16[srcIndex];

                            int px = x0 + x + i;
                            int py = y0 + line;

                            int dstOffset = dstBase + (py * dstPitch) + (px * dstBytesPerPixel);
                            if ((uint)(dstOffset + 1) >= (uint)dstBuf.Length)
                            {
                                continue;
                            }

                            ushort dstColor = ReadUInt16LE(dstBuf, dstOffset);
                            ushort blended = Blend565(dstColor, srcColor, a, invA);
                            WriteUInt16LE(dstBuf, dstOffset, blended);
                        }
                    }

                    x += count;

                    if ((uint)pos >= (uint)_packedLineData.Length)
                    {
                        break;
                    }

                    b = _packedLineData[pos];
                }
            }
        }

        // NXBasics::CBobManager::PrintBob_FX_DrawOutline(unsigned int, NXBasics::CBitmap const&, int, int, NXBasics::SColorRGB const&, int) const
        internal void PrintBob_FX_DrawOutline(uint bobId, CBitmap destination, int dstX, int dstY, SColorRGB color, int thickness)
        {
            if (destination == null)
            {
                return;
            }

            if (thickness < 1)
            {
                thickness = 1;
            }

            // Best-effort outline: draw outline pixels (4-neighborhood) around covered pixels.
            // Implementation uses a per-line hash-set of covered pixels and outlines their neighbors.
            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return;
            }

            int height = bob.Area.Height;
            if (height <= 0)
            {
                return;
            }

            int baseLine = bob.Area.Y;
            int x0 = dstX + bob.Area.X;
            int y0 = dstY + bob.Area.Y;

            for (int line = 0; line < height; line++)
            {
                int ctrlIndex = baseLine + line;
                if ((uint)ctrlIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[ctrlIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                int pos = (int)(ctrl & PackedOffsetMask);
                if ((uint)pos >= (uint)_packedLineData.Length)
                {
                    continue;
                }

                int xMin = (int)(ctrl >> PackedXShift);

                System.Collections.Generic.HashSet<int> covered = [];
                CollectCoveredX(covered, pos, xMin);

                if (covered.Count == 0)
                {
                    continue;
                }

                // Outline neighbors.
                foreach (int x in covered)
                {
                    for (int t = 1; t <= thickness; t++)
                    {
                        DrawOutlinePixel(destination, x0 + x - t, y0 + line, color);
                        DrawOutlinePixel(destination, x0 + x + t, y0 + line, color);
                        DrawOutlinePixel(destination, x0 + x, y0 + line - t, color);
                        DrawOutlinePixel(destination, x0 + x, y0 + line + t, color);
                    }
                }
            }
        }

        // NXBasics::CBobManager::PrintBob_FX_DrawArea(unsigned int, NXBasics::CBitmap const&, int, int, NXBasics::SColorRGB const&, int) const
        internal void PrintBob_FX_DrawArea(uint bobId, CBitmap destination, int dstX, int dstY, SColorRGB color, int expand)
        {
            if (destination == null)
            {
                return;
            }

            if (expand < 0)
            {
                expand = 0;
            }

            // Best-effort "area" effect: fill the bob's covered pixels expanded by 'expand' in X.
            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            SBobData bob = _bobData[index];
            if (bob.Type == 0)
            {
                return;
            }

            int height = bob.Area.Height;
            if (height <= 0)
            {
                return;
            }

            int baseLine = bob.Area.Y;
            int x0 = dstX + bob.Area.X;
            int y0 = dstY + bob.Area.Y;

            for (int line = 0; line < height; line++)
            {
                int ctrlIndex = baseLine + line;
                if ((uint)ctrlIndex >= _lineControlCount)
                {
                    continue;
                }

                uint ctrl = _lineControl[ctrlIndex];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                int pos = (int)(ctrl & PackedOffsetMask);
                if ((uint)pos >= (uint)_packedLineData.Length)
                {
                    continue;
                }

                int xMin = (int)(ctrl >> PackedXShift);

                HashSet<int> covered = [];
                CollectCoveredX(covered, pos, xMin);

                if (covered.Count == 0)
                {
                    continue;
                }

                foreach (int x in covered)
                {
                    for (int e = -expand; e <= expand; e++)
                    {
                        DrawOutlinePixel(destination, x0 + x + e, y0 + line, color);
                    }
                }
            }
        }

        // -------------------- TimeMask line printers (managed) --------------------

        private void PrintPackedLine_TimeMaskAsImage(CBitmap dst, int dstBaseX, int dstY, int xMin, uint packedOffset, uint time, CPalette palette, byte[]? remap)
        {
            int pos = (int)packedOffset;
            if ((uint)pos >= (uint)_packedLineData.Length)
            {
                return;
            }

            int x = xMin;
            byte b = _packedLineData[pos];

            while (b != 0)
            {
                pos++;
                int count = b & 0x7F;

                if ((b & 0x80) == 0)
                {
                    // Raw run: [valueByte, timeByte] pairs
                    for (int i = 0; i < count; i++)
                    {
                        if ((uint)(pos + 1) >= (uint)_packedLineData.Length)
                        {
                            return;
                        }

                        byte value = _packedLineData[pos++];
                        byte t = _packedLineData[pos++];

                        if (t > (byte)time)
                        {
                            continue;
                        }

                        if (remap != null)
                        {
                            value = remap[value];
                        }

                        if (dst.BitsPerPixel == 8)
                        {
                            dst.Draw_SetPixel(dstBaseX + x + i, dstY, value);
                        }
                        else if (dst.BitsPerPixel == 16)
                        {
                            ushort[] table = palette.GetHighColorTablePtr();
                            dst.Draw_SetPixel(dstBaseX + x + i, dstY, table[value]);
                        }
                        else if (dst.BitsPerPixel == 32)
                        {
                            uint[] table = palette.GetTrueColorTablePtr();
                            dst.Draw_SetPixel(dstBaseX + x + i, dstY, table[value]);
                        }
                    }
                }

                x += count;

                if ((uint)pos >= (uint)_packedLineData.Length)
                {
                    return;
                }

                b = _packedLineData[pos];
            }
        }

        private void PrintPackedLine_TimeMask8bpp(CBitmap dst, int dstBaseX, int dstY, int xMin, uint packedOffset, uint time, byte[]? remap)
        {
            int pos = (int)packedOffset;
            if ((uint)pos >= (uint)_packedLineData.Length)
            {
                return;
            }

            int x = xMin;
            byte b = _packedLineData[pos];

            while (b != 0)
            {
                pos++;
                int count = b & 0x7F;

                if ((b & 0x80) == 0)
                {
                    // Raw run: [valueByte, timeByte] pairs
                    for (int i = 0; i < count; i++)
                    {
                        if ((uint)(pos + 1) >= (uint)_packedLineData.Length)
                        {
                            return;
                        }

                        byte value = _packedLineData[pos++];
                        byte t = _packedLineData[pos++];

                        if (t > (byte)time)
                        {
                            continue;
                        }

                        byte outValue = (remap == null) ? value : remap[value];
                        dst.Draw_SetPixel(dstBaseX + x + i, dstY, outValue);
                    }
                }

                x += count;

                if ((uint)pos >= (uint)_packedLineData.Length)
                {
                    return;
                }

                b = _packedLineData[pos];
            }
        }

        // -------------------- Outline / area helpers --------------------

        private void CollectCoveredX(HashSet<int> covered, int packedOffset, int xMin)
        {
            int pos = packedOffset;
            if ((uint)pos >= (uint)_packedLineData.Length)
            {
                return;
            }

            int x = xMin;
            byte b = _packedLineData[pos];

            while (b != 0)
            {
                pos++;
                int count = b & 0x7F;

                if ((b & 0x80) == 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if ((uint)pos >= (uint)_packedLineData.Length)
                        {
                            return;
                        }

                        pos++; // consume source
                        covered.Add(x + i);
                    }
                }

                x += count;

                if ((uint)pos >= (uint)_packedLineData.Length)
                {
                    return;
                }

                b = _packedLineData[pos];
            }
        }

        private static void DrawOutlinePixel(CBitmap dst, int x, int y, SColorRGB color)
        {
            if (dst.BitsPerPixel == 8)
            {
                // No palette given in this FX path; write max index as a visible marker.
                dst.Draw_SetPixel(x, y, 0xFF);
                return;
            }

            if (dst.BitsPerPixel == 16)
            {
                ushort c = To565(color);
                dst.Draw_SetPixel(x, y, c);
                return;
            }

            if (dst.BitsPerPixel == 32)
            {
                uint c = ToBgr32(color);
                dst.Draw_SetPixel(x, y, c);
            }
        }

        // -------------------- Pixel math helpers --------------------

        private static uint Blend32(uint dst, uint src, int a, int invA)
        {
            int db = (int)(dst & 0xFF);
            int dg = (int)((dst >> 8) & 0xFF);
            int dr = (int)((dst >> 16) & 0xFF);

            int sb = (int)(src & 0xFF);
            int sg = (int)((src >> 8) & 0xFF);
            int sr = (int)((src >> 16) & 0xFF);

            int ob = ((sb * a) + (db * invA)) >> 8;
            int og = ((sg * a) + (dg * invA)) >> 8;
            int orr = ((sr * a) + (dr * invA)) >> 8;

            return (uint)(ob | (og << 8) | (orr << 16));
        }

        private static ushort Blend565(ushort dst, ushort src, int a, int invA)
        {
            int db = dst & 0x1F;
            int dg = (dst >> 5) & 0x3F;
            int dr = (dst >> 11) & 0x1F;

            int sb = src & 0x1F;
            int sg = (src >> 5) & 0x3F;
            int sr = (src >> 11) & 0x1F;

            int ob = ((sb * a) + (db * invA)) >> 8;
            int og = ((sg * a) + (dg * invA)) >> 8;
            int orr = ((sr * a) + (dr * invA)) >> 8;

            if (ob < 0) ob = 0; else if (ob > 31) ob = 31;
            if (og < 0) og = 0; else if (og > 63) og = 63;
            if (orr < 0) orr = 0; else if (orr > 31) orr = 31;

            return (ushort)(ob | (og << 5) | (orr << 11));
        }

        private static uint AddColor32(uint dst, int addR, int addG, int addB)
        {
            int b = (int)(dst & 0xFF) + addB;
            int g = (int)((dst >> 8) & 0xFF) + addG;
            int r = (int)((dst >> 16) & 0xFF) + addR;

            if (b > 255) b = 255;
            if (g > 255) g = 255;
            if (r > 255) r = 255;

            return (uint)(b | (g << 8) | (r << 16));
        }

        private static ushort AddColor565(ushort dst, int addR, int addG, int addB)
        {
            int db = dst & 0x1F;
            int dg = (dst >> 5) & 0x3F;
            int dr = (dst >> 11) & 0x1F;

            int b = db + (addB >> 3);
            int g = dg + (addG >> 2);
            int r = dr + (addR >> 3);

            if (b > 31) b = 31;
            if (g > 63) g = 63;
            if (r > 31) r = 31;

            return (ushort)(b | (g << 5) | (r << 11));
        }

        private static ushort To565(SColorRGB c)
        {
            int r = (c.R * 31) / 255;
            int g = (c.G * 63) / 255;
            int b = (c.B * 31) / 255;
            return (ushort)(b | (g << 5) | (r << 11));
        }

        private static uint ToBgr32(SColorRGB c)
        {
            return (uint)(c.B | (c.G << 8) | (c.R << 16));
        }

        private static ushort ReadUInt16LE(byte[] buf, int offset)
        {
            return (ushort)(buf[offset] | (buf[offset + 1] << 8));
        }

        private static void WriteUInt16LE(byte[] buf, int offset, ushort value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static uint ReadUInt32LE(byte[] buf, int offset)
        {
            return (uint)(buf[offset] | (buf[offset + 1] << 8) | (buf[offset + 2] << 16) | (buf[offset + 3] << 24));
        }

        private static void WriteUInt32LE(byte[] buf, int offset, uint value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
            buf[offset + 2] = (byte)((value >> 16) & 0xFF);
            buf[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static void ShadePixel16(byte[] buf, int baseOffset, int pitch, int bpp, int x, int y, int factor)
        {
            int o = baseOffset + (y * pitch) + (x * bpp);
            if ((uint)(o + 1) >= (uint)buf.Length)
            {
                return;
            }

            ushort p = ReadUInt16LE(buf, o);

            int pb = p & 0x1F;
            int pg = (p >> 5) & 0x3F;
            int pr = (p >> 11) & 0x1F;

            pb = (pb * factor) >> 8;
            pg = (pg * factor) >> 8;
            pr = (pr * factor) >> 8;

            ushort outP = (ushort)(pb | (pg << 5) | (pr << 11));
            WriteUInt16LE(buf, o, outP);
        }

        private static void ShadePixel32(byte[] buf, int baseOffset, int pitch, int bpp, int x, int y, int factor)
        {
            int o = baseOffset + (y * pitch) + (x * bpp);
            if ((uint)(o + 3) >= (uint)buf.Length)
            {
                return;
            }

            uint p = ReadUInt32LE(buf, o);

            int b = (int)(p & 0xFF);
            int g = (int)((p >> 8) & 0xFF);
            int r = (int)((p >> 16) & 0xFF);

            b = (b * factor) >> 8;
            g = (g * factor) >> 8;
            r = (r * factor) >> 8;

            uint outP = (uint)(b | (g << 8) | (r << 16));
            WriteUInt32LE(buf, o, outP);
        }




        // -------------------------
        // Internal types
        // -------------------------

        internal enum TBobType : int
        {
            Bob8Bit = 1,
            Bob1Bit = 2,
            TimeMask = 3,
            Double8Bit = 4
        }

        internal enum TBobPrintHighColorMaskMode : int
        {
            Normal = 0,
            Invert = 1
        }

        private readonly struct StaticVars
        {
            private readonly ulong _dummy;

            public StaticVars()
            {
                _dummy = 0;
            }

            public static void Reset()
            {
                // Placeholder for the original "mStaticVars" zeroing; kept for layout parity.
            }
        }

        internal struct SBobData
        {
            public int Type;
            public SRectangle Area;
            public uint Misc;
        }
    }
}