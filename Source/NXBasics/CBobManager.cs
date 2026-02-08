using OpenVikings.NXBasics.Structs;
using System.Runtime.CompilerServices;
using static OpenVikings.NXBasics.CBitmap;

namespace OpenVikings.NXBasics
{
    // Note:
    // This translation keeps the original memory layout semantics (offset-based fields)
    // without inventing new dependencies/types.
    // Pointers that the original code reads/writes are represented as nint.

    internal unsafe sealed class CBobManager
    {
        // Assumed internal backing buffers that mirror the original layout:
        // this+0x08 : int baseBobId
        // this+0x0C : uint bobCount
        // this+0x30 : pointer to bob entry array (entry size 0x18)
        // this+0x38 : pointer to packed line data buffer
        // this+0x40 : pointer to line control array (uint[])
        private int _baseBobId;
        private uint _bobCount;
        private nint _bobEntryArray;     // byte*
        private nint _packedLineData;    // byte*
        private nint _lineControlArray;  // uint*
        private readonly BobEntry[] _bobEntries;  // this+0x30
        private readonly uint[] _lineControl;     // this+0x40

        // === Instance fields mapped from decompile offsets ===
        // this + 0x08 .. 0x20 (7 x uint)
        private uint _firstBobId;        // +0x08
        private readonly uint _bobEntryCount;      // this+0x0C
        private uint _numberOfBobs;      // +0x0C
        private uint _field10;           // +0x10
        private uint _field14;           // +0x14
        private uint _field18;           // +0x18
        private uint _field1C;           // +0x1C
        private uint _field20;           // +0x20

        // this + 0x28 (bool flag in ctor)
        private bool _flag28;            // +0x28

        // this + 0x30, 0x38, 0x40 (raw pointers)
        private nint _bobDataPtr;        // +0x30 (points to bob entries, 0x18 bytes each)
        private nint _ptr38;             // +0x38
        private nint _ptr40;             // +0x40 (used by IsBobHit in original file)

        // this + 0x48, 0x50, 0x58 (object pointers loaded via XB_Storable_LoadObject)
        private nint _obj48;             // +0x48
        private nint _obj50;             // +0x50
        private nint _obj58;             // +0x58

        // === Static "mStaticVars" (8 bytes) ===
        private static ulong _mStaticVars;

        // This is the C++ "mStaticVars" pointer used as scratch/effect source.
        // You likely already have this in your CBobManager port as a static.
        private static CBitmap? _mStaticBitmap;

        private void L_Generate_AllocateBobDataStructureArrayBuffer(uint newCount) { }
        private void L_Generate_AllocatePackedLineDataBuffer(uint newSize) { }
        private void L_Generate_AllocateLineControlArrayBuffer(uint newCount) { }

        private static extern uint XB_Tool_FindMemoryInMemory(byte* haystack, uint haystackLen, byte* needle, uint needleLen);

        private static extern void* operator_new__(nuint size);
        private static extern void* operator_new(int size);
        private static extern void operator_delete__(void* ptr);


        // This corresponds to the C++ static guarded initialization inside PrintBob_UsingEffectMatrix.
        // In the decompile there are 32 "effect codes" (< 0x20) each mapping to (dx, dy).
        // The values visible in the dump clearly contain the 4x4 grid of offsets (-2/-1/+1/+2) for both axes.
        // The remaining pairs (for 32 total) are consistent with the earlier initialization block (-4..-1,+1..+4)
        // and the symmetric vertical line (0, -4..-1, +1..+4).
        private static readonly int[] s_effectDx =
        [
            // Horizontal line: (-4..-1, +1..+4)
            -4, -3, -2, -1,  1,  2,  3,  4,

            // Vertical line: (0, -4..-1, +1..+4)
             0,  0,  0,  0,  0,  0,  0,  0,

            // 4x4 grid: x in {-2,-1,+1,+2}, y = -2
            -2, -1,  1,  2,
            // y = -1
            -2, -1,  1,  2,
            // y = +1
            -2, -1,  1,  2,
            // y = +2
            -2, -1,  1,  2
        ];

        private static readonly int[] s_effectDy =
        [
            // Horizontal line y=0
             0, 0, 0, 0, 0, 0, 0, 0,

            // Vertical line x=0
            -4, -3, -2, -1,  1,  2,  3,  4,

            // 4x4 grid (y rows)
            -2, -2, -2, -2,
            -1, -1, -1, -1,
             1,  1,  1,  1,
             2,  2,  2,  2
        ];

        // Minimal shape the method expects (matches the RE layout: 0x18 per entry).
        // If you already have these differently, map the fields accordingly.
        private readonly struct BobEntry
        {
            internal readonly int Type;            // +0x00
            internal readonly SRectangle Rect;     // +0x04
            internal readonly int LineControlIndex;// +0x14
        }

        // Placeholder: keep your real SBobData layout; only these two fields are used here.
        internal readonly struct SBobData
        {
            internal readonly int Field_0x10;   // +0x10
            internal readonly uint Field_0x14;  // +0x14

            internal SBobData(int field0x10, uint field0x14)
            {
                Field_0x10 = field0x10;
                Field_0x14 = field0x14;
            }
        }

        // ------------------------------------------------------------
        // NXBasics::CBobManager::CBobManager(unsigned int, bool)
        // ------------------------------------------------------------
        internal CBobManager(uint param1, bool param2)
        {
            // vtable assignment ignored in C#
            ClearInstance();
            _mStaticVars = 0;

            _firstBobId = param1;
            _flag28 = param2;
        }

        // ------------------------------------------------------------
        // NXBasics::CBobManager::l_System_InitObject()
        // ------------------------------------------------------------
        internal void L_System_InitObject()
        {
            ClearInstance();
            _mStaticVars = 0;
        }

        // ------------------------------------------------------------
        // NXBasics::CBobManager::~CBobManager()
        // ------------------------------------------------------------
        internal void Destruct()
        {
            // vtable assignment ignored in C#

            // The original calls virtual destructor at vtable+0x20 for these objects:
            //   (**(code **)(**(long **)obj + 0x20))();
            // We cannot replicate vtable calls safely in C# without the real types,
            // so we only null the pointers to match lifetime semantics at this level.
            if (_obj48 != 0)
            {
                _obj48 = 0;
            }

            if (_obj50 != 0)
            {
                _obj50 = 0;
            }

            if (_obj58 != 0)
            {
                _obj58 = 0;
            }

            ClearInstance();
            _mStaticVars = 0;
        }

        // ------------------------------------------------------------
        // NXBasics::CBobManager::CBobManager(NXBasics::CFile&, unsigned int)
        // ------------------------------------------------------------
        internal CBobManager(CFile file, uint param2)
        {
            // vtable assignment ignored in C#
            ClearInstance();
            _mStaticVars = 0;

            // The original reads 7 longs, but your CFile.ReadLong() returns ulong in your codebase.
            // The decompile stores them into 32-bit fields, so we truncate to uint.
            _firstBobId = (uint)file.ReadLong();
            _numberOfBobs = (uint)file.ReadLong();
            _field10 = (uint)file.ReadLong();
            _field14 = (uint)file.ReadLong();
            _field18 = (uint)file.ReadLong();
            _field1C = (uint)file.ReadLong();
            _field20 = (uint)file.ReadLong();

            if (_numberOfBobs != 0)
            {
                // These functions must already exist in your project (do not invent them here).
                _obj48 = (nint)XB_Storable_LoadObject(file);
                _obj50 = (nint)XB_Storable_LoadObject(file);
                _obj58 = (nint)XB_Storable_LoadObject(file);
            }

            // The original caches raw pointers from loaded objects at +0x10.
            // Without the real object layouts, we cannot legally dereference them here.
            // So we keep the behavior structure but only do the null checks.
            if (_obj48 == 0)
            {
                _bobDataPtr = 0;
            }
            else
            {
                // Original: _bobDataPtr = *(long*)(_obj48 + 0x10);
                // Keep placeholder until you expose the real type/field.
                _bobDataPtr = 0;
            }

            if (_obj50 == 0)
            {
                _ptr38 = 0;
            }
            else
            {
                // Original: _ptr38 = *(long*)(_obj50 + 0x10);
                _ptr38 = 0;
            }

            if (_obj58 == 0)
            {
                _ptr40 = 0;
            }
            else
            {
                // Original: _ptr40 = *(long*)(_obj58 + 0x10);
                _ptr40 = 0;
            }

            _flag28 = false;
        }

        // ------------------------------------------------------------
        // NXBasics::CBobManager::l_System_SetRawPointer()
        // ------------------------------------------------------------
        internal void L_System_SetRawPointer()
        {
            // Decompile intent: refresh cached raw pointers from the 3 loaded objects.
            // If object is null -> cached ptr becomes 0; else cached ptr = *(obj + 0x10).
            // We keep the exact structure but cannot dereference unknown native layouts here.
            if (_obj48 == 0)
            {
                _bobDataPtr = 0;
            }
            else
            {
                // Original: _bobDataPtr = *(long*)(_obj48 + 0x10);
                _bobDataPtr = 0;
            }

            if (_obj50 == 0)
            {
                _ptr38 = 0;
            }
            else
            {
                // Original: _ptr38 = *(long*)(_obj50 + 0x10);
                _ptr38 = 0;
            }

            if (_obj58 == 0)
            {
                _ptr40 = 0;
            }
            else
            {
                // Original: _ptr40 = *(long*)(_obj58 + 0x10);
                _ptr40 = 0;
            }
        }

        // ------------------------------------------------------------
        // NXBasics::CBobManager::Storable_SaveData(NXBasics::CFile&)
        // ------------------------------------------------------------
        internal void Storable_SaveData(CFile file)
        {
            file.WriteLong(_firstBobId);
            file.WriteLong(_numberOfBobs);
            file.WriteLong(_field10);
            file.WriteLong(_field14);
            file.WriteLong(_field18);
            file.WriteLong(_field1C);
            file.WriteLong(_field20);

            if (_numberOfBobs != 0)
            {
                // Original writes 3 storables:
                //   XB_Storable_SaveObject(file, obj48/50/58);
                XB_Storable_SaveObject(file, (nint)_obj48);
                XB_Storable_SaveObject(file, (nint)_obj50);
                XB_Storable_SaveObject(file, (nint)_obj58);
            }
        }

        // ------------------------------------------------------------
        // NXBasics::CBobManager::l_Generate_FinalizePackedLineDataBuffer()
        // ------------------------------------------------------------
        internal void L_Generate_FinalizePackedLineDataBuffer()
        {
            // This method is small enough to translate 1:1,
            // but it depends on the real meaning of _ptr38/_obj50 and offsets within it.
            // The decompile shows it walking buffers and finalizing a packed-line buffer.
            //
            // Because the underlying buffers live behind native pointers and unknown layouts,
            // providing a fake managed reimplementation here would be "inventing dependencies".
            //
            // So we keep a faithful stub that you can wire once the underlying structures
            // (the object behind _obj50 and its fields) are identified.
        }

        // ------------------------------------------------------------
        // NXBasics::CBobManager::GetFirstBobId() const
        // ------------------------------------------------------------
        internal uint GetFirstBobId()
        {
            return _firstBobId;
        }

        // ------------------------------------------------------------
        // NXBasics::CBobManager::GetNumberOfBobs() const
        // ------------------------------------------------------------
        internal uint GetNumberOfBobs()
        {
            return _numberOfBobs;
        }

        // ------------------------------------------------------------
        // NXBasics::CBobManager::DoesBobExists(unsigned int) const
        // ------------------------------------------------------------
        internal bool DoesBobExists(uint bobId)
        {
            uint index = bobId - _firstBobId;
            if (index < _numberOfBobs)
            {
                nint entryPtr = GetBobEntryPtr(index);
                if (entryPtr == 0)
                {
                    return false;
                }

                int typeOrFlag = *(int*)entryPtr;
                return typeOrFlag != 0;
            }

            return false;
        }

        // ------------------------------------------------------------
        // NXBasics::CBobManager::GetBobType(unsigned int) const
        // ------------------------------------------------------------
        internal uint GetBobType(uint bobId)
        {
            uint result = 0;
            uint index = bobId - _firstBobId;

            if (index < _numberOfBobs)
            {
                nint entryPtr = GetBobEntryPtr(index);
                if (entryPtr != 0)
                {
                    result = (uint)(*(int*)entryPtr);
                }
            }

            return result;
        }

        // ------------------------------------------------------------
        // NXBasics::CBobManager::GetBobAreaRectanglePtr(unsigned int) const
        // returns pointer to entry+4 if entry type!=0 else 0
        // ------------------------------------------------------------
        internal nint GetBobAreaRectanglePtr(uint bobId)
        {
            uint index = bobId - _firstBobId;
            if (index < _numberOfBobs)
            {
                nint entryPtr = GetBobEntryPtr(index);
                if (entryPtr != 0)
                {
                    int typeOrFlag = *(int*)entryPtr;
                    if (typeOrFlag != 0)
                    {
                        return entryPtr + 4; // rectangle starts right after the first int
                    }
                }

                return 0;
            }

            return 0;
        }

        // ------------------------------------------------------------
        // NXBasics::CBobManager::Storable_GetId() const
        // ------------------------------------------------------------
        internal ulong Storable_GetId()
        {
            // Decompile: return 0x3f4;
            return 0x3F4UL;
        }

        // NXBasics::CBobManager::l_Generate_PackLine_Double8BitBob(...)
        // Pointer-free managed version: writes into output buffer and returns bytes written.
        internal int L_Generate_PackLine_Double8BitBob(byte[] output, int outputOffset, CBitmap bitmapA, uint startX, uint y, byte transparentA, CBitmap bitmapB, byte defaultB)
        {
            int writePos = outputOffset;
            int widthA = bitmapA.Width;

            int yi = unchecked((int)y);
            uint currentX = startX;

            // Local helpers to keep the method readable.
            byte ReadA(uint x)
            {
                int index = bitmapA.GetUnclippedPixelPtr(unchecked((int)x), yi);
                return bitmapA.Pixels8[index];
            }

            byte ReadBOrDefault(uint x)
            {
                // Keep the same intent as your old IsPointInside(bitmapB, x, y).
                // If you already have a fast bounds check, use it here.
                int xi = unchecked((int)x);

                if (xi >= 0 && xi < bitmapB.Width && yi >= 0 && yi < bitmapB.Height)
                {
                    int index = bitmapB.GetUnclippedPixelPtr(xi, yi);
                    return bitmapB.Pixels8[index];
                }

                return defaultB;
            }

            while (true)
            {
                // ---- 1) Count visible run in A (until transparent or end of row) ----
                uint scanX = currentX;
                uint runLen = 0;

                while (scanX < (uint)widthA)
                {
                    if (ReadA(scanX) == transparentA)
                    {
                        break;
                    }

                    runLen++;
                    scanX++;
                }

                // ---- 2) Emit visible run, chunked by 0x7F ----
                while (runLen > 0x7F)
                {
                    output[writePos] = 0x7F;
                    writePos++;

                    int i = 0;
                    while (i < 0x7F)
                    {
                        uint x = currentX + (uint)i;

                        output[writePos] = ReadA(x);
                        output[writePos + 1] = ReadBOrDefault(x);
                        writePos += 2;

                        i++;
                    }

                    currentX += 0x7F;
                    runLen -= 0x7F;
                }

                // Emit remaining (0..0x7F)
                output[writePos] = (byte)runLen;
                writePos++;

                if (runLen != 0)
                {
                    uint i = 0;
                    while (i < runLen)
                    {
                        uint x = currentX + i;

                        output[writePos] = ReadA(x);
                        output[writePos + 1] = ReadBOrDefault(x);
                        writePos += 2;

                        i++;
                    }

                    currentX += runLen;
                }

                // If we stopped because we hit end-of-row, terminate.
                if (scanX >= (uint)widthA)
                {
                    output[writePos] = 0;
                    writePos++;

                    return writePos - outputOffset;
                }

                // ---- 3) Count transparent skip in A starting at scanX ----
                uint skipStartX = scanX;
                uint skipLen = 0;

                while (scanX < (uint)widthA && ReadA(scanX) == transparentA)
                {
                    skipLen++;
                    scanX++;
                }

                // End-of-row after skipping transparents -> terminate.
                if (scanX >= (uint)widthA)
                {
                    output[writePos] = 0;
                    writePos++;

                    return writePos - outputOffset;
                }

                // ---- 4) Emit skip, chunked by 0x7F as 0xFF ----
                while (skipLen > 0x7F)
                {
                    output[writePos] = 0xFF; // 0x80|0x7F
                    writePos++;

                    skipLen -= 0x7F;
                    skipStartX += 0x7F;
                }

                output[writePos] = (byte)(0x80 | (byte)skipLen);
                writePos++;

                // Continue at first non-transparent pixel after skip.
                currentX = scanX;
            }
        }


        // NXBasics::CBobManager::AddBob8BitDouble(...)
        // Pointer-free, managed-buffer version.
        // Returns 1 on success, 0 on failure (low dword semantics preserved by ulong return type).
        internal ulong AddBob8BitDouble(uint bobId, CBitmap bitmapA, byte transparentA, int offsetX, int offsetY, CBitmap bitmapB, byte defaultB)
        {
            if (bitmapA.ColorDepthBits != 8 || bitmapB.ColorDepthBits != 8)
            {
                return 0;
            }

            if (CBitmap.GetBoundingRectangle(bitmapA, transparentA, out SRectangle bounds) == 0)
            {
                return 0;
            }

            uint index = bobId - _firstBobId;
            if (index >= 0x186A1)
            {
                return 0;
            }

            if (_numberOfBobs <= index)
            {
                L_Generate_AllocateBobDataStructureArrayBuffer(index + 1);
            }

            // NOTE:
            // Your original code writes into a packed native bob-entry array (0x18 bytes each).
            // In managed code you should already have some bob-entry storage; below we keep the same intent:
            // - entry.Type must be 0 to be free
            // - set Type=4
            // - store bounds (then move by -offsetX/-offsetY)
            // - store lineControlStart (entry+0x14)
            //
            // Replace these two lines with your real managed bob-entry access.
            BobEntry entry = _bobEntries[index];
            if (entry.Type != 0)
            {
                return 0;
            }

            entry.Type = 4;
            entry.Bounds = bounds;
            SRectangle.MovePosition(ref entry.Bounds, -offsetX, -offsetY);

            uint height = unchecked((uint)bounds.Height);
            uint width = unchecked((uint)bounds.Width);

            uint[] lineControl = new uint[height];
            byte[] temp = new byte[5000];

            // Cropped bitmaps covering bounds.
            // Keep the exact ctor/factory you already have; this line is the only place that depends on it.
            CBitmap croppedA = new CBitmap(bitmapA, in bounds);
            CBitmap croppedB = new CBitmap(bitmapB, in bounds);

            try
            {
                int widthA = croppedA.Width;
                int yi;

                if (height != 0)
                {
                    uint row = 0;
                    while (row < height)
                    {
                        _field18 += 1;

                        yi = unchecked((int)row);

                        if (width != 0)
                        {
                            uint x = 0;
                            uint xFlag = 0;

                            while (x < width)
                            {
                                // Check first pixel in A at (x, row)
                                int aIndex = croppedA.GetUnclippedPixelPtr(unchecked((int)x), yi);
                                byte a0 = croppedA.Pixels8[aIndex];

                                if (a0 != transparentA)
                                {
                                    int packedLen = L_Generate_PackLine_Double8BitBob(
                                        temp,
                                        0,
                                        croppedA,
                                        x,
                                        row,
                                        transparentA,
                                        croppedB,
                                        defaultB);

                                    if (_flag28)
                                    {
                                        int found = FindSubsequence(_packedLineData.AsSpan(0, unchecked((int)_field10)), temp.AsSpan(0, packedLen));
                                        if (found >= 0)
                                        {
                                            lineControl[row] = unchecked((uint)found) | xFlag;
                                            _field20 += 1;
                                            goto NextRow;
                                        }
                                    }

                                    uint writeOffset = _field10;
                                    lineControl[row] = xFlag | writeOffset;

                                    if (((ulong)packedLen + (ulong)writeOffset) < 0x400000UL)
                                    {
                                        EnsurePackedLineCapacity(writeOffset + unchecked((uint)packedLen));
                                        Buffer.BlockCopy(temp, 0, _packedLineData, unchecked((int)writeOffset), packedLen);

                                        _field10 = writeOffset + unchecked((uint)packedLen);
                                        goto NextRow;
                                    }

                                    return 0;
                                }

                                x += 1;
                                xFlag += 0x400000;
                            }
                        }

                        lineControl[row] = 0xFFFFFFFF;
                        _field1C += 1;

                    NextRow:
                        row += 1;
                    }
                }

                uint totalLines = height;
                uint lineControlCount = _field14;

                if (_flag28 && totalLines <= lineControlCount)
                {
                    uint start = 0;
                    while (start <= (lineControlCount - totalLines))
                    {
                        if (MemoryCompareUInt32(_lineControlArray, start, lineControl, totalLines))
                        {
                            entry.LineControlStart = unchecked((int)start);
                            _bobEntries[index] = entry;
                            return 1;
                        }

                        start += 1;
                    }
                }

                // Append new line-control block
                entry.LineControlStart = unchecked((int)lineControlCount);

                EnsureLineControlCapacity(lineControlCount + totalLines);
                Array.Copy(lineControl, 0, _lineControlArray, unchecked((int)lineControlCount), unchecked((int)totalLines));
                _field14 = lineControlCount + totalLines;

                _bobEntries[index] = entry;
                return 1;
            }
            finally
            {
                // Keep whatever disposal pattern your CBitmap uses.
                croppedA.Dispose();
                croppedB.Dispose();
            }
        }

        private static bool MemoryCompareUInt32(uint[] haystack, uint haystackStart, uint[] needle, uint count)
        {
            uint i = 0;
            int hs = unchecked((int)haystackStart);

            while (i < count)
            {
                if (haystack[hs + unchecked((int)i)] != needle[unchecked((int)i)])
                {
                    return false;
                }

                i += 1;
            }

            return true;
        }

        private static int FindSubsequence(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
        {
            if (needle.Length == 0)
            {
                return 0;
            }

            if (needle.Length > haystack.Length)
            {
                return -1;
            }

            int last = haystack.Length - needle.Length;
            int i = 0;

            while (i <= last)
            {
                if (haystack[i] == needle[0])
                {
                    if (haystack.Slice(i, needle.Length).SequenceEqual(needle))
                    {
                        return i;
                    }
                }

                i += 1;
            }

            return -1;
        }

        // Managed capacity helpers (replace with your existing Allocate* methods if you prefer)
        private void EnsurePackedLineCapacity(uint requiredSize)
        {
            if (_packedLineData == null)
            {
                _packedLineData = new byte[requiredSize];
                return;
            }

            if ((uint)_packedLineData.Length >= requiredSize)
            {
                return;
            }

            uint newSize = requiredSize;
            if (newSize < (uint)_packedLineData.Length * 2U)
            {
                newSize = (uint)_packedLineData.Length * 2U;
            }

            Array.Resize(ref _packedLineData, unchecked((int)newSize));
        }

        private void EnsureLineControlCapacity(uint requiredCount)
        {
            if (_lineControlArray == null)
            {
                _lineControlArray = new uint[requiredCount];
                return;
            }

            if ((uint)_lineControlArray.Length >= requiredCount)
            {
                return;
            }

            uint newCount = requiredCount;
            if (newCount < (uint)_lineControlArray.Length * 2U)
            {
                newCount = (uint)_lineControlArray.Length * 2U;
            }

            Array.Resize(ref _lineControlArray, unchecked((int)newCount));
        }

        // NXBasics::CBobManager::Generate_SetMemoryOptimizationFlag(bool)
        internal void Generate_SetMemoryOptimizationFlag(bool enabled)
        {
            _flag28 = enabled;
        }

        // NXBasics::CBobManager::l_Generate_PackLine_8BitBob(...)
        internal int L_Generate_PackLine_8BitBob(byte[] output, int outputOffset, CBitmap bitmap, uint x, uint y, byte transparent, bool copyPixels)
        {
            return L_Generate_PackLine_DoPack(output, outputOffset, bitmap, x, y, transparent, copyPixels);
        }

        // NXBasics::CBobManager::l_Generate_PackLine_DoPack(...)
        // Pointer-free: writes into output buffer starting at outputOffset and returns bytes written.
        internal int L_Generate_PackLine_DoPack(byte[] output, int outputOffset, CBitmap bitmap, uint startX, uint y, byte transparent, bool copyPixels)
        {
            int writePos = outputOffset;

            int width = bitmap.Width;
            int yi = unchecked((int)y);

            while (true)
            {
                // Find run of non-transparent pixels from startX until transparent or end of row.
                uint x = startX;
                uint runLen = 0;

                while (x < (uint)width)
                {
                    int pixelIndex = bitmap.GetUnclippedPixelPtr(unchecked((int)x), yi);
                    if (bitmap.Pixels8[pixelIndex] == transparent)
                    {
                        break;
                    }

                    runLen++;
                    x++;
                }

                // Emit visible run in chunks of 0x7F.
                while (runLen > 0x7F)
                {
                    output[writePos] = 0x7F;
                    writePos++;

                    if (copyPixels)
                    {
                        int srcIndex = bitmap.GetUnclippedPixelPtr(unchecked((int)startX), yi);
                        Buffer.BlockCopy(bitmap.Pixels8, srcIndex, output, writePos, 0x7F);
                        writePos += 0x7F;
                    }

                    startX += 0x7F;
                    runLen -= 0x7F;
                }

                // Emit remaining visible run (0..0x7F).
                output[writePos] = (byte)runLen;
                writePos++;

                if (copyPixels && runLen != 0)
                {
                    int srcIndex = bitmap.GetUnclippedPixelPtr(unchecked((int)startX), yi);
                    Buffer.BlockCopy(bitmap.Pixels8, srcIndex, output, writePos, unchecked((int)runLen));
                    writePos += unchecked((int)runLen);
                }

                // If we reached end of row, terminate.
                if (x == (uint)width)
                {
                    output[writePos] = 0;
                    writePos++;

                    return writePos - outputOffset;
                }

                // Count transparent skip starting at x (which is at a transparent pixel).
                uint skipLen = 0;

                while (x < (uint)width)
                {
                    int pixelIndex = bitmap.GetUnclippedPixelPtr(unchecked((int)x), yi);
                    if (bitmap.Pixels8[pixelIndex] != transparent)
                    {
                        break;
                    }

                    skipLen++;
                    x++;

                    if (x == (uint)width)
                    {
                        output[writePos] = 0;
                        writePos++;

                        return writePos - outputOffset;
                    }
                }

                // Emit skip in chunks. Encoding: (skipLen | 0x80). If skipLen == 0x7F => 0xFF.
                while (skipLen > 0x7F)
                {
                    output[writePos] = 0xFF; // 0x80|0x7F
                    writePos++;

                    skipLen -= 0x7F;
                }

                output[writePos] = (byte)(0x80 | (byte)skipLen);
                writePos++;

                // Continue after the skipped transparent pixels.
                startX = x;
            }
        }

        // NXBasics::CBobManager::l_Generate_PackLine_1BitBob(unsigned char*, NXBasics::CBitmap const*, unsigned int, unsigned int, unsigned char) const
        internal void L_Generate_PackLine_1BitBob(byte[] output, int outputOffset, CBitmap bitmap, uint x, uint y, byte transparent)
        {
            _ = L_Generate_PackLine_DoPack(output, outputOffset, bitmap, x, y, transparent, false);
        }

        // NXBasics::CBobManager::l_Generate_PackLine_TimeMaskBob(...)
        //
        // Run-length encode a scanline of 8-bit values starting at (startX, y).
        // Emits pairs: [count (1..255), value].
        // If the current run value is 0xFF, it stops (does not emit that run) and only writes the 0 terminator.
        // Always appends a single 0 byte terminator.
        // Returns bytes written including the terminator.
        internal int L_Generate_PackLine_TimeMaskBob(byte[] output, int outputOffset, CBitmap bitmap, uint startX, uint y, byte unusedParam5)
        {
            int writePos = outputOffset;

            uint width = unchecked((uint)bitmap.Width);
            if (startX >= width)
            {
                output[writePos] = 0;
                return 1;
            }

            int yi = unchecked((int)y);

            byte ReadPixel(uint x)
            {
                int index = bitmap.GetUnclippedPixelPtr(unchecked((int)x), yi);
                return bitmap.Pixels8[index];
            }

            byte currentValue = ReadPixel(startX);
            uint runLen = 1;

            uint xPos = startX + 1;
            while (xPos < width)
            {
                byte value = ReadPixel(xPos);

                if (value == currentValue)
                {
                    runLen++;
                }
                else
                {
                    if (currentValue == 0xFF)
                    {
                        output[writePos] = 0;
                        return (writePos - outputOffset) + 1;
                    }

                    // Emit run in chunks of 255
                    while (runLen > 0xFF)
                    {
                        output[writePos] = 0xFF;
                        output[writePos + 1] = currentValue;
                        writePos += 2;

                        runLen -= 0xFF;
                    }

                    output[writePos] = (byte)runLen;
                    output[writePos + 1] = currentValue;
                    writePos += 2;

                    currentValue = value;
                    runLen = 1;
                }

                xPos++;
            }

            // Final run
            if (currentValue != 0xFF)
            {
                while (runLen > 0xFF)
                {
                    output[writePos] = 0xFF;
                    output[writePos + 1] = currentValue;
                    writePos += 2;

                    runLen -= 0xFF;
                }

                output[writePos] = (byte)runLen;
                output[writePos + 1] = currentValue;
                writePos += 2;
            }

            output[writePos] = 0;
            return (writePos - outputOffset) + 1;
        }


        // NXBasics::CBobManager::AddBob(unsigned int, NXBasics::TBobType, NXBasics::CBitmap const&, unsigned char, int, int)
        internal ulong AddBob(uint bobId, int bobType, CBitmap bitmap, byte transparent, int offsetX, int offsetY)
        {
            if (!bitmap.GetBoundingRectangle(transparent, out SRectangle bounds))
            {
                return 0;
            }

            byte[] pixels8 = bitmap.Pixels8;
            if (pixels8 == null)
            {
                return 0;
            }

            uint index = bobId - _firstBobId;
            if (index >= 0x186A1)
            {
                return 0;
            }

            if (_numberOfBobs <= index)
            {
                L_Generate_AllocateBobDataStructureArrayBuffer(index + 1);
            }

            // Pointer-free bob entry access (replace with your actual managed storage).
            BobEntry entry = _bobEntries[index];
            if (entry.Type != 0)
            {
                return 0;
            }

            entry.Type = bobType;
            entry.Bounds = bounds;
            SRectangle.MovePosition(ref entry.Bounds, -offsetX, -offsetY);

            uint rectWidth = unchecked((uint)bounds.Width);
            uint rectHeight = unchecked((uint)bounds.Height);

            uint[] lineControl = new uint[rectHeight];
            byte[] tempPack = new byte[5000];

            // Crop once so our packers automatically operate within bounds.
            CBitmap cropped = new(bitmap, in bounds);

            try
            {
                if (rectHeight != 0)
                {
                    for (uint row = 0; row < rectHeight; row++)
                    {
                        _field18 += 1;

                        if (rectWidth == 0)
                        {
                            lineControl[row] = 0xFFFFFFFF;
                            _field1C += 1;
                            continue;
                        }

                        // Find first non-transparent pixel inside the bounded row.
                        uint localX = 0;
                        uint xFlag = 0;

                        int yi = unchecked((int)row);

                        while (localX < rectWidth)
                        {
                            int aIndex = cropped.GetUnclippedPixelPtr(unchecked((int)localX), yi);
                            if (cropped.Pixels8[aIndex] != transparent)
                            {
                                break;
                            }

                            localX += 1;
                            xFlag += 0x400000;
                        }

                        // Entire bounded row transparent.
                        if (localX >= rectWidth)
                        {
                            lineControl[row] = 0xFFFFFFFF;
                            _field1C += 1;
                            continue;
                        }

                        int packedLen;

                        if (bobType == 3)
                        {
                            packedLen = L_Generate_PackLine_TimeMaskBob(tempPack, 0, cropped, localX, row, 0);
                        }
                        else
                        {
                            bool copyPixels;

                            if (bobType == 2)
                            {
                                copyPixels = false;
                            }
                            else if (bobType == 1)
                            {
                                copyPixels = true;
                            }
                            else
                            {
                                return 0;
                            }

                            packedLen = L_Generate_PackLine_DoPack(tempPack, 0, cropped, localX, row, transparent, copyPixels);
                        }

                        int foundIndex = -1;

                        if (_flag28)
                        {
                            foundIndex = FindSubsequence(_packedLineData.AsSpan(0, unchecked((int)_field10)), tempPack.AsSpan(0, packedLen));
                        }

                        if (!_flag28 || foundIndex < 0)
                        {
                            uint writeOffset = _field10;
                            lineControl[row] = xFlag | writeOffset;

                            // Original: fail if (0x3FFFFF < writeOffset + packedLen)
                            uint newEnd = writeOffset + unchecked((uint)packedLen);
                            if (newEnd > 0x3FFFFF)
                            {
                                return 0;
                            }

                            EnsurePackedLineCapacity(newEnd);
                            Buffer.BlockCopy(tempPack, 0, _packedLineData, unchecked((int)writeOffset), packedLen);
                            _field10 = newEnd;
                        }
                        else
                        {
                            lineControl[row] = xFlag | unchecked((uint)foundIndex);
                            _field20 += 1;
                        }
                    }
                }

                uint lineControlUsed = _field14;

                if (_flag28 && rectHeight <= lineControlUsed)
                {
                    uint maxStart = lineControlUsed - rectHeight;

                    uint start = 0;
                    while (start <= maxStart)
                    {
                        if (MemoryCompareUInt32(_lineControlArray, start, lineControl, rectHeight))
                        {
                            entry.LineControlStart = unchecked((int)start);
                            _bobEntries[index] = entry;
                            return 1;
                        }

                        start += 1;
                    }
                }

                // Append new line-control block.
                entry.LineControlStart = unchecked((int)lineControlUsed);

                EnsureLineControlCapacity(lineControlUsed + rectHeight);
                Array.Copy(lineControl, 0, _lineControlArray, unchecked((int)lineControlUsed), unchecked((int)rectHeight));
                _field14 = lineControlUsed + rectHeight;

                _bobEntries[index] = entry;
                return 1;
            }
            finally
            {
                cropped.Dispose();
            }

            static bool MemoryCompareUInt32(uint[] haystack, uint haystackStart, uint[] needle, uint count)
            {
                int hs = unchecked((int)haystackStart);
                uint i = 0;

                while (i < count)
                {
                    if (haystack[hs + unchecked((int)i)] != needle[unchecked((int)i)])
                    {
                        return false;
                    }

                    i += 1;
                }

                return true;
            }

            static int FindSubsequence(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
            {
                if (needle.Length == 0)
                {
                    return 0;
                }

                if (needle.Length > haystack.Length)
                {
                    return -1;
                }

                int last = haystack.Length - needle.Length;
                int i = 0;

                while (i <= last)
                {
                    if (haystack[i] == needle[0])
                    {
                        if (haystack.Slice(i, needle.Length).SequenceEqual(needle))
                        {
                            return i;
                        }
                    }

                    i += 1;
                }

                return -1;
            }
        }

        // NXBasics::CBobManager::PrintBob_UsingEffectMatrix(unsigned int, NXBasics::CBitmap const&, int, int) const
        internal void PrintBob_UsingEffectMatrix(uint bobId, CBitmap target, int dstX, int dstY)
        {
            uint rel = bobId - _firstBobId;
            if (rel >= _numberOfBobs)
            {
                return;
            }

            BobEntry entry = _bobEntries[rel];
            int bobType = entry.Type;
            if (bobType == 0)
            {
                return;
            }

            SRectangle bobRect = entry.Bounds;
            bobRect.MovePosition(dstX, dstY);

            SRectangle targetRect = target.Rect;

            if (!bobRect.IsTouching(targetRect))
            {
                return;
            }

            SRectangle clipped = new SRectangle(in bobRect);
            bool didCut = clipped.CutInsideX(targetRect);

            if (!didCut &&
                bobType == 1 &&
                _mStaticBitmap != null &&
                _mStaticBitmap.Format == target.Format)
            {
                uint lineControlIndex = unchecked((uint)entry.LineControlStart);

                int startRowInBob = clipped.Y - bobRect.Y;
                if (startRowInBob < 0)
                {
                    startRowInBob = 0;
                }

                int metaW = entry.MetaW;
                int metaH = entry.MetaH;

                using CBitmap scratch = new CBitmap(_mStaticBitmap, 0, 0, metaW + 10, metaH + 10);

                target.CopyIntoBitmap(scratch, 5 - bobRect.X, 5 - clipped.Y);

                int rowsToDraw = clipped.Height;
                if (rowsToDraw <= 0)
                {
                    return;
                }

                int lineControlStart = unchecked((int)lineControlIndex) + startRowInBob;

                if (target.Format == BitmapFormat.TrueColor32)
                {
                    DrawEffect_32bpp(
                        target,
                        scratch,
                        lineControlStart,
                        clipped,
                        bobRect,
                        rowsToDraw);
                }
                else if (target.Format == BitmapFormat.HighColor16)
                {
                    DrawEffect_16bpp(
                        target,
                        scratch,
                        lineControlStart,
                        clipped,
                        bobRect,
                        rowsToDraw);
                }

                return;
            }
        }

        private void DrawEffect_32bpp(
            CBitmap target,
            CBitmap scratch,
            int lineControlStart,
            in SRectangle clipped,
            in SRectangle bobRectMoved,
            int rowsToDraw)
        {
            int scratchY = 5;
            int dstY = clipped.Y;

            int row = 0;
            while (row < rowsToDraw)
            {
                uint control = _lineControlArray[lineControlStart + row];
                if (control == 0xFFFFFFFF)
                {
                    scratchY++;
                    dstY++;
                    row++;
                    continue;
                }

                uint xFlag = control >> 0x16;          // multiples of 0x400000
                uint dataOffset = control & 0x3FFFFF;  // packed-line offset

                int dstX = bobRectMoved.X + unchecked((int)xFlag);
                int srcX = 5 + unchecked((int)xFlag);

                int packedPos = unchecked((int)dataOffset);

                byte token = _packedLineData[packedPos];
                if (token == 0)
                {
                    scratchY++;
                    dstY++;
                    row++;
                    continue;
                }

                while (token != 0)
                {
                    packedPos++;

                    if (unchecked((sbyte)token) < 0)
                    {
                        int skip = token & 0x7F;
                        dstX += skip;
                        srcX += skip;
                    }
                    else
                    {
                        int runLen = token;

                        int i = 0;
                        while (i < runLen)
                        {
                            byte effect = _packedLineData[packedPos + i];

                            if (effect < 0x20)
                            {
                                int dx = s_effectDx[effect];
                                int dy = s_effectDy[effect];

                                uint color = scratch.Draw_GetPixel_Long(srcX + i + dx, scratchY + dy);
                                target.Draw_SetPixel(dstX + i + dx, dstY + dy, color);
                            }
                            else if (effect < 0x40)
                            {
                                int idx = effect - 0x20;
                                int dx = s_effectDx[idx];
                                int dy = s_effectDy[idx];

                                uint color = scratch.Draw_GetPixel_Long(srcX + i + dx, scratchY + dy);
                                target.Draw_SetPixel(dstX + i, dstY, color);
                            }

                            i++;
                        }

                        dstX += runLen;
                        srcX += runLen;
                        packedPos += runLen;
                    }

                    token = _packedLineData[packedPos];
                }

                scratchY++;
                dstY++;
                row++;
            }
        }

        private void DrawEffect_16bpp(
            CBitmap target,
            CBitmap scratch,
            int lineControlStart,
            in SRectangle clipped,
            in SRectangle bobRectMoved,
            int rowsToDraw)
        {
            int scratchY = 5;
            int dstY = clipped.Y;

            int row = 0;
            while (row < rowsToDraw)
            {
                uint control = _lineControlArray[lineControlStart + row];
                if (control == 0xFFFFFFFF)
                {
                    scratchY++;
                    dstY++;
                    row++;
                    continue;
                }

                uint xFlag = control >> 0x16;
                uint dataOffset = control & 0x3FFFFF;

                int dstX = bobRectMoved.X + unchecked((int)xFlag);
                int srcX = 5 + unchecked((int)xFlag);

                int packedPos = unchecked((int)dataOffset);

                byte token = _packedLineData[packedPos];
                if (token == 0)
                {
                    scratchY++;
                    dstY++;
                    row++;
                    continue;
                }

                while (token != 0)
                {
                    packedPos++;

                    if (unchecked((sbyte)token) < 0)
                    {
                        int skip = token & 0x7F;
                        dstX += skip;
                        srcX += skip;
                    }
                    else
                    {
                        int runLen = token;

                        int i = 0;
                        while (i < runLen)
                        {
                            byte effect = _packedLineData[packedPos + i];

                            if (effect < 0x20)
                            {
                                int dx = s_effectDx[effect];
                                int dy = s_effectDy[effect];

                                ushort color = scratch.Draw_GetPixel_Word(srcX + i + dx, scratchY + dy);
                                target.Draw_SetPixel(dstX + i + dx, dstY + dy, color);
                            }
                            else if (effect < 0x40)
                            {
                                int idx = effect - 0x20;
                                int dx = s_effectDx[idx];
                                int dy = s_effectDy[idx];

                                ushort color = scratch.Draw_GetPixel_Word(srcX + i + dx, scratchY + dy);
                                target.Draw_SetPixel(dstX + i, dstY, color);
                            }

                            i++;
                        }

                        dstX += runLen;
                        srcX += runLen;
                        packedPos += runLen;
                    }

                    token = _packedLineData[packedPos];
                }

                scratchY++;
                dstY++;
                row++;
            }
        }

        // NXBasics::CBobManager::PrintBob_UsingHorizontalEffectUnbuffered(...)
        internal void PrintBob_UsingHorizontalEffectUnbuffered(uint bobId, CBitmap target, int dstX, int dstY)
        {
            uint rel = bobId - _firstBobId;
            if (rel >= _numberOfBobs)
            {
                return;
            }

            BobEntry entry = _bobEntries[rel];
            int bobType = entry.Type;

            if (bobType != 1)
            {
                return;
            }

            SRectangle bobRect = entry.Bounds;
            bobRect.MovePosition(dstX, dstY);

            SRectangle targetRect = target.Rect;
            if (!bobRect.IsTouching(targetRect))
            {
                return;
            }

            SRectangle clipped = new SRectangle(in bobRect);
            bool didCut = clipped.CutInsideX(targetRect);

            int rowsToDraw = clipped.Height;
            if (rowsToDraw <= 0)
            {
                return;
            }

            uint lineControlIndex = unchecked((uint)entry.LineControlStart);

            int startRowInBob = clipped.Y - bobRect.Y;
            if (startRowInBob < 0)
            {
                startRowInBob = 0;
            }

            int lineControlStart = unchecked((int)lineControlIndex) + startRowInBob;

            int targetWidth = target.Width;
            int targetHeight = target.Height;

            int stride = target.StridePixels;
            if (stride <= 0)
            {
                stride = targetWidth;
            }

            if (target.Format == BitmapFormat.HighColor16)
            {
                ushort[] pixels16 = target.Pixels16;
                if (pixels16 == null)
                {
                    return;
                }

                DrawHorizontalEffect16(
                    pixels16,
                    stride,
                    targetWidth,
                    targetHeight,
                    lineControlStart,
                    clipped,
                    bobRect,
                    rowsToDraw,
                    didCut);
            }
            else if (target.Format == BitmapFormat.TrueColor32)
            {
                uint[] pixels32 = target.Pixels32;
                if (pixels32 == null)
                {
                    return;
                }

                DrawHorizontalEffect32(
                    pixels32,
                    stride,
                    targetWidth,
                    targetHeight,
                    lineControlStart,
                    clipped,
                    bobRect,
                    rowsToDraw,
                    didCut);
            }
        }

        private void DrawHorizontalEffect16(
            ushort[] pixels,
            int stride,
            int width,
            int height,
            int lineControlStart,
            in SRectangle clipped,
            in SRectangle bobRect,
            int rowsToDraw,
            bool didCut)
        {
            int yDest = clipped.Y;

            for (int row = 0; row < rowsToDraw; row++)
            {
                uint control = _lineControlArray[lineControlStart + row];
                if (control == 0xFFFFFFFF)
                {
                    yDest++;
                    continue;
                }

                uint xFlag = control >> 0x16;
                uint dataOffset = control & 0x3FFFFF;

                int xStart = bobRect.X + unchecked((int)xFlag);
                int packedPos = unchecked((int)dataOffset);

                DecodeAndApplyHorizontalEffect16(
                    pixels,
                    stride,
                    width,
                    height,
                    packedPos,
                    xStart,
                    yDest,
                    clipped);

                yDest++;
            }
        }

        private void DecodeAndApplyHorizontalEffect16(
            ushort[] pixels,
            int stride,
            int width,
            int height,
            int packedPos,
            int xStart,
            int yDest,
            in SRectangle clipped)
        {
            byte token = _packedLineData[packedPos];
            if (token == 0)
            {
                return;
            }

            int x = xStart;

            while (token != 0)
            {
                packedPos++;

                if (unchecked((sbyte)token) < 0)
                {
                    int skip = token & 0x7F;
                    x += skip;
                }
                else
                {
                    int runLen = token;

                    for (int i = 0; i < runLen; i++)
                    {
                        int destX = x + i;

                        if (destX < clipped.X || destX > (clipped.X + clipped.Width - 1))
                        {
                            continue;
                        }

                        if (yDest < 0 || yDest >= height)
                        {
                            continue;
                        }

                        int offsetIndex = packedPos + i;
                        if ((uint)offsetIndex >= (uint)_packedLineData.Length)
                        {
                            continue;
                        }

                        byte offset = _packedLineData[offsetIndex];
                        int ySrc = yDest + offset;

                        if (ySrc < 0 || ySrc >= height)
                        {
                            continue;
                        }

                        if (destX < 0 || destX >= width)
                        {
                            continue;
                        }

                        int dstIndex = (yDest * stride) + destX;
                        int srcIndex = (ySrc * stride) + destX;

                        pixels[dstIndex] = pixels[srcIndex];
                    }

                    x += runLen;
                    packedPos += runLen;
                }

                token = _packedLineData[packedPos];
            }
        }

        private void DrawHorizontalEffect32(
            uint[] pixels,
            int stride,
            int width,
            int height,
            int lineControlStart,
            in SRectangle clipped,
            in SRectangle bobRect,
            int rowsToDraw,
            bool didCut)
        {
            int yDest = clipped.Y;

            for (int row = 0; row < rowsToDraw; row++)
            {
                uint control = _lineControlArray[lineControlStart + row];
                if (control == 0xFFFFFFFF)
                {
                    yDest++;
                    continue;
                }

                uint xFlag = control >> 0x16;
                uint dataOffset = control & 0x3FFFFF;

                int xStart = bobRect.X + unchecked((int)xFlag);
                int packedPos = unchecked((int)dataOffset);

                DecodeAndApplyHorizontalEffect32(
                    pixels,
                    stride,
                    width,
                    height,
                    packedPos,
                    xStart,
                    yDest,
                    clipped);

                yDest++;
            }
        }

        private void DecodeAndApplyHorizontalEffect32(
            uint[] pixels,
            int stride,
            int width,
            int height,
            int packedPos,
            int xStart,
            int yDest,
            in SRectangle clipped)
        {
            byte token = _packedLineData[packedPos];
            if (token == 0)
            {
                return;
            }

            int x = xStart;

            while (token != 0)
            {
                packedPos++;

                if (unchecked((sbyte)token) < 0)
                {
                    int skip = token & 0x7F;
                    x += skip;
                }
                else
                {
                    int runLen = token;

                    for (int i = 0; i < runLen; i++)
                    {
                        int destX = x + i;

                        if (destX < clipped.X || destX > (clipped.X + clipped.Width - 1))
                        {
                            continue;
                        }

                        if (yDest < 0 || yDest >= height)
                        {
                            continue;
                        }

                        int offsetIndex = packedPos + i;
                        if ((uint)offsetIndex >= (uint)_packedLineData.Length)
                        {
                            continue;
                        }

                        byte offset = _packedLineData[offsetIndex];
                        int ySrc = yDest + offset;

                        if (ySrc < 0 || ySrc >= height)
                        {
                            continue;
                        }

                        if (destX < 0 || destX >= width)
                        {
                            continue;
                        }

                        int dstIndex = (yDest * stride) + destX;
                        int srcIndex = (ySrc * stride) + destX;

                        pixels[dstIndex] = pixels[srcIndex];
                    }

                    x += runLen;
                    packedPos += runLen;
                }

                token = _packedLineData[packedPos];
            }
        }

        // NXBasics::CBobManager::PrintBob_UseTimeMaskAsMask(...)
        internal void PrintBob_UseTimeMaskAsMask(uint bobId, uint threshold, CBitmap target, int dstX, int dstY, byte[] maskLut)
        {
            if (maskLut == null)
            {
                return;
            }

            uint rel = bobId - _firstBobId;
            if (rel >= _numberOfBobs)
            {
                return;
            }

            BobEntry entry = _bobEntries[rel];
            int bobType = entry.Type;

            // TimeMask bob required
            if (bobType != 3)
            {
                return;
            }

            SRectangle bobRect = entry.Bounds;
            bobRect.MovePosition(dstX, dstY);

            SRectangle targetRect = target.Rect;

            if (!bobRect.IsTouching(targetRect))
            {
                return;
            }

            SRectangle clipped = new SRectangle(in bobRect);
            clipped.CutInside(targetRect);

            int rowsToDraw = clipped.Height;
            if (rowsToDraw <= 0)
            {
                return;
            }

            int targetWidth = target.Width;

            int stride = target.StridePixels;
            if (stride <= 0)
            {
                stride = targetWidth;
            }

            uint lineControlIndex = unchecked((uint)entry.LineControlStart);

            int startRowInBob = clipped.Y - bobRect.Y;
            int rowIndexAdjustment = 0;
            if (startRowInBob < 0)
            {
                rowIndexAdjustment = -startRowInBob;
            }

            int lineControlStart = unchecked((int)lineControlIndex) + rowIndexAdjustment;

            int bobRightMinus1 = bobRect.X + clipped.Width - 1;

            if (target.Format == BitmapFormat.Indexed8)
            {
                byte[] pixels8 = target.Pixels8;
                if (pixels8 == null)
                {
                    return;
                }

                int yDest = clipped.Y;

                for (int row = 0; row < rowsToDraw; row++)
                {
                    uint control = _lineControlArray[lineControlStart + row];
                    if (control != 0xFFFFFFFF)
                    {
                        int packedPos = unchecked((int)(control & 0x3FFFFF));
                        int x = bobRect.X + unchecked((int)(control >> 0x16));

                        ApplyTimeMaskAsMask8(
                            pixels8,
                            stride,
                            targetWidth,
                            yDest,
                            x,
                            bobRightMinus1,
                            clipped,
                            packedPos,
                            threshold,
                            maskLut);
                    }

                    yDest++;
                }

                return;
            }

            if (target.Format == BitmapFormat.TrueColor32)
            {
                uint[] pixels32 = target.Pixels32;
                if (pixels32 == null)
                {
                    return;
                }

                int yDest = clipped.Y;

                for (int row = 0; row < rowsToDraw; row++)
                {
                    uint control = _lineControlArray[lineControlStart + row];
                    if (control != 0xFFFFFFFF)
                    {
                        int packedPos = unchecked((int)(control & 0x3FFFFF));
                        int x = bobRect.X + unchecked((int)(control >> 0x16));

                        ApplyTimeMaskAsMask32(
                            pixels32,
                            stride,
                            targetWidth,
                            yDest,
                            x,
                            bobRightMinus1,
                            clipped,
                            packedPos,
                            threshold);
                    }

                    yDest++;
                }
            }
        }

        private void ApplyTimeMaskAsMask8(
            byte[] pixels,
            int stride,
            int width,
            int yDest,
            int xStart,
            int bobRightMinus1,
            in SRectangle clipped,
            int packedPos,
            uint threshold,
            byte[] lut)
        {
            int x = xStart;

            int clippedLeft = clipped.X;
            int clippedRightMinus1 = clipped.X + clipped.Width - 1;

            byte token = _packedLineData[packedPos];
            while (token != 0)
            {
                byte runLen = token;
                byte value = _packedLineData[packedPos + 1];

                if (value <= threshold)
                {
                    int runStart = x;
                    int runEndExclusive = x + runLen;

                    int writeStart = runStart;
                    int writeEnd = runEndExclusive;

                    if (writeStart < clippedLeft) writeStart = clippedLeft;
                    if (writeEnd - 1 > clippedRightMinus1) writeEnd = clippedRightMinus1 + 1;

                    if (writeStart < 0) writeStart = 0;
                    if (writeEnd > width) writeEnd = width;

                    if (writeEnd - 1 > bobRightMinus1) writeEnd = bobRightMinus1 + 1;

                    if (writeEnd > writeStart)
                    {
                        int baseIndex = (yDest * stride) + writeStart;
                        int count = writeEnd - writeStart;

                        for (int i = 0; i < count; i++)
                        {
                            byte p = pixels[baseIndex + i];
                            pixels[baseIndex + i] = lut[p];
                        }
                    }
                }

                x += runLen;
                packedPos += 2;
                token = _packedLineData[packedPos];
            }
        }

        private void ApplyTimeMaskAsMask32(
            uint[] pixels,
            int stride,
            int width,
            int yDest,
            int xStart,
            int bobRightMinus1,
            in SRectangle clipped,
            int packedPos,
            uint threshold)
        {
            int x = xStart;

            int clippedLeft = clipped.X;
            int clippedRightMinus1 = clipped.X + clipped.Width - 1;

            byte token = _packedLineData[packedPos];
            while (token != 0)
            {
                byte runLen = token;
                byte value = _packedLineData[packedPos + 1];

                if (value <= threshold)
                {
                    int runStart = x;
                    int runEndExclusive = x + runLen;

                    int writeStart = runStart;
                    int writeEnd = runEndExclusive;

                    if (writeStart < clippedLeft) writeStart = clippedLeft;
                    if (writeEnd - 1 > clippedRightMinus1) writeEnd = clippedRightMinus1 + 1;

                    if (writeStart < 0) writeStart = 0;
                    if (writeEnd > width) writeEnd = width;

                    if (writeEnd - 1 > bobRightMinus1) writeEnd = bobRightMinus1 + 1;

                    if (writeEnd > writeStart)
                    {
                        int baseIndex = (yDest * stride) + writeStart;
                        int count = writeEnd - writeStart;

                        for (int i = 0; i < count; i++)
                        {
                            uint p = pixels[baseIndex + i];
                            pixels[baseIndex + i] = (p >> 1) & 0x007F7F7Fu;
                        }
                    }
                }

                x += runLen;
                packedPos += 2;
                token = _packedLineData[packedPos];
            }
        }

        // NXBasics::CBobManager::PrintBob_UseTimeMaskAsImage(...)
        internal void PrintBob_UseTimeMaskAsImage(uint bobId, uint threshold, CBitmap target, int dstX, int dstY)
        {
            uint rel = bobId - _firstBobId;
            if (rel >= _numberOfBobs)
            {
                return;
            }

            BobEntry entry = _bobEntries[rel];

            // Needs bobType == 3 (time mask)
            if (entry.Type != 3)
            {
                return;
            }

            SRectangle bobRect = entry.Bounds;
            bobRect.MovePosition(dstX, dstY);

            SRectangle targetRect = target.Rect;

            if (!bobRect.IsTouching(targetRect))
            {
                return;
            }

            SRectangle clipped = new SRectangle(in bobRect);
            clipped.CutInside(targetRect);

            if (target.Format != BitmapFormat.Indexed8 || clipped.Height <= 0)
            {
                return;
            }

            byte[] pixels = target.Pixels8;
            if (pixels == null)
            {
                return;
            }

            int stride = target.StridePixels;
            if (stride <= 0)
            {
                stride = target.Width;
            }

            int bobRightMinus1 = bobRect.X + clipped.Width - 1;

            int startRowInBob = clipped.Y - bobRect.Y;

            int rowIndexAdjustment = 0;
            if (startRowInBob < 0)
            {
                rowIndexAdjustment = -startRowInBob;
            }

            uint lineControlIndex = unchecked((uint)entry.LineControlStart);
            int lineControlStart = unchecked((int)lineControlIndex) + rowIndexAdjustment;

            int targetWidth = target.Width;
            int yDest = clipped.Y;

            int clippedLeft = clipped.X;
            int clippedRightMinus1 = clipped.X + clipped.Width - 1;

            int rowsToDraw = clipped.Height;

            for (int row = 0; row < rowsToDraw; row++)
            {
                uint control = _lineControlArray[lineControlStart + row];
                if (control != 0xFFFFFFFF)
                {
                    int packedPos = unchecked((int)(control & 0x3FFFFF));
                    int x = bobRect.X + unchecked((int)(control >> 0x16));

                    while (true)
                    {
                        byte len = _packedLineData[packedPos];
                        if (len == 0)
                        {
                            break;
                        }

                        byte value = _packedLineData[packedPos + 1];

                        if (value > threshold)
                        {
                            x += len;
                        }
                        else
                        {
                            int runStart = x;
                            int runEndExclusive = x + len;

                            int writeStart = runStart;
                            int writeEnd = runEndExclusive;

                            if (writeStart < clippedLeft) writeStart = clippedLeft;
                            if (writeEnd - 1 > clippedRightMinus1) writeEnd = clippedRightMinus1 + 1;

                            if (writeStart < 0) writeStart = 0;
                            if (writeEnd > targetWidth) writeEnd = targetWidth;

                            if (writeEnd - 1 > bobRightMinus1) writeEnd = bobRightMinus1 + 1;

                            if (writeEnd > writeStart)
                            {
                                int baseIndex = (yDest * stride) + writeStart;
                                int count = writeEnd - writeStart;

                                pixels.AsSpan(baseIndex, count).Fill(value);
                            }

                            x += len;
                        }

                        packedPos += 2;
                    }
                }

                yDest++;
            }
        }

        // NXBasics::CBobManager::PrintBob_1Bit_HighColor(...)
        internal void PrintBob_1Bit_HighColor(uint bobId, CBitmap target, int dstX, int dstY, uint maskMode)
        {
            uint rel = bobId - _firstBobId;
            if (rel >= _numberOfBobs)
            {
                return;
            }

            BobEntry entry = _bobEntries[rel];

            // Needs bobType == 2 (1-bit bob)
            if (entry.Type != 2)
            {
                return;
            }

            if (target.Format != BitmapFormat.HighColor16)
            {
                return;
            }

            CHighColorCreator creator = CXBSystemManager.sHighColorCreatorPtr;
            if (creator == null)
            {
                return;
            }

            SRectangle bobRect = entry.Bounds;
            bobRect.MovePosition(dstX, dstY);

            SRectangle targetRect = target.Rect;

            if (!bobRect.IsTouching(targetRect))
            {
                return;
            }

            SRectangle clipped = new SRectangle(in bobRect);
            clipped.CutInside(targetRect);

            if (clipped.Height <= 0 || clipped.Width <= 0)
            {
                return;
            }

            ushort[] pixels16 = target.Pixels16;
            if (pixels16 == null)
            {
                return;
            }

            int stride = target.StridePixels;
            if (stride <= 0)
            {
                stride = target.Width;
            }

            // lineControlStart = entry.LineControlStart + max(0, -(clipped.Y - bobRect.Y))
            int startRowInBob = clipped.Y - bobRect.Y;
            int rowIndexAdjustment = 0;

            if (startRowInBob < 0)
            {
                rowIndexAdjustment = -startRowInBob;
            }

            int lineControlStart = entry.LineControlStart + rowIndexAdjustment;

            int yDest = clipped.Y;

            int clippedLeft = clipped.X;
            int clippedRightMinus1 = clipped.X + clipped.Width - 1;

            int targetWidth = target.Width;

            // Packed stream tokens:
            //  token == 0 -> end of line
            //  token < 0  -> skip (token & 0x7F)
            //  token >= 0 -> run  (token & 0x7F) : apply effect on existing target pixels
            for (int row = 0; row < clipped.Height; row++)
            {
                uint control = _lineControlArray[lineControlStart + row];
                if (control != 0xFFFFFFFF)
                {
                    int packedPos = unchecked((int)(control & 0x3FFFFF));
                    int x = bobRect.X + unchecked((int)(control >> 0x16));

                    byte token = _packedLineData[packedPos];

                    while (token != 0)
                    {
                        if (unchecked((sbyte)token) < 0)
                        {
                            int skip = token & 0x7F;
                            x += skip;

                            packedPos++;
                            token = _packedLineData[packedPos];
                            continue;
                        }

                        int runLen = token & 0x7F;
                        if (runLen != 0)
                        {
                            int runStart = x;
                            int runEndExclusive = x + runLen;

                            int writeStart = runStart;
                            int writeEnd = runEndExclusive;

                            if (writeStart < clippedLeft) writeStart = clippedLeft;
                            if (writeEnd - 1 > clippedRightMinus1) writeEnd = clippedRightMinus1 + 1;

                            if (writeStart < 0) writeStart = 0;
                            if (writeEnd > targetWidth) writeEnd = targetWidth;

                            if (writeEnd > writeStart)
                            {
                                int baseIndex = (yDest * stride) + writeStart;
                                int count = writeEnd - writeStart;

                                ApplyHighColorMaskEffect(pixels16, baseIndex, count, creator, maskMode);
                            }

                            x += runLen;
                        }

                        packedPos++;
                        token = _packedLineData[packedPos];
                    }
                }

                yDest++;
            }
        }

        // NXBasics::CBobManager::PrintBob_1Bit_TrueColor(...)
        //
        // Renders a 1-bit bob onto a 32bpp (true color) target by applying a per-pixel transform
        // on the target pixels where the bob mask has "solid" runs.
        internal void PrintBob_1Bit_TrueColor(uint bobId, CBitmap target, int dstX, int dstY)
        {
            uint rel = bobId - _firstBobId;
            if (rel >= _numberOfBobs)
            {
                return;
            }

            BobEntry entry = _bobEntries[rel];
            if (entry.Type != 2)
            {
                return;
            }

            // Target must be true color.
            if (target.Format != BitmapFormat.TrueColor32)
            {
                return;
            }

            uint[] pixels32 = target.Pixels32;
            if (pixels32 == null)
            {
                return;
            }

            SRectangle bobRect = entry.Bounds;
            bobRect.MovePosition(dstX, dstY);

            SRectangle targetRect = target.Rect;
            if (!bobRect.IsTouching(in targetRect))
            {
                return;
            }

            SRectangle clipped = new SRectangle(in bobRect);
            clipped.CutInside(in targetRect);

            if (clipped.Height <= 0)
            {
                return;
            }

            int targetWidth = target.Width;
            int targetHeight = target.Height;

            int stride = target.StridePixels;
            if (stride <= 0)
            {
                stride = targetWidth;
            }

            int rightBobMinus1 = bobRect.X + bobRect.Width - 1;

            // Line control start: entry.LineControlStart + ((bobRect.Y < 0) ? -bobRect.Y : 0)
            int lineControlStart = entry.LineControlStart;
            if (bobRect.Y < 0)
            {
                lineControlStart += -bobRect.Y;
            }

            int startRowInBob = clipped.Y - bobRect.Y;
            if (startRowInBob < 0)
            {
                startRowInBob = 0;
            }

            int lineControlIndex = lineControlStart + startRowInBob;

            int rowsToDraw = clipped.Height;

            for (int row = 0; row < rowsToDraw; row++)
            {
                uint control = _lineControlArray[lineControlIndex + row];
                if (control == 0xFFFFFFFFu)
                {
                    continue;
                }

                int xStart = bobRect.X + unchecked((int)(control >> 0x16));
                int packedPos = unchecked((int)(control & 0x3FFFFFu));

                int absoluteTargetY = clipped.Y + row;
                if (absoluteTargetY < 0 || absoluteTargetY >= targetHeight)
                {
                    continue;
                }

                int targetRowBase = absoluteTargetY * stride;

                bool needBoundsChecks = xStart < 0 || targetWidth <= rightBobMinus1;

                if (needBoundsChecks)
                {
                    DecodeAndApply_TrueColor_WithBoundsChecks(
                        packedPos,
                        xStart,
                        clipped,
                        targetRect,
                        pixels32,
                        targetRowBase,
                        stride,
                        targetWidth,
                        targetHeight,
                        absoluteTargetY);
                }
                else
                {
                    DecodeAndApply_TrueColor_Fast(
                        packedPos,
                        xStart,
                        clipped,
                        pixels32,
                        targetRowBase);
                }
            }
        }

        private void DecodeAndApply_TrueColor_WithBoundsChecks(
            int packedPos,
            int xStart,
            SRectangle bobClippedRect,
            SRectangle targetRect,
            uint[] pixels32,
            int targetRowBase,
            int targetStride,
            int targetWidth,
            int targetHeight,
            int absoluteTargetY)
        {
            int x = xStart;

            while (true)
            {
                byte token = _packedLineData[packedPos];
                if (token == 0)
                {
                    return;
                }

                packedPos++;

                // NOTE: This routine uses "high bit means run" encoding.
                // token & 0x80 != 0 => solid run (apply transform)
                // else => skip
                if ((token & 0x80) != 0)
                {
                    int runLen = token & 0x7F;

                    for (int i = 0; i < runLen; i++)
                    {
                        int px = x + i;

                        // Clip against bob-clipped rect (matches your intent checks)
                        if (px < bobClippedRect.X || px > (bobClippedRect.X + bobClippedRect.Width - 1))
                        {
                            continue;
                        }

                        // Clip against target rect
                        if (px < targetRect.X || px >= targetRect.X + targetRect.Width)
                        {
                            continue;
                        }

                        if (absoluteTargetY < targetRect.Y || absoluteTargetY >= targetRect.Y + targetRect.Height)
                        {
                            continue;
                        }

                        // General bounds safety
                        if (px < 0 || px >= targetWidth)
                        {
                            continue;
                        }

                        int idx = targetRowBase + px;
                        pixels32[idx] = TransformTrueColorPixel(pixels32[idx]);
                    }

                    x += runLen;
                }
                else
                {
                    x += token;
                }
            }
        }

        private void DecodeAndApply_TrueColor_Fast(
            int packedPos,
            int xStart,
            SRectangle bobClippedRect,
            uint[] pixels32,
            int targetRowBase)
        {
            int x = xStart;

            while (true)
            {
                byte token = _packedLineData[packedPos];
                if (token == 0)
                {
                    return;
                }

                packedPos++;

                if ((token & 0x80) != 0)
                {
                    int runLen = token & 0x7F;

                    for (int i = 0; i < runLen; i++)
                    {
                        int px = x + i;

                        // Even in fast path, keep the bob-clipped check (matches your original guard).
                        if (px >= bobClippedRect.X && px <= (bobClippedRect.X + bobClippedRect.Width - 1))
                        {
                            int idx = targetRowBase + px;
                            pixels32[idx] = TransformTrueColorPixel(pixels32[idx]);
                        }
                    }

                    x += runLen;
                }
                else
                {
                    x += token;
                }
            }
        }

        // Exact scalar pixel transform derived from the decompiled arithmetic (non-SIMD path).
        private static uint TransformTrueColorPixel(uint pixel)
        {
            uint uVar36 = (pixel >> 15) & 0xFFFFFFFEu;
            uint uVar37 = ((pixel & 0xFFu) * 2u) / 3u;

            uint partR = (uVar36 / 3u) << 16;

            uint partG = (pixel >> 7) & 0x1FEu;
            partG = (partG * 0xAAABu) >> 9;
            partG &= 0xFF00u;

            uint partB = (((uVar36 / 3u) + uVar37) >> 4) + uVar37;

            return partR | partG | partB;
        }

        // NXBasics::CBobManager::PrintBob_FX_DrawArea(...)
        internal void PrintBob_FX_DrawArea(uint bobId, CBitmap target, int x, int y, in SColorRGB color, int thickness)
        {
            uint index = bobId - _baseBobId;
            if (index >= _bobCount)
            {
                return;
            }

            BobEntry entry = _bobEntries[index];
            int bobType = entry.Type;
            if (bobType == 0)
            {
                return;
            }

            SRectangle bobRect = entry.Bounds;
            int lineControlStart = entry.LineControlStart;

            bobRect.MovePosition(x, y);

            SRectangle targetRect = target.Rect;
            if (!bobRect.IsTouching(in targetRect))
            {
                return;
            }

            SRectangle clipped = new SRectangle(in bobRect);
            clipped.CutInside(in targetRect);

            int yOffset = bobRect.Y < 0 ? -bobRect.Y : 0;
            int lineCtrlBase = lineControlStart + yOffset;

            int dstY = clipped.Y;
            int rows = clipped.Height;
            int startX = bobRect.X;

            if (bobType == 1)
            {
                for (int row = 0; row < rows; row++)
                {
                    uint ctrl = _lineControlArray[lineCtrlBase + row];
                    if (ctrl != 0xFFFFFFFFu)
                    {
                        int packedPos = unchecked((int)(ctrl & 0x3FFFFFu));
                        byte control = _packedLineData[packedPos];

                        if (control != 0)
                        {
                            int curX = startX;

                            do
                            {
                                packedPos++;
                                int len = control & 0x7F;

                                if (unchecked((sbyte)control) >= 0)
                                {
                                    target.Draw_HorizontalLine(curX - thickness, dstY, unchecked((uint)len) + unchecked((uint)(thickness * 2)), in color);
                                    packedPos += len;
                                }

                                curX += len;
                                control = _packedLineData[packedPos];
                            } while (control != 0);
                        }
                    }

                    dstY++;
                }

                return;
            }

            if (bobType == 4)
            {
                for (int row = 0; row < rows; row++)
                {
                    uint ctrl = _lineControlArray[lineCtrlBase + row];
                    if (ctrl != 0xFFFFFFFFu)
                    {
                        int packedPos = unchecked((int)(ctrl & 0x3FFFFFu));
                        byte control = _packedLineData[packedPos];

                        if (control != 0)
                        {
                            int curX = startX;

                            do
                            {
                                packedPos++;
                                int len = control & 0x7F;

                                if (unchecked((sbyte)control) >= 0)
                                {
                                    target.Draw_HorizontalLine(curX - thickness, dstY, unchecked((uint)len) + unchecked((uint)(thickness * 2)), in color);
                                    packedPos += len * 2;
                                }

                                curX += len;
                                control = _packedLineData[packedPos];
                            } while (control != 0);
                        }
                    }

                    dstY++;
                }
            }
        }

        // NXBasics::CBobManager::PrintBob_Shade_HighColor(...)
        internal void PrintBob_Shade_HighColor(uint bobId, CBitmap target, int dstX, int dstY, CPalette palette, int shade)
        {
            uint bobIndex = bobId - _baseBobId;
            if (bobIndex >= _bobCount)
            {
                return;
            }

            BobEntry entry = _bobEntries[unchecked((int)bobIndex)];
            if (entry.Type == 0)
            {
                return;
            }

            // Only packed-line bobs (Type==1) and HighColor16 target (0x10)
            if (entry.Type != 1 || target.FormatCode != 0x10)
            {
                return;
            }

            CHighColorCreator creator = CXBSystemManager.sHighColorCreatorPtr;
            if (creator == null)
            {
                return;
            }

            SRectangle moved = entry.Rect;
            moved.MovePosition(dstX, dstY);

            SRectangle targetRect = target.Rect;

            if (!moved.IsTouching(in targetRect))
            {
                return;
            }

            SRectangle clipped = new SRectangle(in moved);
            bool clippedInX = clipped.CutInsideX(in targetRect);

            if (clipped.Height <= 0)
            {
                return;
            }

            int startSkipLines = moved.Y < 0 ? -moved.Y : 0;
            int lineCtrlBase = entry.LineControlIndex + startSkipLines;

            int rightMoved = moved.X + moved.Width - 1;
            int targetWidth = target.Width;

            byte blueShiftDown = creator.BlueShiftDown;
            byte greenShiftDown = creator.GreenShiftDown;
            byte redShiftDown = creator.RedShiftDown;

            byte blueShiftUp = creator.BlueShiftUp;
            byte greenShiftUp = creator.GreenShiftUp;
            byte redShiftUp = creator.RedShiftUp;

            if ((uint)shade < 0x81u)
            {
                blueShiftDown = unchecked((byte)(blueShiftDown + 7));
                greenShiftDown = unchecked((byte)(greenShiftDown + 7));
                redShiftDown = unchecked((byte)(redShiftDown + 7));
            }

            ushort rMask = creator.RMask16;
            ushort gMask = creator.GMask16;
            ushort bMask = creator.BMask16;

            int dstStride = target.StridePixels;
            if (dstStride <= 0)
            {
                dstStride = targetWidth;
            }

            ushort[] dst16 = target.Pixels16;
            if (dst16 == null)
            {
                return;
            }

            int dstYLine = clipped.Y;

            for (int line = 0; line < clipped.Height; line++, dstYLine++, lineCtrlBase++)
            {
                uint ctrl = _lineControlArray[lineCtrlBase];
                if (ctrl == 0xFFFFFFFFu)
                {
                    continue;
                }

                int packedOffset = unchecked((int)(ctrl & 0x3FFFFFu));
                int xStartFromCtrl = unchecked((int)(ctrl >> 0x16));
                int x = xStartFromCtrl + moved.X;

                int p = packedOffset;
                if (_packedLineData[p] == 0)
                {
                    continue;
                }

                bool canFastWrite = !clippedInX && x >= 0 && rightMoved < targetWidth;

                if ((uint)shade < 0x81u)
                {
                    if (canFastWrite)
                    {
                        int dstIndex = (dstYLine * dstStride) + x;

                        byte code = _packedLineData[p];
                        while (code != 0)
                        {
                            p++;

                            if ((code & 0x80) != 0)
                            {
                                dstIndex += (code & 0x7F);
                            }
                            else
                            {
                                int run = code & 0x7F;

                                for (int i = 0; i < run; i++)
                                {
                                    byte palIndex = _packedLineData[p++];
                                    int palBase = (palIndex * 4) + 8;

                                    uint b = unchecked((uint)((byte)palette[palBase + 0] * shade)) & 0xFFFFu;
                                    uint g = unchecked((uint)((byte)palette[palBase + 1] * shade)) & 0xFFFFu;
                                    uint r = unchecked((uint)((byte)palette[palBase + 2] * shade)) & 0xFFFFu;

                                    ushort pixel =
                                        (ushort)(
                                            (((b >> (blueShiftDown & 0x1F)) << (blueShiftUp & 0x1F)) & 0xFFFFu) |
                                            (((g >> (greenShiftDown & 0x1F)) << (greenShiftUp & 0x1F)) & 0xFFFFu) |
                                            (((r >> (redShiftDown & 0x1F)) << (redShiftUp & 0x1F)) & 0xFFFFu));

                                    dst16[dstIndex] = pixel;
                                    dstIndex++;
                                }
                            }

                            code = _packedLineData[p];
                        }
                    }
                    else
                    {
                        int xMin = clipped.X;
                        int xMax = clipped.X + clipped.Width - 1;

                        byte code = _packedLineData[p];
                        int xCur = x;

                        while (code != 0)
                        {
                            p++;

                            if ((code & 0x80) != 0)
                            {
                                xCur += (code & 0x7F);
                            }
                            else
                            {
                                int run = code & 0x7F;

                                for (int i = 0; i < run; i++)
                                {
                                    byte palIndex = _packedLineData[p++];

                                    if (xCur >= xMin && xCur <= xMax)
                                    {
                                        int palBase = (palIndex * 4) + 8;

                                        uint b = unchecked((uint)((byte)palette[palBase + 0] * shade)) & 0xFFFFu;
                                        uint g = unchecked((uint)((byte)palette[palBase + 1] * shade)) & 0xFFFFu;
                                        uint r = unchecked((uint)((byte)palette[palBase + 2] * shade)) & 0xFFFFu;

                                        ushort pixel =
                                            (ushort)(
                                                (((b >> (blueShiftDown & 0x1F)) << (blueShiftUp & 0x1F)) & 0xFFFFu) |
                                                (((g >> (greenShiftDown & 0x1F)) << (greenShiftUp & 0x1F)) & 0xFFFFu) |
                                                (((r >> (redShiftDown & 0x1F)) << (redShiftUp & 0x1F)) & 0xFFFFu));

                                        dst16[(dstYLine * dstStride) + xCur] = pixel;
                                    }

                                    xCur++;
                                }
                            }

                            code = _packedLineData[p];
                        }
                    }
                }
                else
                {
                    if (canFastWrite)
                    {
                        int dstIndex = (dstYLine * dstStride) + x;

                        byte code = _packedLineData[p];
                        while (code != 0)
                        {
                            p++;

                            if ((code & 0x80) != 0)
                            {
                                dstIndex += (code & 0x7F);
                            }
                            else
                            {
                                int run = code & 0x7F;

                                for (int i = 0; i < run; i++)
                                {
                                    byte palIndex = _packedLineData[p++];
                                    int palBase = (palIndex * 4) + 8;

                                    int rVal = ((byte)palette[palBase + 2] * shade) >> 7;
                                    ushort rPart = (ushort)(((uint)rVal >> (redShiftDown & 0x1F)) << (redShiftUp & 0x1F));
                                    if ((uint)rVal > 0xFFu) rPart = rMask;

                                    int gVal = ((byte)palette[palBase + 1] * shade) >> 7;
                                    ushort gPart = (ushort)(((uint)gVal >> (greenShiftDown & 0x1F)) << (greenShiftUp & 0x1F));
                                    if ((uint)gVal > 0xFFu) gPart = gMask;

                                    int bVal = ((byte)palette[palBase + 0] * shade) >> 7;
                                    ushort bPart = (ushort)(((uint)bVal >> (blueShiftDown & 0x1F)) << (blueShiftUp & 0x1F));
                                    if ((uint)bVal > 0xFFu) bPart = bMask;

                                    dst16[dstIndex] = (ushort)(rPart | gPart | bPart);
                                    dstIndex++;
                                }
                            }

                            code = _packedLineData[p];
                        }
                    }
                    else
                    {
                        int xMin = clipped.X;
                        int xMax = clipped.X + clipped.Width - 1;

                        byte code = _packedLineData[p];
                        int xCur = x;

                        while (code != 0)
                        {
                            p++;

                            if ((code & 0x80) != 0)
                            {
                                xCur += (code & 0x7F);
                            }
                            else
                            {
                                int run = code & 0x7F;

                                for (int i = 0; i < run; i++)
                                {
                                    byte palIndex = _packedLineData[p++];

                                    if (xCur >= xMin && xCur <= xMax)
                                    {
                                        int palBase = (palIndex * 4) + 8;

                                        int rVal = ((byte)palette[palBase + 2] * shade) >> 7;
                                        ushort rPart = (ushort)(((uint)rVal >> (redShiftDown & 0x1F)) << (redShiftUp & 0x1F));
                                        if ((uint)rVal > 0xFFu) rPart = rMask;

                                        int gVal = ((byte)palette[palBase + 1] * shade) >> 7;
                                        ushort gPart = (ushort)(((uint)gVal >> (greenShiftDown & 0x1F)) << (greenShiftUp & 0x1F));
                                        if ((uint)gVal > 0xFFu) gPart = gMask;

                                        int bVal = ((byte)palette[palBase + 0] * shade) >> 7;
                                        ushort bPart = (ushort)(((uint)bVal >> (blueShiftDown & 0x1F)) << (blueShiftUp & 0x1F));
                                        if ((uint)bVal > 0xFFu) bPart = bMask;

                                        dst16[(dstYLine * dstStride) + xCur] = (ushort)(rPart | gPart | bPart);
                                    }

                                    xCur++;
                                }
                            }

                            code = _packedLineData[p];
                        }
                    }
                }
            }
        }

        // NXBasics::CBobManager::PrintBob_Shade_MMX(...)
        //
        // Only does clipping checks; if entry.Type == 1 it forwards to PrintBob_Shade_HighColor(...).
        internal void PrintBob_Shade_MMX(uint bobId, CBitmap target, int x, int y, CPalette palette, int shade)
        {
            uint index = bobId - _firstBobId;
            if (index >= _bobEntryCount)
            {
                return;
            }

            BobEntry entry = _entries[unchecked((int)index)];
            if (entry.Type == 0)
            {
                return;
            }

            SRectangle bobRect = entry.Rectangle;
            bobRect.MovePosition(x, y);

            SRectangle targetRect = target.Rectangle;

            if (!bobRect.IsTouching(targetRect))
            {
                return;
            }

            SRectangle clipped = new SRectangle(in bobRect);
            clipped.CutInsideX(targetRect);

            if (entry.Type == 1)
            {
                PrintBob_Shade_HighColor(bobId, target, x, y, palette, shade);
            }
        }


        internal void PrintBob_Glow_AddColor(uint bobId, CBitmap target, int x, int y, ref SColorRGB color, uint strength)
        {
            if (target == null)
            {
                return;
            }

            uint rel = bobId - _baseBobId;
            if (rel >= _bobCount)
            {
                return;
            }

            BobEntry entry = _bobEntries[unchecked((int)rel)];
            int bobType = entry.Type;
            if (bobType == 0)
            {
                return;
            }

            SRectangle rect = entry.Bounds;
            rect.MovePosition(x, y);

            SRectangle targetRect = target.Rect;
            if (!rect.IsTouching(in targetRect))
            {
                return;
            }

            SRectangle cut = new SRectangle(in rect);
            bool cutInsideXChanged = cut.CutInsideX(in targetRect);

            int rows = cut.Height;
            if (rows <= 0)
            {
                return;
            }

            int lastX = rect.X + rect.Width - 1;
            int targetWidth = target.Width;
            int targetHeight = target.Height;

            int stride = target.StridePixels;
            if (stride <= 0)
            {
                stride = targetWidth;
            }

            int maxXInclusive = cut.X + cut.Width - 1;

            uint a = strength & 0xFFu;
            uint invA = 0x100u - a;

            // Scanline table start for this bob
            int scanBase = entry.LineControlStart;

            // NOTE: Y-based offset (matches all your other routines)
            int yOffset = rect.Y < 0 ? -rect.Y : 0;
            int scanStart = scanBase + yOffset;

            if (bobType == 4 && target.Bpp == 0x20)
            {
                uint[] dst32 = target.Pixels32;
                if (dst32 == null)
                {
                    return;
                }

                uint src = CXBSystemManager.sTrueColorCreatorPtr.GetTrueColorWord(ref color);

                uint srcR = (src & 0x00FF0000u) * a;
                uint srcG = (src & 0x0000FF00u) * a;
                uint srcB = (src & 0x000000FFu) * a;

                for (int i = 0; i < rows; i++)
                {
                    uint lineEntry = _lineControlArray[scanStart + i];
                    if (lineEntry == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int packedPos = unchecked((int)(lineEntry & 0x003FFFFFu));
                    int px = unchecked((int)(lineEntry >> 0x16)) + rect.X;

                    int rowY = cut.Y + i;
                    if (rowY < 0 || rowY >= targetHeight)
                    {
                        continue;
                    }

                    int rowBase = rowY * stride;

                    bool needPerPixelClip = (px < 0) || (targetWidth <= lastX) || cutInsideXChanged;

                    while (true)
                    {
                        byte b = _packedLineData[packedPos];
                        if (b == 0)
                        {
                            break;
                        }

                        int count = b & 0x7F;

                        if (unchecked((sbyte)b) < 0)
                        {
                            packedPos += 1;
                            px += count;
                            continue;
                        }

                        if (count != 0)
                        {
                            if (needPerPixelClip)
                            {
                                for (int k = 0; k < count; k++)
                                {
                                    int xPix = px + k;
                                    if (xPix < 0 || xPix > maxXInclusive || xPix >= targetWidth)
                                    {
                                        continue;
                                    }

                                    // targetRect Y clip (kept like your original “touch/cut” logic)
                                    if (rowY < targetRect.Y || rowY >= targetRect.Y + targetRect.Height)
                                    {
                                        continue;
                                    }

                                    int idx = rowBase + xPix;

                                    uint dst = dst32[idx];

                                    uint outB = ((dst & 0xFFu) * invA + srcB) >> 8;
                                    uint outG = (((dst & 0xFF00u) * invA + srcG) >> 8) & 0xFF00u;
                                    uint outR = (((dst & 0xFF0000u) * invA + srcR) >> 8) & 0xFF0000u;

                                    dst32[idx] = outB + outG + outR;
                                }
                            }
                            else
                            {
                                int baseIdx = rowBase + px;

                                for (int k = 0; k < count; k++)
                                {
                                    uint dst = dst32[baseIdx + k];

                                    uint outB = ((dst & 0xFFu) * invA + srcB) >> 8;
                                    uint outG = (((dst & 0xFF00u) * invA + srcG) >> 8) & 0xFF00u;
                                    uint outR = (((dst & 0xFF0000u) * invA + srcR) >> 8) & 0xFF0000u;

                                    dst32[baseIdx + k] = outB + outG + outR;
                                }
                            }

                            // Type==4 advance: 2*count + 1 bytes
                            packedPos += ((count - 1) * 2) + 3;
                            px += count;
                        }
                        else
                        {
                            packedPos += 3;
                        }
                    }
                }

                return;
            }

            if (bobType == 4 && target.Bpp == 0x10)
            {
                ushort[] dst16 = target.Pixels16;
                if (dst16 == null)
                {
                    return;
                }

                CHighColorCreator hc = CXBSystemManager.sHighColorCreatorPtr;
                if (hc == null)
                {
                    return;
                }

                ushort src16 = hc.GetHighColorWord(ref color);

                uint rMask = hc.RMask16;
                uint gMask = hc.GMask16;
                uint bMask = hc.BMask16;

                for (int i = 0; i < rows; i++)
                {
                    uint lineEntry = _lineControlArray[scanStart + i];
                    if (lineEntry == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int packedPos = unchecked((int)(lineEntry & 0x003FFFFFu));
                    int px = unchecked((int)(lineEntry >> 0x16)) + rect.X;

                    int rowY = cut.Y + i;
                    if (rowY < 0 || rowY >= targetHeight)
                    {
                        continue;
                    }

                    int rowBase = rowY * stride;

                    bool needPerPixelClip = (px < 0) || (targetWidth <= lastX) || cutInsideXChanged;

                    while (true)
                    {
                        byte b = _packedLineData[packedPos];
                        if (b == 0)
                        {
                            break;
                        }

                        int count = b & 0x7F;

                        if (unchecked((sbyte)b) < 0)
                        {
                            packedPos += 1;
                            px += count;
                            continue;
                        }

                        if (count != 0)
                        {
                            for (int k = 0; k < count; k++)
                            {
                                int xPix = px + k;

                                if (needPerPixelClip)
                                {
                                    if (xPix < 0 || xPix > maxXInclusive || xPix >= targetWidth)
                                    {
                                        continue;
                                    }
                                }

                                int idx = rowBase + xPix;

                                ushort d = dst16[idx];

                                uint db = (uint)d & bMask;
                                uint dg = (uint)d & gMask;
                                uint dr = (uint)d & rMask;

                                uint sb = (uint)src16 & bMask;
                                uint sg = (uint)src16 & gMask;
                                uint sr = (uint)src16 & rMask;

                                uint nb = (db * invA + sb * a) >> 8;
                                uint ng = ((dg * invA + sg * a) >> 8) & gMask;
                                uint nr = ((dr * invA + sr * a) >> 8) & rMask;

                                dst16[idx] = (ushort)(nb | ng | nr);
                            }

                            // Type==4 advance: 2*count + 1 bytes
                            packedPos += ((count - 1) * 2) + 3;
                            px += count;
                        }
                        else
                        {
                            packedPos += 3;
                        }
                    }
                }

                return;
            }

            if (bobType == 1 && target.Bpp == 0x20)
            {
                uint[] dst32 = target.Pixels32;
                if (dst32 == null)
                {
                    return;
                }

                uint src = CXBSystemManager.sTrueColorCreatorPtr.GetTrueColorWord(ref color);

                uint srcR = (src & 0x00FF0000u) * a;
                uint srcG = (src & 0x0000FF00u) * a;
                uint srcB = (src & 0x000000FFu) * a;

                for (int i = 0; i < rows; i++)
                {
                    uint lineEntry = _lineControlArray[scanStart + i];
                    if (lineEntry == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int packedPos = unchecked((int)(lineEntry & 0x003FFFFFu));
                    int px = unchecked((int)(lineEntry >> 0x16)) + rect.X;

                    int rowY = cut.Y + i;
                    if (rowY < 0 || rowY >= targetHeight)
                    {
                        continue;
                    }

                    int rowBase = rowY * stride;

                    bool needPerPixelClip = (px < 0) || (targetWidth <= lastX) || cutInsideXChanged;

                    while (true)
                    {
                        byte b = _packedLineData[packedPos];
                        if (b == 0)
                        {
                            break;
                        }

                        int count = b & 0x7F;

                        if (unchecked((sbyte)b) < 0)
                        {
                            packedPos += 1;
                            px += count;
                            continue;
                        }

                        if (count != 0)
                        {
                            if (needPerPixelClip)
                            {
                                for (int k = 0; k < count; k++)
                                {
                                    int xPix = px + k;
                                    if (xPix < 0 || xPix > maxXInclusive || xPix >= targetWidth)
                                    {
                                        continue;
                                    }

                                    int idx = rowBase + xPix;

                                    uint dst = dst32[idx];

                                    uint outB = ((dst & 0xFFu) * invA + srcB) >> 8;
                                    uint outG = (((dst & 0xFF00u) * invA + srcG) >> 8) & 0xFF00u;
                                    uint outR = (((dst & 0xFF0000u) * invA + srcR) >> 8) & 0xFF0000u;

                                    dst32[idx] = outB + outG + outR;
                                }
                            }
                            else
                            {
                                int baseIdx = rowBase + px;

                                for (int k = 0; k < count; k++)
                                {
                                    uint dst = dst32[baseIdx + k];

                                    uint outB = ((dst & 0xFFu) * invA + srcB) >> 8;
                                    uint outG = (((dst & 0xFF00u) * invA + srcG) >> 8) & 0xFF00u;
                                    uint outR = (((dst & 0xFF0000u) * invA + srcR) >> 8) & 0xFF0000u;

                                    dst32[baseIdx + k] = outB + outG + outR;
                                }
                            }

                            // Type==1 advance: count + 1 bytes
                            packedPos += (count - 1) + 2;
                            px += count;
                        }
                        else
                        {
                            packedPos += 2;
                        }
                    }
                }

                return;
            }

            if (bobType == 1 && target.Bpp == 0x10)
            {
                ushort[] dst16 = target.Pixels16;
                if (dst16 == null)
                {
                    return;
                }

                CHighColorCreator hc = CXBSystemManager.sHighColorCreatorPtr;
                if (hc == null)
                {
                    return;
                }

                ushort src16 = hc.GetHighColorWord(ref color);

                uint rMask = hc.RMask16;
                uint gMask = hc.GMask16;
                uint bMask = hc.BMask16;

                for (int i = 0; i < rows; i++)
                {
                    uint lineEntry = _lineControlArray[scanStart + i];
                    if (lineEntry == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int packedPos = unchecked((int)(lineEntry & 0x003FFFFFu));
                    int px = unchecked((int)(lineEntry >> 0x16)) + rect.X;

                    int rowY = cut.Y + i;
                    if (rowY < 0 || rowY >= targetHeight)
                    {
                        continue;
                    }

                    int rowBase = rowY * stride;

                    bool needPerPixelClip = (px < 0) || (targetWidth <= lastX) || cutInsideXChanged;

                    while (true)
                    {
                        byte b = _packedLineData[packedPos];
                        if (b == 0)
                        {
                            break;
                        }

                        int count = b & 0x7F;

                        if (unchecked((sbyte)b) < 0)
                        {
                            packedPos += 1;
                            px += count;
                            continue;
                        }

                        if (count != 0)
                        {
                            for (int k = 0; k < count; k++)
                            {
                                int xPix = px + k;

                                if (needPerPixelClip)
                                {
                                    if (xPix < 0 || xPix > maxXInclusive || xPix >= targetWidth)
                                    {
                                        continue;
                                    }
                                }

                                int idx = rowBase + xPix;

                                ushort d = dst16[idx];

                                uint db = (uint)d & bMask;
                                uint dg = (uint)d & gMask;
                                uint dr = (uint)d & rMask;

                                uint sb = (uint)src16 & bMask;
                                uint sg = (uint)src16 & gMask;
                                uint sr = (uint)src16 & rMask;

                                uint nb = (db * invA + sb * a) >> 8;
                                uint ng = ((dg * invA + sg * a) >> 8) & gMask;
                                uint nr = ((dr * invA + sr * a) >> 8) & rMask;

                                dst16[idx] = (ushort)(nb | ng | nr);
                            }

                            // Type==1 advance: count + 1 bytes
                            packedPos += (count - 1) + 2;
                            px += count;
                        }
                        else
                        {
                            packedPos += 2;
                        }
                    }
                }
            }
        }

        // NXBasics::CBobManager::PrintBob_Glow_AddColor_OnlyBobByteRange(...)
        internal void PrintBob_Glow_AddColor_OnlyBobByteRange(
            uint bobId,
            CBitmap target,
            int posX,
            int posY,
            SColorRGB color,
            uint alpha,
            byte minBobByte,
            byte maxBobByte)
        {
            if (target == null)
            {
                return;
            }

            uint rel = bobId - _baseBobId;
            if (rel >= _bobCount)
            {
                return;
            }

            BobEntry entry = _bobEntries[unchecked((int)rel)];
            int bobType = entry.Type;
            if (bobType == 0)
            {
                return;
            }

            // Decompile: returns early unless bobType == 1
            if (bobType != 1)
            {
                return;
            }

            SRectangle rect = entry.Bounds;
            rect.MovePosition(posX, posY);

            SRectangle targetRect = target.Rect;

            if (!rect.IsTouching(in targetRect))
            {
                return;
            }

            // CutInsideX(rect, targetRect) -> flag used later (clipped path)
            SRectangle cut = new SRectangle(in rect);
            bool clippedXChanged = cut.CutInsideX(in targetRect);

            int scanlineCount = rect.Height;
            if (scanlineCount <= 0)
            {
                return;
            }

            int targetWidth = target.Width;
            int targetHeight = target.Height;

            int stride = target.StridePixels;
            if (stride <= 0)
            {
                stride = targetWidth;
            }

            int rectRightMost = rect.X + rect.Width - 1;

            // Like original sign-fix for negative rect.Y
            int yOffset = rect.Y < 0 ? -rect.Y : 0;
            int lineControlBase = entry.LineControlIndex + yOffset;

            uint a = alpha & 0xFFu;
            uint invA = 0x100u - a;

            // Horizontal clip bounds from "cut"
            int cutLeft = cut.X;
            int cutRightMinus1 = cut.X + cut.Width - 1;

            if (target.Bpp == 0x20)
            {
                uint[] dst32 = target.Pixels32;
                if (dst32 == null)
                {
                    return;
                }

                uint src = CXBSystemManager.sTrueColorCreatorPtr.GetTrueColorWord(ref color);

                uint addR = (src & 0x00FF0000u) * a;
                uint addG = (src & 0x0000FF00u) * a;
                uint addB = (src & 0x000000FFu) * a;

                // For each bob scanline
                for (int i = 0; i < scanlineCount; i++)
                {
                    uint lineRef = _lineControlArray[lineControlBase + i];
                    int dstY = rect.Y + i;

                    if (lineRef == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    // Y bounds safety (managed)
                    if (dstY < 0 || dstY >= targetHeight)
                    {
                        continue;
                    }

                    int packedPos = unchecked((int)(lineRef & 0x003FFFFFu));
                    int x = unchecked((int)(lineRef >> 0x16)) + rect.X;

                    // Need slow path if out-of-bounds on left/right or if CutInsideX modified.
                    bool needPerPixelClip = (x < 0) || (targetWidth <= rectRightMost) || clippedXChanged;

                    int rowBase = dstY * stride;

                    // Decode packed stream
                    while (true)
                    {
                        byte token = _packedLineData[packedPos];
                        if (token == 0)
                        {
                            break;
                        }

                        int count = token & 0x7F;

                        if (unchecked((sbyte)token) < 0)
                        {
                            // skip
                            packedPos += 1;
                            x += count;
                            continue;
                        }

                        // run: payload bytes follow
                        int payloadPos = packedPos + 1;

                        if (count != 0)
                        {
                            if (needPerPixelClip)
                            {
                                for (int k = 0; k < count; k++)
                                {
                                    byte bobByte = _packedLineData[payloadPos + k];
                                    if (bobByte < minBobByte || bobByte > maxBobByte)
                                    {
                                        continue;
                                    }

                                    int xPix = x + k;

                                    if (xPix < 0 || xPix >= targetWidth)
                                    {
                                        continue;
                                    }

                                    // Apply X clip from cut
                                    if (xPix < cutLeft || xPix > cutRightMinus1)
                                    {
                                        continue;
                                    }

                                    int idx = rowBase + xPix;
                                    if ((uint)idx >= (uint)dst32.Length)
                                    {
                                        continue;
                                    }

                                    uint dst = dst32[idx];

                                    uint outB = ((dst & 0x000000FFu) * invA + addB) >> 8;
                                    uint outG = (((dst & 0x0000FF00u) * invA + addG) >> 8) & 0x0000FF00u;
                                    uint outR = (((dst & 0x00FF0000u) * invA + addR) >> 8) & 0x00FF0000u;

                                    dst32[idx] = outB + outG + outR;
                                }
                            }
                            else
                            {
                                // Fast contiguous: we assume horizontal in-bounds.
                                int baseIdx = rowBase + x;

                                for (int k = 0; k < count; k++)
                                {
                                    byte bobByte = _packedLineData[payloadPos + k];
                                    if (bobByte < minBobByte || bobByte > maxBobByte)
                                    {
                                        continue;
                                    }

                                    int idx = baseIdx + k;

                                    uint dst = dst32[idx];

                                    uint outB = ((dst & 0x000000FFu) * invA + addB) >> 8;
                                    uint outG = (((dst & 0x0000FF00u) * invA + addG) >> 8) & 0x0000FF00u;
                                    uint outR = (((dst & 0x00FF0000u) * invA + addR) >> 8) & 0x00FF0000u;

                                    dst32[idx] = outB + outG + outR;
                                }
                            }

                            x += count;
                            packedPos += 1 + count; // token + payload
                        }
                        else
                        {
                            // count == 0 still advances "token + 0 payload"
                            packedPos += 1;
                        }
                    }
                }

                return;
            }

            if (target.Bpp == 0x10)
            {
                ushort[] dst16 = target.Pixels16;
                if (dst16 == null)
                {
                    return;
                }

                CHighColorCreator hc = CXBSystemManager.sHighColorCreatorPtr;
                if (hc == null)
                {
                    return;
                }

                ushort src16 = hc.GetHighColorWord(ref color);

                uint rMask = hc.RMask16;
                uint gMask = hc.GMask16;
                uint bMask = hc.BMask16;

                for (int i = 0; i < scanlineCount; i++)
                {
                    uint lineRef = _lineControlArray[lineControlBase + i];
                    int dstY = rect.Y + i;

                    if (lineRef == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    if (dstY < 0 || dstY >= targetHeight)
                    {
                        continue;
                    }

                    int packedPos = unchecked((int)(lineRef & 0x003FFFFFu));
                    int x = unchecked((int)(lineRef >> 0x16)) + rect.X;

                    // decompile has two paths depending on clippedX; we keep same behavior
                    bool needPerPixelClip = clippedXChanged || (x < 0) || (targetWidth <= rectRightMost);

                    int rowBase = dstY * stride;

                    while (true)
                    {
                        byte token = _packedLineData[packedPos];
                        if (token == 0)
                        {
                            break;
                        }

                        int count = token & 0x7F;

                        if (unchecked((sbyte)token) < 0)
                        {
                            packedPos += 1;
                            x += count;
                            continue;
                        }

                        int payloadPos = packedPos + 1;

                        if (count != 0)
                        {
                            for (int k = 0; k < count; k++)
                            {
                                byte bobByte = _packedLineData[payloadPos + k];
                                if (bobByte < minBobByte || bobByte > maxBobByte)
                                {
                                    continue;
                                }

                                int xPix = x + k;

                                if (needPerPixelClip)
                                {
                                    if (xPix < 0 || xPix >= targetWidth)
                                    {
                                        continue;
                                    }

                                    if (xPix < cutLeft || xPix > cutRightMinus1)
                                    {
                                        continue;
                                    }
                                }

                                int idx = rowBase + xPix;
                                if ((uint)idx >= (uint)dst16.Length)
                                {
                                    continue;
                                }

                                ushort d = dst16[idx];

                                uint db = (uint)d & bMask;
                                uint dg = (uint)d & gMask;
                                uint dr = (uint)d & rMask;

                                uint sb = (uint)src16 & bMask;
                                uint sg = (uint)src16 & gMask;
                                uint sr = (uint)src16 & rMask;

                                uint nb = (db * invA + sb * a) >> 8;
                                uint ng = ((dg * invA + sg * a) >> 8) & gMask;
                                uint nr = ((dr * invA + sr * a) >> 8) & rMask;

                                dst16[idx] = (ushort)(nb | ng | nr);
                            }

                            x += count;
                            packedPos += 1 + count;
                        }
                        else
                        {
                            packedPos += 1;
                        }
                    }
                }
            }
        }

        // NXBasics::CBobManager::PrintBob(unsigned int, NXBasics::CBitmap const&, int, int, NXBasics::CPalette*) const
        internal void PrintBob(uint bobId, CBitmap target, int x, int y, CPalette palette)
        {
            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            BobEntry entry = _bobEntries[unchecked((int)index)];
            int entryType = entry.Type;
            if (entryType == 0)
            {
                return;
            }

            // Only entryType == 1 is handled here (matches your original early return).
            if (entryType != 1 || palette == null)
            {
                return;
            }

            SRectangle bobRect = entry.Bounds;
            bobRect.MovePosition(x, y);

            SRectangle targetRect = target.Rect;
            if (!bobRect.IsTouching(in targetRect))
            {
                return;
            }

            SRectangle cutRect = new SRectangle(in bobRect);
            bool cutInsideXChanged = cutRect.CutInsideX(in targetRect);

            int targetWidth = target.Width;
            int targetHeight = target.Height;

            int rowsToDraw = cutRect.Height;
            if (rowsToDraw <= 0)
            {
                return;
            }

            int stride = target.StridePixels;
            if (stride <= 0)
            {
                stride = targetWidth;
            }

            // Row table start = entry.LineControlIndex + max(0, -bobRect.Y)
            int startRow = entry.LineControlIndex;
            if (bobRect.Y < 0)
            {
                startRow = startRow + (-bobRect.Y);
            }

            int bobRight = bobRect.X + bobRect.Width - 1;

            int dstRowY = cutRect.Y;

            byte bpp = target.ColorDepthBits;

            // ========= 32bpp =========
            if (bpp == 0x20)
            {
                uint[] table32 = palette.TrueColorTable32;
                if (table32 == null || table32.Length == 0)
                {
                    return;
                }

                uint[] dst32 = target.Pixels32;
                if (dst32 == null)
                {
                    return;
                }

                for (int r = 0; r < rowsToDraw; r++)
                {
                    int yDest = dstRowY + r;
                    if (yDest < 0 || yDest >= targetHeight)
                    {
                        continue;
                    }

                    uint rowToken = _lineControlArray[startRow + r];
                    if (rowToken == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int packedPos = unchecked((int)(rowToken & 0x003FFFFFu));
                    int xPos = unchecked((int)(rowToken >> 0x16)) + bobRect.X;

                    // Slow bring-in path if X is out-of-range (like the decompile does).
                    if (xPos < 0 || targetWidth <= bobRight)
                    {
                        while (xPos < 0 || targetWidth <= bobRight)
                        {
                            byte token = _packedLineData[packedPos];
                            if (token == 0)
                            {
                                goto NextRow32;
                            }

                            packedPos++;

                            int len = token & 0x7F;

                            if (unchecked((sbyte)token) < 0)
                            {
                                // skip
                                xPos += len;
                            }
                            else
                            {
                                // run
                                for (int i = 0; i < len; i++)
                                {
                                    int px = xPos + i;

                                    if (px >= 0 && px <= (cutRect.X + cutRect.Width - 1) && px < targetWidth)
                                    {
                                        int dstIndex = (yDest * stride) + px;
                                        if ((uint)dstIndex < (uint)dst32.Length)
                                        {
                                            byte palIndex = _packedLineData[packedPos + i];
                                            dst32[dstIndex] = table32[palIndex];
                                        }
                                    }
                                }

                                xPos += len;
                                packedPos += len;
                            }
                        }
                    }

                    // Fast decode
                    {
                        int dstIndex = (yDest * stride) + xPos;
                        if (dstIndex < 0)
                        {
                            dstIndex = 0;
                        }

                        while (true)
                        {
                            byte token = _packedLineData[packedPos];
                            if (token == 0)
                            {
                                break;
                            }

                            packedPos++;

                            int len = token & 0x7F;

                            if (unchecked((sbyte)token) < 0)
                            {
                                // skip: payload bytes are NOT used; advance X only
                                dstIndex += len;
                            }
                            else
                            {
                                // run: len palette indices follow
                                for (int i = 0; i < len; i++)
                                {
                                    int di = dstIndex + i;
                                    if ((uint)di < (uint)dst32.Length)
                                    {
                                        byte palIndex = _packedLineData[packedPos + i];
                                        dst32[di] = table32[palIndex];
                                    }
                                }

                                packedPos += len;
                                dstIndex += len;
                            }
                        }
                    }

                NextRow32:
                    continue;
                }

                return;
            }

            // ========= 16bpp =========
            if (bpp == 0x10)
            {
                ushort[] table16 = palette.HighColorTable16;
                if (table16 == null || table16.Length == 0)
                {
                    return;
                }

                ushort[] dst16 = target.Pixels16;
                if (dst16 == null)
                {
                    return;
                }

                int clipLeft = cutRect.X;
                int clipRight = cutRect.X + cutRect.Width - 1;

                for (int r = 0; r < rowsToDraw; r++)
                {
                    int yDest = dstRowY + r;
                    if (yDest < 0 || yDest >= targetHeight)
                    {
                        continue;
                    }

                    uint rowToken = _lineControlArray[startRow + r];
                    if (rowToken == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int packedPos = unchecked((int)(rowToken & 0x003FFFFFu));
                    int xPos = unchecked((int)(rowToken >> 0x16)) + bobRect.X;

                    // If not clipped in X, we can skip per-pixel X clip checks (matches your cutInsideX==0 branch),
                    // but we still keep managed bounds safety.
                    bool needClip = cutInsideXChanged || (xPos < 0) || (targetWidth <= bobRight);

                    // Slow bring-in path if needed
                    if (xPos < 0 || targetWidth <= bobRight)
                    {
                        while (xPos < 0 || targetWidth <= bobRight)
                        {
                            byte token = _packedLineData[packedPos];
                            if (token == 0)
                            {
                                goto NextRow16;
                            }

                            packedPos++;

                            int len = token & 0x7F;

                            if (unchecked((sbyte)token) < 0)
                            {
                                xPos += len;
                            }
                            else
                            {
                                for (int i = 0; i < len; i++)
                                {
                                    int px = xPos + i;

                                    if (px >= 0 && px < targetWidth)
                                    {
                                        if (!needClip || (px >= clipLeft && px <= clipRight))
                                        {
                                            int dstIndex = (yDest * stride) + px;
                                            if ((uint)dstIndex < (uint)dst16.Length)
                                            {
                                                byte palIndex = _packedLineData[packedPos + i];
                                                dst16[dstIndex] = table16[palIndex];
                                            }
                                        }
                                    }
                                }

                                xPos += len;
                                packedPos += len;
                            }
                        }
                    }

                    // Fast decode
                    {
                        int dstIndex = (yDest * stride) + xPos;
                        if (dstIndex < 0)
                        {
                            dstIndex = 0;
                        }

                        int curX = xPos;

                        while (true)
                        {
                            byte token = _packedLineData[packedPos];
                            if (token == 0)
                            {
                                break;
                            }

                            packedPos++;

                            int len = token & 0x7F;

                            if (unchecked((sbyte)token) < 0)
                            {
                                // skip
                                curX += len;
                                dstIndex += len;
                            }
                            else
                            {
                                if (!needClip)
                                {
                                    for (int i = 0; i < len; i++)
                                    {
                                        int di = dstIndex + i;
                                        if ((uint)di < (uint)dst16.Length)
                                        {
                                            byte palIndex = _packedLineData[packedPos + i];
                                            dst16[di] = table16[palIndex];
                                        }
                                    }
                                }
                                else
                                {
                                    for (int i = 0; i < len; i++)
                                    {
                                        int px = curX + i;
                                        if (px >= 0 && px < targetWidth && px >= clipLeft && px <= clipRight)
                                        {
                                            int di = dstIndex + i;
                                            if ((uint)di < (uint)dst16.Length)
                                            {
                                                byte palIndex = _packedLineData[packedPos + i];
                                                dst16[di] = table16[palIndex];
                                            }
                                        }
                                    }
                                }

                                packedPos += len;
                                curX += len;
                                dstIndex += len;
                            }
                        }
                    }

                NextRow16:
                    continue;
                }

                return;
            }

            // ========= 8bpp =========
            if (bpp == 0x08)
            {
                byte[] dst8 = target.Pixels8;
                if (dst8 == null)
                {
                    return;
                }

                int clipRight = cutRect.X + cutRect.Width - 1;

                for (int r = 0; r < rowsToDraw; r++)
                {
                    int yDest = dstRowY + r;
                    if (yDest < 0 || yDest >= targetHeight)
                    {
                        continue;
                    }

                    uint rowToken = _lineControlArray[startRow + r];
                    if (rowToken == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int packedPos = unchecked((int)(rowToken & 0x003FFFFFu));
                    int xPos = unchecked((int)(rowToken >> 0x16)) + bobRect.X;

                    // Slow bring-in path for out-of-range
                    if (xPos < 0 || targetWidth <= bobRight)
                    {
                        while (xPos < 0 || targetWidth <= bobRight)
                        {
                            byte token = _packedLineData[packedPos];
                            if (token == 0)
                            {
                                goto NextRow8;
                            }

                            packedPos++;

                            int len = token & 0x7F;

                            if (unchecked((sbyte)token) < 0)
                            {
                                xPos += len;
                            }
                            else
                            {
                                for (int i = 0; i < len; i++)
                                {
                                    int px = xPos + i;

                                    if (px >= 0 && px <= clipRight && px < targetWidth)
                                    {
                                        int di = (yDest * stride) + px;
                                        if ((uint)di < (uint)dst8.Length)
                                        {
                                            dst8[di] = _packedLineData[packedPos + i];
                                        }
                                    }
                                }

                                xPos += len;
                                packedPos += len;
                            }
                        }
                    }

                    // Fast decode
                    {
                        int dstIndex = (yDest * stride) + xPos;
                        if (dstIndex < 0)
                        {
                            dstIndex = 0;
                        }

                        while (true)
                        {
                            byte token = _packedLineData[packedPos];
                            if (token == 0)
                            {
                                break;
                            }

                            packedPos++;

                            int len = token & 0x7F;

                            if (unchecked((sbyte)token) < 0)
                            {
                                dstIndex += len;
                            }
                            else
                            {
                                for (int i = 0; i < len; i++)
                                {
                                    int di = dstIndex + i;
                                    if ((uint)di < (uint)dst8.Length)
                                    {
                                        dst8[di] = _packedLineData[packedPos + i];
                                    }
                                }

                                packedPos += len;
                                dstIndex += len;
                            }
                        }
                    }

                NextRow8:
                    continue;
                }
            }
        }

        // NXBasics::CBobManager::PrintBob_Remapped(unsigned int, NXBasics::CBitmap const&, int, int, NXBasics::CPalette*, unsigned char const*) const
        internal void PrintBob_Remapped(uint bobId, CBitmap target, int x, int y, CPalette palette, byte[] remapTable)
        {
            if (remapTable == null || remapTable.Length < 256)
            {
                return;
            }

            uint index = bobId - _firstBobId;
            if (index >= _bobCount)
            {
                return;
            }

            BobEntry entry = _bobEntries[unchecked((int)index)];
            if (entry.Type == 0)
            {
                return;
            }

            // Only type == 1 (like decompile)
            if (entry.Type != 1)
            {
                return;
            }

            SRectangle bobRect = entry.Bounds;
            bobRect.MovePosition(x, y);

            SRectangle targetRect = target.Rect;
            if (!bobRect.IsTouching(in targetRect))
            {
                return;
            }

            SRectangle clipped = new SRectangle(in bobRect);
            bool cutInsideXChanged = clipped.CutInsideX(in targetRect);

            int rows = clipped.Height;
            if (rows <= 0)
            {
                return;
            }

            int targetWidth = target.Width;
            int targetHeight = target.Height;

            int stride = target.StridePixels;
            if (stride <= 0)
            {
                stride = targetWidth;
            }

            int bobRight = bobRect.X + bobRect.Width - 1;

            // scanline base = entry.LineControlIndex + max(0, -bobRect.Y)
            int startRow = entry.LineControlIndex;
            if (bobRect.Y < 0)
            {
                startRow = startRow + (-bobRect.Y);
            }

            int dstStartY = clipped.Y;
            int clipLeft = clipped.X;
            int clipRight = clipped.X + clipped.Width - 1;

            byte bpp = target.ColorDepthBits;

            if (bpp == 0x20)
            {
                uint[] table32 = palette?.TrueColorTable32;
                uint[] dst32 = target.Pixels32;

                if (table32 == null || dst32 == null)
                {
                    return;
                }

                for (int row = 0; row < rows; row++)
                {
                    int yDest = dstStartY + row;
                    if ((uint)yDest >= (uint)targetHeight)
                    {
                        continue;
                    }

                    uint scanVal = _lineControlArray[startRow + row];
                    if (scanVal == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int packedPos = unchecked((int)(scanVal & 0x003FFFFFu));
                    int xPos = unchecked((int)(scanVal >> 0x16)) + bobRect.X;

                    // Bring-in slow path if needed (left offscreen or right edge beyond target)
                    if (xPos < 0 || targetWidth <= bobRight)
                    {
                        while (xPos < 0 || targetWidth <= bobRight)
                        {
                            byte token = _packedLineData[packedPos];
                            if (token == 0)
                            {
                                goto NextRow32;
                            }

                            packedPos++;

                            int len = token & 0x7F;

                            if (unchecked((sbyte)token) < 0)
                            {
                                xPos += len;
                            }
                            else
                            {
                                // run payload follows (len bytes)
                                for (int i = 0; i < len; i++)
                                {
                                    int px = xPos + i;

                                    if (px >= 0 && px <= clipRight && px < targetWidth)
                                    {
                                        int di = (yDest * stride) + px;
                                        if ((uint)di < (uint)dst32.Length)
                                        {
                                            byte srcIndex = _packedLineData[packedPos + i];
                                            byte remapped = remapTable[srcIndex];
                                            dst32[di] = table32[remapped];
                                        }
                                    }
                                }

                                xPos += len;
                                packedPos += len;
                            }

                            if (!(xPos < 0 || targetWidth <= bobRight))
                            {
                                break;
                            }
                        }
                    }

                    // Fast decode for the rest of the row
                    {
                        int curX = xPos;
                        int dstIndex = (yDest * stride) + curX;

                        while (true)
                        {
                            byte token = _packedLineData[packedPos];
                            if (token == 0)
                            {
                                break;
                            }

                            packedPos++;

                            int len = token & 0x7F;

                            if (unchecked((sbyte)token) < 0)
                            {
                                // skip: no payload
                                curX += len;
                                dstIndex += len;
                            }
                            else
                            {
                                // run: payload len bytes
                                if (!cutInsideXChanged)
                                {
                                    for (int i = 0; i < len; i++)
                                    {
                                        int di = dstIndex + i;
                                        if ((uint)di < (uint)dst32.Length)
                                        {
                                            byte srcIndex = _packedLineData[packedPos + i];
                                            dst32[di] = table32[remapTable[srcIndex]];
                                        }
                                    }
                                }
                                else
                                {
                                    for (int i = 0; i < len; i++)
                                    {
                                        int px = curX + i;
                                        if (px >= clipLeft && px <= clipRight && (uint)px < (uint)targetWidth)
                                        {
                                            int di = (yDest * stride) + px;
                                            if ((uint)di < (uint)dst32.Length)
                                            {
                                                byte srcIndex = _packedLineData[packedPos + i];
                                                dst32[di] = table32[remapTable[srcIndex]];
                                            }
                                        }
                                    }
                                }

                                packedPos += len;
                                curX += len;
                                dstIndex += len;
                            }
                        }
                    }

                NextRow32:
                    continue;
                }

                return;
            }

            if (bpp == 0x10)
            {
                ushort[] table16 = palette?.HighColorTable16;
                ushort[] dst16 = target.Pixels16;

                if (table16 == null || dst16 == null)
                {
                    return;
                }

                for (int row = 0; row < rows; row++)
                {
                    int yDest = dstStartY + row;
                    if ((uint)yDest >= (uint)targetHeight)
                    {
                        continue;
                    }

                    uint scanVal = _lineControlArray[startRow + row];
                    if (scanVal == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int packedPos = unchecked((int)(scanVal & 0x003FFFFFu));
                    int xPos = unchecked((int)(scanVal >> 0x16)) + bobRect.X;

                    // Bring-in slow path if needed
                    if (xPos < 0 || targetWidth <= bobRight)
                    {
                        while (xPos < 0 || targetWidth <= bobRight)
                        {
                            byte token = _packedLineData[packedPos];
                            if (token == 0)
                            {
                                goto NextRow16;
                            }

                            packedPos++;

                            int len = token & 0x7F;

                            if (unchecked((sbyte)token) < 0)
                            {
                                xPos += len;
                            }
                            else
                            {
                                for (int i = 0; i < len; i++)
                                {
                                    int px = xPos + i;

                                    if (px >= 0 && px <= clipRight && px < targetWidth)
                                    {
                                        int di = (yDest * stride) + px;
                                        if ((uint)di < (uint)dst16.Length)
                                        {
                                            byte srcIndex = _packedLineData[packedPos + i];
                                            byte remapped = remapTable[srcIndex];
                                            dst16[di] = table16[remapped];
                                        }
                                    }
                                }

                                xPos += len;
                                packedPos += len;
                            }

                            if (!(xPos < 0 || targetWidth <= bobRight))
                            {
                                break;
                            }
                        }
                    }

                    // Fast decode
                    {
                        int curX = xPos;
                        int dstIndex = (yDest * stride) + curX;

                        while (true)
                        {
                            byte token = _packedLineData[packedPos];
                            if (token == 0)
                            {
                                break;
                            }

                            packedPos++;

                            int len = token & 0x7F;

                            if (unchecked((sbyte)token) < 0)
                            {
                                curX += len;
                                dstIndex += len;
                            }
                            else
                            {
                                if (!cutInsideXChanged)
                                {
                                    for (int i = 0; i < len; i++)
                                    {
                                        int di = dstIndex + i;
                                        if ((uint)di < (uint)dst16.Length)
                                        {
                                            byte srcIndex = _packedLineData[packedPos + i];
                                            dst16[di] = table16[remapTable[srcIndex]];
                                        }
                                    }
                                }
                                else
                                {
                                    for (int i = 0; i < len; i++)
                                    {
                                        int px = curX + i;
                                        if (px >= clipLeft && px <= clipRight && (uint)px < (uint)targetWidth)
                                        {
                                            int di = (yDest * stride) + px;
                                            if ((uint)di < (uint)dst16.Length)
                                            {
                                                byte srcIndex = _packedLineData[packedPos + i];
                                                dst16[di] = table16[remapTable[srcIndex]];
                                            }
                                        }
                                    }
                                }

                                packedPos += len;
                                curX += len;
                                dstIndex += len;
                            }
                        }
                    }

                NextRow16:
                    continue;
                }
            }
        }

        // NXBasics::CBobManager::PrintBob_DoubleByteBobNormal(unsigned int, NXBasics::CBitmap const&, int, int, NXBasics::CPalette*) const
        internal void PrintBob_DoubleByteBobNormal(uint bobId, CBitmap target, int dstX, int dstY, CPalette palette)
        {
            uint rel = bobId - _firstBobId;
            if (rel >= _bobCount)
            {
                return;
            }

            BobEntry entry = _bobEntries[unchecked((int)rel)];
            if (entry.Type == 0)
            {
                return;
            }

            // Only double-byte bobs (type == 4) in this routine
            if (entry.Type != 4)
            {
                return;
            }

            SRectangle bobRect = entry.Bounds;
            bobRect.MovePosition(dstX, dstY);

            SRectangle targetRect = target.Rect;
            if (!bobRect.IsTouching(in targetRect))
            {
                return;
            }

            SRectangle cutRect = new SRectangle(in bobRect);
            bool cutInsideXChanged = cutRect.CutInsideX(in targetRect);

            int rowsToDraw = cutRect.Height;
            if (rowsToDraw <= 0)
            {
                return;
            }

            int targetWidth = target.Width;
            int targetHeight = target.Height;

            int stride = target.StridePixels;
            if (stride <= 0)
            {
                stride = targetWidth;
            }

            int bobRight = bobRect.X + bobRect.Width - 1;

            // line control start = entry.LineControlIndex + max(0, -bobRect.Y)
            int lineStart = entry.LineControlIndex;
            if (bobRect.Y < 0)
            {
                lineStart = lineStart + (-bobRect.Y);
            }

            int yDest = cutRect.Y;

            int clipLeft = cutRect.X;
            int clipRight = cutRect.X + cutRect.Width - 1;

            byte bpp = target.ColorDepthBits;

            if (bpp == 0x20)
            {
                uint[] dst32 = target.Pixels32;
                uint[] table32 = palette?.TrueColorTable32;

                if (dst32 == null || table32 == null)
                {
                    return;
                }

                for (int row = 0; row < rowsToDraw; row++, yDest++)
                {
                    if ((uint)yDest >= (uint)targetHeight)
                    {
                        continue;
                    }

                    uint ctrl = _lineControlArray[lineStart + row];
                    if (ctrl == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int packedPos = unchecked((int)(ctrl & 0x003FFFFFu));
                    int x = bobRect.X + unchecked((int)(ctrl >> 0x16));

                    bool needBoundsChecks = cutInsideXChanged || x < 0 || targetWidth <= bobRight;

                    if (needBoundsChecks)
                    {
                        DecodeDoubleByteLine_32_Clipped(dst32, stride, targetWidth, yDest, clipLeft, clipRight, ref x, packedPos, table32);
                    }
                    else
                    {
                        DecodeDoubleByteLine_32_Fast(dst32, stride, yDest, x, packedPos, table32);
                    }
                }

                return;
            }

            if (bpp == 0x10)
            {
                ushort[] dst16 = target.Pixels16;
                ushort[] table16 = palette?.HighColorTable16;

                if (dst16 == null || table16 == null)
                {
                    return;
                }

                for (int row = 0; row < rowsToDraw; row++, yDest++)
                {
                    if ((uint)yDest >= (uint)targetHeight)
                    {
                        continue;
                    }

                    uint ctrl = _lineControlArray[lineStart + row];
                    if (ctrl == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int packedPos = unchecked((int)(ctrl & 0x003FFFFFu));
                    int x = bobRect.X + unchecked((int)(ctrl >> 0x16));

                    bool needBoundsChecks = cutInsideXChanged || x < 0 || targetWidth <= bobRight;

                    if (needBoundsChecks)
                    {
                        DecodeDoubleByteLine_16_Clipped(dst16, stride, targetWidth, yDest, clipLeft, clipRight, ref x, packedPos, table16);
                    }
                    else
                    {
                        DecodeDoubleByteLine_16_Fast(dst16, stride, yDest, x, packedPos, table16);
                    }
                }
            }
        }

        private void DecodeDoubleByteLine_32_Fast(uint[] dst32, int stride, int yDest, int xStart, int packedPos, uint[] table32)
        {
            int dstIndex = (yDest * stride) + xStart;
            int x = xStart;

            while (true)
            {
                byte token = _packedLineData[packedPos];
                if (token == 0)
                {
                    return;
                }

                packedPos++;

                int len = token & 0x7F;

                if (unchecked((sbyte)token) < 0)
                {
                    // Skip has no payload in this format
                    dstIndex += len;
                    x += len;
                }
                else
                {
                    // Run: payload is 2 bytes per pixel, first byte is palette index
                    for (int i = 0; i < len; i++)
                    {
                        byte palIndex = _packedLineData[packedPos];
                        dst32[dstIndex + i] = table32[palIndex];
                        packedPos += 2;
                    }

                    dstIndex += len;
                    x += len;
                }
            }
        }

        private void DecodeDoubleByteLine_32_Clipped(
            uint[] dst32,
            int stride,
            int width,
            int yDest,
            int clipLeft,
            int clipRight,
            ref int x,
            int packedPos,
            uint[] table32)
        {
            while (true)
            {
                byte token = _packedLineData[packedPos];
                if (token == 0)
                {
                    return;
                }

                packedPos++;

                int len = token & 0x7F;

                if (unchecked((sbyte)token) < 0)
                {
                    x += len;
                }
                else
                {
                    for (int i = 0; i < len; i++)
                    {
                        int px = x + i;

                        byte palIndex = _packedLineData[packedPos];
                        packedPos += 2;

                        if (px >= clipLeft && px <= clipRight && (uint)px < (uint)width)
                        {
                            int di = (yDest * stride) + px;
                            if ((uint)di < (uint)dst32.Length)
                            {
                                dst32[di] = table32[palIndex];
                            }
                        }
                    }

                    x += len;
                }
            }
        }

        private void DecodeDoubleByteLine_16_Fast(ushort[] dst16, int stride, int yDest, int xStart, int packedPos, ushort[] table16)
        {
            int dstIndex = (yDest * stride) + xStart;
            int x = xStart;

            while (true)
            {
                byte token = _packedLineData[packedPos];
                if (token == 0)
                {
                    return;
                }

                packedPos++;

                int len = token & 0x7F;

                if (unchecked((sbyte)token) < 0)
                {
                    dstIndex += len;
                    x += len;
                }
                else
                {
                    for (int i = 0; i < len; i++)
                    {
                        byte palIndex = _packedLineData[packedPos];
                        dst16[dstIndex + i] = table16[palIndex];
                        packedPos += 2;
                    }

                    dstIndex += len;
                    x += len;
                }
            }
        }

        private void DecodeDoubleByteLine_16_Clipped(ushort[] dst16, int stride, int width, int yDest, int clipLeft, int clipRight, ref int x, int packedPos, ushort[] table16)
        {
            while (true)
            {
                byte token = _packedLineData[packedPos];
                if (token == 0)
                {
                    return;
                }

                packedPos++;

                int len = token & 0x7F;

                if (unchecked((sbyte)token) < 0)
                {
                    x += len;
                }
                else
                {
                    for (int i = 0; i < len; i++)
                    {
                        int px = x + i;

                        byte palIndex = _packedLineData[packedPos];
                        packedPos += 2;

                        if (px >= clipLeft && px <= clipRight && (uint)px < (uint)width)
                        {
                            int di = (yDest * stride) + px;
                            if ((uint)di < (uint)dst16.Length)
                            {
                                dst16[di] = table16[palIndex];
                            }
                        }
                    }

                    x += len;
                }
            }
        }

        // NXBasics::CBobManager::PrintBob_UsingTimeMask(unsigned int, unsigned int, NXBasics::CBitmap const&, int, int, NXBasics::CPalette*) const
        internal void PrintBob_UsingTimeMask(uint bobId, uint timeMask, CBitmap target, int x, int y, CPalette palette)
        {
            uint rel = bobId - _firstBobId;
            if (rel >= _bobCount)
            {
                return;
            }

            BobEntry entry = _bobEntries[unchecked((int)rel)];
            if (entry.Type == 0)
            {
                return;
            }

            // Decompile only handles entryType == 4 here.
            if (entry.Type != 4)
            {
                return;
            }

            if (palette == null)
            {
                return;
            }

            SRectangle bobRect = entry.Bounds;
            bobRect.MovePosition(x, y);

            SRectangle targetRect = target.Rect;
            if (!bobRect.IsTouching(in targetRect))
            {
                return;
            }

            SRectangle cutRect = new SRectangle(in bobRect);
            bool cutInsideXChanged = cutRect.CutInsideX(in targetRect);

            int rowsToDraw = cutRect.Height;
            if (rowsToDraw <= 0)
            {
                return;
            }

            int targetWidth = target.Width;
            int targetHeight = target.Height;

            int stride = target.StridePixels;
            if (stride <= 0)
            {
                stride = targetWidth;
            }

            int bobRight = bobRect.X + bobRect.Width - 1;

            // row table start = entry.LineControlIndex + max(0, -bobRect.Y)
            int lineStart = entry.LineControlIndex;
            if (bobRect.Y < 0)
            {
                lineStart = lineStart + (-bobRect.Y);
            }

            int yDest = cutRect.Y;

            int clipLeft = cutRect.X;
            int clipRight = cutRect.X + cutRect.Width - 1;

            byte maskByte = (byte)timeMask;

            // Target bpp is usually exposed by your wrapper; decompile checks target[8] for 0x20 / 0x10.
            byte bpp = target.ColorDepthBits;

            if (bpp == 0x20)
            {
                uint[] dst32 = target.Pixels32;
                uint[] table32 = palette.TrueColorTable32;

                if (dst32 == null || table32 == null)
                {
                    return;
                }

                for (int row = 0; row < rowsToDraw; row++, yDest++)
                {
                    if ((uint)yDest >= (uint)targetHeight)
                    {
                        continue;
                    }

                    uint ctrl = _lineControlArray[lineStart + row];
                    if (ctrl == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int packedPos = unchecked((int)(ctrl & 0x003FFFFFu));
                    int xStart = bobRect.X + unchecked((int)(ctrl >> 0x16));

                    bool fast =
                        !cutInsideXChanged &&
                        xStart >= 0 &&
                        bobRight < targetWidth;

                    if (fast)
                    {
                        DecodeTimeMaskLine32_Fast(dst32, stride, yDest, xStart, packedPos, table32, maskByte);
                    }
                    else
                    {
                        DecodeTimeMaskLine32_Clipped(dst32, stride, targetWidth, yDest, clipLeft, clipRight, xStart, packedPos, table32, maskByte);
                    }
                }

                return;
            }

            if (bpp == 0x10)
            {
                ushort[] dst16 = target.Pixels16;
                ushort[] table16 = palette.HighColorTable16;

                if (dst16 == null || table16 == null)
                {
                    return;
                }

                for (int row = 0; row < rowsToDraw; row++, yDest++)
                {
                    if ((uint)yDest >= (uint)targetHeight)
                    {
                        continue;
                    }

                    uint ctrl = _lineControlArray[lineStart + row];
                    if (ctrl == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int packedPos = unchecked((int)(ctrl & 0x003FFFFFu));
                    int xStart = bobRect.X + unchecked((int)(ctrl >> 0x16));

                    bool fast =
                        !cutInsideXChanged &&
                        xStart >= 0 &&
                        bobRight < targetWidth;

                    if (fast)
                    {
                        DecodeTimeMaskLine16_Fast(dst16, stride, yDest, xStart, packedPos, table16, maskByte);
                    }
                    else
                    {
                        DecodeTimeMaskLine16_Clipped(dst16, stride, targetWidth, yDest, clipLeft, clipRight, xStart, packedPos, table16, maskByte);
                    }
                }
            }
        }

        private void DecodeTimeMaskLine32_Fast(
            uint[] dst32,
            int stride,
            int yDest,
            int xStart,
            int packedPos,
            uint[] table32,
            byte timeMask)
        {
            int dstIndex = (yDest * stride) + xStart;

            while (true)
            {
                byte token = _packedLineData[packedPos];
                if (token == 0)
                {
                    return;
                }

                packedPos++;

                int len = token & 0x7F;

                if (unchecked((sbyte)token) < 0)
                {
                    // Skip has no payload for this format.
                    dstIndex += len;
                }
                else
                {
                    // Run payload: 2 bytes per pixel => [colorIndex][timeByte]
                    for (int i = 0; i < len; i++)
                    {
                        byte colorIndex = _packedLineData[packedPos + 0];
                        byte t = _packedLineData[packedPos + 1];
                        packedPos += 2;

                        if (t <= timeMask)
                        {
                            dst32[dstIndex] = table32[colorIndex];
                        }

                        dstIndex++;
                    }
                }
            }
        }

        private void DecodeTimeMaskLine32_Clipped(
            uint[] dst32,
            int stride,
            int width,
            int yDest,
            int clipLeft,
            int clipRight,
            int xStart,
            int packedPos,
            uint[] table32,
            byte timeMask)
        {
            int x = xStart;

            while (true)
            {
                byte token = _packedLineData[packedPos];
                if (token == 0)
                {
                    return;
                }

                packedPos++;

                int len = token & 0x7F;

                if (unchecked((sbyte)token) < 0)
                {
                    x += len;
                }
                else
                {
                    for (int i = 0; i < len; i++)
                    {
                        int px = x + i;

                        byte colorIndex = _packedLineData[packedPos + 0];
                        byte t = _packedLineData[packedPos + 1];
                        packedPos += 2;

                        if (t <= timeMask && px >= clipLeft && px <= clipRight && (uint)px < (uint)width)
                        {
                            int di = (yDest * stride) + px;
                            if ((uint)di < (uint)dst32.Length)
                            {
                                dst32[di] = table32[colorIndex];
                            }
                        }
                    }

                    x += len;
                }
            }
        }

        private void DecodeTimeMaskLine16_Fast(
            ushort[] dst16,
            int stride,
            int yDest,
            int xStart,
            int packedPos,
            ushort[] table16,
            byte timeMask)
        {
            int dstIndex = (yDest * stride) + xStart;

            while (true)
            {
                byte token = _packedLineData[packedPos];
                if (token == 0)
                {
                    return;
                }

                packedPos++;

                int len = token & 0x7F;

                if (unchecked((sbyte)token) < 0)
                {
                    dstIndex += len;
                }
                else
                {
                    for (int i = 0; i < len; i++)
                    {
                        byte colorIndex = _packedLineData[packedPos + 0];
                        byte t = _packedLineData[packedPos + 1];
                        packedPos += 2;

                        if (t <= timeMask)
                        {
                            dst16[dstIndex] = table16[colorIndex];
                        }

                        dstIndex++;
                    }
                }
            }
        }

        private void DecodeTimeMaskLine16_Clipped(
            ushort[] dst16,
            int stride,
            int width,
            int yDest,
            int clipLeft,
            int clipRight,
            int xStart,
            int packedPos,
            ushort[] table16,
            byte timeMask)
        {
            int x = xStart;

            while (true)
            {
                byte token = _packedLineData[packedPos];
                if (token == 0)
                {
                    return;
                }

                packedPos++;

                int len = token & 0x7F;

                if (unchecked((sbyte)token) < 0)
                {
                    x += len;
                }
                else
                {
                    for (int i = 0; i < len; i++)
                    {
                        int px = x + i;

                        byte colorIndex = _packedLineData[packedPos + 0];
                        byte t = _packedLineData[packedPos + 1];
                        packedPos += 2;

                        if (t <= timeMask && px >= clipLeft && px <= clipRight && (uint)px < (uint)width)
                        {
                            int di = (yDest * stride) + px;
                            if ((uint)di < (uint)dst16.Length)
                            {
                                dst16[di] = table16[colorIndex];
                            }
                        }
                    }

                    x += len;
                }
            }
        }

        // NXBasics::CBobManager::PrintBob_UsingCollapseTimeMask(unsigned int, unsigned int, NXBasics::CBitmap const&, int, int, NXBasics::CPalette*) const
        internal int PrintBob_UsingCollapseTimeMask(uint bobId, uint timeMask, CBitmap target, int x, int y, CPalette palette)
        {
            uint rel = bobId - (uint)_baseBobId;
            if (rel >= _bobCount)
            {
                return 0;
            }

            // Entry is 0x18 in the original; here we assume you already have the mapped representation.
            BobEntry entry = _bobEntries[unchecked((int)rel)];

            // Decompiled routine only handles bobType == 4 (double-byte / time-mask format).
            if (entry.Type != 4)
            {
                return 0;
            }

            int bobHeight = entry.Bounds.Height;

            // Scan from the last line upward to find how many bottom lines can be "collapsed".
            // foundLine is the number of scanned lines from the bottom before we find any timeByte > timeMask.
            int foundLine = bobHeight;

            if (bobHeight == 0)
            {
                foundLine = 0;
            }
            else
            {
                int baseLine = entry.LineControlIndex;

                // Start from (baseLine + bobHeight - 1) and go upward.
                for (int scanned = 0; scanned < bobHeight; scanned++)
                {
                    int lineIndex = baseLine + (bobHeight - 1 - scanned);

                    uint packed = _lineControlArray[lineIndex];
                    if (packed == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int p = unchecked((int)(packed & 0x003FFFFFu));

                    byte cmd = _packedLineData[p];
                    if (cmd == 0)
                    {
                        continue;
                    }

                    // Decode tokens until 0; check time byte for literal runs.
                    while (cmd != 0)
                    {
                        if (unchecked((sbyte)cmd) < 0)
                        {
                            // Skip token: only the opcode byte.
                            p += 1;
                        }
                        else
                        {
                            int count = cmd & 0x7F;

                            // Literal run: cmd pixels, each pixel = 2 bytes [colorIndex][timeByte]
                            // Time bytes are at: (p + 2), (p + 4), ...
                            for (int i = 0; i < count; i++)
                            {
                                byte t = _packedLineData[p + 2 + (i * 2)];
                                if (t > (byte)timeMask)
                                {
                                    foundLine = scanned;
                                    goto CollapseFound;
                                }
                            }

                            // Advance: opcode + (2 * count) payload bytes
                            p += 1 + (count * 2);
                        }

                        cmd = _packedLineData[p];
                    }
                }

                foundLine = bobHeight;
            }

        CollapseFound:
            // Build rect from entry bounds and apply collapse adjustment, then move by (x,y)
            SRectangle rect = entry.Bounds;
            rect.Height -= foundLine;
            rect.Y += foundLine;
            rect.MovePosition(x, y);

            SRectangle targetRect = target.Rect;

            if (!rect.IsTouching(in targetRect))
            {
                return foundLine;
            }

            SRectangle drawRect = new SRectangle(in rect);
            bool clippedX = drawRect.CutInsideX(in targetRect);

            byte bpp = target.ColorDepthBits;

            // Start line index like other PrintBob_* variants:
            // base + max(0, -rect.Y)
            int startLine = entry.LineControlIndex;
            if (rect.Y < 0)
            {
                startLine = startLine + (-rect.Y);
            }

            int xMax = rect.X + rect.Width - 1;
            int targetClipXMax = targetRect.X + targetRect.Width - 1;

            int dstRow = drawRect.Y;
            int rows = drawRect.Height;
            if (rows <= 0)
            {
                return foundLine;
            }

            if (bpp == 0x20)
            {
                uint[] dst32 = target.Pixels32;
                uint[] table32 = palette != null ? palette.TrueColorTable32 : null;

                if (dst32 == null || table32 == null)
                {
                    return foundLine;
                }

                int stride = target.StridePixels;
                if (stride <= 0)
                {
                    stride = target.Width;
                }

                for (int row = 0; row < rows; row++, dstRow++)
                {
                    uint packed = _lineControlArray[startLine + row];
                    if (packed == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int p = unchecked((int)(packed & 0x003FFFFFu));
                    int dstX = unchecked((int)(packed >> 0x16)) + rect.X;

                    // Slow path while out-of-range / needs clipping like decompile
                    if (dstX < 0 || targetClipXMax <= xMax)
                    {
                        while (dstX < 0 || targetClipXMax <= xMax)
                        {
                            byte cmd = _packedLineData[p];
                            if (cmd == 0)
                            {
                                goto EndRow32;
                            }

                            p += 1;

                            if (unchecked((sbyte)cmd) < 0)
                            {
                                dstX += (cmd & 0x7F);
                            }
                            else
                            {
                                int count = cmd;

                                while (count != 0)
                                {
                                    if (dstX >= 0 && dstX <= (drawRect.X + drawRect.Width - 1))
                                    {
                                        // Draw when timeByte > timeMask (collapse version)
                                        if (_packedLineData[p + 1] > (byte)timeMask)
                                        {
                                            uint colorIndex = _packedLineData[p + 0];
                                            int di = (dstRow * stride) + dstX;
                                            if ((uint)di < (uint)dst32.Length)
                                            {
                                                dst32[di] = table32[colorIndex];
                                            }
                                        }
                                    }

                                    dstX += 1;
                                    p += 2;
                                    count -= 1;
                                }
                            }
                        }
                    }

                    // Fast/normal decode
                    byte cmd2 = _packedLineData[p];
                    if (cmd2 != 0)
                    {
                        int di = (dstRow * stride) + dstX;

                        while (cmd2 != 0)
                        {
                            int len = cmd2 & 0x7F;

                            if (unchecked((sbyte)cmd2) < 0)
                            {
                                di += len;
                                p += 1;
                            }
                            else
                            {
                                // opcode at p, payload at p+1
                                p += 1;

                                for (int i = 0; i < len; i++)
                                {
                                    byte colorIndex = _packedLineData[p + 0];
                                    byte t = _packedLineData[p + 1];

                                    if (t > (byte)timeMask)
                                    {
                                        int outIndex = di + i;
                                        if ((uint)outIndex < (uint)dst32.Length)
                                        {
                                            dst32[outIndex] = table32[colorIndex];
                                        }
                                    }

                                    p += 2;
                                }

                                di += len;
                            }

                            cmd2 = _packedLineData[p];
                        }
                    }

                EndRow32:
                    ;
                }

                return foundLine;
            }

            if (bpp == 0x10)
            {
                ushort[] dst16 = target.Pixels16;
                ushort[] table16 = palette != null ? palette.HighColorTable16 : null;

                if (dst16 == null || table16 == null)
                {
                    return foundLine;
                }

                int stride = target.StridePixels;
                if (stride <= 0)
                {
                    stride = target.Width;
                }

                for (int row = 0; row < rows; row++, dstRow++)
                {
                    uint packed = _lineControlArray[startLine + row];
                    if (packed == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int p = unchecked((int)(packed & 0x003FFFFFu));
                    int dstX = unchecked((int)(packed >> 0x16)) + rect.X;

                    if (!clippedX)
                    {
                        byte first = _packedLineData[p];
                        if (first == 0)
                        {
                            continue;
                        }

                        int di = (dstRow * stride) + dstX;

                        while (true)
                        {
                            byte cmd = _packedLineData[p];
                            if (cmd == 0)
                            {
                                break;
                            }

                            int len = cmd & 0x7F;

                            if (unchecked((sbyte)cmd) < 0)
                            {
                                di += len;
                                p += 1;
                            }
                            else
                            {
                                p += 1;

                                for (int i = 0; i < len; i++)
                                {
                                    byte colorIndex = _packedLineData[p + 0];
                                    byte t = _packedLineData[p + 1];

                                    if (t > (byte)timeMask)
                                    {
                                        int outIndex = di + i;
                                        if ((uint)outIndex < (uint)dst16.Length)
                                        {
                                            dst16[outIndex] = table16[colorIndex];
                                        }
                                    }

                                    p += 2;
                                }

                                di += len;
                            }
                        }
                    }
                    else
                    {
                        // Clipped-X path with the slow pre-loop
                        while (dstX < 0 || targetClipXMax <= xMax)
                        {
                            byte cmd = _packedLineData[p];
                            if (cmd == 0)
                            {
                                goto EndRow16;
                            }

                            p += 1;

                            if (unchecked((sbyte)cmd) < 0)
                            {
                                dstX += (cmd & 0x7F);
                            }
                            else
                            {
                                int count = cmd;

                                while (count != 0)
                                {
                                    if (dstX >= 0 && dstX <= (drawRect.X + drawRect.Width - 1))
                                    {
                                        if (_packedLineData[p + 1] > (byte)timeMask)
                                        {
                                            byte colorIndex = _packedLineData[p + 0];
                                            int outIndex = (dstRow * stride) + dstX;
                                            if ((uint)outIndex < (uint)dst16.Length)
                                            {
                                                dst16[outIndex] = table16[colorIndex];
                                            }
                                        }
                                    }

                                    dstX += 1;
                                    p += 2;
                                    count -= 1;
                                }
                            }
                        }

                        byte cmd2 = _packedLineData[p];
                        if (cmd2 != 0)
                        {
                            int di = (dstRow * stride) + dstX;

                            while (cmd2 != 0)
                            {
                                int len = cmd2 & 0x7F;

                                if (unchecked((sbyte)cmd2) < 0)
                                {
                                    di += len;
                                    p += 1;
                                }
                                else
                                {
                                    p += 1;

                                    for (int i = 0; i < len; i++)
                                    {
                                        byte colorIndex = _packedLineData[p + 0];
                                        byte t = _packedLineData[p + 1];

                                        if (t > (byte)timeMask)
                                        {
                                            int outIndex = di + i;
                                            if ((uint)outIndex < (uint)dst16.Length)
                                            {
                                                dst16[outIndex] = table16[colorIndex];
                                            }
                                        }

                                        p += 2;
                                    }

                                    di += len;
                                }

                                cmd2 = _packedLineData[p];
                            }
                        }

                    EndRow16:
                        ;
                    }
                }

                return foundLine;
            }

            // Original decompile effectively skips 8bpp here.
            return 0;
        }

        // NXBasics::CBobManager::l_PrintTimeMask_BottomUpScanTimeMask(unsigned int, NXBasics::SBobData const*) const
        internal unsafe int L_PrintTimeMask_BottomUpScanTimeMask(uint timeValue, in SBobData bobData)
        {
            int lineCount = bobData.Field_0x10; // *(int*)(param_2 + 0x10)
            if (lineCount == 0)
            {
                return 0;
            }

            // puVar7 = (uint*)(this+0x40) + (bobData.Field_0x14 + lineCount)
            uint* lineTable = (uint*)_bobLineTablePtr;
            uint baseIndex = bobData.Field_0x14; // *(uint*)(param_2 + 0x14)

            uint* entryPtr = lineTable + (nuint)baseIndex + (nuint)lineCount;

            int scannedLinesFromBottom = 0;
            int remaining = lineCount;

            do
            {
                entryPtr -= 1;

                uint entry = *entryPtr;
                if (entry != 0xFFFFFFFFu)
                {
                    nuint dataOffset = (nuint)(entry & 0x003FFFFFu);

                    byte* dataBase = (byte*)_bobByteDataPtr;
                    byte* cursor = dataBase + dataOffset;

                    byte control = *cursor;
                    if (control != 0)
                    {
                        byte* p = cursor;

                        do
                        {
                            byte* next;

                            if (unchecked((sbyte)control) < 0)
                            {
                                // skip-run
                                next = p + 1;
                            }
                            else
                            {
                                // literal-run with per-pixel time bytes interleaved
                                uint count = (uint)(control & 0x7Fu);
                                next = p + ((nint)(count - 1u) * 2) + 3;

                                for (uint i = 0; i < count; i++)
                                {
                                    // time byte is at p[2 + i*2]
                                    if (timeValue < p[(nint)(i * 2) + 2])
                                    {
                                        return scannedLinesFromBottom;
                                    }
                                }
                            }

                            control = *next;
                            p = next;
                        }
                        while (control != 0);
                    }
                }

                scannedLinesFromBottom += 1;
                remaining -= 1;
            }
            while (remaining != 0);

            return lineCount;
        }

        // NXBasics::CBobManager::PrintBob_UsingRangedCollapseTimeMask(...)
        // Notes:
        // - Only BobType == 4 (double-byte bob: [colorIndex][timeByte]).
        // - timeMinExclusive < timeByte < timeMaxExclusive enables drawing for that pixel.
        // - Does a bottom-up scan to compute "collapseLines" (how many bottom lines are fully <= timeMinExclusive).
        internal unsafe void PrintBob_UsingRangedCollapseTimeMask(
            uint bobId,
            uint timeMinExclusive,
            uint timeMaxExclusive,
            CBitmap target,
            int dstX,
            int dstY,
            CPalette palette)
        {
            if (timeMinExclusive >= timeMaxExclusive)
            {
                return;
            }

            uint bobIndex = unchecked(bobId - (uint)_bobIdBase);
            if (bobIndex >= _bobCount)
            {
                return;
            }

            byte* entriesBase = (byte*)_bobEntriesPtr;
            byte* entryPtr = entriesBase + (nint)bobIndex * 0x18;

            int bobType = *(int*)entryPtr;
            if (bobType != 4)
            {
                return;
            }

            int bobHeight = *(int*)(entryPtr + 0x10);
            if (bobHeight == 0)
            {
                return;
            }

            uint lineTableOffset = *(uint*)(entryPtr + 0x14);

            // --- Bottom-up scan to compute collapseLines ---
            int collapseLines = bobHeight;
            {
                uint* linePtr = (uint*)((byte*)_bobLineIndexPtr + (nint)((lineTableOffset + (uint)bobHeight) * 4u));
                int scannedFromBottom = 0;
                int remaining = bobHeight;

                while (remaining != 0)
                {
                    linePtr -= 1;

                    uint lineToken = *linePtr;
                    if (lineToken != 0xFFFFFFFFu)
                    {
                        byte* stream = (byte*)_bobByteStreamBasePtr + (nint)(lineToken & 0x3FFFFFu);
                        byte cmd = *stream;

                        if (cmd != 0)
                        {
                            byte* p = stream;

                            while (cmd != 0)
                            {
                                if (unchecked((sbyte)cmd) < 0)
                                {
                                    // Skip run
                                    p += 1;
                                }
                                else
                                {
                                    int run = cmd & 0x7F;

                                    for (int j = 0; j < run; j++)
                                    {
                                        // time byte is at p[2 + j*2]
                                        if (timeMinExclusive < p[j * 2 + 2])
                                        {
                                            collapseLines = scannedFromBottom;
                                            goto CollapseDone;
                                        }
                                    }

                                    // Advance: (run - 1) * 2 + 3 bytes
                                    p += (nint)((run - 1) * 2 + 3);
                                }

                                cmd = *p;
                            }
                        }
                    }

                    scannedFromBottom += 1;
                    remaining -= 1;

                    // If nothing found, decompile ends with "height"
                    collapseLines = bobHeight;
                }

            CollapseDone:
                ;
            }

            // --- Build and adjust bob rect (collapse + move) ---
            SRectangle bobRect;
            SRectangle.SRectangle(out bobRect, (SRectangle*)(entryPtr + 4));

            bobRect.Height -= collapseLines;
            bobRect.Y += collapseLines;
            SRectangle.MovePosition(ref bobRect, dstX, dstY);

            if (!bobRect.IsTouching(target.Rect))
            {
                return;
            }

            SRectangle clipRect = new SRectangle(in bobRect);
            bool cutInsideX = clipRect.CutInsideX(target.Rect);

            // scanline start:
            // base = lineTableOffset + collapseLines + max(0, -bobRect.Y)
            int rectY = bobRect.Y;
            uint yNegFix = (uint)(((long)rectY >> 63) & -(long)rectY);

            uint* scanlineTokens = (uint*)((byte*)_bobLineIndexPtr + (nint)((lineTableOffset + (uint)collapseLines + yNegFix) * 4u));

            int rightEdge = bobRect.X + bobRect.Width - 1;
            int targetClipRightEdge = target.Rect.X + target.Rect.Width - 1;

            int dstRow = clipRect.Y;
            int rows = clipRect.Height;
            if (rows <= 0)
            {
                return;
            }

            byte bpp = target.ColorDepthBits;

            // ===== 32-bit truecolor =====
            if (bpp == 0x20)
            {
                nint tablePtr = CPalette.GetTrueColorTablePtr(palette);
                if (tablePtr == 0)
                {
                    return;
                }

                uint* dst32 = (uint*)target.Buffer32;
                int stride = target.StridePixels;

                for (int rowIndex = 0; rowIndex < rows; rowIndex++, scanlineTokens++, dstRow++)
                {
                    uint token = *scanlineTokens;
                    if (token == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    byte* p = (byte*)_bobByteStreamBasePtr + (nint)(token & 0x3FFFFFu);
                    int x = (int)((token >> 0x16) + (uint)bobRect.X);

                    // Slow pre-loop (clipping / out-of-range)
                    if (x < 0 || targetClipRightEdge <= rightEdge)
                    {
                        while (x < 0 || targetClipRightEdge <= rightEdge)
                        {
                            byte cmd = *p;
                            if (cmd == 0)
                            {
                                goto NextRow32;
                            }

                            p += 1;

                            if (unchecked((sbyte)cmd) < 0)
                            {
                                x += (cmd & 0x7F);
                            }
                            else
                            {
                                uint run = (uint)cmd;
                                while (run != 0)
                                {
                                    if ((x >= 0) &&
                                        (x <= clipRect.X + clipRect.Width - 1) &&
                                        (timeMinExclusive < p[1]) &&
                                        (p[1] < timeMaxExclusive))
                                    {
                                        dst32[x + stride * dstRow] = *(uint*)((byte*)tablePtr + (nint)p[0] * 4);
                                    }

                                    x += 1;
                                    p += 2;
                                    run -= 1;
                                }
                            }

                            if (!(x < 0 || targetClipRightEdge <= rightEdge))
                            {
                                break;
                            }
                        }
                    }

                    // Main decode
                    {
                        byte cmd = *p;
                        if (cmd != 0)
                        {
                            uint* outPx = dst32 + (stride * dstRow) + x;

                            while (cmd != 0)
                            {
                                byte* data = p + 1;
                                int count = cmd & 0x7F;

                                if (unchecked((sbyte)cmd) < 0)
                                {
                                    outPx += count;
                                    p = data;
                                }
                                else
                                {
                                    // literal run, 2 bytes per pixel: [color][time]
                                    if ((cmd & 1) != 0)
                                    {
                                        if ((timeMinExclusive < p[2]) && (p[2] < timeMaxExclusive))
                                        {
                                            *outPx = *(uint*)((byte*)tablePtr + (nint)data[0] * 4);
                                        }

                                        outPx += 1;
                                        count -= 1;
                                        p += 3;
                                    }
                                    else
                                    {
                                        p = data;
                                    }

                                    int pairs = count / 2;
                                    for (int j = 0; j < pairs; j++)
                                    {
                                        if ((timeMinExclusive < p[1]) && (p[1] < timeMaxExclusive))
                                        {
                                            outPx[0] = *(uint*)((byte*)tablePtr + (nint)p[0] * 4);
                                        }
                                        if ((timeMinExclusive < p[3]) && (p[3] < timeMaxExclusive))
                                        {
                                            outPx[1] = *(uint*)((byte*)tablePtr + (nint)p[2] * 4);
                                        }

                                        p += 4;
                                        outPx += 2;
                                    }
                                }

                                cmd = *p;
                            }
                        }
                    }

                NextRow32:
                    ;
                }

                return;
            }

            // ===== 16-bit highcolor =====
            if (bpp == 0x10)
            {
                nint tablePtr = CPalette.GetHighColorTablePtr(palette);
                if (tablePtr == 0)
                {
                    return;
                }

                ushort* dst16 = (ushort*)target.Buffer16;
                int stride = target.StridePixels;

                for (int rowIndex = 0; rowIndex < rows; rowIndex++, scanlineTokens++, dstRow++)
                {
                    uint token = *scanlineTokens;
                    if (token == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    byte* p = (byte*)_bobByteStreamBasePtr + (nint)(token & 0x3FFFFFu);
                    int x = (int)((token >> 0x16) + (uint)bobRect.X);

                    if (!cutInsideX)
                    {
                        byte cmd = *p;
                        if (cmd == 0)
                        {
                            continue;
                        }

                        ushort* outPx = dst16 + (stride * dstRow) + x;

                        while (cmd != 0)
                        {
                            byte* data = p + 1;
                            int count = cmd & 0x7F;

                            if (unchecked((sbyte)cmd) < 0)
                            {
                                outPx += count;
                                p = data;
                            }
                            else
                            {
                                if ((cmd & 1) != 0)
                                {
                                    if ((timeMinExclusive < p[2]) && (p[2] < timeMaxExclusive))
                                    {
                                        *outPx = *(ushort*)((byte*)tablePtr + (nint)data[0] * 2);
                                    }

                                    outPx += 1;
                                    count -= 1;
                                    p += 3;
                                }
                                else
                                {
                                    p = data;
                                }

                                int pairs = count / 2;
                                for (int j = 0; j < pairs; j++)
                                {
                                    if ((timeMinExclusive < p[1]) && (p[1] < timeMaxExclusive))
                                    {
                                        outPx[0] = *(ushort*)((byte*)tablePtr + (nint)p[0] * 2);
                                    }
                                    if ((timeMinExclusive < p[3]) && (p[3] < timeMaxExclusive))
                                    {
                                        outPx[1] = *(ushort*)((byte*)tablePtr + (nint)p[2] * 2);
                                    }

                                    p += 4;
                                    outPx += 2;
                                }
                            }

                            cmd = *p;
                        }

                        continue;
                    }

                    // cutInsideX == true: slow pre-loop until in-range, then main decode
                    while (x < 0 || targetClipRightEdge <= rightEdge)
                    {
                        byte cmd = *p;
                        if (cmd == 0)
                        {
                            goto NextRow16Cut;
                        }

                        p += 1;

                        if (unchecked((sbyte)cmd) < 0)
                        {
                            x += (cmd & 0x7F);
                        }
                        else
                        {
                            uint run = (uint)cmd;
                            while (run != 0)
                            {
                                if ((x >= 0) &&
                                    (x <= clipRect.X + clipRect.Width - 1) &&
                                    (timeMinExclusive < p[1]) &&
                                    (p[1] < timeMaxExclusive))
                                {
                                    dst16[x + stride * dstRow] = *(ushort*)((byte*)tablePtr + (nint)p[0] * 2);
                                }

                                x += 1;
                                p += 2;
                                run -= 1;
                            }
                        }

                        if (!(x < 0 || targetClipRightEdge <= rightEdge))
                        {
                            break;
                        }
                    }

                    {
                        byte cmd2 = *p;
                        if (cmd2 != 0)
                        {
                            ushort* outPx = dst16 + (stride * dstRow) + x;

                            while (cmd2 != 0)
                            {
                                byte* data = p + 1;
                                int count = cmd2 & 0x7F;

                                if (unchecked((sbyte)cmd2) < 0)
                                {
                                    outPx += count;
                                    p = data;
                                }
                                else
                                {
                                    if ((cmd2 & 1) != 0)
                                    {
                                        if ((timeMinExclusive < p[2]) && (p[2] < timeMaxExclusive))
                                        {
                                            *outPx = *(ushort*)((byte*)tablePtr + (nint)data[0] * 2);
                                        }

                                        outPx += 1;
                                        count -= 1;
                                        p += 3;
                                    }
                                    else
                                    {
                                        p = data;
                                    }

                                    int pairs = count / 2;
                                    for (int j = 0; j < pairs; j++)
                                    {
                                        if ((timeMinExclusive < p[1]) && (p[1] < timeMaxExclusive))
                                        {
                                            outPx[0] = *(ushort*)((byte*)tablePtr + (nint)p[0] * 2);
                                        }
                                        if ((timeMinExclusive < p[3]) && (p[3] < timeMaxExclusive))
                                        {
                                            outPx[1] = *(ushort*)((byte*)tablePtr + (nint)p[2] * 2);
                                        }

                                        p += 4;
                                        outPx += 2;
                                    }
                                }

                                cmd2 = *p;
                            }
                        }
                    }

                NextRow16Cut:
                    ;
                }
            }
        }

        // NXBasics::CBobManager::l_PrintTimeMask_BottomUpScanTimeMask8Bit(unsigned int, NXBasics::SBobData const*) const
        internal int L_PrintTimeMask_BottomUpScanTimeMask8Bit(uint timeValue, in SBobData bobData)
        {
            int count = bobData.Field_0x10;
            if (count == 0)
            {
                return 0;
            }

            uint baseIndex = bobData.Field_0x14;

            // Start at (baseIndex + count) and scan backwards (bottom-up).
            uint start = unchecked(baseIndex + (uint)count);

            int scanned = 0;

            while (scanned < count)
            {
                uint rowIndex = unchecked(start - 1u - (uint)scanned);

                uint token = ReadUInt32(_bobLineTablePtr + (nint)rowIndex * 4);
                if (token != 0xFFFFFFFFu)
                {
                    uint offset = token & 0x3FFFFFu;

                    nint p = _bobByteDataPtr + (nint)offset;
                    byte control = ReadByte(p);

                    if (control != 0)
                    {
                        while (control != 0)
                        {
                            if ((sbyte)control >= 0)
                            {
                                int run = control & 0x7F;

                                // time bytes are consecutive starting at p+1, length = run
                                for (int j = 0; j < run; j++)
                                {
                                    byte t = ReadByte(p + 1 + j);
                                    if (timeValue < t)
                                    {
                                        return scanned;
                                    }
                                }

                                // next = p + ((run - 1) + 2) == p + run + 1
                                p = p + run + 1;
                            }
                            else
                            {
                                // skip-run: next = p + 1
                                p = p + 1;
                            }

                            control = ReadByte(p);
                        }
                    }
                }

                scanned++;
            }

            return count;
        }

        // NXBasics::CBobManager::PrintBob_Shadow(unsigned int, NXBasics::CBitmap const&, int, int) const
        internal void PrintBob_Shadow(uint bobId, CBitmap target, int x, int y)
        {
            byte bppOrFormat = target.ColorDepthBits;

            if (bppOrFormat == 0x20)
            {
                PrintBob_1Bit_TrueColor(bobId, target, x, y);
                return;
            }

            if (bppOrFormat == 0x10)
            {
                PrintBob_1Bit_HighColor(bobId, target, x, y);
                return;
            }
        }

        // --- Raw readers (same style as your other refactors; no unsafe) ---

        private static uint ReadUInt32(nint address)
        {
            int v = System.Runtime.InteropServices.Marshal.ReadInt32(address);
            return unchecked((uint)v);
        }

        private static byte ReadByte(nint address)
        {
            return System.Runtime.InteropServices.Marshal.ReadByte(address);
        }

        // NXBasics::CBobManager::PrintBob_Shadow_TimeMask(unsigned int, unsigned int, NXBasics::CBitmap const&, int, int) const
        internal void PrintBob_Shadow_TimeMask(uint bobId, uint timeMask, CBitmap targetBitmap, int posX, int posY)
        {
            uint rel = unchecked(bobId - (uint)_baseBobId);
            if (rel >= _bobCount)
            {
                return;
            }

            BobEntry entry = _bobEntries[(int)rel];
            if (entry.Type == 0 || entry.Type != 1)
            {
                return;
            }

            SRectangle bobRect = entry.Rect;
            bobRect.MovePosition(posX, posY);

            if (!bobRect.IsTouching(in targetBitmap.Rect))
            {
                return;
            }

            SRectangle cutRect = new SRectangle(in bobRect);
            bool clippedX = cutRect.CutInsideX(in targetBitmap.Rect);
            if (cutRect.Width <= 0 || cutRect.Height <= 0)
            {
                return;
            }

            int ySkip = bobRect.Y < 0 ? -bobRect.Y : 0;
            int lineIndex = (int)entry.LineControlIndex + ySkip;

            int bobRight = bobRect.X + bobRect.Width - 1;
            int targetWidth = targetBitmap.Width;

            byte bpp = targetBitmap.ColorDepthBits;

            if (bpp == 0x20)
            {
                ProcessShadow32(
                    targetBitmap,
                    lineIndex,
                    bobRect.X,
                    bobRight,
                    targetWidth,
                    clippedX,
                    cutRect,
                    timeMask);
                return;
            }

            if (bpp == 0x10)
            {
                // Map this to whatever your HighColor shadow lookup really is.
                // In the decompile it came from (sHighColorCreatorPtr + 0x10).
                ushort[] shadowTable = CXBSystemManager.sHighColorCreatorPtr.ShadowTable;
                if (shadowTable == null || shadowTable.Length == 0)
                {
                    return;
                }

                ProcessShadow16(
                    targetBitmap,
                    shadowTable,
                    lineIndex,
                    bobRect.X,
                    bobRight,
                    targetWidth,
                    clippedX,
                    cutRect,
                    timeMask);
            }
        }

        private void ProcessShadow32(
            CBitmap target,
            int lineCtrlBase,
            int bobX,
            int bobRight,
            int targetWidth,
            bool clippedX,
            in SRectangle cut,
            uint timeMask)
        {
            uint[] dst = target.Pixels32;
            int stride = target.StridePixels;

            int dstY = cut.Y;
            int rows = cut.Height;

            int clipLeft = cut.X;
            int clipRight = cut.X + cut.Width - 1;

            for (int row = 0; row < rows; row++, dstY++, lineCtrlBase++)
            {
                uint token = _lineControl[lineCtrlBase];
                if (token == 0xFFFFFFFFu)
                {
                    continue;
                }

                int x = (int)(token >> 22) + bobX;
                int p = (int)(token & 0x3FFFFFu);

                // If we’re clipped OR outside left OR possibly outside right, do the “slow pre-walk” until in-range.
                if (clippedX || x < 0 || targetWidth <= bobRight)
                {
                    if (!AdvanceStream32_ToInBounds(ref p, ref x, bobRight, targetWidth))
                    {
                        continue;
                    }

                    ApplyShadow32_Clipped(dst, stride, dstY, ref p, ref x, clipLeft, clipRight, timeMask);
                    continue;
                }

                ApplyShadow32_Fast(dst, stride, dstY, ref p, x, timeMask);
            }
        }

        private void ProcessShadow16(
            CBitmap target,
            ushort[] shadowTable,
            int lineCtrlBase,
            int bobX,
            int bobRight,
            int targetWidth,
            bool clippedX,
            in SRectangle cut,
            uint timeMask)
        {
            ushort[] dst = target.Pixels16;
            int stride = target.StridePixels;

            int dstY = cut.Y;
            int rows = cut.Height;

            int clipLeft = cut.X;
            int clipRight = cut.X + cut.Width - 1;

            for (int row = 0; row < rows; row++, dstY++, lineCtrlBase++)
            {
                uint token = _lineControl[lineCtrlBase];
                if (token == 0xFFFFFFFFu)
                {
                    continue;
                }

                int x = (int)(token >> 22) + bobX;
                int p = (int)(token & 0x3FFFFFu);

                if (clippedX || x < 0 || targetWidth <= bobRight)
                {
                    if (!AdvanceStream16_ToInBounds(ref p, ref x, bobRight, targetWidth))
                    {
                        continue;
                    }

                    ApplyShadow16_Clipped(dst, stride, dstY, shadowTable, ref p, ref x, clipLeft, clipRight, timeMask);
                    continue;
                }

                ApplyShadow16_Fast(dst, stride, dstY, shadowTable, ref p, x, timeMask);
            }
        }

        private bool AdvanceStream32_ToInBounds(ref int p, ref int x, int bobRight, int targetWidth)
        {
            while (x < 0 || targetWidth <= bobRight)
            {
                byte ctrl = _bobByteStream[p];
                if (ctrl == 0)
                {
                    return false;
                }

                if ((sbyte)ctrl < 0)
                {
                    x += (ctrl & 0x7F);
                    p += 1;
                }
                else
                {
                    int count = ctrl & 0x7F;
                    x += count;
                    p += 1 + count * 2;
                }

                if (!(x < 0 || targetWidth <= bobRight))
                {
                    return true;
                }
            }

            return true;
        }

        private bool AdvanceStream16_ToInBounds(ref int p, ref int x, int bobRight, int targetWidth)
        {
            // Same stream layout assumption here as your function: time bytes are present per pixel decision.
            while (x < 0 || targetWidth <= bobRight)
            {
                byte ctrl = _bobByteStream[p];
                if (ctrl == 0)
                {
                    return false;
                }

                if ((sbyte)ctrl < 0)
                {
                    x += (ctrl & 0x7F);
                    p += 1;
                }
                else
                {
                    int count = ctrl & 0x7F;
                    x += count;
                    p += 1 + count; // NOTE: if your 16-bit shadow stream is 1 time-byte per pixel (not pairs), keep this.
                                    // If it is pairs like 32-bit, change to: p += 1 + count * 2;
                }

                if (!(x < 0 || targetWidth <= bobRight))
                {
                    return true;
                }
            }

            return true;
        }

        private void ApplyShadow32_Fast(uint[] dst, int stride, int dstY, ref int p, int startX, uint timeMask)
        {
            int dstIndex = dstY * stride + startX;

            byte ctrl = _bobByteStream[p];
            while (ctrl != 0)
            {
                if ((sbyte)ctrl < 0)
                {
                    int skip = ctrl & 0x7F;
                    dstIndex += skip;
                    p += 1;
                }
                else
                {
                    int count = ctrl & 0x7F;

                    for (int i = 0; i < count; i++)
                    {
                        // time at (p + 1 + i*2), as in your code paths
                        byte t = _bobByteStream[p + 1 + i * 2];
                        if (t <= (byte)timeMask)
                        {
                            dst[dstIndex + i] = Darken32(dst[dstIndex + i]);
                        }
                    }

                    dstIndex += count;
                    p += 1 + count * 2;
                }

                ctrl = _bobByteStream[p];
            }
        }

        private void ApplyShadow32_Clipped(uint[] dst, int stride, int dstY, ref int p, ref int x, int clipLeft, int clipRight, uint timeMask)
        {
            byte ctrl = _bobByteStream[p];
            while (ctrl != 0)
            {
                if ((sbyte)ctrl < 0)
                {
                    int skip = ctrl & 0x7F;
                    x += skip;
                    p += 1;
                }
                else
                {
                    int count = ctrl & 0x7F;

                    for (int i = 0; i < count; i++)
                    {
                        int xi = x + i;
                        if (xi >= clipLeft && xi <= clipRight)
                        {
                            byte t = _bobByteStream[p + 1 + i * 2];
                            if (t <= (byte)timeMask)
                            {
                                int idx = dstY * stride + xi;
                                dst[idx] = Darken32(dst[idx]);
                            }
                        }
                    }

                    x += count;
                    p += 1 + count * 2;
                }

                ctrl = _bobByteStream[p];
            }
        }

        private void ApplyShadow16_Fast(ushort[] dst, int stride, int dstY, ushort[] shadowTable, ref int p, int startX, uint timeMask)
        {
            int dstIndex = dstY * stride + startX;

            byte ctrl = _bobByteStream[p];
            while (ctrl != 0)
            {
                if ((sbyte)ctrl < 0)
                {
                    int skip = ctrl & 0x7F;
                    dstIndex += skip;
                    p += 1;
                }
                else
                {
                    int count = ctrl & 0x7F;

                    for (int i = 0; i < count; i++)
                    {
                        // If your 16-bit time stream is 1 byte per pixel, time is at (p + 1 + i).
                        // If it is paired, change to (p + 1 + i*2).
                        byte t = _bobByteStream[p + 1 + i];
                        if (t <= (byte)timeMask)
                        {
                            ushort pix = dst[dstIndex + i];
                            dst[dstIndex + i] = shadowTable[pix];
                        }
                    }

                    dstIndex += count;
                    p += 1 + count;
                }

                ctrl = _bobByteStream[p];
            }
        }

        private void ApplyShadow16_Clipped(ushort[] dst, int stride, int dstY, ushort[] shadowTable, ref int p, ref int x, int clipLeft, int clipRight, uint timeMask)
        {
            byte ctrl = _bobByteStream[p];
            while (ctrl != 0)
            {
                if ((sbyte)ctrl < 0)
                {
                    int skip = ctrl & 0x7F;
                    x += skip;
                    p += 1;
                }
                else
                {
                    int count = ctrl & 0x7F;

                    for (int i = 0; i < count; i++)
                    {
                        int xi = x + i;
                        if (xi >= clipLeft && xi <= clipRight)
                        {
                            byte t = _bobByteStream[p + 1 + i];
                            if (t <= (byte)timeMask)
                            {
                                int idx = dstY * stride + xi;
                                dst[idx] = shadowTable[dst[idx]];
                            }
                        }
                    }

                    x += count;
                    p += 1 + count;
                }

                ctrl = _bobByteStream[p];
            }
        }

        private static uint Darken32(uint color)
        {
            // Same math as your decompile-port.
            uint hi = (color >> 0x0F) & 0xFFFFFFFEu;
            uint lo = (uint)(((color & 0xFFu) * 2u) / 3u);

            return
                (hi / 3u) << 16 |
                ((((color >> 7) & 0x1FEu) * 0xAAABu) >> 9) & 0xFF00u |
                (((hi / 3u) + lo) >> 4) + lo;
        }

        // Wrapper 1: konstantes Alpha (dein erster Fall)
        internal void PrintBob_UsingTransparency(uint bobId, CBitmap target, uint alpha, int x, int y, CPalette palette)
        {
            byte a = (byte)(alpha & 0xFFu);
            PrintBob_UsingTransparencyCore(bobId, target, x, y, palette, TransparencyAlphaMode.ConstantAlpha, a);
        }

        // Wrapper 2: Alpha pro Pixel aus Stream (dein "double-byte" Fall)
        internal void PrintBob_UsingTransparency(uint bobId, CBitmap target, int x, int y, CPalette palette)
        {
            PrintBob_UsingTransparencyCore(bobId, target, x, y, palette, TransparencyAlphaMode.PerPixelAlpha, 0);
        }

        private enum TransparencyAlphaMode
        {
            ConstantAlpha = 0,
            PerPixelAlpha = 1
        }

        // Das ist die EINZIGE große Funktion, die du noch brauchst.
        private void PrintBob_UsingTransparencyCore(
            uint bobId,
            CBitmap target,
            int x,
            int y,
            CPalette palette,
            TransparencyAlphaMode alphaMode,
            byte constantAlpha)
        {
            uint index = unchecked(bobId - (uint)_firstBobId);
            if (index >= _bobCount)
            {
                return;
            }

            // Entry holen (Type + Rect + ScanBase). Mappe das auf dein echtes Layout.
            BobEntry entry = _bobEntries[(int)index];
            if (entry.Type == 0 || entry.Type != 1)
            {
                return;
            }

            SRectangle bobRect = entry.Rect;
            bobRect.MovePosition(x, y);

            if (!bobRect.IsTouching(in target.Rect))
            {
                return;
            }

            SRectangle clipRect = new SRectangle(in bobRect);
            bool clippedX = clipRect.CutInsideX(in target.Rect);
            if (clipRect.Width <= 0 || clipRect.Height <= 0)
            {
                return;
            }

            // Scanline table start (sign-fix wie im Original: y<0 => +(-y))
            int ySkip = bobRect.Y < 0 ? -bobRect.Y : 0;
            int scanBase = (int)entry.ScanlineBase + ySkip;

            int rightEdge = bobRect.X + bobRect.Width - 1;
            int targetWidth = target.Width;

            int dstRow = clipRect.Y;
            int rows = clipRect.Height;

            // Für Bounds-Checks brauchen wir clip left/right
            int clipLeft = clipRect.X;
            int clipRight = clipRect.X + clipRect.Width - 1;

            // RLE-Stream bytes pro Pixel:
            // - ConstantAlpha: 1 byte/pixel (palIndex)
            // - PerPixelAlpha: 2 bytes/pixel (palIndex, alpha)
            int bytesPerPixelInStream = alphaMode == TransparencyAlphaMode.PerPixelAlpha ? 2 : 1;

            byte bpp = target.ColorDepthBits;

            if (bpp == 0x20)
            {
                uint[] table32 = palette.TrueColorTable32;
                if (table32 == null || table32.Length == 0)
                {
                    return;
                }

                uint[] dst32 = target.Pixels32;
                int stride = target.StridePixels;

                for (int row = 0; row < rows; row++, dstRow++)
                {
                    uint scanVal = _scanlineTbl[scanBase + row];
                    if (scanVal == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int srcOffset = (int)(scanVal & 0x3FFFFFu);
                    int dstX = (int)(scanVal >> 22) + bobRect.X;

                    bool needsBoundsChecks = clippedX || dstX < 0 || targetWidth <= rightEdge;

                    DecodeAndBlendLine32(
                        dst32,
                        stride,
                        dstRow,
                        dstX,
                        needsBoundsChecks,
                        clipLeft,
                        clipRight,
                        table32,
                        alphaMode,
                        constantAlpha,
                        _bobByteData,
                        srcOffset,
                        bytesPerPixelInStream);
                }

                return;
            }

            if (bpp == 0x10)
            {
                ushort[] table16 = palette.HighColorTable16;
                if (table16 == null || table16.Length == 0)
                {
                    return;
                }

                ushort[] dst16 = target.Pixels16;
                int stride = target.StridePixels;

                // Mask wie im Original (Flag46 entscheidet)
                uint mask = _highColorCreatorFlag46 != 0 ? 0x07E007E0u : 0x03E003E0u;
                ushort mask16 = (ushort)mask;

                for (int row = 0; row < rows; row++, dstRow++)
                {
                    uint scanVal = _scanlineTbl[scanBase + row];
                    if (scanVal == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int srcOffset = (int)(scanVal & 0x3FFFFFu);
                    int dstX = (int)(scanVal >> 22) + bobRect.X;

                    bool needsBoundsChecks = clippedX || dstX < 0 || targetWidth <= rightEdge;

                    DecodeAndBlendLine16(
                        dst16,
                        stride,
                        dstRow,
                        dstX,
                        needsBoundsChecks,
                        clipLeft,
                        clipRight,
                        table16,
                        alphaMode,
                        constantAlpha,
                        _bobByteData,
                        srcOffset,
                        bytesPerPixelInStream,
                        mask,
                        mask16);
                }
            }
        }

        private void DecodeAndBlendLine32(
            uint[] dst,
            int stride,
            int dstRow,
            int dstXStart,
            bool bounds,
            int clipLeft,
            int clipRight,
            uint[] table,
            TransparencyAlphaMode alphaMode,
            byte constantAlpha,
            byte[] streamData,
            int streamOffset,
            int bytesPerPixelInStream)
        {
            int dstIndex = dstRow * stride + dstXStart;
            int x = dstXStart;

            int p = streamOffset;
            byte cmd = streamData[p];

            while (cmd != 0)
            {
                if ((sbyte)cmd < 0)
                {
                    int skip = cmd & 0x7F;
                    x += skip;
                    dstIndex += skip;
                    p += 1;
                }
                else
                {
                    int count = cmd & 0x7F;
                    p += 1;

                    for (int i = 0; i < count; i++)
                    {
                        int px = x + i;

                        if (!bounds || (px >= clipLeft && px <= clipRight))
                        {
                            byte palIndex = streamData[p + i * bytesPerPixelInStream];
                            byte a = alphaMode == TransparencyAlphaMode.PerPixelAlpha
                                ? streamData[p + i * bytesPerPixelInStream + 1]
                                : constantAlpha;

                            uint src = table[palIndex];
                            uint dstPix = dst[dstIndex + i];

                            dst[dstIndex + i] = Blend32(src, dstPix, a);
                        }
                    }

                    x += count;
                    dstIndex += count;
                    p += count * bytesPerPixelInStream;
                }

                cmd = streamData[p];
            }
        }

        private void DecodeAndBlendLine16(
            ushort[] dst,
            int stride,
            int dstRow,
            int dstXStart,
            bool bounds,
            int clipLeft,
            int clipRight,
            ushort[] table,
            TransparencyAlphaMode alphaMode,
            byte constantAlpha,
            byte[] streamData,
            int streamOffset,
            int bytesPerPixelInStream,
            uint mask,
            ushort mask16)
        {
            int dstIndex = dstRow * stride + dstXStart;
            int x = dstXStart;

            int p = streamOffset;
            byte cmd = streamData[p];

            while (cmd != 0)
            {
                if ((sbyte)cmd < 0)
                {
                    int skip = cmd & 0x7F;
                    x += skip;
                    dstIndex += skip;
                    p += 1;
                }
                else
                {
                    int count = cmd & 0x7F;
                    p += 1;

                    for (int i = 0; i < count; i++)
                    {
                        int px = x + i;

                        if (!bounds || (px >= clipLeft && px <= clipRight))
                        {
                            byte palIndex = streamData[p + i * bytesPerPixelInStream];
                            byte a = alphaMode == TransparencyAlphaMode.PerPixelAlpha
                                ? streamData[p + i * bytesPerPixelInStream + 1]
                                : constantAlpha;

                            ushort src = table[palIndex];
                            ushort dstPix = dst[dstIndex + i];

                            dst[dstIndex + i] = Blend16(src, dstPix, a, mask, mask16);
                        }
                    }

                    x += count;
                    dstIndex += count;
                    p += count * bytesPerPixelInStream;
                }

                cmd = streamData[p];
            }
        }

        private static uint Blend32(uint src, uint dst, byte alpha)
        {
            uint a = alpha;
            uint inv = 0x100u - a;

            uint outB = ((src & 0x000000FFu) * a + (dst & 0x000000FFu) * inv) >> 8;
            uint outG = ((src & 0x0000FF00u) * a + (dst & 0x0000FF00u) * inv) >> 8;
            uint outR = ((src & 0x00FF0000u) * a + (dst & 0x00FF0000u) * inv) >> 8;

            return (outB & 0xFFu) | (outG & 0xFF00u) | (outR & 0xFF0000u);
        }

        private static ushort Blend16(ushort src, ushort dst, byte alpha, uint mask, ushort mask16)
        {
            uint a = alpha;
            uint inv = 0x100u - a;

            uint srcB = (uint)(src & 0x001Fu);
            uint dstB = (uint)(dst & 0x001Fu);

            uint srcG = (uint)(src & (ushort)mask);
            uint dstG = (uint)(dst & (ushort)mask);

            uint srcR = (uint)(src & 0xF800u);
            uint dstR = (uint)(dst & 0xF800u);

            uint outB = ((srcB * a + dstB * inv) >> 8) & 0x001Fu;
            uint outG = (((srcG * a + dstG * inv) >> 8) & mask16);
            uint outR = ((srcR * a + dstR * inv) >> 8) & 0xF800u;

            return (ushort)(outB | outG | outR);
        }

        internal void PrintBob_UsingShadedAlpha(uint bobId, CBitmap target, int x, int y, CPalette palette, int shade)
        {
            uint index = unchecked(bobId - (uint)_firstBobId);
            if (index >= _bobCount)
            {
                return;
            }

            BobEntry entry = _bobEntries[(int)index];
            if (entry.Type != 4)
            {
                return;
            }

            SRectangle bobRect = entry.Rect;
            bobRect.MovePosition(x, y);

            if (!bobRect.IsTouching(in target.Rect))
            {
                return;
            }

            SRectangle clipRect = new SRectangle(in bobRect);
            bool clippedX = clipRect.CutInsideX(in target.Rect);

            if (clipRect.Width <= 0 || clipRect.Height <= 0)
            {
                return;
            }

            int ySkip = bobRect.Y < 0 ? -bobRect.Y : 0;
            int scanBase = (int)entry.ScanlineBase + ySkip;

            int dstRow = clipRect.Y;
            int rows = clipRect.Height;

            int clipLeft = clipRect.X;
            int clipRight = clipRect.X + clipRect.Width - 1;

            int rightEdge = bobRect.X + bobRect.Width - 1;
            int targetWidth = target.Width;

            byte bpp = target.ColorDepthBits;

            if (bpp == 0x20)
            {
                uint[] table32 = palette.TrueColorTable32;
                uint[] dst32 = target.Pixels32;

                if (table32 == null || table32.Length == 0 || dst32 == null)
                {
                    return;
                }

                int stride = target.StridePixels;

                for (int row = 0; row < rows; row++, dstRow++)
                {
                    uint token = _scanlineTbl[scanBase + row];
                    if (token == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int streamOffset = (int)(token & 0x003FFFFFu);
                    int dstXStart = (int)(token >> 22) + bobRect.X;

                    bool bounds = clippedX || dstXStart < 0 || targetWidth <= rightEdge;

                    DecodeRleAlphaPairs(
                        _maskStream,
                        streamOffset,
                        dstXStart,
                        bounds,
                        clipLeft,
                        clipRight,
                        dst32,
                        stride,
                        dstRow,
                        table32,
                        shade);
                }

                return;
            }

            if (bpp == 0x10)
            {
                ushort[] table16 = palette.HighColorTable16;
                ushort[] dst16 = target.Pixels16;

                if (table16 == null || table16.Length == 0 || dst16 == null)
                {
                    return;
                }

                int stride = target.StridePixels;

                // 555/565 masks like decompile:
                // flag46 == 0 => 555: rMask=0x7C00, gMask=0x03E0
                // flag46 != 0 => 565: rMask=0xF800, gMask=0x07E0
                uint gMask = _highColorCreatorFlag46 == 0 ? 0x03E0u : 0x07E0u;
                uint rMask = _highColorCreatorFlag46 == 0 ? 0x7C00u : 0xF800u;
                ushort gMask16 = (ushort)gMask;
                ushort rMask16 = (ushort)rMask;

                uint rMax = rMask << 8;
                uint gMax = gMask << 8;

                for (int row = 0; row < rows; row++, dstRow++)
                {
                    uint token = _scanlineTbl[scanBase + row];
                    if (token == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int streamOffset = (int)(token & 0x003FFFFFu);
                    int dstXStart = (int)(token >> 22) + bobRect.X;

                    bool bounds = clippedX || dstXStart < 0 || targetWidth <= rightEdge;

                    DecodeRleAlphaPairs16(
                        _maskStream,
                        streamOffset,
                        dstXStart,
                        bounds,
                        clipLeft,
                        clipRight,
                        dst16,
                        stride,
                        dstRow,
                        table16,
                        shade,
                        rMask,
                        gMask,
                        rMask16,
                        gMask16,
                        rMax,
                        gMax);
                }
            }
        }

        private static void DecodeRleAlphaPairs(
            byte[] stream,
            int offset,
            int dstXStart,
            bool bounds,
            int clipLeft,
            int clipRight,
            uint[] dst,
            int stride,
            int dstRow,
            uint[] table,
            int shade)
        {
            int p = offset;
            int x = dstXStart;

            // Fast path uses linear index; bounds path uses absolute index per pixel.
            int dstIndex = dstRow * stride + dstXStart;

            byte cmd = stream[p];
            while (cmd != 0)
            {
                if ((sbyte)cmd < 0)
                {
                    int skip = cmd & 0x7F;
                    x += skip;
                    dstIndex += skip;
                    p += 1;
                }
                else
                {
                    int count = cmd & 0x7F;
                    p += 1;

                    if (!bounds)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            byte palIndex = stream[p + i * 2];
                            byte a = stream[p + i * 2 + 1];

                            uint src = table[palIndex];
                            uint d = dst[dstIndex + i];

                            dst[dstIndex + i] = ShadeBlend32(src, d, a, shade);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < count; i++)
                        {
                            int px = x + i;
                            if (px >= clipLeft && px <= clipRight)
                            {
                                int idx = dstRow * stride + px;

                                byte palIndex = stream[p + i * 2];
                                byte a = stream[p + i * 2 + 1];

                                uint src = table[palIndex];
                                uint d = dst[idx];

                                dst[idx] = ShadeBlend32(src, d, a, shade);
                            }
                        }

                        x += count;
                        dstIndex += count;
                    }

                    p += count * 2;
                }

                cmd = stream[p];
            }
        }

        private static uint ShadeBlend32(uint src, uint dst, byte alpha, int shade)
        {
            uint a = alpha;
            uint inv = 0x100u - a;

            // Exactly the decompile-style math:
            // r = (((src&0xff0000)*a >> 7) * shade) + ((dst&0xff0000)*inv)
            uint r = (((src & 0x00FF0000u) * a >> 7) * (uint)shade) + ((dst & 0x00FF0000u) * inv);
            uint g = (((src & 0x0000FF00u) * a >> 7) * (uint)shade) + ((dst & 0x0000FF00u) * inv);
            uint b = (((src & 0x000000FFu) * a >> 7) * (uint)shade) + ((dst & 0x000000FFu) * inv);

            // Saturation caps copied from your decompile port:
            if (r > 0xFEFFFFFFu) r = 0xFF000000u;
            if (g > 0x00FEFFFFu) g = 0x00FF0000u;
            if (b > 0x0000FEFFu) b = 0x0000FF00u;

            return (b >> 8) | ((g >> 8) & 0x0000FF00u) | ((r >> 8) & 0x00FF0000u);
        }

        private static void DecodeRleAlphaPairs16(
            byte[] stream,
            int offset,
            int dstXStart,
            bool bounds,
            int clipLeft,
            int clipRight,
            ushort[] dst,
            int stride,
            int dstRow,
            ushort[] table,
            int shade,
            uint rMask,
            uint gMask,
            ushort rMask16,
            ushort gMask16,
            uint rMax,
            uint gMax)
        {
            int p = offset;
            int x = dstXStart;

            int dstIndex = dstRow * stride + dstXStart;

            byte cmd = stream[p];
            while (cmd != 0)
            {
                if ((sbyte)cmd < 0)
                {
                    int skip = cmd & 0x7F;
                    x += skip;
                    dstIndex += skip;
                    p += 1;
                }
                else
                {
                    int count = cmd & 0x7F;
                    p += 1;

                    if (!bounds)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            byte palIndex = stream[p + i * 2];
                            byte a = stream[p + i * 2 + 1];

                            ushort src = table[palIndex];
                            ushort d = dst[dstIndex + i];

                            dst[dstIndex + i] = ShadeBlend16(src, d, a, shade, rMask, gMask, rMask16, gMask16, rMax, gMax);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < count; i++)
                        {
                            int px = x + i;
                            if (px >= clipLeft && px <= clipRight)
                            {
                                int idx = dstRow * stride + px;

                                byte palIndex = stream[p + i * 2];
                                byte a = stream[p + i * 2 + 1];

                                ushort src = table[palIndex];
                                ushort d = dst[idx];

                                dst[idx] = ShadeBlend16(src, d, a, shade, rMask, gMask, rMask16, gMask16, rMax, gMax);
                            }
                        }

                        x += count;
                        dstIndex += count;
                    }

                    p += count * 2;
                }

                cmd = stream[p];
            }
        }

        private static ushort ShadeBlend16(
            ushort src,
            ushort dst,
            byte alpha,
            int shade,
            uint rMask,
            uint gMask,
            ushort rMask16,
            ushort gMask16,
            uint rMax,
            uint gMax)
        {
            uint a = alpha;
            uint inv = 0x100u - a;

            // Terms are decompile-faithful:
            uint rTerm = ((rMask & src) * a >> 7) * (uint)shade + (rMask & dst) * inv;
            uint gTerm = ((gMask & src) * a >> 7) * (uint)shade + (gMask & dst) * inv;
            uint bTerm = (((uint)(src & 0x001Fu)) * a >> 7) * (uint)shade + inv * (uint)(dst & 0x001Fu);

            uint rOut = rTerm <= rMax ? rTerm : rMax;
            uint gOut = gTerm <= gMax ? gTerm : gMax;

            if (bTerm > 0x1EFFu)
            {
                bTerm = 0x1F00u;
            }

            return (ushort)(
                (bTerm >> 8) |
                ((gOut >> 8) & gMask16) |
                ((rOut >> 8) & rMask16)
            );
        }

        // ============================================================
        // Helpers
        // ============================================================

        private void ClearInstance()
        {
            _firstBobId = 0;
            _numberOfBobs = 0;
            _field10 = 0;
            _field14 = 0;
            _field18 = 0;
            _field1C = 0;
            _field20 = 0;

            _flag28 = false;

            _bobDataPtr = 0;
            _ptr38 = 0;
            _ptr40 = 0;

            _obj48 = 0;
            _obj50 = 0;
            _obj58 = 0;
        }

        private nint GetBobEntryPtr(uint index)
        {
            // Original layout: basePtr + index * 0x18
            if (_bobDataPtr == 0)
            {
                return 0;
            }

            return _bobDataPtr + (nint)(index * 0x18U);
        }

        // These must exist elsewhere in your project (same names), so we reference them only.
        private static extern nint XB_Storable_LoadObject(CFile file);
        private static extern void XB_Storable_SaveObject(CFile file, nint obj);

        internal static void MovePosition(ref SRectangle rect, int dx, int dy)
        {
            rect.X += dx;
            rect.Y += dy;
        }

        internal static bool IsTouching(ref SRectangle a, ref SRectangle b)
        {
            if (a.Width <= 0 || a.Height <= 0 || b.Width <= 0 || b.Height <= 0) return false;
            if (a.X + a.Width <= b.X) return false;
            if (b.X + b.Width <= a.X) return false;
            if (a.Y + a.Height <= b.Y) return false;
            if (b.Y + b.Height <= a.Y) return false;
            return true;
        }

        internal static bool CutInsideX(ref SRectangle rect, ref SRectangle clip)
        {
            int left = rect.X;
            int right = rect.X + rect.Width;

            int clipLeft = clip.X;
            int clipRight = clip.X + clip.Width;

            int newLeft = left < clipLeft ? clipLeft : left;
            int newRight = right > clipRight ? clipRight : right;

            int newWidth = newRight - newLeft;
            if (newWidth <= 0)
            {
                rect.Width = 0;
                return false;
            }

            rect.X = newLeft;
            rect.Width = newWidth;
            return (newLeft != left) || (newRight != right);
        }
    }
}