// DexterGFX.cs
// Managed port of DexterGFX.cxx (SDL backend via Silk.NET.SDL).
// Notes:
// - Instance-based (fits DexterApp.BindGfx(...) and avoids mixing global/static state).
// - No unsafe/pointer exposure in the public surface. Internally, Silk.NET.SDL requires unsafe for SDL_Window*/SDL_Renderer*.
// - Dirty buffering is implemented in a faithful-but-simplified way (byte-wise compare), matching the intent of the C++ DirtyList.
// - Palette tables (8->16) are implemented (565/555). 8->32 and 8->24 paths are intentionally not implemented because the original
//   C++ runtime here creates a 16-bit SDL surface and uses a 16-bit texture format.

using OpenVikings.SDL2;

namespace OpenVikings.Dexter
{
    internal sealed class DexterGFX : IDisposable
    {
        // --------------------------------------------------------------------
        // "Globals" in the original C++ (kept as instance fields here)
        // --------------------------------------------------------------------

        internal ushort CallBackTime { get; set; } // C++: DexterGFX::CallBackTime, used by DexterApp main loop

        internal int RenderWidth { get; private set; }     // C++ default 0x140
        internal int RenderHeight { get; private set; }    // C++ default 0x0F0
        internal byte RenderBitDepth { get; private set; } // C++ default 8
        internal byte RenderByteDepth { get; private set; } // derived: 1 for 8bpp, 2 for 16bpp

        internal int WindowWidth { get; private set; }     // C++ default 0x140
        internal int WindowHeight { get; private set; }    // C++ default 0x0F0

        internal bool ScreenLocked { get; private set; }   // derived from SetRenderDepth param_2 in C++
        internal bool GFXActiveState { get; private set; }
        internal bool DisplayOpenBefore { get; private set; }

        internal bool ShowPointer { get; private set; }    // C++ default 1

        internal bool ForceNoDirty { get; private set; }   // C++ default 1

        private readonly bool _ownsWindowAndRenderer;

        // C++ uses DisplayMode in DexterGFX. The user stated DisplayMode is in DexterOS in the project.
        // Therefore, it is read via a callback instead of storing it here.
        internal delegate byte GetDisplayModeCallback();
        private readonly GetDisplayModeCallback _getDisplayMode;

        // --------------------------------------------------------------------
        // SDL state (mirrors the C++ file's members)
        // --------------------------------------------------------------------

        private readonly SdlDisplayBackend _backend;

        // Used for setting title (C++: _SDL_SetWindowTitle(_sdlWindow, &DexterOS::AppTitle))
        internal delegate string GetWindowTitleCallback();
        private readonly GetWindowTitleCallback _getWindowTitle;

        // App activity check (C++: DexterOS::AppActive) – if not available, default true.
        internal delegate bool IsAppActiveCallback();
        private readonly IsAppActiveCallback _isAppActive;

        // Screen size hint: in C++ OSInitDisplay reads ScreenWidth/Height (desktop).
        // Here: injected to avoid hard dependency on project-specific gfx screen types.
        internal delegate (int width, int height) GetDesktopSizeCallback();
        private readonly GetDesktopSizeCallback _getDesktopSize;

        // --------------------------------------------------------------------
        // Chunkies
        // --------------------------------------------------------------------

        private readonly ChunkyObject[] _chunkies;
        private ChunkyObject _activeChunky;
        private int _activeChunkyIndex;

        // --------------------------------------------------------------------
        // Palette / tables (C++: PalTable/PalLookup etc.)
        // --------------------------------------------------------------------

        private readonly byte[] _paletteR; // 256
        private readonly byte[] _paletteG; // 256
        private readonly byte[] _paletteB; // 256

        // 8-bit index -> 16-bit pixel lookup (565 or 555 depending on pixel format)
        private readonly ushort[] _palLookup16; // 256

        // Update tracking (C++: PalLookupSize)
        private bool _paletteTablesValid;

        // Pixel format selection (C++: SystemPixelFormat/InternalPixelFormat etc.)
        internal enum PixelFormat16
        {
            Rgb565 = 0,
            Rgb555 = 1
        }

        private PixelFormat16 _pixelFormat16;

        // --------------------------------------------------------------------
        // Dirty buffer (C++: DirtyList)
        // --------------------------------------------------------------------

        private byte[]? _dirtyPrev;
        private int _dirtyPrevWidth;
        private int _dirtyPrevHeight;
        private int _dirtyPrevBpp; // 1 for 8bpp input (chunky), 2 for 16-bit destination effective comparisons

        // --------------------------------------------------------------------
        // Construction
        // --------------------------------------------------------------------

        internal DexterGFX(int maxChunkies, int maxFonts, GetDisplayModeCallback getDisplayMode, GetWindowTitleCallback getWindowTitle, GetDesktopSizeCallback getDesktopSize, IsAppActiveCallback? isAppActive = null, bool ownsSdlWindow = true)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChunkies);
            ArgumentOutOfRangeException.ThrowIfNegative(maxFonts);

            _getDisplayMode = getDisplayMode ?? throw new ArgumentNullException(nameof(getDisplayMode));
            _getWindowTitle = getWindowTitle ?? throw new ArgumentNullException(nameof(getWindowTitle));
            _getDesktopSize = getDesktopSize ?? throw new ArgumentNullException(nameof(getDesktopSize));
            _isAppActive = isAppActive ?? (() => true);

            // Backend (the only unsafe part lives there).
            _backend = new SdlDisplayBackend();

            // If keeping this parameter for now, store it so OSInitDisplay() can use it.
            // If desired, this can be removed and hardcoded in OSInitDisplay instead.
            _ownsWindowAndRenderer = ownsSdlWindow;

            // C++ ctor defaults:
            GFXActiveState = false;
            DisplayOpenBefore = false;
            ShowPointer = true;

            RenderWidth = 0x140;
            RenderHeight = 0x0F0;
            RenderBitDepth = 8;
            RenderByteDepth = 1;

            ScreenLocked = false;

            WindowWidth = 0x140;
            WindowHeight = 0x0F0;

            ForceNoDirty = true;

            _chunkies = new ChunkyObject[maxChunkies];
            for (int i = 0; i < _chunkies.Length; i++)
            {
                _chunkies[i] = new ChunkyObject();
            }

            _activeChunkyIndex = 0;
            _activeChunky = _chunkies[0];

            _paletteR = new byte[256];
            _paletteG = new byte[256];
            _paletteB = new byte[256];
            _palLookup16 = new ushort[256];

            _pixelFormat16 = PixelFormat16.Rgb565;
            _paletteTablesValid = false;

            InitColour();
        }

        public void Dispose()
        {
            GFXShutDown();
            GC.SuppressFinalize(this);
        }

        ~DexterGFX()
        {
            try
            {
                GFXShutDown();
            }
            catch
            {
                // Finalizer must not throw.
            }
        }

        // --------------------------------------------------------------------
        // C++: DexterGFX::InitColour()
        // --------------------------------------------------------------------
        private void InitColour()
        {
            // The original initializes palette tables and pixel format defaults.
            // Keep a sane default: 16-bit 565.
            SetPixelFormat(PixelFormat16.Rgb565);
        }

        // --------------------------------------------------------------------
        // Pixel format selection (C++: SetPixelFormat variants)
        // --------------------------------------------------------------------

        internal void SetPixelFormat(byte internalPixelFormat)
        {
            // In the C++ decompile, the byte variant routes through switch cases.
            // Here: accept only "565 vs 555" style values used by the old code path.
            // If unknown, default to 565.
            if (internalPixelFormat == 1)
            {
                SetPixelFormat(PixelFormat16.Rgb555);
                return;
            }

            SetPixelFormat(PixelFormat16.Rgb565);
        }

        internal void SetPixelFormat(ushort redBits, ushort greenBits, ushort blueBits, ushort bytesPerPixel)
        {
            // C++ computes bitcounts from SDL surface masks and picks format.
            // Simplify: detect 565 vs 555 via bit counts.
            _ = bytesPerPixel;

            if (redBits == 5 && greenBits == 5 && blueBits == 5)
            {
                SetPixelFormat(PixelFormat16.Rgb555);
                return;
            }

            SetPixelFormat(PixelFormat16.Rgb565);
        }

        private void SetPixelFormat(PixelFormat16 fmt)
        {
            _pixelFormat16 = fmt;
            _paletteTablesValid = false;
        }

        // --------------------------------------------------------------------
        // C++: DexterGFX::UpdatePaletteTables(unsigned char*)
        // - param ignored in the decompile for most cases; it rebuilds lookup tables.
        // --------------------------------------------------------------------
        internal void UpdatePaletteTables(byte[]? paletteBytesRgbTriplesOrNull)
        {
            if (paletteBytesRgbTriplesOrNull != null)
            {
                // Expect 256 * 3 layout: R,G,B per index (common).
                if (paletteBytesRgbTriplesOrNull.Length >= 256 * 3)
                {
                    int src = 0;
                    for (int i = 0; i < 256; i++)
                    {
                        _paletteR[i] = paletteBytesRgbTriplesOrNull[src + 0];
                        _paletteG[i] = paletteBytesRgbTriplesOrNull[src + 1];
                        _paletteB[i] = paletteBytesRgbTriplesOrNull[src + 2];
                        src += 3;
                    }
                }
            }

            for (int i = 0; i < 256; i++)
            {
                _palLookup16[i] = MakePixel16(_paletteR[i], _paletteG[i], _paletteB[i]);
            }

            _paletteTablesValid = true;
        }

        private ushort MakePixel16(byte r, byte g, byte b)
        {
            if (_pixelFormat16 == PixelFormat16.Rgb555)
            {
                // 5/5/5 (0RRRRRGGGGGBBBBB)
                int rr = r >> 3;
                int gg = g >> 3;
                int bb = b >> 3;
                return (ushort)((rr << 10) | (gg << 5) | bb);
            }
            else
            {
                // 5/6/5
                int rr = r >> 3;
                int gg = g >> 2;
                int bb = b >> 3;
                return (ushort)((rr << 11) | (gg << 5) | bb);
            }
        }

        // --------------------------------------------------------------------
        // C++: DexterGFX::ShowPalette(bool)
        // --------------------------------------------------------------------
        internal void ShowPalette(bool force)
        {
            // In the cxx, this updates palette tables and calls OSshowpalette (empty there).
            // Keep behavior: rebuild tables if needed.
            if (force || !_paletteTablesValid)
            {
                UpdatePaletteTables(null);
            }

            OSshowpalette();
        }

        // C++: DexterGFX::OSshowpalette()
        private void OSshowpalette()
        {
            // The decompiled cxx function is empty.
        }

        // --------------------------------------------------------------------
        // Chunky management
        // --------------------------------------------------------------------

        // C++: InitChunky(unsigned int, int, int, int)
        internal bool InitChunky(uint chunkyIndex, uint width, uint height, uint bitDepth)
        {
            if (chunkyIndex >= (uint)_chunkies.Length)
            {
                return false;
            }

            ChunkyObject chunky = _chunkies[(int)chunkyIndex];
            bool ok = chunky.Init(width, height, (byte)bitDepth);
            return ok;
        }

        // C++: UseChunky(unsigned int)
        internal void UseChunky(uint chunkyIndex)
        {
            if (chunkyIndex >= (uint)_chunkies.Length)
            {
                return;
            }

            _activeChunkyIndex = (int)chunkyIndex;
            _activeChunky = _chunkies[_activeChunkyIndex];
        }

        // C++: FreeAllChunkies()
        internal void FreeAllChunkies()
        {
            for (int i = 0; i < _chunkies.Length; i++)
            {
                _chunkies[i].Dispose();
                _chunkies[i] = new ChunkyObject();
            }

            _activeChunkyIndex = 0;
            _activeChunky = _chunkies[0];
        }

        // --------------------------------------------------------------------
        // ClearChunky(unsigned int, DexCol const&, unsigned short)
        // In the original, DexCol carries RGB and flags; it clears active chunky memory.
        // This port keeps a pragmatic signature used by the original call sites:
        // - If render is 8bpp, fill with palette index (value8).
        // - If render is 16bpp, fill with 16-bit pixel value.
        // --------------------------------------------------------------------

        internal void ClearChunky(uint chunkyIndex, DexCol colour, ushort value)
        {
            if (chunkyIndex >= (uint)_chunkies.Length)
            {
                return;
            }

            ChunkyObject chunky = _chunkies[(int)chunkyIndex];
            byte[]? pixels = chunky.PixelData;
            if (pixels == null)
            {
                return;
            }

            if (chunky.BitDepth == 8)
            {
                byte fill = (byte)(value & 0xFF);
                pixels.AsSpan().Fill(fill);
                return;
            }

            if (chunky.BitDepth == 16)
            {
                // Fill 16-bit little-endian
                Span<byte> span = pixels.AsSpan();
                byte lo = (byte)(value & 0xFF);
                byte hi = (byte)(value >> 8);
                for (int i = 0; i + 1 < span.Length; i += 2)
                {
                    span[i + 0] = lo;
                    span[i + 1] = hi;
                }

                return;
            }

            // Fallback: use colour RGB if possible
            ushort px = MakePixel16(colour.R, colour.G, colour.B);
            Span<byte> spanFallback = pixels.AsSpan();
            byte lo2 = (byte)(px & 0xFF);
            byte hi2 = (byte)(px >> 8);
            for (int i = 0; i + 1 < spanFallback.Length; i += 2)
            {
                spanFallback[i + 0] = lo2;
                spanFallback[i + 1] = hi2;
            }
        }

        // Minimal managed replacement for COLOUR_ZERO usage in the cxx.
        internal readonly struct DexCol
        {
            internal readonly byte R;
            internal readonly byte G;
            internal readonly byte B;

            internal DexCol(byte r, byte g, byte b)
            {
                R = r;
                G = g;
                B = b;
            }

            internal static DexCol Zero => new(0, 0, 0);
        }

        // --------------------------------------------------------------------
        // Dirty list (C++: InitDirtyList())
        // This port uses a simple previous-frame buffer matching the active chunky input.
        // --------------------------------------------------------------------

        internal void InitDirtyList()
        {
            if (ForceNoDirty)
            {
                _dirtyPrev = null;
                _dirtyPrevWidth = 0;
                _dirtyPrevHeight = 0;
                _dirtyPrevBpp = 0;
                return;
            }

            byte[]? src = _activeChunky.PixelData;
            if (src == null)
            {
                _dirtyPrev = null;
                _dirtyPrevWidth = 0;
                _dirtyPrevHeight = 0;
                _dirtyPrevBpp = 0;
                return;
            }

            int w = _activeChunky.Width;
            int h = _activeChunky.Height;
            int bpp = _activeChunky.BitDepth == 16 ? 2 : 1;

            int bytes = checked(w * h * bpp);

            if (_dirtyPrev == null || _dirtyPrev.Length != bytes)
            {
                _dirtyPrev = new byte[bytes];
            }

            _dirtyPrevWidth = w;
            _dirtyPrevHeight = h;
            _dirtyPrevBpp = bpp;

            // Initialize as "unknown" to force updates on first redraw.
            _dirtyPrev.AsSpan().Fill(0xCD);
        }

        // --------------------------------------------------------------------
        // C++: SetRenderDepth(unsigned char, unsigned char)
        // param_2 bit3 indicates ScreenLocked in decompile.
        // --------------------------------------------------------------------

        internal void SetRenderDepth(byte renderBitDepth, byte flags)
        {
            ScreenLocked = ((flags >> 3) & 1) != 0;

            RenderBitDepth = renderBitDepth;
            RenderByteDepth = (byte)((renderBitDepth + 7) >> 3);

            // Palette tables depend on pixel format and must exist for 8->16 conversion.
            UpdatePaletteTables(null);

            // Ensure chunky 0 matches render dimensions and depth (like cxx).
            ChunkyObject chunky0 = _chunkies[0];
            bool needsReinit = true;

            if (chunky0.PixelData != null)
            {
                if (chunky0.Width == RenderWidth && chunky0.Height == RenderHeight && chunky0.BitDepth == RenderBitDepth)
                {
                    needsReinit = false;
                }
            }

            if (needsReinit)
            {
                InitChunky(0, (uint)RenderWidth, (uint)RenderHeight, RenderBitDepth);
            }

            UseChunky(0);

            // Reset dirty list to match new surface.
            InitDirtyList();
        }

        // --------------------------------------------------------------------
        // C++: GFXActive()
        // --------------------------------------------------------------------
        internal bool GFXActive()
        {
            return GFXActiveState;
        }

        // --------------------------------------------------------------------
        // C++: SetAppRefresh(unsigned short) – not materially used in the provided cxx
        // Keep stub for compatibility if other modules call it.
        // --------------------------------------------------------------------
        internal void SetAppRefresh(ushort refresh)
        {
            _ = refresh;
        }

        // --------------------------------------------------------------------
        // C++: SetScreenMode(unsigned short, unsigned short, unsigned char, unsigned char)
        // In cxx this also toggles fullscreen depending on DisplayMode.
        // Here: updates render sizing and reinitializes.
        // --------------------------------------------------------------------
        internal void SetScreenMode(ushort width, ushort height, byte bitDepth, byte flags)
        {
            RenderWidth = width & unchecked(0xFFFC);
            RenderHeight = height;

            WindowWidth = RenderWidth;
            WindowHeight = RenderHeight;

            SetRenderDepth(bitDepth, flags);

            if (DisplayOpenBefore)
            {
                OSInitDisplay();
            }
        }

        // --------------------------------------------------------------------
        // Mouse pointer visibility
        // --------------------------------------------------------------------
        internal void HideMousePointer()
        {
            ShowPointer = false;
            _backend?.SetShowCursor(false);
        }

        internal void ShowMousePointer()
        {
            ShowPointer = true;
            _backend?.SetShowCursor(true);
        }

        // --------------------------------------------------------------------
        // C++: ReleaseDisplay() / CloseDisplay()
        // In the provided cxx both are empty; in a managed port, they should free SDL resources.
        // --------------------------------------------------------------------

        internal void ReleaseDisplay()
        {
            // Keep separate for fidelity; actual cleanup is in CloseDisplay.
        }

        internal void CloseDisplay()
        {
            _backend.Shutdown();
        }

        // --------------------------------------------------------------------
        // C++: GFXOSInit() (always returns 1 in the cxx)
        // --------------------------------------------------------------------
        internal bool GFXOSInit()
        {
            return true;
        }

        // --------------------------------------------------------------------
        // C++: OSInitDisplay()
        // Mirrors the C++ SDL path but keeps it minimal and robust.
        // --------------------------------------------------------------------
        internal bool OSInitDisplay()
        {
            // Render size adjustments like the original cxx:
            if (!DisplayOpenBefore)
            {
                (int desktopW, int desktopH) = _getDesktopSize();

                if (desktopW > 0 && desktopH > 0)
                {
                    if (desktopW < RenderWidth || desktopH < RenderHeight)
                    {
                        RenderWidth = desktopW & unchecked(0xFFFC);
                        RenderHeight = desktopH;

                        WindowWidth = RenderWidth;
                        WindowHeight = RenderHeight;
                    }
                }
            }

            byte displayMode = _getDisplayMode();

            // C++ behavior:
            // - DisplayMode == 2 => fullscreen desktop
            // - DisplayMode == 1 => "windowed tweak" on height if enough space above/below
            bool fullscreenDesktop = displayMode == 2;

            int windowW = RenderWidth;
            int windowH = RenderHeight;

            if (displayMode == 1)
            {
                (int desktopW, int desktopH) = _getDesktopSize();

                // In the earlier draft we mirrored the cxx: if desktop has enough headroom, shrink the window height a bit.
                // The cxx checks (desktopH - RenderHeight) > 0x31 and then subtracts 0x32.
                if (desktopH > 0 && (desktopH - RenderHeight) > 0x31)
                {
                    windowH = RenderHeight - 0x32;
                }
            }

            // Keep WindowWidth/Height in sync with the actual window size we create.
            WindowWidth = windowW;
            WindowHeight = windowH;

            // If already open, re-init in-place (backend will recreate surface/texture).
            // If not open, create window+renderer and set up surfaces.
            bool ok = _backend.InitWindowAndRenderer(_getWindowTitle(), windowW, windowH, RenderWidth, RenderHeight, fullscreenDesktop, ShowPointer, ownsWindowAndRenderer: _ownsWindowAndRenderer);

            if (!ok)
            {
                return false;
            }

            // If the display was already open before, the old cxx sets ForceNoDirty to true.
            // Keep that behavior so the first redraw after mode switch is full.
            if (DisplayOpenBefore)
            {
                ForceNoDirty = true;
            }

            DisplayOpenBefore = true;
            return true;
        }

        // --------------------------------------------------------------------
        // C++: GFXInit()
        // --------------------------------------------------------------------
        internal bool GFXInit()
        {
            // Allocate/prepare chunky 0 etc. (C++ does more with Font array; fonts live elsewhere in this port).
            if (!GFXOSInit())
            {
                return false;
            }

            if (!OSInitDisplay())
            {
                return false;
            }

            DisplayOpenBefore = true;
            GFXActiveState = true;

            SetRenderDepth(RenderBitDepth, 0x10); // matches cxx call SetRenderDepth(RenderBitDepth,'\x10')
            ShowPalette(true);
            ClearChunky(0, DexCol.Zero, 0);
            RedrawDisplay();

            return true;
        }

        // --------------------------------------------------------------------
        // C++: InitDisplay()
        // --------------------------------------------------------------------
        internal bool InitDisplay()
        {
            bool ok = OSInitDisplay();
            if (!ok)
            {
                return false;
            }

            DisplayOpenBefore = true;
            GFXActiveState = true;

            SetRenderDepth(RenderBitDepth, 0x10);
            ShowPalette(true);
            ClearChunky(0, DexCol.Zero, 0);
            RedrawDisplay();

            return true;
        }

        // --------------------------------------------------------------------
        // C++: GFXShutDown()
        // --------------------------------------------------------------------
        internal void GFXShutDown()
        {
            // Make idempotent: shutdown can be called multiple times.
            GFXActiveState = false;

            // In the original cxx: Pause(200), CloseDisplay(), ReleaseDisplay(), free DirtyList, Fonts, Chunkies...
            // Managed port: rely on backend shutdown + managed cleanup.

            CloseDisplay();
            ReleaseDisplay();

            _dirtyPrev = null;
            _dirtyPrevWidth = 0;
            _dirtyPrevHeight = 0;
            _dirtyPrevBpp = 0;

            FreeAllChunkies();

            DisplayOpenBefore = false;
        }

        // --------------------------------------------------------------------
        // C++: DumpChunkyBuffer(...) and variants
        // This port only supports:
        // - active chunky 8bpp -> destination 16bpp surface buffer (little-endian)
        // - active chunky 16bpp -> destination 16bpp surface buffer (little-endian)
        // and uses dirty compare if enabled.
        // --------------------------------------------------------------------

        private void DumpChunkyBufferTo16(ChunkyObject chunky, Span<byte> dest, int destPitchBytes)
        {
            byte[]? srcArr = chunky.PixelData;
            if (srcArr == null)
            {
                return;
            }

            ReadOnlySpan<byte> src = srcArr.AsSpan();
            int w = chunky.Width;
            int h = chunky.Height;

            if (w <= 0 || h <= 0)
            {
                return;
            }

            if (!_paletteTablesValid)
            {
                UpdatePaletteTables(null);
            }

            bool useDirty = !ForceNoDirty && _dirtyPrev != null && _dirtyPrevWidth == w && _dirtyPrevHeight == h && _dirtyPrevBpp == (chunky.BitDepth == 16 ? 2 : 1);

            if (chunky.BitDepth == 16)
            {
                // Copy line by line (optionally dirty compare on 2-byte pixels)
                int srcPitch = w * 2;

                for (int y = 0; y < h; y++)
                {
                    ReadOnlySpan<byte> srcLine = src.Slice(y * srcPitch, srcPitch);
                    Span<byte> dstLine = dest.Slice(y * destPitchBytes, srcPitch);

                    if (!useDirty)
                    {
                        srcLine.CopyTo(dstLine);
                        continue;
                    }

                    Span<byte> prevLine = _dirtyPrev!.AsSpan().Slice(y * srcPitch, srcPitch);

                    for (int i = 0; i < srcPitch; i++)
                    {
                        byte v = srcLine[i];
                        if (prevLine[i] != v)
                        {
                            prevLine[i] = v;
                            dstLine[i] = v;
                        }
                    }
                }

                return;
            }

            if (chunky.BitDepth == 8)
            {
                int srcPitch = w;

                for (int y = 0; y < h; y++)
                {
                    ReadOnlySpan<byte> srcLine = src.Slice(y * srcPitch, srcPitch);
                    Span<byte> dstLine = dest.Slice(y * destPitchBytes, w * 2);

                    Span<byte> prevLine = default;
                    if (useDirty)
                    {
                        prevLine = _dirtyPrev!.AsSpan().Slice(y * srcPitch, srcPitch);
                    }

                    int di = 0;
                    for (int x = 0; x < w; x++)
                    {
                        byte idx = srcLine[x];

                        if (useDirty && prevLine[x] == idx)
                        {
                            di += 2;
                            continue;
                        }

                        if (useDirty)
                        {
                            prevLine[x] = idx;
                        }

                        ushort px = _palLookup16[idx];
                        dstLine[di + 0] = (byte)(px & 0xFF);
                        dstLine[di + 1] = (byte)(px >> 8);
                        di += 2;
                    }
                }

                return;
            }

            // Other bit depths are not supported in this port.
        }

        // C++: DumpChunkyBuffer(ChunkyObject const&, unsigned int*, int)
        private void DumpChunkyBuffer(ChunkyObject chunky, Span<byte> dest, int destPitchBytes)
        {
            // The cxx uses SystemBitDepth and RenderBitDepth combos.
            // Here: always render into a 16-bit surface/texture.
            DumpChunkyBufferTo16(chunky, dest, destPitchBytes);
        }

        // --------------------------------------------------------------------
        // C++: RedrawDisplay()
        // --------------------------------------------------------------------
        internal void RedrawDisplay()
        {
            if (!GFXActive())
            {
                return;
            }

            if (!_isAppActive())
            {
                return;
            }

            if (!_backend.IsInitialized)
            {
                return;
            }

            if (!_backend.LockSurface(out Span<byte> pixels, out int pitch))
            {
                return;
            }

            try
            {
                DumpChunkyBuffer(_activeChunky, pixels, pitch);
            }
            finally
            {
                _backend.UnlockSurface();
            }

            _backend.PresentSurface();
        }
    }
}