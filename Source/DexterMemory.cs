namespace OpenVikings
{
    internal static class DexterMemory
    {
        // --- Internal bookkeeping -------------------------------------------------------

        private struct MallocEntry
        {
            internal IntPtr Ptr;      // +0x00
            internal uint Size;       // +0x08
            internal byte Pad;        // +0x0C (alignment/padding info in original; not needed here)
            internal byte Flags;      // +0x0D
        }

        private sealed class CacheEntry
        {
            internal IntPtr Ptr;
            internal uint Size;
            internal uint Time;
            internal string Key;
        }

        private static readonly object _mutex = new object();

        // "MallocList"
        private static MallocEntry[]? _mallocList;
        private static uint _mallocListSize;
        private static uint _mallocsUsed;

        // "MallocCount" (tracks OS mallocs)
        private static int _mallocCount;

        // Heap system
        private static int _heapSizeRequest;
        private static IntPtr _dexterHeap;
        private static int[]? _dexterHeapList; // block map (1024 byte blocks)
        private static uint _heapSize;
        private static uint _heapListBlocks;

        // Cache system
        private static uint _maxCacheEntries;
        private static uint _currentCacheSize;
        private static CacheEntry[]? _cache;

        // --- External dependencies (stubs to be wired) ---------------------------------
        // These are placeholders for your existing engine bindings.
        // Replace bodies with your actual DexterOS / DexterFile / OSGeneric calls.

        private static class DexterOS
        {
            internal static void MutexLock(object sync) => Monitor.Enter(sync);
            internal static void MutexUnLock(object sync) => Monitor.Exit(sync);

            internal static void GetDexterThreadID()
            {
                // No-op placeholder (original seems to record/debug current thread).
            }

            internal static uint Time()
            {
                // Good enough for cache aging behavior.
                return unchecked((uint)Environment.TickCount);
            }
        }

        private static class OSGeneric
        {
            internal static IntPtr Malloc(int tag, uint size)
            {
                // Original uses OSGeneric::Malloc(tag, size)
                return System.Runtime.InteropServices.Marshal.AllocHGlobal(checked((int)size));
            }

            internal static void Free(IntPtr ptr)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
            }

            internal static unsafe void MemSet(IntPtr dst, byte value, int count)
            {
                if (dst == IntPtr.Zero || count <= 0)
                {
                    return;
                }

                Span<byte> span = new((void*)dst, count);
                span.Fill(value);
            }

            internal static unsafe void MemCopy(IntPtr dst, IntPtr src, int count)
            {
                if (dst == IntPtr.Zero || src == IntPtr.Zero || count <= 0)
                {
                    return;
                }

                Buffer.MemoryCopy((void*)src, (void*)dst, count, count);
            }
        }

        private static class DexterFile
        {
            internal static IntPtr FileOpen(string path, byte mode) => IntPtr.Zero;
            internal static void FileClose(IntPtr handle) { }
            internal static uint FileSize(IntPtr handle) => 0;

            internal static void FileRead(IntPtr handle, IntPtr buffer, uint size, byte flags) { }
            internal static void FileWrite(IntPtr handle, IntPtr buffer, uint size) { }
        }

        // --- API: SetMaxMallocs ---------------------------------------------------------

        // DexterMemory::SetMaxMallocs(unsigned int)
        internal static void SetMaxMallocs(uint maxEntries)
        {
            DexterOS.MutexLock(_mutex);
            try
            {
                if (_mallocList == null)
                {
                    DexterOS.GetDexterThreadID();

                    _mallocList = new MallocEntry[maxEntries];
                    _mallocsUsed = 0;
                    _mallocListSize = maxEntries;
                    return;
                }

                if (_mallocsUsed < maxEntries)
                {
                    DexterOS.GetDexterThreadID();

                    MallocEntry[] oldList = _mallocList;
                    uint oldUsed = _mallocsUsed;

                    MallocEntry[] newList = new MallocEntry[maxEntries];

                    // Copy only used entries (original copies entry-by-entry, 0x10 bytes each).
                    for (uint i = 0; i < oldUsed; i++)
                    {
                        newList[i] = oldList[i];
                    }

                    _mallocList = newList;
                    _mallocListSize = maxEntries;
                }
                else
                {
                    // If used >= requested, original keeps existing list/size.
                    _mallocListSize = _mallocListSize;
                }
            }
            finally
            {
                DexterOS.MutexUnLock(_mutex);
            }
        }

        // --- API: MemorySystemShutDown --------------------------------------------------

        // DexterMemory::MemorySystemShutDown()
        internal static void MemorySystemShutDown()
        {
            // Original: iterates list and frees any ptr != 0 and (flags & 2) == 0
            // and restarts scan when list changes (because FreeMemory compacts).
            if (_mallocListSize == 0 || _mallocList == null)
            {
                return;
            }

            uint index = 0;
            while (index < _mallocListSize)
            {
                IntPtr ptr = _mallocList[index].Ptr;
                byte flags = _mallocList[index].Flags;

                if (ptr != IntPtr.Zero && (flags & 0x02) == 0)
                {
                    FreeMemory(ptr);
                    // Original does index-- because list is compacted.
                    if (index > 0)
                    {
                        index--;
                    }
                    continue;
                }

                index++;
            }
        }

        // --- API: FreeMemory ------------------------------------------------------------

        // DexterMemory::FreeMemory(void*)
        internal static void FreeMemory(IntPtr ptr)
        {
            DexterOS.MutexLock(_mutex);
            try
            {
                if (ptr == IntPtr.Zero || _mallocsUsed == 0 || _mallocList == null)
                {
                    return;
                }

                for (uint i = 0; i < _mallocsUsed; i++)
                {
                    if (_mallocList[i].Ptr != ptr)
                    {
                        continue;
                    }

                    byte flags = _mallocList[i].Flags;

                    // Original: if (flags & 0x10) == 0 => OS free, else free from heap map
                    if ((flags & 0x10) == 0)
                    {
                        OSGeneric.Free(ptr);
                        _mallocCount--;
                    }
                    else
                    {
                        // Heap block release: mark heapList[blockIndex]=0
                        if (_dexterHeap != IntPtr.Zero && _dexterHeapList != null && _heapListBlocks != 0)
                        {
                            long diff = ptr.ToInt64() - _dexterHeap.ToInt64();
                            if (diff >= 0)
                            {
                                uint block = unchecked((uint)(diff >> 10));
                                if (block < _heapListBlocks)
                                {
                                    _dexterHeapList[block] = 0;
                                }
                            }
                        }
                    }

                    // Compact list by moving last entry into freed slot (original exact behavior)
                    _mallocsUsed--;
                    uint last = _mallocsUsed;

                    if (i != last)
                    {
                        _mallocList[i] = _mallocList[last];
                    }

                    // Clear last slot
                    _mallocList[last] = default;
                    return;
                }
            }
            finally
            {
                DexterOS.MutexUnLock(_mutex);
            }
        }

        // --- API: AllocMemory -----------------------------------------------------------

        // DexterMemory::AllocMemory(unsigned int, unsigned char)
        // param2 flags (in original): bit 0x04 => zero init, bit 0x10 => from heap (set internally)
        internal static IntPtr AllocMemory(uint size, byte flags)
        {
            DexterOS.MutexLock(_mutex);
            try
            {
                if (size == 0)
                {
                    return IntPtr.Zero;
                }

                // Original does alignment calculation; in managed/unmanaged AllocHGlobal it doesn't matter,
                // but the engine tracked an "alignment pad" byte. We'll keep a compatible notion.
                uint aligned = (size + 8U) & 0xFFFFFFFCU;
                if ((size & 3U) == 0)
                {
                    aligned = size + 4U;
                }

                // Try heap allocation if allowed and heap available and size fits.
                bool usedOsMalloc = true;
                IntPtr ptr = IntPtr.Zero;

                bool heapAllowed = (flags & 0x09) == 0; // mirrors "((param_2 & 9) == 0)"
                if (heapAllowed && _dexterHeap != IntPtr.Zero && _dexterHeapList != null && aligned <= _heapSize && _heapListBlocks != 0)
                {
                    IntPtr heapPtr = TryAllocFromHeapInternal(aligned);
                    if (heapPtr != IntPtr.Zero)
                    {
                        ptr = heapPtr;
                        usedOsMalloc = false;
                    }
                }

                if (ptr == IntPtr.Zero)
                {
                    ptr = OSGeneric.Malloc(0x476820, aligned);
                    if (ptr == IntPtr.Zero)
                    {
                        return IntPtr.Zero;
                    }

                    usedOsMalloc = true;
                }

                // Ensure malloc list exists and has capacity (original: 3000 then grow by 1.2)
                if (_mallocList == null)
                {
                    SetMaxMallocs(3000);
                }

                if (_mallocList == null)
                {
                    // Should not happen, but keep it safe.
                    if (usedOsMalloc)
                    {
                        OSGeneric.Free(ptr);
                    }
                    else
                    {
                        FreeMemoryFromHeap(ptr);
                    }

                    return IntPtr.Zero;
                }

                if (_mallocsUsed == _mallocListSize)
                {
                    uint grown = unchecked((uint)(MathF.Floor(_mallocsUsed * 1.2f)));
                    if (grown <= _mallocsUsed)
                    {
                        grown = _mallocsUsed + 1;
                    }

                    SetMaxMallocs(grown);
                }

                if (_mallocList == null || _mallocsUsed >= _mallocListSize)
                {
                    // Could not grow; free allocated memory
                    if (usedOsMalloc)
                    {
                        OSGeneric.Free(ptr);
                    }
                    else
                    {
                        FreeMemoryFromHeap(ptr);
                    }

                    return IntPtr.Zero;
                }

                uint index = _mallocsUsed;
                _mallocsUsed++;

                byte pad = unchecked((byte)(aligned - size));

                byte entryFlags;
                if (usedOsMalloc)
                {
                    _mallocCount++;
                    entryFlags = flags;
                }
                else
                {
                    // Mark as heap allocation (original sets 0x10)
                    entryFlags = unchecked((byte)(flags | 0x10));
                }

                _mallocList[index] = new MallocEntry
                {
                    Ptr = ptr,
                    Size = size,
                    Pad = pad,
                    Flags = entryFlags
                };

                // If flags & 4 -> zero init original.
                if ((flags & 0x04) != 0)
                {
                    OSGeneric.MemSet(ptr, 0, checked((int)size));
                }

                return ptr;
            }
            finally
            {
                DexterOS.MutexUnLock(_mutex);
            }
        }

        // --- API: AllocMemoryFromHeap ---------------------------------------------------

        // DexterMemory::AllocMemoryFromHeap(unsigned int)
        internal static IntPtr AllocMemoryFromHeap(uint size)
        {
            if (_dexterHeap == IntPtr.Zero || _dexterHeapList == null || size == 0 || size > _heapSize)
            {
                return IntPtr.Zero;
            }

            return TryAllocFromHeapInternal(size);
        }

        private static IntPtr TryAllocFromHeapInternal(uint sizeBytes)
        {
            // Mirrors the engine behavior: heap is tracked in 1024-byte blocks, store block-run length at start index.
            uint blocksNeeded = (sizeBytes >> 10) + 1U;

            if (_dexterHeapList == null || _heapListBlocks == 0)
            {
                return IntPtr.Zero;
            }

            for (uint start = 0; start < _heapListBlocks; start++)
            {
                bool freeRun = true;

                for (uint i = 0; i < blocksNeeded; i++)
                {
                    uint idx = start + i;
                    if (idx >= _heapListBlocks)
                    {
                        freeRun = false;
                        break;
                    }

                    if (_dexterHeapList[idx] != 0)
                    {
                        // skip ahead like the original does (jump to end of occupied run)
                        uint occupiedLen = unchecked((uint)_dexterHeapList[idx]);
                        if (occupiedLen == 0)
                        {
                            occupiedLen = 1;
                        }

                        start = idx + occupiedLen - 1;
                        freeRun = false;
                        break;
                    }
                }

                if (!freeRun)
                {
                    continue;
                }

                _dexterHeapList[start] = checked((int)blocksNeeded);
                long addr = _dexterHeap.ToInt64() + ((long)start << 10);
                return new IntPtr(addr);
            }

            return IntPtr.Zero;
        }

        // --- API: FindFreeMalloc / FindMalloc ------------------------------------------

        // DexterMemory::FindFreeMalloc()
        internal static IntPtr FindFreeMalloc()
        {
            DexterOS.MutexLock(_mutex);
            try
            {
                if (_mallocList == null)
                {
                    SetMaxMallocs(3000);
                }

                if (_mallocsUsed == _mallocListSize)
                {
                    uint grown = unchecked((uint)(MathF.Floor(_mallocsUsed * 1.2f)));
                    if (grown <= _mallocsUsed)
                    {
                        grown = _mallocsUsed + 1;
                    }

                    SetMaxMallocs(grown);
                }

                if (_mallocList == null || _mallocsUsed >= _mallocListSize)
                {
                    return IntPtr.Zero;
                }

                uint index = _mallocsUsed;
                _mallocsUsed++;

                _mallocList[index] = default;
                return new IntPtr(index); // In C++ this returns pointer-to-entry; in C# we return an index token.
            }
            finally
            {
                DexterOS.MutexUnLock(_mutex);
            }
        }

        // DexterMemory::FindMalloc(void const*)
        internal static int FindMalloc(IntPtr ptr)
        {
            DexterOS.MutexLock(_mutex);
            try
            {
                if (_mallocList == null || _mallocsUsed == 0)
                {
                    return -1;
                }

                for (uint i = 0; i < _mallocsUsed; i++)
                {
                    if (_mallocList[i].Ptr == ptr)
                    {
                        return unchecked((int)i);
                    }
                }

                return -1;
            }
            finally
            {
                DexterOS.MutexUnLock(_mutex);
            }
        }

        // --- API: Heap allocate/free ----------------------------------------------------

        // DexterMemory::AllocHeap()
        internal static void AllocHeap()
        {
            if (_heapSizeRequest <= 99999)
            {
                return;
            }

            uint blocks = unchecked((uint)(_heapSizeRequest >> 10));
            uint heapBytes = unchecked((uint)_heapSizeRequest & 0xFFFFFC00U);

            IntPtr heap = OSGeneric.Malloc(0x476820, heapBytes);
            if (heap == IntPtr.Zero)
            {
                _heapListBlocks = 0;
                _heapSize = 0;
                _dexterHeap = IntPtr.Zero;
                _dexterHeapList = null;
                return;
            }

            int[] heapList = new int[blocks];
            // already zero-initialized by CLR

            _dexterHeap = heap;
            _dexterHeapList = heapList;
            _heapListBlocks = blocks;
            _heapSize = heapBytes;
        }

        // DexterMemory::FreeHeap()
        internal static void FreeHeap()
        {
            if (_dexterHeap != IntPtr.Zero)
            {
                DexterOS.GetDexterThreadID();
                OSGeneric.Free(_dexterHeap);
            }

            _dexterHeap = IntPtr.Zero;
            _dexterHeapList = null;
            _heapSize = 0;
            _heapListBlocks = 0;
        }

        // DexterMemory::FreeMemoryFromHeap(unsigned char const*)
        internal static void FreeMemoryFromHeap(IntPtr ptr)
        {
            if (_dexterHeap == IntPtr.Zero || _dexterHeapList == null || ptr == IntPtr.Zero)
            {
                return;
            }

            long diff = ptr.ToInt64() - _dexterHeap.ToInt64();
            if (diff < 0)
            {
                return;
            }

            uint block = unchecked((uint)(diff >> 10));
            if (block < _heapListBlocks)
            {
                _dexterHeapList[block] = 0;
            }
        }

        // DexterMemory::SetHeapSize(int)
        internal static void SetHeapSize(int size)
        {
            _heapSizeRequest = size;
        }

        // --- API: Memory helpers --------------------------------------------------------

        // DexterMemory::MemorySet(void*, unsigned char, int)
        internal static void MemorySet(IntPtr dst, byte value, int count)
        {
            OSGeneric.MemSet(dst, value, count);
        }

        // Convenience overload used heavily in your code (byte[] target)
        internal static void MemorySet(byte[] dst, byte value, int count)
        {
            if (dst == null || count <= 0)
            {
                return;
            }

            if (count > dst.Length)
            {
                count = dst.Length;
            }

            Array.Fill(dst, value, 0, count);
        }

        // DexterMemory::MemoryCopy(void*, void const*, int)
        internal static void MemoryCopy(IntPtr dst, IntPtr src, int count)
        {
            OSGeneric.MemCopy(dst, src, count);
        }

        // Convenience overload (byte[] -> byte[])
        internal static void MemoryCopy(byte[] dst, int dstOffset, byte[] src, int srcOffset, int count)
        {
            if (dst == null || src == null || count <= 0)
            {
                return;
            }

            Buffer.BlockCopy(src, srcOffset, dst, dstOffset, count);
        }

        internal static void MemMove(byte[] destination, int destinationIndex, byte[] source, int sourceIndex, int count)
        {
            ArgumentNullException.ThrowIfNull(destination);

            ArgumentNullException.ThrowIfNull(source);

            if (count <= 0)
            {
                return;
            }

            if (destinationIndex < 0 || sourceIndex < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (sourceIndex + count > source.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceIndex));
            }

            if (destinationIndex + count > destination.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(destinationIndex));
            }

            // Overlap-safe, equivalent to C memmove.
            Buffer.BlockCopy(source, sourceIndex, destination, destinationIndex, count);
        }

        // DexterMemory::MemoryCompare(void const*, void const*, int)
        internal static unsafe int MemoryCompare(IntPtr a, IntPtr b, int count)
        {
            if (a == IntPtr.Zero && b == IntPtr.Zero)
            {
                return 0;
            }

            if (count < 1)
            {
                // Original: debug check and return 0
                return 0;
            }

            byte* pa = (byte*)a;
            byte* pb = (byte*)b;

            for (int i = 0; i < count; i++)
            {
                byte va = pa[i];
                byte vb = pb[i];
                if (va != vb)
                {
                    return va - vb;
                }
            }

            return 0;
        }

        // DexterMemory::MemorySize(void const*)
        internal static uint MemorySize(IntPtr ptr)
        {
            DexterOS.MutexLock(_mutex);
            try
            {
                if (_mallocList == null || _mallocsUsed == 0 || ptr == IntPtr.Zero)
                {
                    return 0;
                }

                for (uint i = 0; i < _mallocsUsed; i++)
                {
                    if (_mallocList[i].Ptr == ptr)
                    {
                        return _mallocList[i].Size;
                    }
                }

                return 0;
            }
            finally
            {
                DexterOS.MutexUnLock(_mutex);
            }
        }

        // --- API: FileToMemory / MemoryToFile ------------------------------------------

        // DexterMemory::FileToMemory(char*, void*, unsigned char)
        internal static IntPtr FileToMemory(string path, IntPtr existingBuffer, byte mode)
        {
            IntPtr file = DexterFile.FileOpen(path, mode);
            if (file == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            try
            {
                uint size = DexterFile.FileSize(file);
                if (size == 0)
                {
                    return IntPtr.Zero;
                }

                IntPtr target = existingBuffer;

                if (target == IntPtr.Zero)
                {
                    target = AllocMemory(size, 0);
                    if (target == IntPtr.Zero)
                    {
                        return IntPtr.Zero;
                    }
                }
                else
                {
                    uint existingSize = MemorySize(target);
                    if (existingSize != 0 && existingSize < size)
                    {
                        // Original reads only existingSize and then returns null
                        DexterFile.FileRead(file, target, existingSize, 0);
                        return IntPtr.Zero;
                    }

                    if (existingSize == 0)
                    {
                        // Not tracked; original will allocate new and replace
                        target = AllocMemory(size, 0);
                        if (target == IntPtr.Zero)
                        {
                            return IntPtr.Zero;
                        }
                    }
                }

                DexterFile.FileRead(file, target, size, 0);
                return target;
            }
            finally
            {
                DexterFile.FileClose(file);
            }
        }

        // DexterMemory::MemoryToFile(char*, void const*, unsigned int, unsigned char)
        internal static bool MemoryToFile(string path, IntPtr buffer, uint size, byte mode)
        {
            if (buffer == IntPtr.Zero)
            {
                return false;
            }

            if (size == 0)
            {
                uint tracked = MemorySize(buffer);
                if (tracked == 0)
                {
                    return false;
                }

                size = tracked;
            }

            IntPtr file = DexterFile.FileOpen(path, mode);
            if (file == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                DexterFile.FileWrite(file, buffer, size);
                return true;
            }
            finally
            {
                DexterFile.FileClose(file);
            }
        }

        // --- API: Cache (simplified but behavior-aligned) -------------------------------

        internal static void SetMaxCacheEntries(uint entries)
        {
            DexterOS.MutexLock(_mutex);
            try
            {
                _maxCacheEntries = entries;
                _cache = entries == 0 ? null : new CacheEntry[entries];
                _currentCacheSize = 0;
            }
            finally
            {
                DexterOS.MutexUnLock(_mutex);
            }
        }

        // DexterMemory::AddMallocCache(void*, char*)
        internal static uint AddMallocCache(IntPtr ptr, string key)
        {
            if (ptr == IntPtr.Zero || key == null || key.Length == 0 || key.Length > 255)
            {
                return 0;
            }

            DexterOS.MutexLock(_mutex);
            try
            {
                if (_maxCacheEntries == 0 || _cache == null)
                {
                    return 0;
                }

                int mallocIndex = FindMalloc_NoLock(ptr);
                if (mallocIndex < 0)
                {
                    return 0;
                }

                uint size = _mallocList![mallocIndex].Size;

                // Find free slot
                for (uint i = 0; i < _maxCacheEntries; i++)
                {
                    if (_cache[i] == null || _cache[i].Ptr == IntPtr.Zero)
                    {
                        _cache[i] = new CacheEntry
                        {
                            Ptr = ptr,
                            Size = size,
                            Time = DexterOS.Time(),
                            Key = key
                        };

                        _currentCacheSize = unchecked(_currentCacheSize + size);
                        return 1;
                    }
                }

                // No free slot -> purge and retry like original
                PurgeMallocCache_NoLock();

                for (uint i = 0; i < _maxCacheEntries; i++)
                {
                    if (_cache[i] == null || _cache[i].Ptr == IntPtr.Zero)
                    {
                        _cache[i] = new CacheEntry
                        {
                            Ptr = ptr,
                            Size = size,
                            Time = DexterOS.Time(),
                            Key = key
                        };

                        _currentCacheSize = unchecked(_currentCacheSize + size);
                        return 1;
                    }
                }

                PurgeMallocCache_NoLock();
                return 0;
            }
            finally
            {
                DexterOS.MutexUnLock(_mutex);
            }
        }

        // DexterMemory::PurgeMallocCache()
        internal static void PurgeMallocCache()
        {
            DexterOS.MutexLock(_mutex);
            try
            {
                PurgeMallocCache_NoLock();
            }
            finally
            {
                DexterOS.MutexUnLock(_mutex);
            }
        }

        private static void PurgeMallocCache_NoLock()
        {
            if (_cache == null || _maxCacheEntries <= 4)
            {
                return;
            }

            uint purgeCount = _maxCacheEntries / 5;
            while (purgeCount > 0)
            {
                int oldestIndex = FindOldestCacheIndex_NoLock();
                if (oldestIndex < 0)
                {
                    break;
                }

                CacheEntry entry = _cache[oldestIndex]!;
                if (entry.Ptr != IntPtr.Zero)
                {
                    // Free only if it is still in malloc list
                    if (FindMalloc_NoLock(entry.Ptr) >= 0)
                    {
                        FreeMemory(entry.Ptr);
                    }

                    _currentCacheSize = unchecked(_currentCacheSize - entry.Size);
                }

                _cache[oldestIndex] = null;
                purgeCount--;
            }
        }

        private static int FindOldestCacheIndex_NoLock()
        {
            if (_cache == null)
            {
                return -1;
            }

            uint bestTime = 0xFFFFFFFF;
            int bestIndex = -1;

            for (int i = 0; i < _cache.Length; i++)
            {
                CacheEntry? entry = _cache[i];
                if (entry == null || entry.Ptr == IntPtr.Zero)
                {
                    continue;
                }

                if (entry.Time < bestTime)
                {
                    bestTime = entry.Time;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        // DexterMemory::TakeMallocCache(char*)
        internal static IntPtr TakeMallocCache(string key)
        {
            if (key == null || key.Length == 0 || key.Length > 255)
            {
                return IntPtr.Zero;
            }

            DexterOS.MutexLock(_mutex);
            try
            {
                if (_cache == null || _maxCacheEntries == 0)
                {
                    return IntPtr.Zero;
                }

                for (int i = 0; i < _cache.Length; i++)
                {
                    CacheEntry? entry = _cache[i];
                    if (entry == null || entry.Ptr == IntPtr.Zero)
                    {
                        continue;
                    }

                    if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        _currentCacheSize = unchecked(_currentCacheSize - entry.Size);

                        IntPtr ptr = entry.Ptr;
                        _cache[i] = null;
                        return ptr;
                    }
                }

                return IntPtr.Zero;
            }
            finally
            {
                DexterOS.MutexUnLock(_mutex);
            }
        }

        // DexterMemory::ClearMallocCache()
        internal static void ClearMallocCache()
        {
            DexterOS.MutexLock(_mutex);
            try
            {
                if (_cache == null || _maxCacheEntries == 0)
                {
                    return;
                }

                for (int i = 0; i < _cache.Length; i++)
                {
                    CacheEntry? entry = _cache[i];
                    if (entry == null || entry.Ptr == IntPtr.Zero)
                    {
                        continue;
                    }

                    if (FindMalloc_NoLock(entry.Ptr) >= 0)
                    {
                        FreeMemory(entry.Ptr);
                    }

                    _currentCacheSize = unchecked(_currentCacheSize - entry.Size);
                    _cache[i] = null;
                }
            }
            finally
            {
                DexterOS.MutexUnLock(_mutex);
            }
        }

        // DexterMemory::PurgeMallocCacheEntry(unsigned int)
        internal static void PurgeMallocCacheEntry(uint index)
        {
            DexterOS.MutexLock(_mutex);
            try
            {
                if (_cache == null || index >= _maxCacheEntries)
                {
                    return;
                }

                CacheEntry? entry = _cache[index];
                if (entry == null || entry.Ptr == IntPtr.Zero)
                {
                    _cache[index] = null;
                    return;
                }

                if (FindMalloc_NoLock(entry.Ptr) >= 0)
                {
                    FreeMemory(entry.Ptr);
                }

                _currentCacheSize = unchecked(_currentCacheSize - entry.Size);
                _cache[index] = null;
            }
            finally
            {
                DexterOS.MutexUnLock(_mutex);
            }
        }

        private static int FindMalloc_NoLock(IntPtr ptr)
        {
            if (_mallocList == null || _mallocsUsed == 0 || ptr == IntPtr.Zero)
            {
                return -1;
            }

            for (uint i = 0; i < _mallocsUsed; i++)
            {
                if (_mallocList[i].Ptr == ptr)
                {
                    return unchecked((int)i);
                }
            }

            return -1;
        }

        // --- API: Get/Put primitives ----------------------------------------------------

        // DexterMemory::GetByte(unsigned char const*)
        internal static unsafe byte GetByte(IntPtr ptr)
        {
            return ptr == IntPtr.Zero ? (byte)0 : *(byte*)ptr;
        }

        // DexterMemory::GetWord(unsigned short const*)
        internal static unsafe ushort GetWord(IntPtr ptr)
        {
            return ptr == IntPtr.Zero ? (ushort)0 : *(ushort*)ptr;
        }

        // DexterMemory::GetLong(unsigned int const*)
        internal static unsafe uint GetLong(IntPtr ptr)
        {
            return ptr == IntPtr.Zero ? 0U : *(uint*)ptr;
        }

        // DexterMemory::PutByte(unsigned char*, unsigned char)
        internal static unsafe void PutByte(IntPtr ptr, byte value)
        {
            if (ptr != IntPtr.Zero)
            {
                *(byte*)ptr = value;
            }
        }

        // DexterMemory::PutWord(unsigned short*, unsigned short)
        internal static unsafe void PutWord(IntPtr ptr, ushort value)
        {
            if (ptr != IntPtr.Zero)
            {
                *(ushort*)ptr = value;
            }
        }

        // DexterMemory::PutLong(unsigned int*, unsigned int)
        internal static unsafe void PutLong(IntPtr ptr, uint value)
        {
            if (ptr != IntPtr.Zero)
            {
                *(uint*)ptr = value;
            }
        }
    }
}