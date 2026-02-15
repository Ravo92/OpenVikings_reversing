using OpenVikings.Dexter.Struct;

namespace OpenVikings.Dexter
{
    internal enum PixelFormat16
    {
        Rgb565 = 0,
        Rgb555 = 1
    }

    internal sealed class DexterGFX
    {
        private readonly ChunkySurface[]? _chunkies;
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

        // --------------------------------------------------------------------
        // Fields added for GFX5 (palette backup, brightness, close table, fast blend)
        // --------------------------------------------------------------------

        private byte[]? _backupPaletteR;
        private byte[]? _backupPaletteG;
        private byte[]? _backupPaletteB;

        // Close-table (32*32*32) for fast nearest palette mapping.
        private byte[]? _closeTable8; // 0x8000

        // C++ "Brightness" multiplier used when writing palette entries.
        private float _paletteBrightness = 1.0f;

        // C++: TransparentMask16 / FastBlendMode / FastBlendPixel / DestMod / SourceMod / Tint*
        private ushort _transparentMask16 = 0x7BEF; // default 565 half-mask
        private bool _fastBlendMode;
        private ushort _fastBlendPixel;
        private ushort _destMod;   // 0..255
        private ushort _sourceMod; // 0..255

        private ushort _tintRed;   // (byte * SourceMod)
        private ushort _tintGreen; // (byte * SourceMod)
        private ushort _tintBlue;  // (byte * SourceMod)

        private bool _gfxActiveState;
        private bool _displayOpenBefore;

        private bool _showPointer = true;

        private ushort _renderWidth = 0x140;
        private ushort _renderHeight = 0xF0;

        private byte _renderBitDepth = 8;
        private byte _renderByteDepth = 1;

        private bool _screenLocked;

        private ushort _windowWidth = 0x140;
        private ushort _windowHeight = 0xF0;

        private short _windowX;
        private short _windowY;

        private byte _displayMode = 1;

        private int[]? _xScaleTable;
        private int _xScaleTableSize;

        private int[]? _yScaleTable;
        private int _yScaleTableSize;

        private bool _forceNoDirty = true;
        private byte[]? _dirtyList;

        private uint _maxFonts;
        private FontSlot[]? _fonts;
        private uint _usedFont;

        private uint _callBackTime;
        private short _fpsLimitMode;

        private byte _maxDepthDxp = 0x18;

        // DexterGFX::SetMaxDepthDXP(unsigned char)
        internal void SetMaxDepthDXP(byte maxDepth)
        {
            // Original clamps to [9..24]
            if (maxDepth < 9) maxDepth = 9;
            if (maxDepth > 0x18) maxDepth = 0x18;
            _maxDepthDxp = maxDepth;
        }

        // These are referenced by other chunks; keep as properties for consistency.
        internal ushort RenderWidth => _renderWidth;
        internal ushort RenderHeight => _renderHeight;
        internal byte RenderBitDepth => _renderBitDepth;
        internal byte RenderByteDepth => _renderByteDepth;

        internal ushort WindowWidth => _windowWidth;
        internal ushort WindowHeight => _windowHeight;

        internal bool GfxActiveState => _gfxActiveState;
        internal bool DisplayOpenBefore => _displayOpenBefore;

        internal bool ShowPointer => _showPointer;

        private int _lastXScale;
        private int _lastYScale;

        private float _lastXRatio;
        private float _lastYRatio;

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

        // --------------------------------------------------------------------
        // DexterGFX::InitDisplay()
        // --------------------------------------------------------------------
        internal bool InitDisplay()
        {
            bool ok = OSInitDisplay();
            if (!ok)
            {
                return false;
            }

            PostDisplayOpenInit();
            return true;
        }

        // --------------------------------------------------------------------
        // DexterGFX::GFXInit()
        // --------------------------------------------------------------------
        internal bool GFXInit()
        {
            EnsureFontsAllocatedForGfxInit();

            if (!GFXOSInit())
            {
                return false;
            }

            if (!OSInitDisplay())
            {
                return false;
            }

            PostDisplayOpenInit();
            return true;
        }

        private void PostDisplayOpenInit()
        {
            _displayOpenBefore = true;
            _gfxActiveState = true;

            SetRenderDepth(_renderBitDepth, 0x10);
            ShowPalette(true);
            ClearChunky(0, in COLOUR_ZERO, 0);
            RedrawDisplay();
        }

        private void EnsureFontsAllocatedForGfxInit()
        {
            // Mirrors the C++ behavior:
            // If MaxFonts != 0, clamp to MaxChunkies >> 8, allocate Font slots, and reset UsedFont.
            if (_maxFonts == 0)
            {
                return;
            }

            uint maxChunkies = (uint)_chunkies.Length;
            uint cap = maxChunkies >> 8;
            if (_maxFonts > cap)
            {
                _maxFonts = cap;
            }

            if (_maxFonts == 0)
            {
                _fonts = null;
                _usedFont = 0;
                return;
            }

            if (_fonts == null || (uint)_fonts.Length != _maxFonts)
            {
                _fonts = new FontSlot[_maxFonts];
                for (int i = 0; i < _fonts.Length; i++)
                {
                    _fonts[i] = new FontSlot();
                }
            }
            else
            {
                // Ensure clean reset even if the array already exists.
                for (int i = 0; i < _fonts.Length; i++)
                {
                    _fonts[i] = new FontSlot();
                }
            }

            _usedFont = 0;
        }

        // --------------------------------------------------------------------
        // DexterGFX::GFXActivate()
        // --------------------------------------------------------------------
        internal void GFXActivate()
        {
            _gfxActiveState = true;
        }

        // --------------------------------------------------------------------
        // DexterGFX::GFXDeActivate()
        // --------------------------------------------------------------------
        internal void GFXDeActivate()
        {
            _gfxActiveState = false;

            // Original sleeps 200ms to let the OS settle.
            // Keep the pause at OS layer if needed (DexterOS::Pause); here it is omitted intentionally.
        }

        // --------------------------------------------------------------------
        // DexterGFX::GFXActive()
        // --------------------------------------------------------------------
        internal bool GFXActive()
        {
            return _gfxActiveState;
        }

        // --------------------------------------------------------------------
        // DexterGFX::SetRenderDepth(unsigned char, unsigned char)
        // param_2: bit3 controls ScreenLocked (param_2 >> 3 & 1)
        // --------------------------------------------------------------------
        internal void SetRenderDepth(byte renderBitDepth, byte flags)
        {
            _screenLocked = ((flags >> 3) & 1) != 0;

            _renderBitDepth = renderBitDepth;
            _renderByteDepth = (byte)(renderBitDepth >> 3);

            UpdatePaletteTables();

            // Ensure chunky 0 matches the current render size/depth.
            ChunkySurface? c0 = _chunkies.Length > 0 ? _chunkies[0] : null;
            bool ok =
                c0 != null &&
                c0.Width == _renderWidth &&
                c0.Height == _renderHeight &&
                c0.BitsPerPixel == _renderBitDepth;

            if (!ok)
            {
                InitChunky(0, _renderWidth, _renderHeight, _renderBitDepth);
            }

            UseChunky(0);
            InitDirtyList();
        }

        // --------------------------------------------------------------------
        // DexterGFX::GFXShutDown()
        // --------------------------------------------------------------------
        internal void GFXShutDown()
        {
            FreeXScaleTable();
            FreeYScaleTable();

            _gfxActiveState = false;

            // Original: Pause(200), CloseDisplay, ReleaseDisplay
            CloseDisplay();
            ReleaseDisplay();

            FreeDirtyList();

            if (_fonts != null)
            {
                _fonts = null;
            }

            FreeAllChunkies();
        }

        // --------------------------------------------------------------------
        // DexterGFX::FreeXScaleTable()
        // --------------------------------------------------------------------
        internal void FreeXScaleTable()
        {
            _xScaleTable = null;
            _xScaleTableSize = 0;
        }

        // --------------------------------------------------------------------
        // DexterGFX::FreeYScaleTable()
        // --------------------------------------------------------------------
        internal void FreeYScaleTable()
        {
            _yScaleTable = null;
            _yScaleTableSize = 0;
        }

        // --------------------------------------------------------------------
        // DexterGFX::FreeDirtyList()
        // --------------------------------------------------------------------
        internal void FreeDirtyList()
        {
            _dirtyList = null;
        }

        // --------------------------------------------------------------------
        // DexterGFX::InitDirtyList()
        // Original benchmarks dirty tracking vs full redraw.
        // Managed deterministic heuristic:
        // - If the backbuffer is large, disable dirty tracking (ForceNoDirty=true).
        // - Else allocate a dirty buffer the size of the render buffer (bytes).
        // --------------------------------------------------------------------
        internal void InitDirtyList()
        {
            FreeDirtyList();

            int bytes = checked(_renderWidth * _renderHeight * _renderByteDepth);

            // Heuristic threshold: above ~768 KB prefer full redraw.
            if (bytes >= 768 * 1024)
            {
                _forceNoDirty = true;
                return;
            }

            _dirtyList = new byte[bytes];
            _forceNoDirty = false;

            // In the original, the dirty list is initialized by toggling bytes and forcing redraws.
            // Here: deterministic state "everything dirty" is represented by 0xFF.
            Array.Fill(_dirtyList, (byte)0xFF);
        }

        // --------------------------------------------------------------------
        // DexterGFX::SetAppRefresh(unsigned short)
        // --------------------------------------------------------------------
        internal void SetAppRefresh(ushort refresh)
        {
            ushort cbTime;
            short mode;

            if (refresh == 0)
            {
                mode = 2;
                cbTime = 2000;
            }
            else if (refresh == 0x7777 || refresh == 0x6666)
            {
                mode = 0;
                cbTime = 0;
            }
            else
            {
                // 0x3b6 / refresh  (950 / hz) in the decompile
                cbTime = (ushort)(0x3B6 / refresh);
                mode = 1;
            }

            _callBackTime = cbTime;
            _fpsLimitMode = mode;
        }

        // --------------------------------------------------------------------
        // DexterGFX::SetScreenMode(unsigned short, unsigned short, unsigned char, unsigned char)
        // param_4: if non-zero it may change DisplayMode.
        // Returns success.
        // --------------------------------------------------------------------
        internal uint SetScreenMode(ushort width, ushort height, byte bitDepth, byte displayMode)
        {
            if (!_displayOpenBefore)
            {
                // Original returns 0 when display was never opened.
                return 0;
            }

            bool needChange =
                _renderWidth != width ||
                _renderHeight != height ||
                _renderBitDepth != bitDepth ||
                (displayMode != 0 && _displayMode != displayMode);

            if (!needChange)
            {
                return 1;
            }

            // Save current chunky0 image if it exists.
            byte[]? backup8 = null;
            ushort[]? backup16 = null;
            int backupW = 0;
            int backupH = 0;
            int backupBpp = 0;

            ChunkySurface? c0 = _chunkies.Length > 0 ? _chunkies[0] : null;
            if (c0 == null)
            {
                return 0;
            }

            backupW = c0.Width;
            backupH = c0.Height;
            backupBpp = c0.BitsPerPixel;

            if (backupW > 0 && backupH > 0)
            {
                if (backupBpp == 8)
                {
                    ReadOnlySpan<byte> s = c0.Pixels8;
                    backup8 = s.ToArray();
                }
                else if (backupBpp == 16)
                {
                    ReadOnlySpan<ushort> s = c0.Pixels16;
                    backup16 = s.ToArray();
                }
            }

            if (_gfxActiveState)
            {
                _gfxActiveState = false;
                CloseDisplay();
            }

            byte newMode = _displayMode;
            if (displayMode != 0)
            {
                newMode = displayMode == 4 ? (byte)(2 - (_displayMode == 2 ? 1 : 0)) : displayMode;
            }

            _displayMode = newMode;

            _renderWidth = width;
            _renderHeight = height;
            _renderBitDepth = bitDepth;
            _renderByteDepth = (byte)(bitDepth >> 3);

            _windowWidth = width;
            _windowHeight = height;

            uint ok = OSInitDisplay() ? 1u : 0u;
            if (ok != 0)
            {
                _displayOpenBefore = true;
                _gfxActiveState = true;

                SetRenderDepth(_renderBitDepth, 0x10);
                ShowPalette(true);
                ClearChunky(0, in COLOUR_ZERO, 0);
                RedrawDisplay();

                // Restore backup if the surface size stayed identical.
                ChunkySurface? nc0 = _chunkies.Length > 0 ? _chunkies[0] : null;
                if (nc0 != null && nc0.Width == backupW && nc0.Height == backupH && nc0.BitsPerPixel == backupBpp)
                {
                    if (backupBpp == 8 && backup8 != null)
                    {
                        backup8.AsSpan().CopyTo(nc0.Pixels8);
                    }
                    else if (backupBpp == 16 && backup16 != null)
                    {
                        backup16.AsSpan().CopyTo(nc0.Pixels16);
                    }
                }
            }

            return ok;
        }

        // --------------------------------------------------------------------
        // DexterGFX::SetDisplayMode(unsigned char)
        // Original sets global flags (OpenGL, Display2D). In this managed port only DisplayMode is tracked.
        // --------------------------------------------------------------------
        internal void SetDisplayMode(byte mode)
        {
            // Keep the raw mode for later OS/display layer handling (GFX10).
            _displayMode = mode;
        }

        // --------------------------------------------------------------------
        // DexterGFX::SetMaxFonts(unsigned int)
        // Must be called before fonts are allocated.
        // --------------------------------------------------------------------
        internal void SetMaxFonts(uint maxFonts)
        {
            if (_fonts != null)
            {
                return;
            }

            _maxFonts = maxFonts;
        }

        // --------------------------------------------------------------------
        // DexterGFX::HideMousePointer()
        // --------------------------------------------------------------------
        internal void HideMousePointer()
        {
            _showPointer = false;
        }

        // --------------------------------------------------------------------
        // DexterGFX::ShowMousePointer()
        // --------------------------------------------------------------------
        internal void ShowMousePointer()
        {
            _showPointer = true;
        }

        // --------------------------------------------------------------------
        // DexterGFX::GrabFont(...)
        // Builds a font from a grid inside a chunky (glyphs become chunkies from the top of the pool).
        // --------------------------------------------------------------------
        internal void GrabFont(uint chunkyId, short srcX, short srcY, short glyphW, short glyphH, uint columns, byte firstChar, byte lastChar, byte flags, ushort featherStrength, DexCol featherTint)
        {
            if (_fonts == null) return;
            if (!ValidChunky(chunkyId)) return;

            uint fontIndex = _usedFont;
            if (fontIndex >= (uint)_fonts.Length) return;

            FontSlot slot = _fonts[fontIndex];

            // Clear previous slot
            slot.Active = false;

            slot.CharCount = (ushort)((lastChar - firstChar) + 1);
            slot.FirstChar = firstChar;
            slot.FixedAdvance = (short)(glyphW - 2);
            slot.GlyphHeight = glyphH;

            // C++: ChunkyBaseId = MaxChunkies - 256*(UsedFont+1)
            uint maxChunkies = (uint)_chunkies.Length;
            slot.ChunkyBaseId = unchecked((int)(maxChunkies - ((fontIndex + 1) << 8)));

            byte oldTarget = TargetChunkyID;

            if (slot.CharCount != 0)
            {
                UseChunky((int)chunkyId);

                ushort count = slot.CharCount;
                short col = 0;
                short row = 0;

                for (ushort i = 0; i < count; i++)
                {
                    uint dst = (uint)(slot.ChunkyBaseId + i);

                    ushort gx = (ushort)(col * glyphW + srcX);
                    ushort gy = (ushort)(row * glyphH + srcY);

                    GrabChunky(dst, chunkyId, gx, gy, (ushort)glyphW, (ushort)glyphH, flags, featherStrength, featherTint);

                    ChunkySurface? g = ChunkyAddress(dst);
                    ushort w = g != null ? (ushort)g.Width : (ushort)0;
                    slot.CharWidths[i] = (ushort)(w + 1);

                    col++;
                    if ((ushort)columns <= (ushort)col)
                    {
                        col = 0;
                        row++;
                    }
                }

                slot.Active = true;
                UseChunky(oldTarget);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::FontPrint(unsigned short, unsigned short, char const*, unsigned char, DexCol, short)
        // flags (best-effort mapping from decompile):
        // - bit0: center text horizontally around x
        // - bit1: fixed width (use FixedAdvance)
        // - bit2: draw shadow/outline
        // - bit3..: blend/tint mode selection (maps to Blit mode bits)
        // --------------------------------------------------------------------
        internal void FontPrint(uint x, uint y, string text, uint flags, DexCol color, short percent)
        {
            if (_fonts == null) return;
            if (_usedFont >= (uint)_fonts.Length) return;

            FontSlot slot = _fonts[_usedFont];
            if (!slot.Active) return;

            if (string.IsNullOrEmpty(text)) return;

            if ((flags & 1u) != 0)
            {
                uint half = (uint)(FontWidth(text, (byte)flags) >> 1);
                x -= half;
            }

            byte mode = ResolveFontBlitMode(flags, slot);

            int penX = (int)(x & 0xFFFF);
            int penY = (int)(y & 0xFFFF);

            for (int i = 0; i < text.Length; i++)
            {
                int glyphIndex = (byte)text[i] - slot.FirstChar;

                ushort advance;
                if ((flags & 2u) == 0)
                {
                    if (glyphIndex < 0 || glyphIndex >= slot.CharCount)
                    {
                        // Fallback width like C++: use 'A' slot width /2
                        int fallbackIndex = 0x41 - slot.FirstChar;
                        if (fallbackIndex < 0 || fallbackIndex >= slot.CharCount) fallbackIndex = 0;
                        advance = (ushort)(slot.CharWidths[fallbackIndex] >> 1);
                    }
                    else
                    {
                        advance = slot.CharWidths[glyphIndex];
                    }
                }
                else
                {
                    advance = (ushort)slot.FixedAdvance;
                }

                if (glyphIndex >= 0 && glyphIndex < slot.CharCount)
                {
                    if ((flags & 4u) != 0)
                    {
                        // Shadow/outline: mimic the decompile using translucent black.
                        // Two paths exist in C++; keep a deterministic one here.
                        Blit((uint)(slot.ChunkyBaseId + glyphIndex), penX + 2, penY + 2, 4, COLOUR_BLACK, 0x32);
                    }

                    Blit((uint)(slot.ChunkyBaseId + glyphIndex), penX, penY, mode, color, percent);
                }

                penX += advance;
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::FontWidth(char const*, unsigned char)
        // --------------------------------------------------------------------
        internal short FontWidth(string text, byte flags)
        {
            if (_fonts == null) return 0;
            if (_usedFont >= (uint)_fonts.Length) return 0;

            FontSlot slot = _fonts[_usedFont];
            if (!slot.Active) return 0;

            if (string.IsNullOrEmpty(text)) return 0;

            short sum = 0;

            for (int i = 0; i < text.Length; i++)
            {
                ushort w;

                if ((flags & 2) == 0)
                {
                    int glyphIndex = (byte)text[i] - slot.FirstChar;
                    if (glyphIndex < 0 || glyphIndex >= slot.CharCount)
                    {
                        int fallbackIndex = 0x41 - slot.FirstChar;
                        if (fallbackIndex < 0 || fallbackIndex >= slot.CharCount) fallbackIndex = 0;
                        w = (ushort)(slot.CharWidths[fallbackIndex] >> 1);
                    }
                    else
                    {
                        w = slot.CharWidths[glyphIndex];
                    }
                }
                else
                {
                    w = (ushort)slot.FixedAdvance;
                }

                sum = (short)(sum + w);
            }

            return sum;
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

        // DexterGFX::BoxFill16(short, short, short, short, DexCol const&, unsigned char, short)
        // Note: The original does not clip; callers are expected to provide valid coordinates.
        internal void BoxFill16(short x, short y, short width, short height, DexCol color, byte mode, short blendPercent)
        {
            ChunkySurface t = Target;
            if (t.BitsPerPixel != 16) throw new InvalidOperationException("Target is not 16-bit.");

            if (width <= 0 || height <= 0) return;

            int startX = x;
            int startY = y;

            Span<ushort> px = t.Pixels16;
            int pitch = t.Width;

            ushort c16 = Pack16(color);

            if (mode == 0x04)
            {
                int destMod = GetDestModFromPercent(blendPercent);
                int srcMod = 255 - destMod;

                for (int yy = 0; yy < height; yy++)
                {
                    int row = (startY + yy) * pitch + startX;

                    for (int xx = 0; xx < width; xx++)
                    {
                        int i = row + xx;
                        px[i] = Blend16_ModulateDstSrc(px[i], c16, destMod, srcMod);
                    }
                }

                return;
            }

            if (mode == 0x02)
            {
                for (int yy = 0; yy < height; yy++)
                {
                    int row = (startY + yy) * pitch + startX;
                    px.Slice(row, width).Fill(c16);
                }
            }
        }

        // DexterGFX::Blit8(unsigned char const*, unsigned char*, int, int, DexRect const&, unsigned char, short, unsigned char)
        internal void Blit8(ReadOnlySpan<byte> src, Span<byte> dst, int srcPitch, int dstPitch, in DexRect rect, byte colorKey, short modeFlags, byte param8)
        {
            if (rect.IsEmpty) return;
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(srcPitch);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dstPitch);

            int top = rect.Top;
            int left = rect.Left;
            int bottom = rect.Bottom;
            int width = rect.Width;

            int mode = (modeFlags & 0xFFFE) >> 1;
            if ((uint)mode >= 9u) return;

            bool solidVariant = (modeFlags & 1) != 0;

            switch (mode)
            {
                case 0:
                    {
                        if (solidVariant)
                        {
                            for (int y = top; y < bottom; y++)
                            {
                                int sRow = y * srcPitch + left;
                                int dRow = y * dstPitch + left;

                                if ((uint)sRow > (uint)src.Length) continue;
                                if ((uint)dRow > (uint)dst.Length) continue;
                                if (sRow + width > src.Length) continue;
                                if (dRow + width > dst.Length) continue;

                                src.Slice(sRow, width).CopyTo(dst.Slice(dRow, width));
                            }
                        }
                        else
                        {
                            for (int y = top; y < bottom; y++)
                            {
                                int sRow = y * srcPitch + left;
                                int dRow = y * dstPitch + left;

                                if (sRow + width > src.Length) continue;
                                if (dRow + width > dst.Length) continue;

                                for (int x = 0; x < width; x++)
                                {
                                    byte sPix = src[sRow + x];
                                    if (sPix != colorKey) dst[dRow + x] = sPix;
                                }
                            }
                        }

                        break;
                    }

                case 1:
                    {
                        for (int y = top; y < bottom; y++)
                        {
                            int sRow = y * srcPitch + left;
                            int dRow = y * dstPitch + left;

                            if (sRow + width > src.Length) continue;
                            if (dRow + width > dst.Length) continue;

                            for (int x = 0; x < width; x++)
                            {
                                if (src[sRow + x] != colorKey) dst[dRow + x] = param8;
                            }
                        }

                        break;
                    }

                case 2:
                    {
                        for (int y = top; y < bottom; y++)
                        {
                            int sRow = y * srcPitch + left;
                            int dRow = y * dstPitch + left;

                            if (sRow + width > src.Length) continue;
                            if (dRow + width > dst.Length) continue;

                            for (int x = 0; x < width; x++)
                            {
                                if (src[sRow + x] != colorKey)
                                {
                                    byte dPix = dst[dRow + x];
                                    dst[dRow + x] = _transTable8[(param8 << 8) | dPix];
                                }
                            }
                        }

                        break;
                    }

                case 4:
                    {
                        for (int y = top; y < bottom; y++)
                        {
                            int sRow = y * srcPitch + left;
                            int dRow = y * dstPitch + left;

                            if (sRow + width > src.Length) continue;
                            if (dRow + width > dst.Length) continue;

                            for (int x = 0; x < width; x++)
                            {
                                byte sPix = src[sRow + x];
                                if (sPix != colorKey)
                                {
                                    byte dPix = dst[dRow + x];
                                    dst[dRow + x] = _transTable8[(sPix << 8) | dPix];
                                }
                            }
                        }

                        break;
                    }

                case 8:
                    {
                        for (int y = top; y < bottom; y++)
                        {
                            int sRow = y * srcPitch + left;
                            int dRow = y * dstPitch + left;

                            if (sRow + width > src.Length) continue;
                            if (dRow + width > dst.Length) continue;

                            for (int x = 0; x < width; x++)
                            {
                                byte sPix = src[sRow + x];
                                if (sPix != colorKey) dst[dRow + x] = _transTable8[(param8 << 8) | sPix];
                            }
                        }

                        break;
                    }
            }
        }

        // DexterGFX::Blit16(unsigned short const*, unsigned short*, int, int, DexRect const&, unsigned short, short, unsigned short, short)
        internal void Blit16(ReadOnlySpan<ushort> src, Span<ushort> dst, int srcPitch, int dstPitch, in DexRect rect, ushort colorKey, short modeFlags, ushort param8, short param9)
        {
            if (rect.IsEmpty) return;
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(srcPitch);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dstPitch);

            int top = rect.Top;
            int left = rect.Left;
            int bottom = rect.Bottom;
            int width = rect.Width;

            int mode = (modeFlags & 0xFFFE) >> 1;
            if ((uint)mode >= 9u) return;

            bool solidVariant = (modeFlags & 1) != 0;

            switch (mode)
            {
                case 0:
                    {
                        if (solidVariant)
                        {
                            for (int y = top; y < bottom; y++)
                            {
                                int sRow = y * srcPitch + left;
                                int dRow = y * dstPitch + left;

                                if (sRow + width > src.Length) continue;
                                if (dRow + width > dst.Length) continue;

                                src.Slice(sRow, width).CopyTo(dst.Slice(dRow, width));
                            }
                        }
                        else
                        {
                            for (int y = top; y < bottom; y++)
                            {
                                int sRow = y * srcPitch + left;
                                int dRow = y * dstPitch + left;

                                if (sRow + width > src.Length) continue;
                                if (dRow + width > dst.Length) continue;

                                for (int x = 0; x < width; x++)
                                {
                                    ushort sPix = src[sRow + x];
                                    if (sPix != colorKey) dst[dRow + x] = sPix;
                                }
                            }
                        }

                        break;
                    }

                case 1:
                    {
                        if (solidVariant)
                        {
                            for (int y = top; y < bottom; y++)
                            {
                                int dRow = y * dstPitch + left;
                                if (dRow + width > dst.Length) continue;
                                dst.Slice(dRow, width).Fill(param8);
                            }
                        }
                        else
                        {
                            for (int y = top; y < bottom; y++)
                            {
                                int sRow = y * srcPitch + left;
                                int dRow = y * dstPitch + left;

                                if (sRow + width > src.Length) continue;
                                if (dRow + width > dst.Length) continue;

                                for (int x = 0; x < width; x++)
                                {
                                    if (src[sRow + x] != colorKey) dst[dRow + x] = param8;
                                }
                            }
                        }

                        break;
                    }

                case 2:
                    {
                        int destMod = GetDestModFromPercent(param9);
                        int srcMod = 255 - destMod;

                        if (solidVariant)
                        {
                            for (int y = top; y < bottom; y++)
                            {
                                int dRow = y * dstPitch + left;
                                if (dRow + width > dst.Length) continue;

                                for (int x = 0; x < width; x++)
                                {
                                    dst[dRow + x] = Blend16_ModulateDstSrc(dst[dRow + x], param8, destMod, srcMod);
                                }
                            }
                        }
                        else
                        {
                            for (int y = top; y < bottom; y++)
                            {
                                int sRow = y * srcPitch + left;
                                int dRow = y * dstPitch + left;

                                if (sRow + width > src.Length) continue;
                                if (dRow + width > dst.Length) continue;

                                for (int x = 0; x < width; x++)
                                {
                                    if (src[sRow + x] != colorKey)
                                    {
                                        dst[dRow + x] = Blend16_ModulateDstSrc(dst[dRow + x], param8, destMod, srcMod);
                                    }
                                }
                            }
                        }

                        break;
                    }

                case 4:
                    {
                        int destMod = GetDestModFromPercent(param9);
                        int srcMod = 255 - destMod;

                        if (solidVariant)
                        {
                            for (int y = top; y < bottom; y++)
                            {
                                int sRow = y * srcPitch + left;
                                int dRow = y * dstPitch + left;

                                if (sRow + width > src.Length) continue;
                                if (dRow + width > dst.Length) continue;

                                for (int x = 0; x < width; x++)
                                {
                                    dst[dRow + x] = Blend16_ModulateDstSrc(dst[dRow + x], src[sRow + x], destMod, srcMod);
                                }
                            }
                        }
                        else
                        {
                            for (int y = top; y < bottom; y++)
                            {
                                int sRow = y * srcPitch + left;
                                int dRow = y * dstPitch + left;

                                if (sRow + width > src.Length) continue;
                                if (dRow + width > dst.Length) continue;

                                for (int x = 0; x < width; x++)
                                {
                                    ushort sPix = src[sRow + x];
                                    if (sPix != colorKey)
                                    {
                                        dst[dRow + x] = Blend16_ModulateDstSrc(dst[dRow + x], sPix, destMod, srcMod);
                                    }
                                }
                            }
                        }

                        break;
                    }

                case 8:
                    {
                        int destMod = GetDestModFromPercent(param9);
                        int srcMod = 255 - destMod;

                        if (solidVariant)
                        {
                            for (int y = top; y < bottom; y++)
                            {
                                int sRow = y * srcPitch + left;
                                int dRow = y * dstPitch + left;

                                if (sRow + width > src.Length) continue;
                                if (dRow + width > dst.Length) continue;

                                src.Slice(sRow, width).CopyTo(dst.Slice(dRow, width));

                                for (int x = 0; x < width; x++)
                                {
                                    dst[dRow + x] = Blend16_ModulateDstSrc(dst[dRow + x], param8, destMod, srcMod);
                                }
                            }
                        }
                        else
                        {
                            for (int y = top; y < bottom; y++)
                            {
                                int sRow = y * srcPitch + left;
                                int dRow = y * dstPitch + left;

                                if (sRow + width > src.Length) continue;
                                if (dRow + width > dst.Length) continue;

                                for (int x = 0; x < width; x++)
                                {
                                    ushort sPix = src[sRow + x];
                                    if (sPix != colorKey) dst[dRow + x] = sPix;
                                }

                                for (int x = 0; x < width; x++)
                                {
                                    if (src[sRow + x] != colorKey)
                                    {
                                        dst[dRow + x] = Blend16_ModulateDstSrc(dst[dRow + x], param8, destMod, srcMod);
                                    }
                                }
                            }
                        }

                        break;
                    }
            }
        }

        // C++: DestMod = (short)(int)((float)(int)param * 2.55); SourceMod = 0xFF - DestMod;
        private static int GetDestModFromPercent(short percent)
        {
            int v = (int)(percent * 2.55f);
            if (v < 0) v = 0;
            if (v > 255) v = 255;
            return v;
        }

        // Engine-style fixed-point blend: (dst * destMod + src * srcMod) >> 8 per channel.
        private ushort Blend16_ModulateDstSrc(ushort dst, ushort src, int destMod, int srcMod)
        {
            Unpack16(dst, out int dr, out int dg, out int db);
            Unpack16(src, out int sr, out int sg, out int sb);

            int rr = (dr * destMod + sr * srcMod) >> 8;
            int gg = (dg * destMod + sg * srcMod) >> 8;
            int bb = (db * destMod + sb * srcMod) >> 8;

            return Pack16From8((byte)rr, (byte)gg, (byte)bb);
        }

        // DexterGFX::AlphaBlit16(ChunkyObject&, unsigned short*, int, DexRect const&, short, unsigned short, short)
        // - flags & 2 == 0: alpha-blend source pixels into destination using source alpha mask.
        // - flags & 2 != 0: alpha-blend a constant color into destination using source alpha mask.
        // - globalPercent behaves like the original: 0 => full effect, 100 => no effect.
        internal void AlphaBlit16(ChunkySurface source, Span<ushort> destination, int destinationPitchPixels, in DexRect rect, short flags, ushort constantColor, short globalPercent)
        {
            if (source.BitsPerPixel != 16) throw new InvalidOperationException("Source is not 16-bit.");
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(destinationPitchPixels);
            if (rect.IsEmpty) return;

            // ChunkyObject::AlphaData equivalent: ChunkySurface is expected to expose an alpha mask.
            // If the project stores alpha elsewhere, this must be wired accordingly.
            ReadOnlySpan<byte> alpha = source.Alpha8;
            ReadOnlySpan<ushort> spx = source.Pixels16;

            int w = source.Width;

            byte globalScale = GetGlobalAlphaScale(globalPercent);

            bool useConstant = (flags & 2) != 0;

            for (int y = rect.Top; y < rect.Bottom; y++)
            {
                int srcRow = y * w + rect.Left;
                int dstRow = y * destinationPitchPixels + rect.Left;

                int count = rect.Width;

                if (srcRow + count > spx.Length) continue;
                if (srcRow + count > alpha.Length) continue;
                if (dstRow + count > destination.Length) continue;

                for (int x = 0; x < count; x++)
                {
                    byte a = alpha[srcRow + x];
                    if (a == 0) continue;

                    // Apply global scaling like the original caller-controlled fade.
                    int scaled = (a * globalScale) >> 8;
                    if (scaled <= 0) continue;

                    byte aa = (byte)scaled;

                    ushort dstPix = destination[dstRow + x];
                    ushort srcPix = useConstant ? constantColor : spx[srcRow + x];

                    destination[dstRow + x] = Blend16(dstPix, srcPix, aa);
                }
            }
        }

        // DexterGFX::ChunkyScale8(ChunkyObject const&, ChunkyObject const&, DexRect const&, DexRect const&, unsigned char)
        // Managed nearest-neighbor scaler with optional color-key handling (flags & 0x10).
        internal void ChunkyScale8(ChunkySurface src, ChunkySurface dst, in DexRect srcRect, in DexRect dstRect, byte flags)
        {
            if (src.BitsPerPixel != 8 || dst.BitsPerPixel != 8) throw new InvalidOperationException("ChunkyScale8 requires 8-bit surfaces.");

            DexRect s = srcRect.ClampTo(src.Width, src.Height);
            DexRect d = dstRect.ClampTo(dst.Width, dst.Height);
            if (s.IsEmpty || d.IsEmpty) return;

            bool useColorKey = (flags & 0x10) != 0;
            byte ck = _colourKey8;

            ReadOnlySpan<byte> spx = src.Pixels8;
            Span<byte> dpx = dst.Pixels8;

            int sW = s.Width;
            int sH = s.Height;
            int dW = d.Width;
            int dH = d.Height;

            for (int yy = 0; yy < dH; yy++)
            {
                int sy = s.Top + (yy * sH) / dH;
                int dy = d.Top + yy;

                int sRow = sy * src.Width;
                int dRow = dy * dst.Width;

                for (int xx = 0; xx < dW; xx++)
                {
                    int sx = s.Left + (xx * sW) / dW;
                    int dx = d.Left + xx;

                    byte v = spx[sRow + sx];
                    if (useColorKey && v == ck) continue;

                    dpx[dRow + dx] = v;
                }
            }
        }

        // DexterGFX::ChunkyScale16(ChunkyObject const&, ChunkyObject const&, DexRect const&, DexRect const&, unsigned char)
        internal void ChunkyScale16(ChunkySurface src, ChunkySurface dst, in DexRect srcRect, in DexRect dstRect, byte flags)
        {
            if (src.BitsPerPixel != 16 || dst.BitsPerPixel != 16) throw new InvalidOperationException("ChunkyScale16 requires 16-bit surfaces.");

            DexRect s = srcRect.ClampTo(src.Width, src.Height);
            DexRect d = dstRect.ClampTo(dst.Width, dst.Height);
            if (s.IsEmpty || d.IsEmpty) return;

            bool useColorKey = (flags & 0x10) != 0;
            ushort ck = _colourKey16;

            ReadOnlySpan<ushort> spx = src.Pixels16;
            Span<ushort> dpx = dst.Pixels16;

            int sW = s.Width;
            int sH = s.Height;
            int dW = d.Width;
            int dH = d.Height;

            for (int yy = 0; yy < dH; yy++)
            {
                int sy = s.Top + (yy * sH) / dH;
                int dy = d.Top + yy;

                int sRow = sy * src.Width;
                int dRow = dy * dst.Width;

                for (int xx = 0; xx < dW; xx++)
                {
                    int sx = s.Left + (xx * sW) / dW;
                    int dx = d.Left + xx;

                    ushort v = spx[sRow + sx];
                    if (useColorKey && v == ck) continue;

                    dpx[dRow + dx] = v;
                }
            }
        }

        // DexterGFX::BlitRotate16(ChunkyObject const&, ChunkyObject const&, short, short, double, short, short, unsigned char)
        // This overload maps the original degrees input to the existing managed rotate helper.
        internal void BlitRotate16(ChunkySurface src, ChunkySurface dst, short dstX, short dstY, double angleDegrees, short pivotX, short pivotY, byte flags)
        {
            bool useColorKey = flags != 0;

            // C++ used (360 - angle) degrees; match rotation direction here.
            float radians = (float)((360.0 - angleDegrees) * (Math.PI / 180.0));

            // The original used pivotX/pivotY in the math; the existing managed implementation rotates around srcRect center.
            // To preserve behavior more closely, use the full surface rect and rotate around its center (common engine usage).
            DexRect full = new(0, 0, src.Width, src.Height);

            BlitRotate16(src, dst, dstX, dstY, full, radians, useColorKey, _colourKey16);
        }

        // DexterGFX::DumpChunkyBuffer8(ChunkyObject const&, unsigned int*, int, bool)
        // Managed implementation:
        // - Writes ARGB8888 into destination (uint per pixel).
        // - destinationPitchPixels is the number of uints per row.
        // - If dirtyOnly == true, no dirty-list exists in this managed port => full copy (deterministic).
        internal void DumpChunkyBuffer8(ChunkySurface src, Span<uint> destination, int destinationPitchPixels, bool dirtyOnly)
        {
            if (src.BitsPerPixel != 8) throw new InvalidOperationException("DumpChunkyBuffer8 requires an 8-bit surface.");
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(destinationPitchPixels);

            ReadOnlySpan<byte> spx = src.Pixels8;

            int w = src.Width;
            int h = src.Height;

            for (int y = 0; y < h; y++)
            {
                int srcRow = y * w;
                int dstRow = y * destinationPitchPixels;

                if (dstRow + w > destination.Length) break;

                for (int x = 0; x < w; x++)
                {
                    byte idx = spx[srcRow + x];

                    byte r = _paletteR[idx];
                    byte g = _paletteG[idx];
                    byte b = _paletteB[idx];

                    // ARGB 0xFFRRGGBB
                    destination[dstRow + x] = 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;
                }
            }
        }

        // C++: if (percent < 100) alpha = (short)((float)percent * -2.56 + 255.0) else 0;
        private static byte GetGlobalAlphaScale(short percent)
        {
            if (percent >= 100) return 0;
            int v = (int)(255.0f - (percent * 2.56f));
            if (v < 0) v = 0;
            if (v > 255) v = 255;
            return (byte)v;
        }


        // DexterGFX::DumpChunkyBuffer16(ChunkyObject const&, unsigned int*, int, bool)
        // Managed port notes:
        // - This port targets ARGB8888 output (0xFFRRGGBB) like DumpChunkyBuffer8.
        // - No "dirty list" exists in the managed port; dirtyOnly is accepted but treated deterministically as full copy.
        internal void DumpChunkyBuffer16(ChunkySurface src, Span<uint> destination, int destinationPitchPixels, bool dirtyOnly)
        {
            if (src.BitsPerPixel != 16) throw new InvalidOperationException("DumpChunkyBuffer16 requires a 16-bit surface.");
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(destinationPitchPixels);

            ReadOnlySpan<ushort> spx = src.Pixels16;

            int w = src.Width;
            int h = src.Height;

            for (int y = 0; y < h; y++)
            {
                int srcRow = y * w;
                int dstRow = y * destinationPitchPixels;

                if (dstRow + w > destination.Length) break;

                for (int x = 0; x < w; x++)
                {
                    ushort c16 = spx[srcRow + x];
                    Unpack16(c16, out int rr, out int gg, out int bb);
                    destination[dstRow + x] = 0xFF000000u | ((uint)(byte)rr << 16) | ((uint)(byte)gg << 8) | (byte)bb;
                }
            }
        }

        // DexterGFX::ChunkyFlip(ChunkyObject const&, unsigned char)
        // flags:
        // - bit0 => horizontal flip
        // - bit1 => vertical flip
        internal void ChunkyFlip(ChunkySurface surface, byte flags)
        {
            int w = surface.Width;
            int h = surface.Height;

            bool flipX = (flags & 0x01) != 0;
            bool flipY = (flags & 0x02) != 0;

            if (!flipX && !flipY) return;

            if (surface.BitsPerPixel == 8)
            {
                Span<byte> px = surface.Pixels8;

                if (flipY)
                {
                    for (int x = 0; x < w; x++)
                    {
                        for (int y = 0; y < (h >> 1); y++)
                        {
                            int y2 = (h - 1) - y;
                            int i0 = y * w + x;
                            int i1 = y2 * w + x;

                            (px[i1], px[i0]) = (px[i0], px[i1]);
                        }
                    }
                }

                if (flipX)
                {
                    for (int y = 0; y < h; y++)
                    {
                        int row = y * w;
                        for (int x = 0; x < (w >> 1); x++)
                        {
                            int x2 = (w - 1) - x;
                            int i0 = row + x;
                            int i1 = row + x2;

                            (px[i1], px[i0]) = (px[i0], px[i1]);
                        }
                    }
                }

                return;
            }

            if (surface.BitsPerPixel == 16)
            {
                Span<ushort> px = surface.Pixels16;

                if (flipY)
                {
                    for (int x = 0; x < w; x++)
                    {
                        for (int y = 0; y < (h >> 1); y++)
                        {
                            int y2 = (h - 1) - y;
                            int i0 = y * w + x;
                            int i1 = y2 * w + x;

                            (px[i1], px[i0]) = (px[i0], px[i1]);
                        }
                    }
                }

                if (flipX)
                {
                    for (int y = 0; y < h; y++)
                    {
                        int row = y * w;
                        for (int x = 0; x < (w >> 1); x++)
                        {
                            int x2 = (w - 1) - x;
                            int i0 = row + x;
                            int i1 = row + x2;

                            (px[i1], px[i0]) = (px[i0], px[i1]);
                        }
                    }
                }

                return;
            }

            throw new InvalidOperationException("Unsupported BitsPerPixel.");
        }

        // DexterGFX::SetMaxChunkies(unsigned int)
        // In this managed port, max chunkies is fixed at construction time.
        // The method is preserved for call-compatibility: it only succeeds when no chunkies were allocated yet.
        internal void SetMaxChunkies(uint maxChunkies)
        {
            if (maxChunkies == 0) throw new ArgumentOutOfRangeException(nameof(maxChunkies));

            for (int i = 0; i < _chunkies.Length; i++)
            {
                if (_chunkies[i] != null)
                {
                    return;
                }
            }

            // No-op by design (array size cannot change without reallocating the whole engine object).
        }

        // DexterGFX::ValidChunky(unsigned int)
        internal bool ValidChunky(uint chunkyId)
        {
            return ChunkyAddress(chunkyId) != null;
        }

        // DexterGFX::ValidAlphaChunky(unsigned int)
        internal bool ValidAlphaChunky(uint index)
        {
            if (index >= (uint)_chunkies.Length) return false;
            ChunkySurface? c = _chunkies[index];
            return c != null && c.HasAlpha8;
        }

        // DexterGFX::ChunkyWidth(unsigned int)
        internal ushort ChunkyWidth(uint index)
        {
            if (index >= (uint)_chunkies.Length) return 0;
            ChunkySurface? c = _chunkies[index];
            if (c == null) return 0;
            return (ushort)c.Width;
        }

        // DexterGFX::ChunkyBitDepth(unsigned int)
        internal byte ChunkyBitDepth(uint index)
        {
            if (index >= (uint)_chunkies.Length) return 0;
            ChunkySurface? c = _chunkies[index];
            if (c == null) return 0;
            return (byte)c.BitsPerPixel;
        }

        // DexterGFX::ChunkyHeight(unsigned int)
        internal ushort ChunkyHeight(uint index)
        {
            if (index >= (uint)_chunkies.Length) return 0;
            ChunkySurface? c = _chunkies[index];
            if (c == null) return 0;
            return (ushort)c.Height;
        }

        // DexterGFX::InitChunky(unsigned int, int, int, int)
        internal bool InitChunky(uint index, int width, int height, int bitsPerPixel)
        {
            if (index >= (uint)_chunkies.Length) return false;
            if (bitsPerPixel != 8 && bitsPerPixel != 16) return false;

            _chunkies[index] = new ChunkySurface(width, height, bitsPerPixel);
            return true;
        }

        // DexterGFX::DumpChunkyBuffer(ChunkyObject const&, unsigned int*, int)
        // Managed replacement chooses by surface BPP.
        internal void DumpChunkyBuffer(ChunkySurface src, Span<uint> destination, int destinationPitchPixels)
        {
            if (src.BitsPerPixel == 8)
            {
                DumpChunkyBuffer8(src, destination, destinationPitchPixels, false);
                return;
            }

            if (src.BitsPerPixel == 16)
            {
                DumpChunkyBuffer16(src, destination, destinationPitchPixels, false);
                return;
            }

            throw new InvalidOperationException("Unsupported BitsPerPixel.");
        }

        // DexterGFX::ClearChunky(unsigned int, DexCol const&, unsigned short)
        // linesToClear:
        // - 0 => clear full height
        // - otherwise clear min(linesToClear, height) lines
        internal void ClearChunky(uint index, in DexCol color, ushort linesToClear)
        {
            if (index >= (uint)_chunkies.Length) return;

            ChunkySurface? c = _chunkies[index];
            if (c == null) return;

            int w = c.Width;
            int h = c.Height;

            int lines = linesToClear == 0 ? h : linesToClear;
            if (lines > h) lines = h;
            if (lines <= 0) return;

            if (c.BitsPerPixel == 8)
            {
                // C++ used (uchar)*DexCol for 8-bit; in this managed port the closest palette match is used.
                byte idx8 = FindClosestPaletteIndex(color.R, color.G, color.B);

                Span<byte> px = c.Pixels8;
                int count = checked(w * lines);
                px[..count].Fill(idx8);
                return;
            }

            if (c.BitsPerPixel == 16)
            {
                ushort c16 = Pack16(color);

                Span<ushort> px = c.Pixels16;
                int count = checked(w * lines);
                px[..count].Fill(c16);
                return;
            }

            throw new InvalidOperationException("Unsupported BitsPerPixel.");
        }

        // DexterGFX::GrabChunky(unsigned int, unsigned int, unsigned short, unsigned short, unsigned short, unsigned short, unsigned char, unsigned short, DexCol)
        // Managed port notes:
        // - The original supports a variety of flags including alpha generation/feathering.
        // - This managed port preserves the main behavior:
        //   1) clip rect against source
        //   2) allocate/resize destination chunky to the clipped size (BPP preserved)
        //   3) copy pixels into destination (solid or color-keyed depending on flags)
        //   4) set OriginX/OriginY so callers can reproduce the same anchor math as the original engine
        //   5) optional alpha buffer creation (mask from color key) with simple deterministic feather
        //
        // flags (best-effort mapping from decompile):
        // - bit0: use color key while copying (skip color key pixels)
        // - bit4: request alpha buffer + feather strength scaling
        // - bit2..bit4 (0x1C): request alpha mask generation path
        internal bool GrabChunky(uint dstIndex, uint srcIndex, ushort x, ushort y, ushort width, ushort height, byte flags, ushort featherStrength, DexCol featherTint)
        {
            if (dstIndex >= (uint)_chunkies.Length) return false;
            if (srcIndex >= (uint)_chunkies.Length) return false;

            ChunkySurface? src = _chunkies[srcIndex];
            if (src == null) return false;

            DexRect requested = new(x, y, x + width, y + height);
            DexRect clipped = requested.ClampTo(src.Width, src.Height);
            if (clipped.IsEmpty) return false;

            int dstW = clipped.Width;
            int dstH = clipped.Height;

            bool wantAlpha = (flags & 0x1C) != 0;
            bool useColorKey = (flags & 0x01) != 0;

            ChunkySurface? dst = _chunkies[dstIndex];
            if (dst == null || dst.Width != dstW || dst.Height != dstH || dst.BitsPerPixel != src.BitsPerPixel)
            {
                dst = new ChunkySurface(dstW, dstH, src.BitsPerPixel, wantAlpha);
                _chunkies[dstIndex] = dst;
            }
            else
            {
                if (wantAlpha && !dst.HasAlpha8) dst.EnsureAlpha8();
            }

            // Preserve original anchor semantics: requested top-left relative to clipped region.
            dst.OriginX = (short)(x - clipped.Left);
            dst.OriginY = (short)(y - clipped.Top);

            if (src.BitsPerPixel == 8)
            {
                ReadOnlySpan<byte> spx = src.Pixels8;
                Span<byte> dpx = dst.Pixels8;

                byte ck = _colourKey8;

                for (int yy = 0; yy < dstH; yy++)
                {
                    int sy = clipped.Top + yy;
                    int sRow = sy * src.Width + clipped.Left;
                    int dRow = yy * dstW;

                    if (useColorKey)
                    {
                        for (int xx = 0; xx < dstW; xx++)
                        {
                            byte v = spx[sRow + xx];
                            if (v == ck) continue;
                            dpx[dRow + xx] = v;
                        }
                    }
                    else
                    {
                        spx.Slice(sRow, dstW).CopyTo(dpx.Slice(dRow, dstW));
                    }
                }
            }
            else if (src.BitsPerPixel == 16)
            {
                ReadOnlySpan<ushort> spx = src.Pixels16;
                Span<ushort> dpx = dst.Pixels16;

                ushort ck = _colourKey16;

                for (int yy = 0; yy < dstH; yy++)
                {
                    int sy = clipped.Top + yy;
                    int sRow = sy * src.Width + clipped.Left;
                    int dRow = yy * dstW;

                    if (useColorKey)
                    {
                        for (int xx = 0; xx < dstW; xx++)
                        {
                            ushort v = spx[sRow + xx];
                            if (v == ck) continue;
                            dpx[dRow + xx] = v;
                        }
                    }
                    else
                    {
                        spx.Slice(sRow, dstW).CopyTo(dpx.Slice(dRow, dstW));
                    }
                }
            }
            else
            {
                return false;
            }

            if (!wantAlpha)
            {
                return true;
            }

            // Alpha generation: 255 for non-color-key, 0 for color-key.
            dst.EnsureAlpha8();
            Span<byte> a = dst.Alpha8;

            if (dst.BitsPerPixel == 8)
            {
                ReadOnlySpan<byte> dpx = dst.Pixels8;
                byte ck = _colourKey8;

                for (int i = 0; i < dpx.Length; i++)
                {
                    a[i] = dpx[i] == ck ? (byte)0 : (byte)255;
                }
            }
            else
            {
                ReadOnlySpan<ushort> dpx = dst.Pixels16;
                ushort ck = _colourKey16;

                for (int i = 0; i < dpx.Length; i++)
                {
                    a[i] = dpx[i] == ck ? (byte)0 : (byte)255;
                }
            }

            // Deterministic feather: apply a small box-blur to alpha if strength > 0.
            // The C++ uses a distance-based weighting; this is a safe managed approximation.
            int radius = (featherStrength >> 3);
            if ((flags & 0x10) != 0)
            {
                // C++ scales feather strength when bit4 is set.
                radius = (int)((featherStrength * 1.6f) / 8.0f);
            }

            if (radius < 1) radius = 1;
            if (radius > 24) radius = 24;

            ApplyAlphaBoxBlur(dstW, dstH, a, radius);

            return true;
        }

        // Simple deterministic alpha blur (box blur, 1 pass).
        private static void ApplyAlphaBoxBlur(int width, int height, Span<byte> alpha, int radius)
        {
            byte[] tmp = new byte[alpha.Length];

            // Horizontal
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int sum = 0;
                    int count = 0;

                    int x0 = x - radius;
                    int x1 = x + radius;

                    if (x0 < 0) x0 = 0;
                    if (x1 >= width) x1 = width - 1;

                    for (int xx = x0; xx <= x1; xx++)
                    {
                        sum += alpha[row + xx];
                        count++;
                    }

                    tmp[row + x] = (byte)(sum / count);
                }
            }

            // Vertical
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int sum = 0;
                    int count = 0;

                    int y0 = y - radius;
                    int y1 = y + radius;

                    if (y0 < 0) y0 = 0;
                    if (y1 >= height) y1 = height - 1;

                    for (int yy = y0; yy <= y1; yy++)
                    {
                        sum += tmp[yy * width + x];
                        count++;
                    }

                    alpha[row + x] = (byte)(sum / count);
                }
            }
        }

        // DexterGFX::UsedChunky()
        // Ensures the active chunky index is valid; if not, it falls back to 0 when available.
        internal uint UsedChunky()
        {
            if ((uint)_activeChunkyIndex < (uint)_chunkies.Length && _chunkies[_activeChunkyIndex] != null)
            {
                return (uint)_activeChunkyIndex;
            }

            if (_chunkies.Length != 0 && _chunkies[0] != null)
            {
                _activeChunkyIndex = 0;
                _target = _chunkies[0];
                return 0;
            }

            _activeChunkyIndex = -1;
            _target = null;
            return 0;
        }

        // DexterGFX::ChunkyScaledWidth(unsigned int, short)
        // Interpreted as: "scaled width when height becomes newHeight" while preserving aspect ratio.
        internal int ChunkyScaledWidth(uint index, short newHeight)
        {
            if ((uint)index >= (uint)_chunkies.Length) return 0;

            ChunkySurface? c = _chunkies[index];
            if (c == null) return 0;

            int w = c.Width;
            int h = c.Height;
            if (w <= 0 || h <= 0) return 0;

            int nh = newHeight;
            if (nh <= 0) return 0;

            if (nh >= h) return w;

            // width' = width * newHeight / height
            return (int)((long)w * nh / h);
        }

        // DexterGFX::ChunkyScaledHeight(unsigned int, short)
        // Interpreted as: "scaled height when width becomes newWidth" while preserving aspect ratio.
        internal int ChunkyScaledHeight(uint index, short newWidth)
        {
            if ((uint)index >= (uint)_chunkies.Length) return 0;

            ChunkySurface? c = _chunkies[index];
            if (c == null) return 0;

            int w = c.Width;
            int h = c.Height;
            if (w <= 0 || h <= 0) return 0;

            int nw = newWidth;
            if (nw <= 0) return 0;

            if (nw >= w) return h;

            // height' = height * newWidth / width
            return (int)((long)h * nw / w);
        }

        // DexterGFX::ChunkyCopy(unsigned int, unsigned int, unsigned short)
        // Copies a rectangle of lines from src to dst. linesToCopy==0 => full height of src.
        internal void ChunkyCopy(uint srcIndex, uint dstIndex, ushort linesToCopy)
        {
            if ((uint)srcIndex >= (uint)_chunkies.Length) return;
            if ((uint)dstIndex >= (uint)_chunkies.Length) return;
            if (srcIndex == dstIndex) return;

            ChunkySurface? src = _chunkies[srcIndex];
            ChunkySurface? dst = _chunkies[dstIndex];
            if (src == null || dst == null) return;

            if (src.BitsPerPixel != dst.BitsPerPixel) return;
            if (src.Width != dst.Width) return;

            int lines = linesToCopy == 0 ? src.Height : linesToCopy;
            if (lines > src.Height) lines = src.Height;
            if (lines > dst.Height) lines = dst.Height;
            if (lines <= 0) return;

            int count = checked(src.Width * lines);

            if (src.BitsPerPixel == 8)
            {
                ReadOnlySpan<byte> spx = src.Pixels8;
                Span<byte> dpx = dst.Pixels8;
                spx.Slice(0, count).CopyTo(dpx.Slice(0, count));
                return;
            }

            if (src.BitsPerPixel == 16)
            {
                ReadOnlySpan<ushort> spx = src.Pixels16;
                Span<ushort> dpx = dst.Pixels16;
                spx.Slice(0, count).CopyTo(dpx.Slice(0, count));
                return;
            }
        }

        // DexterGFX::ChunkyScale(unsigned int, unsigned int, short, short, unsigned char)
        // Managed interpretation:
        // - scaleXPercent/scaleYPercent are percentages (100 => same size)
        // - flags bit3 (0x08): free/recreate destination
        // - flags bit4 (0x10): color-keyed scaling path (passed through to ChunkyScale8/16)
        // - flags bit2 (0x04): bilinear path (passed through to ChunkyScale8/16)
        internal void ChunkyScale(uint srcIndex, uint dstIndex, short scaleXPercent, short scaleYPercent, byte flags)
        {
            if ((uint)srcIndex >= (uint)_chunkies.Length) return;
            if ((uint)dstIndex >= (uint)_chunkies.Length) return;

            ChunkySurface? src = _chunkies[srcIndex];
            if (src == null) return;

            if (scaleXPercent == 0 && scaleYPercent == 0) return;

            int sx = scaleXPercent <= 0 ? 100 : scaleXPercent;
            int sy = scaleYPercent <= 0 ? 100 : scaleYPercent;

            int newW = (int)((long)src.Width * sx / 100);
            int newH = (int)((long)src.Height * sy / 100);

            if (newW <= 0) newW = 1;
            if (newH <= 0) newH = 1;

            bool recreate = (flags & 0x08) != 0;

            ChunkySurface? dst = _chunkies[dstIndex];
            if (recreate)
            {
                _chunkies[dstIndex] = null;
                if (_activeChunkyIndex == (int)dstIndex)
                {
                    _activeChunkyIndex = -1;
                    _target = null;
                }
                dst = null;
            }

            if (dst == null || dst.Width != newW || dst.Height != newH || dst.BitsPerPixel != src.BitsPerPixel)
            {
                dst = new ChunkySurface(newW, newH, src.BitsPerPixel);
                _chunkies[dstIndex] = dst;
            }

            // Preserve origin scaling like the original (best-effort, deterministic).
            // origin' = origin / scaleFactor
            dst.OriginX = (short)(src.OriginX * 100 / sx);
            dst.OriginY = (short)(src.OriginY * 100 / sy);

            DexRect srcRect = new DexRect(0, 0, src.Width, src.Height);
            DexRect dstRect = new DexRect(0, 0, dst.Width, dst.Height);

            if (src.BitsPerPixel == 8)
            {
                ChunkyScale8(src, dst, in srcRect, in dstRect, flags);
                return;
            }

            if (src.BitsPerPixel == 16)
            {
                ChunkyScale16(src, dst, in srcRect, in dstRect, flags);
                return;
            }
        }

        // DexterGFX::ChunkyTint(unsigned int, DexCol const&, short)
        // Applies a constant-color tint on the chunky itself; strength is 0..100.
        internal void ChunkyTint(uint index, in DexCol color, short strength)
        {
            if ((uint)index >= (uint)_chunkies.Length) return;

            ChunkySurface? c = _chunkies[index];
            if (c == null) return;
            if (strength <= 0) return;

            int s = strength;
            if (s > 100) s = 100;

            // C++ called Blit(..., mode=4, color16, percent=(100-strength)).
            // Here: per-pixel blend dst with constant color using the same "percent" mapping as Blit16.
            short percent = (short)(100 - s);
            int destMod = GetDestModFromPercent(percent);
            int srcMod = 255 - destMod;

            if (c.BitsPerPixel == 8)
            {
                // 8-bit tint: map constant RGB to palette index and blend via trans table.
                byte tintIndex = FindClosestPaletteIndex(color.R, color.G, color.B);
                byte ck = _colourKey8;

                Span<byte> px = c.Pixels8;

                for (int i = 0; i < px.Length; i++)
                {
                    byte v = px[i];
                    if (v == ck) continue;

                    // Blend in palette space: use table (tint, dst) with a 50% table; then approximate strength by repeated application.
                    // Deterministic strength approximation: apply N times (N = strength/25, clamped 1..4).
                    int passes = (s + 24) / 25;
                    if (passes < 1) passes = 1;
                    if (passes > 4) passes = 4;

                    byte outV = v;
                    for (int p = 0; p < passes; p++)
                    {
                        outV = _transTable8[(tintIndex << 8) | outV];
                    }

                    px[i] = outV;
                }

                return;
            }

            if (c.BitsPerPixel == 16)
            {
                ushort ck = _colourKey16;
                ushort c16 = Pack16(color);

                Span<ushort> px = c.Pixels16;

                for (int i = 0; i < px.Length; i++)
                {
                    ushort v = px[i];
                    if (v == ck) continue;

                    px[i] = Blend16_ModulateDstSrc(v, c16, destMod, srcMod);
                }

                return;
            }
        }

        // DexterGFX::SetBrightnessLevel(float)
        // Managed port: stores brightness and forces palette table rebuild.
        // Note: The original calls ShowPalette(true); this port uses UpdatePaletteTables().
        internal void SetBrightnessLevel(float level)
        {
            _paletteBrightness = level - 0.25f;
            UpdatePaletteTables();
        }

        // DexterGFX::PaletteEntry(PalEntry*, unsigned char)
        // In this port, the palette lives inside DexterGFX; use SetPaletteEntry.

        internal void SetPaletteEntry(int index, byte r, byte g, byte b)
        {
            if ((uint)index >= 256u) throw new ArgumentOutOfRangeException(nameof(index));
            _paletteR[index] = r;
            _paletteG[index] = g;
            _paletteB[index] = b;
            UpdatePaletteTables();
        }

        // DexterGFX::PaletteEntryRed/Green/Blue(unsigned char)
        internal byte PaletteEntryRed(byte index)
        {
            if ((uint)index >= 256u) return 0;
            return _paletteR[index];
        }

        internal byte PaletteEntryGreen(byte index)
        {
            if ((uint)index >= 256u) return 0;
            return _paletteG[index];
        }

        internal byte PaletteEntryBlue(byte index)
        {
            if ((uint)index >= 256u) return 0;
            return _paletteB[index];
        }

        // DexterGFX::PaletteClear(PalEntry*, unsigned char, unsigned short)
        // Managed helper working on a packed palette buffer (RGBA, 4 bytes per entry).
        internal static void PaletteClear(Span<byte> paletteRgba, byte startIndex, ushort count)
        {
            if (paletteRgba.Length < 256 * 4) throw new ArgumentException("Palette buffer must be at least 1024 bytes.", nameof(paletteRgba));
            if (count == 0) return;

            int start = startIndex * 4;
            int bytes = checked(count * 4);

            if (start + bytes > paletteRgba.Length)
            {
                bytes = paletteRgba.Length - start;
                if (bytes < 0) return;
            }

            paletteRgba.Slice(start, bytes).Clear();
        }

        // DexterGFX::PaletteCopy(PalEntry*, PalEntry const*, unsigned char, unsigned short)
        internal static void PaletteCopy(Span<byte> dstPaletteRgba, ReadOnlySpan<byte> srcPaletteRgba, byte startIndex, ushort count)
        {
            if (dstPaletteRgba.Length < 256 * 4) throw new ArgumentException("Destination palette buffer must be at least 1024 bytes.", nameof(dstPaletteRgba));
            if (srcPaletteRgba.Length < 256 * 4) throw new ArgumentException("Source palette buffer must be at least 1024 bytes.", nameof(srcPaletteRgba));
            if (count == 0) return;

            int start = startIndex * 4;
            int bytes = checked(count * 4);

            if (start + bytes > dstPaletteRgba.Length) bytes = dstPaletteRgba.Length - start;
            if (start + bytes > srcPaletteRgba.Length) bytes = srcPaletteRgba.Length - start;
            if (bytes <= 0) return;

            srcPaletteRgba.Slice(start, bytes).CopyTo(dstPaletteRgba.Slice(start, bytes));
        }

        // DexterGFX::GetDexColRGB(DexCol&, unsigned char&, unsigned char&, unsigned char&)
        // The decompile shows a function pointer call; in this port, Unpack16 is the source of truth.
        internal void GetDexColRGB(ushort color16, out byte r, out byte g, out byte b)
        {
            Unpack16(color16, out int rr, out int gg, out int bb);
            r = (byte)rr;
            g = (byte)gg;
            b = (byte)bb;
        }

        // DexterGFX::AllocatePaletteLookup()
        // Original allocates a lookup buffer depending on bit depth.
        // Managed port uses fixed managed tables; allocation is always "successful".
        internal uint AllocatePaletteLookup()
        {
            if (_paletteLookup32 == null) _paletteLookup32 = new uint[256];
            if (_paletteLookup16 == null) _paletteLookup16 = new ushort[256];

            RebuildPaletteLookups();
            return 1;
        }

        private uint[]? _paletteLookup32;
        private ushort[]? _paletteLookup16;

        private void RebuildPaletteLookups()
        {
            if (_paletteLookup32 == null || _paletteLookup16 == null) return;

            for (int i = 0; i < 256; i++)
            {
                byte r = ApplyBrightness(_paletteR[i]);
                byte g = ApplyBrightness(_paletteG[i]);
                byte b = ApplyBrightness(_paletteB[i]);

                _paletteLookup32[i] = 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;
                _paletteLookup16[i] = Pack16From8(r, g, b);
            }
        }

        private byte ApplyBrightness(byte v)
        {
            // C++ uses Brightness = level - 0.25; exact curve is unknown from the decompile alone.
            // Deterministic linear model: v' = v * (1 + brightness).
            float scale = 1.0f + _paletteBrightness;
            float outV = v * scale;

            if (outV < 0) outV = 0;
            if (outV > 255) outV = 255;

            return (byte)outV;
        }

        // --------------------------------------------------------------------
        // DexterGFX::Make32BitPixel(unsigned char, unsigned char, unsigned char)
        // Returns 0x00RRGGBB (no alpha), matching the decompile.
        // --------------------------------------------------------------------
        internal static uint Make32BitPixel(byte r, byte g, byte b)
        {
            return ((uint)r << 16) | ((uint)g << 8) | b;
        }

        // --------------------------------------------------------------------
        // DexterGFX::PaletteDifference(PalEntry const*, PalEntry const*)
        // RGB squared-distance.
        // --------------------------------------------------------------------
        internal static int PaletteDifference(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
        {
            int dr = r1 - r2;
            int dg = g1 - g2;
            int db = b1 - b2;
            return db * db + dg * dg + dr * dr;
        }

        // --------------------------------------------------------------------
        // DexterGFX::SetColourKey(unsigned int, unsigned int, unsigned int)
        // Uses active render bit depth (Target.BitsPerPixel) like the engine does.
        // --------------------------------------------------------------------
        internal void SetColourKey(uint r, uint g, uint b)
        {
            ChunkySurface t = Target;

            if (t.BitsPerPixel == 8)
            {
                byte idx = FindClosestPaletteIndex((byte)r, (byte)g, (byte)b);
                _colourKey8 = idx;
                return;
            }

            _colourKey16 = Pack16From8((byte)r, (byte)g, (byte)b);
        }

        // --------------------------------------------------------------------
        // DexterGFX::GetColourKey()
        // C++ returns a 16-bit value; for 8-bit the key is stored in the low byte.
        // --------------------------------------------------------------------
        internal ushort GetColourKey()
        {
            ChunkySurface t = Target;

            if (t.BitsPerPixel == 8)
            {
                return _colourKey8;
            }

            return _colourKey16;
        }

        // --------------------------------------------------------------------
        // DexterGFX::SetColourKey(DexCol const&)
        // In this managed port, DexCol is RGB; choose key based on current target bpp.
        // --------------------------------------------------------------------
        internal void SetColourKey(in DexCol color)
        {
            ChunkySurface t = Target;

            if (t.BitsPerPixel == 8)
            {
                _colourKey8 = FindClosestPaletteIndex(color.R, color.G, color.B);
                return;
            }

            _colourKey16 = Pack16From8(color.R, color.G, color.B);
        }

        // --------------------------------------------------------------------
        // DexterGFX::ShowPalette(bool)
        // Managed port: Update translucency tables deterministically.
        // The original also built a per-entry change list and could call OSshowpalette().
        // That platform hook does not exist in this C# port, so only tables are rebuilt.
        // --------------------------------------------------------------------
        internal void ShowPalette(bool force)
        {
            // If brightness-adjusted lookups are implemented (from GFX4), rebuild them there.
            // Otherwise, keep the existing deterministic table rebuild.
            UpdatePaletteTables();
        }

        // --------------------------------------------------------------------
        // DexterGFX::BackUpPalette()
        // --------------------------------------------------------------------
        internal void BackUpPalette()
        {
            if (_backupPaletteR == null) _backupPaletteR = new byte[256];
            if (_backupPaletteG == null) _backupPaletteG = new byte[256];
            if (_backupPaletteB == null) _backupPaletteB = new byte[256];

            Buffer.BlockCopy(_paletteR, 0, _backupPaletteR, 0, 256);
            Buffer.BlockCopy(_paletteG, 0, _backupPaletteG, 0, 256);
            Buffer.BlockCopy(_paletteB, 0, _backupPaletteB, 0, 256);
        }

        // --------------------------------------------------------------------
        // DexterGFX::RestorePalette()
        // --------------------------------------------------------------------
        internal void RestorePalette()
        {
            if (_backupPaletteR == null || _backupPaletteG == null || _backupPaletteB == null)
            {
                return;
            }

            Buffer.BlockCopy(_backupPaletteR, 0, _paletteR, 0, 256);
            Buffer.BlockCopy(_backupPaletteG, 0, _paletteG, 0, 256);
            Buffer.BlockCopy(_backupPaletteB, 0, _paletteB, 0, 256);

            UpdatePaletteTables();
        }

        // --------------------------------------------------------------------
        // DexterGFX::SetColour(DexCol&, unsigned int, unsigned int, unsigned int, unsigned char)
        // C++ writes a pixel value into DexCol (which is bit-depth dependent).
        // This managed port uses DexCol as RGB; so this function becomes a simple RGB setter.
        // The last parameter is kept for call-compatibility (0=exact, 1=close-table in C++).
        // --------------------------------------------------------------------
        internal static DexCol SetColour(uint r, uint g, uint b, byte mode)
        {
            return new DexCol((byte)r, (byte)g, (byte)b, 255);
        }

        // --------------------------------------------------------------------
        // DexterGFX::Make8BitPixel(unsigned char, unsigned char, unsigned char, unsigned char)
        // mode==1 uses a 32x32x32 "close table" (0x8000 entries) like the original.
        // --------------------------------------------------------------------
        internal byte Make8BitPixel(byte r, byte g, byte b, byte mode)
        {
            if (mode == 1)
            {
                EnsureCloseTable8();
                int idx = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);
                return _closeTable8![idx];
            }

            return FindClosestPaletteIndex(r, g, b);
        }

        private void EnsureCloseTable8()
        {
            if (_closeTable8 != null) return;

            _closeTable8 = new byte[0x8000];

            // Build mapping for quantized 5-bit RGB (0..31) scaled by 8 (0,8,16,...,248).
            for (int rr = 0; rr < 32; rr++)
            {
                byte r = (byte)(rr * 8);

                for (int gg = 0; gg < 32; gg++)
                {
                    byte g = (byte)(gg * 8);

                    for (int bb = 0; bb < 32; bb++)
                    {
                        byte b = (byte)(bb * 8);
                        int idx = (rr << 10) | (gg << 5) | bb;
                        _closeTable8[idx] = FindClosestPaletteIndex(r, g, b);
                    }
                }
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::Colour(unsigned int, unsigned int, unsigned int, unsigned char)
        // Returns a bit-depth dependent pixel value:
        // - 8-bit: palette index in low byte
        // - 16-bit: packed 16-bit pixel
        // --------------------------------------------------------------------
        internal ushort Colour(uint r, uint g, uint b, byte mode)
        {
            ChunkySurface t = Target;

            if (t.BitsPerPixel == 8)
            {
                return Make8BitPixel((byte)r, (byte)g, (byte)b, mode);
            }

            return Pack16From8((byte)r, (byte)g, (byte)b);
        }

        // --------------------------------------------------------------------
        // DexterGFX::SetPaletteEntry(unsigned int, unsigned int, unsigned int, unsigned int)
        // Applies brightness factor like the original.
        // --------------------------------------------------------------------
        internal void SetPaletteEntry(uint index, uint r, uint g, uint b)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, 256u);

            _paletteR[index] = ClampToByte((int)(r * _paletteBrightness));
            _paletteG[index] = ClampToByte((int)(g * _paletteBrightness));
            _paletteB[index] = ClampToByte((int)(b * _paletteBrightness));

            UpdatePaletteTables();
        }

        // --------------------------------------------------------------------
        // DexterGFX::SetPaletteEntryRed/Green/Blue(unsigned char, unsigned char)
        // --------------------------------------------------------------------
        internal void SetPaletteEntryRed(byte index, byte value)
        {
            if (index >= 256) return;
            _paletteR[index] = ClampToByte((int)(value * _paletteBrightness));
            UpdatePaletteTables();
        }

        internal void SetPaletteEntryGreen(byte index, byte value)
        {
            if (index >= 256) return;
            _paletteG[index] = ClampToByte((int)(value * _paletteBrightness));
            UpdatePaletteTables();
        }

        internal void SetPaletteEntryBlue(byte index, byte value)
        {
            if (index >= 256) return;
            _paletteB[index] = ClampToByte((int)(value * _paletteBrightness));
            UpdatePaletteTables();
        }

        private static byte ClampToByte(int value)
        {
            return (byte)Math.Clamp(value, 0, 255);
        }

        // --------------------------------------------------------------------
        // DexterGFX::SetPixelFormat(unsigned char)
        // Managed port: only the 16-bit 565/555 choice is relevant for Pack/Unpack.
        // The original supports many formats; here it's reduced deterministically.
        // --------------------------------------------------------------------
        internal void SetPixelFormat(byte systemPixelFormat)
        {
            // Deterministic mapping:
            // - 0 or 5 => RGB565 (common default)
            // - 1      => RGB555
            // Everything else => RGB565
            if (systemPixelFormat == 1)
            {
                _pixelFormat16 = PixelFormat16.Rgb555;
                _transparentMask16 = 0x3DEF; // 555 half-mask used by the original
            }
            else
            {
                _pixelFormat16 = PixelFormat16.Rgb565;
                _transparentMask16 = 0x7BEF; // 565 half-mask used by the original
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::SetPixelFormat(unsigned short, unsigned short, unsigned short, unsigned short)
        // Managed port: interpret (greenBits==5) as RGB555, else RGB565.
        // --------------------------------------------------------------------
        internal void SetPixelFormat(ushort redBits, ushort greenBits, ushort blueBits, ushort bytesPerPixel)
        {
            // Only 16-bit selection is needed by this managed renderer.
            if (bytesPerPixel == 2)
            {
                if (greenBits == 5)
                {
                    _pixelFormat16 = PixelFormat16.Rgb555;
                    _transparentMask16 = 0x3DEF;
                }
                else
                {
                    _pixelFormat16 = PixelFormat16.Rgb565;
                    _transparentMask16 = 0x7BEF;
                }
            }
            else
            {
                // For 8/24/32 in the original, the 16-bit pack/unpack still falls back to 565.
                _pixelFormat16 = PixelFormat16.Rgb565;
                _transparentMask16 = 0x7BEF;
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::Prepare16BitBlend(unsigned short, short)
        // Prepares blend state for the RasterBlit16_* routines.
        // percent is expected as 0..100 (like the other decompiles in your project).
        // --------------------------------------------------------------------
        internal void Prepare16BitBlend(ushort tintColor16, short percent)
        {
            // Fast blend triggers in the original for percent in [48..52] and when TransparentMask16 != 0.
            bool nearHalf = (ushort)(percent - 48) < 5;

            if (_transparentMask16 != 0 && nearHalf)
            {
                _fastBlendPixel = (ushort)(_transparentMask16 & (tintColor16 >> 1));
            }

            _fastBlendMode = _transparentMask16 != 0 && nearHalf;

            int dm = (int)(percent * 2.55f);
            if (dm < 0) dm = 0;
            if (dm > 255) dm = 255;

            _destMod = (ushort)dm;
            _sourceMod = (ushort)(255 - dm);

            Unpack16(tintColor16, out int r8, out int g8, out int b8);

            _tintRed = (ushort)(r8 * _sourceMod);
            _tintGreen = (ushort)(g8 * _sourceMod);
            _tintBlue = (ushort)(b8 * _sourceMod);
        }

        // --------------------------------------------------------------------
        // DexterGFX::RasterBlit16_TransSolid_Generic(unsigned short*, unsigned short*, unsigned int)
        // dstPixels = param_1, srcPixels = param_2
        // --------------------------------------------------------------------
        internal void RasterBlit16_TransSolid_Generic(Span<ushort> dstPixels, ReadOnlySpan<ushort> srcPixels, uint count)
        {
            int n = (int)count;
            if (n <= 0) return;

            int destMod = _destMod;
            int srcMod = _sourceMod;

            for (int i = 0; i < n; i++)
            {
                ushort s = srcPixels[i];
                ushort d = dstPixels[i];

                if (s == d) continue;

                Unpack16(s, out int sr, out int sg, out int sb);
                Unpack16(d, out int dr, out int dg, out int db);

                int rr = (dr * destMod + sr * srcMod) >> 8;
                int gg = (dg * destMod + sg * srcMod) >> 8;
                int bb = (db * destMod + sb * srcMod) >> 8;

                dstPixels[i] = Pack16From8((byte)rr, (byte)gg, (byte)bb);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::RasterBlit16_TransCookie_Generic(unsigned short*, unsigned short*, unsigned int, unsigned short)
        // Skips src pixels equal to colorKey16.
        // --------------------------------------------------------------------
        internal void RasterBlit16_TransCookie_Generic(Span<ushort> dstPixels, ReadOnlySpan<ushort> srcPixels, uint count, ushort colorKey16)
        {
            int n = (int)count;
            if (n <= 0) return;

            int destMod = _destMod;
            int srcMod = _sourceMod;

            for (int i = 0; i < n; i++)
            {
                ushort s = srcPixels[i];
                if (s == colorKey16) continue;

                ushort d = dstPixels[i];
                if (s == d) continue;

                Unpack16(s, out int sr, out int sg, out int sb);
                Unpack16(d, out int dr, out int dg, out int db);

                int rr = (dr * destMod + sr * srcMod) >> 8;
                int gg = (dg * destMod + sg * srcMod) >> 8;
                int bb = (db * destMod + sb * srcMod) >> 8;

                dstPixels[i] = Pack16From8((byte)rr, (byte)gg, (byte)bb);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::RasterBlit16_ColourCookie_Generic(unsigned short*, unsigned short*, unsigned int, unsigned short)
        // Uses prepared tint (Tint* + DestMod) and skips pixels where mask==colorKey16.
        // The srcPixels here are used as a mask/control buffer in the decompile.
        // --------------------------------------------------------------------
        internal void RasterBlit16_ColourCookie_Generic(Span<ushort> dstPixels, ReadOnlySpan<ushort> maskPixels, uint count, ushort cookie)
        {
            int n = (int)count;
            if (n <= 0) return;

            int destMod = _destMod;

            for (int i = 0; i < n; i++)
            {
                if (maskPixels[i] == cookie) continue;

                ushort d = dstPixels[i];
                Unpack16(d, out int dr, out int dg, out int db);

                int rr = (_tintRed + dr * destMod) >> 8;
                int gg = (_tintGreen + dg * destMod) >> 8;
                int bb = (_tintBlue + db * destMod) >> 8;

                dstPixels[i] = Pack16From8((byte)rr, (byte)gg, (byte)bb);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::RasterBlit16_ColourSolid_Generic(unsigned short*, unsigned int)
        // Applies prepared tint to all pixels.
        // --------------------------------------------------------------------
        internal void RasterBlit16_ColourSolid_Generic(Span<ushort> dstPixels, uint count)
        {
            int n = (int)count;
            if (n <= 0) return;

            int destMod = _destMod;

            for (int i = 0; i < n; i++)
            {
                ushort d = dstPixels[i];
                Unpack16(d, out int dr, out int dg, out int db);

                int rr = (_tintRed + dr * destMod) >> 8;
                int gg = (_tintGreen + dg * destMod) >> 8;
                int bb = (_tintBlue + db * destMod) >> 8;

                dstPixels[i] = Pack16From8((byte)rr, (byte)gg, (byte)bb);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::RasterBlit16_TransSolid_RGB565(unsigned short*, unsigned short*, unsigned int)
        // Optimized RGB565 version (mirrors the decompile's math).
        // --------------------------------------------------------------------
        internal void RasterBlit16_TransSolid_RGB565(Span<ushort> dstPixels, ReadOnlySpan<ushort> srcPixels, uint count)
        {
            int n = (int)count;
            if (n <= 0) return;

            if (_fastBlendMode)
            {
                ushort mask = _transparentMask16;

                for (int i = 0; i < n; i++)
                {
                    ushort s = srcPixels[i];
                    ushort d = dstPixels[i];
                    if (s == d) continue;

                    dstPixels[i] = (ushort)(((d >> 1) & mask) + ((s >> 1) & mask));
                }

                return;
            }

            uint srcMod = _sourceMod;
            uint dstMod = _destMod;

            for (int i = 0; i < n; i++)
            {
                ushort s = srcPixels[i];
                ushort d = dstPixels[i];
                if (s == d) continue;

                // 565 channels (scaled like the decompile):
                // Blue: 5-bit from low bits
                uint b = (uint)(((d & 0x1F) * 8 * dstMod + (s & 0x1F) * 8 * srcMod) >> 11);

                // Red: top 5 bits stored at bits 11..15 => (d >> 8 & 0xF8) is aligned to 0..248
                uint rPart = (uint)(((d >> 8) & 0xF8) * dstMod + ((s >> 8) & 0xF8) * srcMod) & 0xF800;

                // Green: 6-bit stored at bits 5..10 => (d >> 3 & 0xFC) aligned
                uint gPart = (uint)(((((d >> 3) & 0xFC) * dstMod + ((s >> 3) & 0xFC) * srcMod) >> 5) & 0x7E0);

                dstPixels[i] = (ushort)(b + rPart + gPart);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::RasterBlit16_TransCookie_RGB565(unsigned short*, unsigned short*, unsigned int, unsigned short)
        // Optimized RGB565 version with color-key.
        // --------------------------------------------------------------------
        internal void RasterBlit16_TransCookie_RGB565(Span<ushort> dstPixels, ReadOnlySpan<ushort> srcPixels, uint count, ushort colorKey16)
        {
            int n = (int)count;
            if (n <= 0) return;

            if (_fastBlendMode)
            {
                ushort mask = _transparentMask16;

                for (int i = 0; i < n; i++)
                {
                    ushort s = srcPixels[i];
                    if (s == colorKey16) continue;

                    ushort d = dstPixels[i];
                    if (s == d) continue;

                    dstPixels[i] = (ushort)(((d >> 1) & mask) + ((s >> 1) & mask));
                }

                return;
            }

            uint srcMod = _sourceMod;
            uint dstMod = _destMod;

            for (int i = 0; i < n; i++)
            {
                ushort s = srcPixels[i];
                if (s == colorKey16) continue;

                ushort d = dstPixels[i];
                if (s == d) continue;

                uint b = (uint)(((d & 0x1F) * 8 * dstMod + (s & 0x1F) * 8 * srcMod) >> 11);
                uint rPart = (uint)(((d >> 8) & 0xF8) * dstMod + ((s >> 8) & 0xF8) * srcMod) & 0xF800;
                uint gPart = (uint)(((((d >> 3) & 0xFC) * dstMod + ((s >> 3) & 0xFC) * srcMod) >> 5) & 0x7E0);

                dstPixels[i] = (ushort)(b + rPart + gPart);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::RasterBlit16_ColourCookie_RGB565(unsigned short*, unsigned short*, unsigned int, unsigned short)
        // Optimized RGB565 tinting with cookie/mask buffer.
        // --------------------------------------------------------------------
        internal void RasterBlit16_ColourCookie_RGB565(Span<ushort> dstPixels, ReadOnlySpan<ushort> maskPixels, uint count, ushort cookie)
        {
            int n = (int)count;
            if (n <= 0) return;

            if (_fastBlendMode)
            {
                ushort mask = _transparentMask16;
                ushort fastPix = _fastBlendPixel;

                int i = 0;

                // Unrolled by 2 like the decompile (deterministic).
                for (; i + 1 < n; i += 2)
                {
                    if (maskPixels[i] != cookie)
                    {
                        ushort d0 = dstPixels[i];
                        dstPixels[i] = (ushort)(((d0 >> 1) & mask) + fastPix);
                    }

                    if (maskPixels[i + 1] != cookie)
                    {
                        ushort d1 = dstPixels[i + 1];
                        dstPixels[i + 1] = (ushort)(((d1 >> 1) & mask) + fastPix);
                    }
                }

                if (i < n && maskPixels[i] != cookie)
                {
                    ushort d = dstPixels[i];
                    dstPixels[i] = (ushort)(((d >> 1) & mask) + fastPix);
                }

                return;
            }

            uint dstMod = _destMod;
            uint tG = _tintGreen;
            uint tB = _tintBlue;
            uint tR = _tintRed;

            for (int i = 0; i < n; i++)
            {
                if (maskPixels[i] == cookie) continue;

                ushort d = dstPixels[i];

                uint b = (uint)(((d & 0x1F) * 8 * dstMod + tB) >> 11);
                uint gPart = (uint)(((((d >> 3) & 0xFC) * dstMod + tG) >> 5) & 0x7E0);
                uint rPart = (uint)(((d >> 8) & 0xF8) * dstMod + tR) & 0xF800;

                dstPixels[i] = (ushort)(b + gPart + rPart);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::RasterBlit16_ColourSolid_RGB565(unsigned short*, unsigned int)
        // --------------------------------------------------------------------
        internal void RasterBlit16_ColourSolid_RGB565(Span<ushort> dstPixels, uint count)
        {
            int n = (int)count;
            if (n <= 0) return;

            if (_fastBlendMode)
            {
                ushort mask = _transparentMask16;
                ushort fastPix = _fastBlendPixel;

                for (int i = 0; i < n; i++)
                {
                    ushort d = dstPixels[i];
                    dstPixels[i] = (ushort)(((d >> 1) & mask) + fastPix);
                }

                return;
            }

            uint dstMod = _destMod;
            uint tR = _tintRed;
            uint tG = _tintGreen;
            uint tB = _tintBlue;

            for (int i = 0; i < n; i++)
            {
                ushort d = dstPixels[i];

                uint b = (uint)(((d & 0x1F) * 8u * dstMod + tB) >> 11);
                uint g = (uint)(((((d >> 3) & 0xFC) * dstMod + tG) >> 5) & 0x7E0);
                uint r = (uint)((((d >> 8) & 0xF8) * dstMod + tR) & 0xF800);

                dstPixels[i] = (ushort)(b + g + r);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::RasterBlit16_TransSolid_RGB555(unsigned short*, unsigned short*, unsigned int)
        // --------------------------------------------------------------------
        internal void RasterBlit16_TransSolid_RGB555(Span<ushort> dstPixels, ReadOnlySpan<ushort> srcPixels, uint count)
        {
            int n = (int)count;
            if (n <= 0) return;

            if (_fastBlendMode)
            {
                ushort mask = _transparentMask16;

                for (int i = 0; i < n; i++)
                {
                    ushort s = srcPixels[i];
                    ushort d = dstPixels[i];
                    if (s == d) continue;

                    dstPixels[i] = (ushort)(((d >> 1) & mask) + ((s >> 1) & mask));
                }

                return;
            }

            uint srcMod = _sourceMod;
            uint dstMod = _destMod;

            for (int i = 0; i < n; i++)
            {
                ushort s = srcPixels[i];
                ushort d = dstPixels[i];
                if (s == d) continue;

                // RGB555 math (matches the decompile)
                uint b = (uint)(((d & 0x1F) * 8u * dstMod + (s & 0x1F) * 8u * srcMod) >> 11);
                uint g = (uint)(((((d >> 2) & 0xF8) * dstMod + ((s >> 2) & 0xF8) * srcMod) >> 6) & 0x3E0);
                uint r = (uint)(((((d >> 7) & 0xF8) * dstMod + ((s >> 7) & 0xF8) * srcMod) >> 1) & 0x7C00);

                dstPixels[i] = (ushort)(b + g + r);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::RasterBlit16_TransCookie_RGB555(unsigned short*, unsigned short*, unsigned int, unsigned short)
        // --------------------------------------------------------------------
        internal void RasterBlit16_TransCookie_RGB555(Span<ushort> dstPixels, ReadOnlySpan<ushort> srcPixels, uint count, ushort colorKey16)
        {
            int n = (int)count;
            if (n <= 0) return;

            if (_fastBlendMode)
            {
                ushort mask = _transparentMask16;

                for (int i = 0; i < n; i++)
                {
                    ushort s = srcPixels[i];
                    if (s == colorKey16) continue;

                    ushort d = dstPixels[i];
                    if (s == d) continue;

                    dstPixels[i] = (ushort)(((d >> 1) & mask) + ((s >> 1) & mask));
                }

                return;
            }

            uint srcMod = _sourceMod;
            uint dstMod = _destMod;

            for (int i = 0; i < n; i++)
            {
                ushort s = srcPixels[i];
                if (s == colorKey16) continue;

                ushort d = dstPixels[i];
                if (s == d) continue;

                uint b = (uint)(((d & 0x1F) * 8u * dstMod + (s & 0x1F) * 8u * srcMod) >> 11);
                uint g = (uint)(((((d >> 2) & 0xF8) * dstMod + ((s >> 2) & 0xF8) * srcMod) >> 6) & 0x3E0);
                uint r = (uint)(((((d >> 7) & 0xF8) * dstMod + ((s >> 7) & 0xF8) * srcMod) >> 1) & 0x7C00);

                dstPixels[i] = (ushort)(b + g + r);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::RasterBlit16_ColourCookie_RGB555(unsigned short*, unsigned short*, unsigned int, unsigned short)
        // maskPixels is a cookie/mask buffer; cookie pixels are skipped.
        // --------------------------------------------------------------------
        internal void RasterBlit16_ColourCookie_RGB555(Span<ushort> dstPixels, ReadOnlySpan<ushort> maskPixels, uint count, ushort cookie)
        {
            int n = (int)count;
            if (n <= 0) return;

            if (_fastBlendMode)
            {
                ushort mask = _transparentMask16;
                ushort fastPix = _fastBlendPixel;

                int i = 0;
                for (; i + 1 < n; i += 2)
                {
                    if (maskPixels[i] != cookie)
                    {
                        ushort d0 = dstPixels[i];
                        dstPixels[i] = (ushort)(((d0 >> 1) & mask) + fastPix);
                    }

                    if (maskPixels[i + 1] != cookie)
                    {
                        ushort d1 = dstPixels[i + 1];
                        dstPixels[i + 1] = (ushort)(((d1 >> 1) & mask) + fastPix);
                    }
                }

                if (i < n && maskPixels[i] != cookie)
                {
                    ushort d = dstPixels[i];
                    dstPixels[i] = (ushort)(((d >> 1) & mask) + fastPix);
                }

                return;
            }

            uint dstMod = _destMod;
            uint tR = _tintRed;
            uint tG = _tintGreen;
            uint tB = _tintBlue;

            for (int i = 0; i < n; i++)
            {
                if (maskPixels[i] == cookie) continue;

                ushort d = dstPixels[i];

                uint b = (uint)(((d & 0x1F) * 8u * dstMod + tB) >> 11);
                uint g = (uint)(((((d >> 2) & 0xF8) * dstMod + tG) >> 6) & 0x3E0);
                uint r = (uint)(((((d >> 7) & 0xF8) * dstMod + tR) >> 1) & 0x7C00);

                dstPixels[i] = (ushort)(b + g + r);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::RasterBlit16_ColourSolid_RGB555(unsigned short*, unsigned int)
        // --------------------------------------------------------------------
        internal void RasterBlit16_ColourSolid_RGB555(Span<ushort> dstPixels, uint count)
        {
            int n = (int)count;
            if (n <= 0) return;

            if (_fastBlendMode)
            {
                ushort mask = _transparentMask16;
                ushort fastPix = _fastBlendPixel;

                for (int i = 0; i < n; i++)
                {
                    ushort d = dstPixels[i];
                    dstPixels[i] = (ushort)(((d >> 1) & mask) + fastPix);
                }

                return;
            }

            uint dstMod = _destMod;
            uint tR = _tintRed;
            uint tG = _tintGreen;
            uint tB = _tintBlue;

            for (int i = 0; i < n; i++)
            {
                ushort d = dstPixels[i];

                uint b = (uint)(((d & 0x1F) * 8u * dstMod + tB) >> 11);
                uint g = (uint)(((((d >> 2) & 0xF8) * dstMod + tG) >> 6) & 0x3E0);
                uint r = (uint)(((((d >> 7) & 0xF8) * dstMod + tR) >> 1) & 0x7C00);

                dstPixels[i] = (ushort)(b + g + r);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::RasterBlit16_AlphaColour_Generic(unsigned short*, unsigned char*, unsigned int, unsigned short, unsigned char, unsigned char, unsigned char, short)
        // Blends the destination pixel towards a constant RGB color based on per-pixel alpha mask.
        // Skips when (alpha - threshold) < 4 or >= 0xFC (matches the decompile guards).
        // cookie16 marks pixels in dst to be skipped (param_4).
        // --------------------------------------------------------------------
        internal void RasterBlit16_AlphaColour_Generic(Span<ushort> dstPixels, ReadOnlySpan<byte> alphaMask, uint count, ushort cookie16, byte r, byte g, byte b, short threshold)
        {
            int n = (int)count;
            if (n <= 0) return;

            for (int i = 0; i < n; i++)
            {
                ushort d = dstPixels[i];
                if (d == cookie16) continue;

                int a = alphaMask[i] - threshold;
                if (a < 4 || a >= 0xFC) continue;

                dstPixels[i] = AlphaMix16_Constant(d, (byte)a, r, g, b);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::RasterBlit16_AlphaColour_RGB565(...)
        // Same behavior as Generic; math is equivalent but kept as a separate entry point.
        // --------------------------------------------------------------------
        internal void RasterBlit16_AlphaColour_RGB565(Span<ushort> dstPixels, ReadOnlySpan<byte> alphaMask, uint count, ushort cookie16, byte r, byte g, byte b, short threshold)
        {
            RasterBlit16_AlphaColour_Generic(dstPixels, alphaMask, count, cookie16, r, g, b, threshold);
        }

        // --------------------------------------------------------------------
        // DexterGFX::RasterBlit16_AlphaColour_RGB555(...)
        // Same behavior as Generic; math is equivalent but kept as a separate entry point.
        // --------------------------------------------------------------------
        internal void RasterBlit16_AlphaColour_RGB555(Span<ushort> dstPixels, ReadOnlySpan<byte> alphaMask, uint count, ushort cookie16, byte r, byte g, byte b, short threshold)
        {
            RasterBlit16_AlphaColour_Generic(dstPixels, alphaMask, count, cookie16, r, g, b, threshold);
        }

        // --------------------------------------------------------------------
        // DexterGFX::RasterBlit16_AlphaBlend_Generic(unsigned short*, unsigned short*, unsigned char*, unsigned int, short)
        // Blends src into dst using alphaMask per pixel (minus threshold).
        // Skips when (alpha - threshold) < 4 or >= 0xFC and when src==dst.
        // --------------------------------------------------------------------
        internal void RasterBlit16_AlphaBlend_Generic(Span<ushort> dstPixels, ReadOnlySpan<ushort> srcPixels, ReadOnlySpan<byte> alphaMask, uint count, short threshold)
        {
            int n = (int)count;
            if (n <= 0) return;

            for (int i = 0; i < n; i++)
            {
                ushort s = srcPixels[i];
                ushort d = dstPixels[i];
                if (s == d) continue;

                int a = alphaMask[i] - threshold;
                if (a < 4 || a >= 0xFC) continue;

                dstPixels[i] = AlphaMix16(d, s, (byte)a);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::RasterBlit16_AlphaBlend_RGB565(...)
        // Separate entry point; in this managed port it maps to the generic implementation.
        // --------------------------------------------------------------------
        internal void RasterBlit16_AlphaBlend_RGB565(Span<ushort> dstPixels, ReadOnlySpan<ushort> srcPixels, ReadOnlySpan<byte> alphaMask, uint count, short threshold)
        {
            RasterBlit16_AlphaBlend_Generic(dstPixels, srcPixels, alphaMask, count, threshold);
        }

        // --------------------------------------------------------------------
        // DexterGFX::RasterBlit16_AlphaBlend_RGB555(...)
        // Separate entry point; in this managed port it maps to the generic implementation.
        // --------------------------------------------------------------------
        internal void RasterBlit16_AlphaBlend_RGB555(Span<ushort> dstPixels, ReadOnlySpan<ushort> srcPixels, ReadOnlySpan<byte> alphaMask, uint count, short threshold)
        {
            RasterBlit16_AlphaBlend_Generic(dstPixels, srcPixels, alphaMask, count, threshold);
        }

        // --------------------------------------------------------------------
        // Helper: alpha blend between two 16-bit pixels in current pixel format.
        // a = 0..255, where 255 means full src.
        // --------------------------------------------------------------------
        private ushort AlphaMix16(ushort dst, ushort src, byte a)
        {
            int inv = 255 - a;

            Unpack16(src, out int sr, out int sg, out int sb);
            Unpack16(dst, out int dr, out int dg, out int db);

            int rr = (sr * a + dr * inv) >> 8;
            int gg = (sg * a + dg * inv) >> 8;
            int bb = (sb * a + db * inv) >> 8;

            return Pack16From8((byte)rr, (byte)gg, (byte)bb);
        }

        // Helper: alpha blend dst towards a constant RGB.
        private ushort AlphaMix16_Constant(ushort dst, byte a, byte r, byte g, byte b)
        {
            int inv = 255 - a;

            Unpack16(dst, out int dr, out int dg, out int db);

            int rr = (r * a + dr * inv) >> 8;
            int gg = (g * a + dg * inv) >> 8;
            int bb = (b * a + db * inv) >> 8;

            return Pack16From8((byte)rr, (byte)gg, (byte)bb);
        }

        // --------------------------------------------------------------------
        // DexterGFX::DexterGFX() (decompiled default constructor)
        //
        // In C# the constructor already exists: DexterGFX(int maxChunkies).
        // The decompiled ctor sets many legacy fields (window size, display mode, dirty list pointers).
        // Those fields do not exist in the current managed port, so the equivalent initialization is:
        // - _activeChunkyIndex = -1
        // - _target = null
        // - _pixelFormat16 default (Rgb565)
        // - InitColour()
        // Which is already done in DexterGFX(int maxChunkies).
        // --------------------------------------------------------------------


        // --------------------------------------------------------------------
        // DexterGFX::Blit(unsigned int, int, int, short, DexCol, short)
        // Managed wrapper around Blit8/Blit16 and a "solid colour mask" mode (0x40).
        // mode bits (best-effort):
        // - bit1 (0x02): use color key
        // - bit2 (0x04): alpha blend
        // - bit6 (0x40): treat source as mask and blend a constant colour into destination
        // --------------------------------------------------------------------
        internal void Blit(uint chunkyId, int x, int y, short mode, DexCol color, short percent)
        {
            ChunkySurface? src = ChunkyAddress(chunkyId);
            if (src == null) return;

            ChunkySurface dst = Target;

            DexRect srcRect = new(0, 0, src.Width, src.Height);

            bool useColorKey = (mode & 0x02) != 0;
            bool alphaBlend = (mode & 0x04) != 0;
            bool colourMask = (mode & 0x40) != 0;

            if (dst.BitsPerPixel == 8)
            {
                ReadOnlySpan<byte> spx = src.Pixels8;

                if (!colourMask)
                {
                    byte alphaMode = alphaBlend ? (byte)1 : (byte)0;
                    Blit8(spx, src.Width, src.Height, dst, x - src.OriginX, y - src.OriginY, srcRect, useColorKey, _colourKey8, alphaMode);
                    return;
                }

                // Colour-mask in 8-bit: treat any non-colourKey source pixel as "ink" and blend towards tint.
                byte tint = FindClosestPaletteIndex(color.R, color.G, color.B);
                Apply8BitMaskTint(spx, src.Width, src.Height, dst, x - src.OriginX, y - src.OriginY, useColorKey ? _colourKey8 : (byte)255, tint, percent);
                return;
            }

            if (dst.BitsPerPixel == 16)
            {
                ReadOnlySpan<ushort> spx = src.Pixels16;

                if (!colourMask)
                {
                    byte a = alphaBlend ? AlphaFromPercent(percent) : (byte)255;
                    Blit16(spx, src.Width, src.Height, dst, x - src.OriginX, y - src.OriginY, srcRect, useColorKey, _colourKey16, alphaBlend, a);
                    return;
                }

                ushort tint16 = Pack16From8(color.R, color.G, color.B);
                Apply16BitMaskTint(spx, src.Width, src.Height, dst, x - src.OriginX, y - src.OriginY, useColorKey ? _colourKey16 : (ushort)0xFFFF, tint16, percent);
                return;
            }
        }

        // --------------------------------------------------------------------
        // Helpers (managed, deterministic)
        // --------------------------------------------------------------------

        private static byte AlphaFromPercent(short percent)
        {
            int p = percent;
            if (p < 0) p = 0;
            if (p > 100) p = 100;

            // C++ blend state uses DestMod = percent*2.55. Here alpha is "src amount".
            int destMod = (int)(p * 2.55f);
            if (destMod < 0) destMod = 0;
            if (destMod > 255) destMod = 255;

            int a = 255 - destMod;
            if (a < 0) a = 0;
            if (a > 255) a = 255;
            return (byte)a;
        }

        private void Apply16BitMaskTint(ReadOnlySpan<ushort> mask, int srcWidth, int srcHeight, ChunkySurface dst, int dstX, int dstY, ushort cookie, ushort tint, short percent)
        {
            if (dst.BitsPerPixel != 16) return;

            DexRect s = new DexRect(0, 0, srcWidth, srcHeight).ClampTo(srcWidth, srcHeight);
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

            int w = outRight - outLeft;
            int h = outBottom - outTop;
            if (w <= 0 || h <= 0) return;

            int sx0 = s.Left + shiftX;
            int sy0 = s.Top + shiftY;

            byte a = AlphaFromPercent(percent);
            Span<ushort> dp = dst.Pixels16;

            for (int yy = 0; yy < h; yy++)
            {
                int sy = sy0 + yy;
                int dy = outTop + yy;

                int sRow = sy * srcWidth;
                int dRow = dy * dst.Width;

                for (int xx = 0; xx < w; xx++)
                {
                    ushort m = mask[sRow + (sx0 + xx)];
                    if (m == cookie) continue;

                    int di = dRow + (outLeft + xx);
                    ushort d = dp[di];
                    dp[di] = Blend16(d, tint, a);
                }
            }
        }

        private void Apply8BitMaskTint(ReadOnlySpan<byte> mask, int srcWidth, int srcHeight, ChunkySurface dst, int dstX, int dstY, byte cookie, byte tintIndex, short percent)
        {
            if (dst.BitsPerPixel != 8) return;

            DexRect s = new DexRect(0, 0, srcWidth, srcHeight).ClampTo(srcWidth, srcHeight);
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

            int w = outRight - outLeft;
            int h = outBottom - outTop;
            if (w <= 0 || h <= 0) return;

            int sx0 = s.Left + shiftX;
            int sy0 = s.Top + shiftY;

            // 8-bit: approximate percent by repeated 50% trans blends.
            int p = percent;
            if (p < 0) p = 0;
            if (p > 100) p = 100;

            int passes = (p + 24) / 25;
            if (passes < 1) passes = 1;
            if (passes > 4) passes = 4;

            Span<byte> dp = dst.Pixels8;

            for (int yy = 0; yy < h; yy++)
            {
                int sy = sy0 + yy;
                int dy = outTop + yy;

                int sRow = sy * srcWidth;
                int dRow = dy * dst.Width;

                for (int xx = 0; xx < w; xx++)
                {
                    byte m = mask[sRow + (sx0 + xx)];
                    if (m == cookie) continue;

                    int di = dRow + (outLeft + xx);

                    byte v = dp[di];
                    for (int k = 0; k < passes; k++)
                    {
                        v = _transTable8[(tintIndex << 8) | v];
                    }

                    dp[di] = v;
                }
            }
        }

        private static byte ResolveFontBlitMode(uint flags, FontSlot slot)
        {
            // The decompile derives a mode byte from several flags; keep a deterministic mapping:
            // default = 2 (cookie / transparent copy)
            byte mode = 2;

            // If bit6 set in flags => mode=4 (translucent path used for special cases)
            if ((flags & 0x40u) != 0)
            {
                mode = 4;
            }

            // If bit5 set => force translucent draw (maps to Blit alpha path)
            if ((flags & 0x20u) == 0)
            {
                // Keep mode as resolved
            }

            // If bit4 set => keep default, else use stored font mode (slot.FontMode)
            if ((flags & 0x10u) == 0)
            {
                mode = slot.FontMode;
            }

            return mode;
        }

        // --------------------------------------------------------------------
        // Font slot: managed replacement for the 0x210-byte struct array
        // --------------------------------------------------------------------
        private sealed class FontSlot
        {
            internal bool Active;
            internal ushort CharCount;
            internal int ChunkyBaseId;

            internal byte FirstChar;

            internal short FixedAdvance;
            internal short GlyphHeight;

            internal byte FontMode;

            internal readonly ushort[] CharWidths;

            internal FontSlot()
            {
                Active = false;
                CharCount = 0;
                ChunkyBaseId = 0;
                FirstChar = 0;
                FixedAdvance = 0;
                GlyphHeight = 0;
                FontMode = 2; // default like typical cookie blit
                CharWidths = new ushort[256];
            }
        }

        // --------------------------------------------------------------------
        // OS hooks (implemented in later chunks, e.g. DexterGFX10.cxx port)
        // --------------------------------------------------------------------
        private bool GFXOSInit() => throw new NotImplementedException();
        private bool OSInitDisplay() => throw new NotImplementedException();
        private void CloseDisplay() => throw new NotImplementedException();
        private void ReleaseDisplay() => throw new NotImplementedException();
        private void RedrawDisplay() => throw new NotImplementedException();

        // --------------------------------------------------------------------
        // Constants used in original code
        // --------------------------------------------------------------------
        private static readonly DexCol COLOUR_ZERO = new(0, 0, 0);
        private static readonly DexCol COLOUR_BLACK = new(0, 0, 0);

        // Compatibility with existing code that tracked the current chunky id.
        private byte TargetChunkyID => (byte)(_activeChunkyIndex < 0 ? 0 : _activeChunkyIndex);

        public static object IO { get; private set; }


        // --------------------------------------------------------------------
        // DexterGFX::FontHeight()
        // --------------------------------------------------------------------
        internal short FontHeight()
        {
            if (_fonts == null) return 0;
            if (_usedFont >= (uint)_fonts.Length) return 0;

            FontSlot slot = _fonts[_usedFont];
            if (!slot.Active) return 0;

            return slot.GlyphHeight;
        }

        // --------------------------------------------------------------------
        // DexterGFX::Plot(unsigned short, unsigned short, DexCol const&)
        // C++ writes palette index in 8-bit, and 16-bit pixel in 16-bit.
        // Managed port maps DexCol (RGB) -> palette/16-bit.
        // --------------------------------------------------------------------
        internal void Plot(ushort x, ushort y, in DexCol color)
        {
            ChunkySurface t = Target;

            if (x >= (uint)t.Width || y >= (uint)t.Height)
            {
                return;
            }

            int idx = y * t.Width + x;

            if (t.BitsPerPixel == 8)
            {
                byte p = FindClosestPaletteIndex(color.R, color.G, color.B);
                t.Pixels8[idx] = p;
                return;
            }

            if (t.BitsPerPixel == 16)
            {
                ushort c16 = Pack16(color);
                t.Pixels16[idx] = c16;
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::Point(unsigned short, unsigned short)
        // Returns: 8-bit index in low byte, or 16-bit pixel.
        // --------------------------------------------------------------------
        internal ushort Point(ushort x, ushort y)
        {
            ChunkySurface t = Target;

            if (x >= (uint)t.Width || y >= (uint)t.Height)
            {
                return 0;
            }

            int idx = y * t.Width + x;

            if (t.BitsPerPixel == 8)
            {
                return t.Pixels8[idx];
            }

            if (t.BitsPerPixel == 16)
            {
                return t.Pixels16[idx];
            }

            return 0;
        }

        // Convenience used by the C++ Line() when RenderBitDepth==8.
        internal byte Point8(short x, short y)
        {
            ChunkySurface t = Target;

            if ((uint)x >= (uint)t.Width || (uint)y >= (uint)t.Height) return 0;
            if (t.BitsPerPixel != 8) return 0;

            return t.Pixels8[y * t.Width + x];
        }

        internal void Plot8(short x, short y, byte colorIndex)
        {
            ChunkySurface t = Target;

            if ((uint)x >= (uint)t.Width || (uint)y >= (uint)t.Height) return;
            if (t.BitsPerPixel != 8) return;

            t.Pixels8[y * t.Width + x] = colorIndex;
        }

        // --------------------------------------------------------------------
        // DexterGFX::WindowBounds()
        // Keeps window position/size inside screen bounds (same math as the decompile).
        // --------------------------------------------------------------------
        internal void WindowBounds()
        {
            _windowX = (short)Math.Max(0, (int)_windowX);
            _windowY = (short)Math.Max(0, (int)_windowY);

            if (_windowX + _windowWidth > _renderWidth)
            {
                _windowX = (short)(_renderWidth - _windowWidth);
            }

            if (_windowY + _windowHeight > _renderHeight)
            {
                _windowY = (short)(_renderHeight - _windowHeight);
            }

            _windowX = (short)Math.Max(0, (int)_windowX);
            _windowY = (short)Math.Max(0, (int)_windowY);

            if (_windowWidth > _renderWidth)
            {
                _windowWidth = _renderWidth;
            }

            if (_windowHeight > _renderHeight)
            {
                _windowHeight = _renderHeight;
            }
        }


        // --------------------------------------------------------------------
        // DexterGFX::CalcShapeClip(unsigned int, DexRect&)
        // Trims fully transparent borders (color key) inside the provided rect.
        // Returns 1 on success, 0 on invalid input.
        // --------------------------------------------------------------------
        internal uint CalcShapeClip(uint chunkyId, ref DexRect rect)
        {
            if (!ValidChunky(chunkyId))
            {
                return 0;
            }

            ChunkySurface? c = ChunkyAddress(chunkyId);
            if (c == null)
            {
                return 0;
            }

            int w = c.Width;
            int h = c.Height;

            // Rect must be inside chunky bounds (strict check like the decompile).
            if (rect.Left < 0 || rect.Top < 0 || rect.Right > w || rect.Bottom > h)
            {
                return 0;
            }

            int left = rect.Left;
            int top = rect.Top;
            int right = rect.Right;
            int bottom = rect.Bottom;

            if (right <= left || bottom <= top)
            {
                return 1;
            }

            if (c.BitsPerPixel == 8)
            {
                byte ck = _colourKey8;
                ReadOnlySpan<byte> px = c.Pixels8;

                // Trim top
                while (bottom - top > 1 && IsRowAll8(px, w, left, right, top, ck))
                {
                    top++;
                }

                // Trim left
                while (right - left > 1 && IsColAll8(px, w, left, top, bottom, ck))
                {
                    left++;
                }

                // Trim bottom
                while (bottom - top > 1 && IsRowAll8(px, w, left, right, bottom - 1, ck))
                {
                    bottom--;
                }

                // Trim right
                while (right - left > 1 && IsColAll8(px, w, right - 1, top, bottom, ck))
                {
                    right--;
                }

                rect = new DexRect(left, top, right, bottom);
                return 1;
            }

            if (c.BitsPerPixel == 16)
            {
                ushort ck = _colourKey16;
                ReadOnlySpan<ushort> px = c.Pixels16;

                while (bottom - top > 1 && IsRowAll16(px, w, left, right, top, ck))
                {
                    top++;
                }

                while (right - left > 1 && IsColAll16(px, w, left, top, bottom, ck))
                {
                    left++;
                }

                while (bottom - top > 1 && IsRowAll16(px, w, left, right, bottom - 1, ck))
                {
                    bottom--;
                }

                while (right - left > 1 && IsColAll16(px, w, right - 1, top, bottom, ck))
                {
                    right--;
                }

                rect = new DexRect(left, top, right, bottom);
                return 1;
            }

            return 1;
        }

        private static bool IsRowAll8(ReadOnlySpan<byte> px, int width, int left, int right, int y, byte ck)
        {
            int row = y * width;
            for (int x = left; x < right; x++)
            {
                if (px[row + x] != ck) return false;
            }
            return true;
        }

        private static bool IsColAll8(ReadOnlySpan<byte> px, int width, int x, int top, int bottom, byte ck)
        {
            for (int y = top; y < bottom; y++)
            {
                if (px[y * width + x] != ck) return false;
            }
            return true;
        }

        private static bool IsRowAll16(ReadOnlySpan<ushort> px, int width, int left, int right, int y, ushort ck)
        {
            int row = y * width;
            for (int x = left; x < right; x++)
            {
                if (px[row + x] != ck) return false;
            }
            return true;
        }

        private static bool IsColAll16(ReadOnlySpan<ushort> px, int width, int x, int top, int bottom, ushort ck)
        {
            for (int y = top; y < bottom; y++)
            {
                if (px[y * width + x] != ck) return false;
            }
            return true;
        }

        // --------------------------------------------------------------------
        // DexterGFX::UseFont(unsigned int)
        // --------------------------------------------------------------------
        internal void UseFont(uint index)
        {
            if (_fonts == null) return;
            if (index >= (uint)_fonts.Length) return;

            _usedFont = index;
        }

        // --------------------------------------------------------------------
        // DexterGFX::BlitArea(unsigned int, int, int, int, int, int, int, unsigned char, DexCol, short)
        //
        // Managed interpretation:
        // - source rect: (srcX, srcY, srcW, srcH) inside source chunky
        // - destination top-left: (dstX, dstY)
        // - mode:
        //   bit1 (0x02) -> use color key
        //   bit4 (0x10) -> invert percent (100-percent) like decompile path
        //   bit6 (0x40) -> alpha-mask blit (uses AlphaBlit16)
        // - percent: -1 => default 50, else 0..100
        // - color is used only by some modes in the original; here it is kept for call-compatibility
        // --------------------------------------------------------------------
        internal void BlitArea(uint chunkyId, int srcX, int srcY, int srcW, int srcH, int dstX, int dstY, byte mode, DexCol color, short percent)
        {
            if (!ValidChunky(chunkyId))
            {
                UsedChunky();
                return;
            }

            ChunkySurface? src = ChunkyAddress(chunkyId);
            if (src == null)
            {
                UsedChunky();
                return;
            }

            int w = src.Width;
            int h = src.Height;

            if (srcW <= 0 || srcH <= 0) return;

            int right = srcX + srcW;
            int bottom = srcY + srcH;

            if (right > w) right = w;
            if (bottom > h) bottom = h;
            if (srcX < 0) srcX = 0;
            if (srcY < 0) srcY = 0;

            if (right <= srcX || bottom <= srcY) return;

            DexRect srcRect = new DexRect(srcX, srcY, right, bottom);

            short p = percent == -1 ? (short)50 : percent;
            if ((mode & 0x10) != 0)
            {
                p = (short)(100 - p);
            }

            bool useColorKey = (mode & 0x02) != 0;

            ChunkySurface dst = Target;

            if (dst.BitsPerPixel == 8)
            {
                ReadOnlySpan<byte> sp = src.Pixels8;
                Blit8(sp, src.Width, src.Height, dst, dstX, dstY, srcRect, useColorKey, _colourKey8, (byte)(((mode & 0x10) != 0) ? 1 : 0));
                return;
            }

            if (dst.BitsPerPixel == 16)
            {
                if ((mode & 0x40) == 0)
                {
                    ReadOnlySpan<ushort> sp = src.Pixels16;
                    byte alpha = AlphaFromPercent(p);
                    bool alphaBlend = (mode & 0x10) != 0;
                    Blit16(sp, src.Width, src.Height, dst, dstX, dstY, srcRect, useColorKey, _colourKey16, alphaBlend, alpha);
                    return;
                }

                // Alpha-mask blit: expects a parallel alpha mask buffer.
                // In the original, the chunky can carry alpha; in this port, a mask must be provided externally.
                // Without an engine-provided alpha plane, this mode becomes a no-op.
                return;
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::UpdateScaleTables(int, int, float, float)
        // Returns 1 on success, 0 on alloc failure (managed => always succeeds unless OOM).
        // --------------------------------------------------------------------
        internal uint UpdateScaleTables(int xScale, int yScale, float xRatio, float yRatio)
        {
            if (xScale < 0 || yScale < 0)
            {
                return 0;
            }

            if (_xScaleTableSize < xScale)
            {
                _xScaleTable = null;
                _xScaleTableSize = 0;

                _xScaleTable = new int[xScale];
                _xScaleTableSize = xScale;

                _lastXScale = 0;
                _lastXRatio = 0.0f;
            }

            if (_yScaleTableSize < yScale)
            {
                _yScaleTable = null;
                _yScaleTableSize = 0;

                _yScaleTable = new int[yScale];
                _yScaleTableSize = yScale;

                _lastYScale = 0;
                _lastYRatio = 0.0f;
            }

            if (_xScaleTable == null || _yScaleTable == null)
            {
                return 0;
            }

            if (_lastXScale != xScale || _lastXRatio != xRatio)
            {
                _lastXScale = xScale;
                _lastXRatio = xRatio;

                float acc = 0.0f;
                for (int i = 0; i < xScale; i++)
                {
                    _xScaleTable[i] = (int)acc;
                    acc += xRatio;
                }
            }

            if (_lastYScale != yScale || _lastYRatio != yRatio)
            {
                _lastYScale = yScale;
                _lastYRatio = yRatio;

                float acc = 0.0f;
                for (int i = 0; i < yScale; i++)
                {
                    _yScaleTable[i] = (int)acc;
                    acc += yRatio;
                }
            }

            return 1;
        }

        // --------------------------------------------------------------------
        // DexterGFX::BlitScale(unsigned int, DexRect const&, DexRect const&, unsigned char)
        // If srcRect == dstRect => forwards to BlitArea.
        // Else => uses ChunkyScale8/ChunkyScale16 (from GFX2/GFX4 ports).
        // --------------------------------------------------------------------
        internal void BlitScale(uint chunkyId, in DexRect srcRect, in DexRect dstRect, byte flags)
        {
            if (!ValidChunky(chunkyId))
            {
                return;
            }

            if (srcRect.Left == dstRect.Left && srcRect.Top == dstRect.Top && srcRect.Right == dstRect.Right && srcRect.Bottom == dstRect.Bottom)
            {
                int w = srcRect.Width;
                int h = srcRect.Height;
                if (w <= 0 || h <= 0) return;

                // C++ uses BlitArea with mode derived from flags; keep a deterministic mapping:
                byte mode = (flags & 0x10) == 0 ? (byte)2 : (byte)4;
                BlitArea(chunkyId, srcRect.Left, srcRect.Top, w, h, dstRect.Left, dstRect.Top, mode, new DexCol(255, 255, 255), -1);
                return;
            }

            int dstW = dstRect.Width;
            int dstH = dstRect.Height;
            if (dstW <= 0 || dstH <= 0) return;

            float xRatio = (float)srcRect.Width / (float)dstW;
            float yRatio = (float)srcRect.Height / (float)dstH;

            UpdateScaleTables(dstW, dstH, xRatio, yRatio);

            ChunkySurface? src = ChunkyAddress(chunkyId);
            ChunkySurface? dst = ChunkyAddress((uint)_activeChunkyIndex);
            if (src == null || dst == null) return;

            DexRect srcLocal = new DexRect(0, 0, srcRect.Width, srcRect.Height);
            DexRect dstLocal = dstRect;

            if (src.BitsPerPixel == 8)
            {
                ChunkyScale8(src, dst, in srcLocal, in dstLocal, flags);
                return;
            }

            if (src.BitsPerPixel == 16)
            {
                ChunkyScale16(src, dst, in srcLocal, in dstLocal, flags);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::BlitRotate(unsigned int, short, short, double, short, short, unsigned char)
        // Calls BlitRotate16 implementation (introduced in earlier/later chunks).
        // --------------------------------------------------------------------
        internal void BlitRotate(uint chunkyId, short x, short y, double radians, short pivotX, short pivotY, byte flags)
        {
            if (!ValidChunky(chunkyId))
            {
                return;
            }

            ChunkySurface? src = ChunkyAddress(chunkyId);
            ChunkySurface dst = Target;
            if (src == null) return;

            if (pivotX == 0x7FFF)
            {
                pivotX = (short)(src.Width >> 1);
            }

            if (pivotY == 0x7FFF)
            {
                pivotY = (short)(src.Height >> 1);
            }

            BlitRotate16(src, dst, x, y, radians, pivotX, pivotY, flags);
        }

        // --------------------------------------------------------------------
        // DexterGFX::Line(short, short, short, short, DexCol const&, unsigned char)
        // mode==4: translucent line (50% for 8-bit using trans table; 16-bit uses 50% Blend16).
        // otherwise: solid.
        // --------------------------------------------------------------------
        internal void Line(short x0, short y0, short x1, short y1, in DexCol color, byte mode)
        {
            ChunkySurface t = Target;

            if (t.BitsPerPixel == 8)
            {
                byte idx = FindClosestPaletteIndex(color.R, color.G, color.B);

                bool translucent = mode == 4;
                int dx = Abs(x1 - x0) * 2;
                int sx = Sign(x1 - x0);
                int dy = Abs(y1 - y0) * 2;
                int sy = Sign(y1 - y0);

                if (dy < dx)
                {
                    int err = dy - (dx >> 1);

                    while (true)
                    {
                        if ((uint)x0 < (uint)t.Width && (uint)y0 < (uint)t.Height)
                        {
                            if (!translucent)
                            {
                                Plot8(x0, y0, idx);
                            }
                            else
                            {
                                byte d = Point8(x0, y0);
                                Plot8(x0, y0, _transTable8[(idx << 8) | d]);
                            }
                        }

                        if (x0 == x1) return;

                        bool stepY = err >= 0;
                        if (stepY) y0 = (short)(y0 + sy);
                        x0 = (short)(x0 + sx);
                        err = err + dy - (stepY ? dx : 0);
                    }
                }
                else
                {
                    int err = dx - (dy >> 1);

                    while (true)
                    {
                        if ((uint)x0 < (uint)t.Width && (uint)y0 < (uint)t.Height)
                        {
                            if (!translucent)
                            {
                                Plot8(x0, y0, idx);
                            }
                            else
                            {
                                byte d = Point8(x0, y0);
                                Plot8(x0, y0, _transTable8[(idx << 8) | d]);
                            }
                        }

                        if (y0 == y1) return;

                        bool stepX = err >= 0;
                        if (stepX) x0 = (short)(x0 + sx);
                        y0 = (short)(y0 + sy);
                        err = err + dx - (stepX ? dy : 0);
                    }
                }
            }

            if (t.BitsPerPixel == 16)
            {
                ushort c16 = Pack16(color);
                bool translucent = mode == 4;

                int dx = Abs(x1 - x0);
                int sx = x0 < x1 ? 1 : -1;
                int dy = -Abs(y1 - y0);
                int sy = y0 < y1 ? 1 : -1;
                int err = dx + dy;

                while (true)
                {
                    if ((uint)x0 < (uint)t.Width && (uint)y0 < (uint)t.Height)
                    {
                        int di = y0 * t.Width + x0;

                        if (!translucent)
                        {
                            t.Pixels16[di] = c16;
                        }
                        else
                        {
                            ushort d = t.Pixels16[di];
                            t.Pixels16[di] = Blend16(d, c16, 128);
                        }
                    }

                    if (x0 == x1 && y0 == y1) break;

                    int e2 = err << 1;
                    if (e2 >= dy)
                    {
                        err += dy;
                        x0 = (short)(x0 + sx);
                    }
                    if (e2 <= dx)
                    {
                        err += dx;
                        y0 = (short)(y0 + sy);
                    }
                }
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::DrawCircle(int, int, unsigned short, unsigned short, DexCol const&)
        // The decompile draws ellipse-ish circles; this port keeps the existing circle algorithm.
        // param_3/param_4 are treated as radius; param_4 is used by the decompile as radius.
        // --------------------------------------------------------------------
        internal void DrawCircle(int cx, int cy, ushort radiusX, ushort radiusY, in DexCol color)
        {
            // Best-effort: use radiusY (matches the decompile's initial plots).
            DrawCircle(cx, cy, radiusY, color);
        }

        // --------------------------------------------------------------------
        // DexterGFX::Box(short, short, short, short, DexCol const&, unsigned char)
        // --------------------------------------------------------------------
        internal void Box(short x0, short y0, short x1, short y1, in DexCol color, byte mode)
        {
            Line(x0, y0, x1, y0, in color, mode);
            Line(x0, y0, x0, y1, in color, mode);
            Line(x1, y0, x1, y1, in color, mode);
            Line(x0, y1, x1, y1, in color, mode);
        }

        // --------------------------------------------------------------------
        // DexterGFX::BoxFill(short, short, short, short, DexCol const&, unsigned char, short)
        // mode/percent are kept for compatibility; percent is used only for translucent in 16-bit here.
        // --------------------------------------------------------------------
        internal void BoxFill(short x0, short y0, short x1, short y1, in DexCol color, byte mode, short percent)
        {
            // Normalize coordinates (matches the decompile swap logic).
            int left = x0;
            int right = x1;
            if (right < left)
            {
                int tmp = left;
                left = right;
                right = tmp;
            }

            int top = y0;
            int bottom = y1;
            if (bottom < top)
            {
                int tmp = top;
                top = bottom;
                bottom = tmp;
            }

            // Clamp to target bounds and fill inclusively like the decompile (it uses +1 sizes).
            ChunkySurface t = Target;

            int cl = left < 0 ? 0 : left;
            int ct = top < 0 ? 0 : top;
            int cr = right >= t.Width ? t.Width - 1 : right;
            int cb = bottom >= t.Height ? t.Height - 1 : bottom;

            if (cr < cl || cb < ct) return;

            DexRect rect = new DexRect(cl, ct, cr + 1, cb + 1);

            if (t.BitsPerPixel == 8)
            {
                byte idx = FindClosestPaletteIndex(color.R, color.G, color.B);
                BoxFill8Core(t, rect, idx);
                return;
            }

            ushort c16 = Pack16(color);

            if (mode != 4)
            {
                BoxFill16Core(t, rect, c16);
                return;
            }

            // Translucent fill in 16-bit: blend existing pixels towards c16.
            byte a = AlphaFromPercent(percent == -1 ? (short)50 : percent);
            Span<ushort> px = t.Pixels16;
            int w = t.Width;

            for (int y = rect.Top; y < rect.Bottom; y++)
            {
                int row = y * w;
                for (int x = rect.Left; x < rect.Right; x++)
                {
                    int i = row + x;
                    ushort d = px[i];
                    px[i] = Blend16(d, c16, a);
                }
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::LoadTransTable(char*, unsigned char)
        // C++ loads 0xFE01 bytes if provided; otherwise generates full 256x256 mapping.
        // Managed port supports optional file load (expects 0x10000 bytes).
        // --------------------------------------------------------------------
        internal void LoadTransTable(string? path, byte mode)
        {
            if (Target.BitsPerPixel != 8)
            {
                return;
            }

            if (!string.IsNullOrEmpty(path))
            {
                DexterFile.FileHandle file = DexterFile.FileOpen(path, mode);
                if (file.IsValid)
                {
                    try
                    {
                        byte[] tmp = new byte[0x10000];
                        int read = DexterFile.FileRead(file, tmp, 0, tmp.Length, 0);

                        if (read == tmp.Length)
                        {
                            Buffer.BlockCopy(tmp, 0, _transTable8, 0, _transTable8.Length);
                            return;
                        }
                    }
                    finally
                    {
                        DexterFile.FileClose(file);
                    }
                }
            }

            // Generate: average palette RGB and remap to closest palette entry.
            for (int a = 0; a < 256; a++)
            {
                byte ar = _paletteR[a];
                byte ag = _paletteG[a];
                byte ab = _paletteB[a];

                for (int b = 0; b < 256; b++)
                {
                    byte br = _paletteR[b];
                    byte bg = _paletteG[b];
                    byte bb = _paletteB[b];

                    int rr = (ar + br) >> 1;
                    int gg = (ag + bg) >> 1;
                    int bb2 = (ab + bb) >> 1;

                    _transTable8[(a << 8) | b] = FindClosestPaletteIndex((byte)rr, (byte)gg, (byte)bb2);
                }
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::SaveTransTable(char*)
        // --------------------------------------------------------------------
        internal void SaveTransTable(string path)
        {
            if (Target.BitsPerPixel != 8)
            {
                return;
            }

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            DexterFile.FileHandle file = DexterFile.FileOpen(path, 0x01);
            if (!file.IsValid)
            {
                return;
            }

            try
            {
                _ = DexterFile.FileWrite(file, _transTable8, 0, _transTable8.Length);
            }
            finally
            {
                DexterFile.FileClose(file);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::LoadChunky(unsigned int, char*, unsigned char)
        // Dispatcher by extension, matches the decompile.
        // Returns 1 on success, 0 on failure.
        // --------------------------------------------------------------------
        internal uint LoadChunky(uint chunkyId, string path, byte flags)
        {
            if ((uint)_chunkies.Length <= chunkyId)
            {
                return 0;
            }

            if (string.IsNullOrEmpty(path))
            {
                return 0;
            }

            string lower = path.ToLowerInvariant();

            DexterFile.FileHandle file = DexterFile.FileOpen(lower, 0);
            if (!file.IsValid)
            {
                return 0;
            }

            try
            {
                if (lower.Contains(".dxp", StringComparison.Ordinal))
                {
                    return LoadChunkyDXP(chunkyId, file, flags);
                }

                if (lower.Contains(".jpg", StringComparison.Ordinal) || lower.Contains(".jpeg", StringComparison.Ordinal))
                {
                    return 0; // Matches decompile: JPG/JPEG path returns 0.
                }

                if (lower.Contains(".pcx", StringComparison.Ordinal))
                {
                    return LoadChunkyPCX(chunkyId, file, flags);
                }

                if (lower.Contains(".bmp", StringComparison.Ordinal))
                {
                    return LoadChunkyBMP(chunkyId, file, flags);
                }

                if (lower.Contains(".png", StringComparison.Ordinal))
                {
                    return 0; // Matches decompile: PNG path returns 0.
                }

                return LoadChunkyTGA(chunkyId, file, flags);
            }
            finally
            {
                DexterFile.FileClose(file);
            }
        }

        // --------------------------------------------------------------------
        // Small helpers copied from the decompile style
        // --------------------------------------------------------------------

        private static int Abs(int v) => v < 0 ? -v : v;

        private static int Sign(int v)
        {
            if (v < 0) return -1;
            if (v > 0) return 1;
            return 0;
        }

        private static int Max(int a, int b) => a >= b ? a : b;

        private static int Min(int a, int b) => a <= b ? a : b;

        // --------------------------------------------------------------------
        // DexterGFX::LoadChunkyDXP(unsigned int, PHYSFS_File*, unsigned char)
        // Returns 1 on success, 0 on failure.
        //
        // Notes:
        // - DXP header: 'DXP1' (little-endian on disk).
        // - Supports optional palette read.
        // - Supports raw and z-compressed blocks.
        // - This port keeps the decompile flow, but uses managed arrays.
        // --------------------------------------------------------------------
        internal uint LoadChunkyDXP(uint chunkyId, DexterFile.FileHandle file, byte flags)
        {
            if (!file.IsValid)
            {
                return 0;
            }

            // Header: "DXP1" (LSB)
            uint magic = DexterEndian.FileReadLongLSB(file);
            if (magic != 0x31505844u)
            {
                return 0;
            }

            _ = DexterFile.FileReadByte(file); // version/minor (ignored)
            byte hasPalette = DexterFile.FileReadByte(file);

            ushort width = DexterEndian.FileReadWordLSB(file);
            ushort height = DexterEndian.FileReadWordLSB(file);

            byte sourceDepthBits = DexterFile.FileReadByte(file);
            byte compressionFlags = DexterFile.FileReadByte(file);

            int packedSize = unchecked((int)DexterEndian.FileReadLongLSB(file));

            // Palette (256 * RGB) if present
            if (hasPalette != 0)
            {
                for (int i = 0; i < 256; i++)
                {
                    byte r = DexterFile.FileReadByte(file);
                    byte g = DexterFile.FileReadByte(file);
                    byte b = DexterFile.FileReadByte(file);

                    if ((flags & 1) != 0)
                    {
                        SetPaletteEntry(i, r, g, b);
                    }
                }
            }

            // Decide destination depth:
            // decompile:
            // - base is 8 unless (flags & 2)==0 => RenderBitDepth
            // - then 24 unless (flags & 8)==0 => that base
            // - then 16 unless (flags & 4)==0 => previous
            int renderDepth = 8;
            if ((flags & 2) == 0)
            {
                renderDepth = Target.BitsPerPixel; // best available proxy in this port
            }

            int preferred = 0x18;
            if ((flags & 8) == 0)
            {
                preferred = renderDepth;
            }

            int destDepth = 0x10;
            if ((flags & 4) == 0)
            {
                destDepth = preferred;
            }

            // Special case: source is 24-bit (RGB) -> we scanline convert into dest depth.
            if (sourceDepthBits == 0x18)
            {
                if (!InitChunkyManaged(chunkyId, width, height, destDepth))
                {
                    return 0;
                }

                // compressionFlags:
                // 0 => raw scanlines
                // 1 => z-compressed blob
                // else => treat as raw (best effort)
                if (compressionFlags == 0)
                {
                    // scanline scratch: width * 3 bytes
                    byte[] scan = new byte[checked(width * 3)];
                    for (int y = 0; y < height; y++)
                    {
                        int got = DexterFile.FileRead(file, scan, 0, scan.Length, 0);
                        if (got != scan.Length)
                        {
                            return 0;
                        }

                        WriteConvertedScanlineToChunky(chunkyId, y, scan, width, (ushort)destDepth, flipBgr: true);
                    }

                    return 1;
                }

                if ((compressionFlags & 1) != 0)
                {
                    // Packed buffer -> decompress -> then per-scanline convert
                    byte[] packed = new byte[checked(packedSize)];
                    int gotPacked = DexterFile.FileRead(file, packed, 0, packed.Length, 0);
                    if (gotPacked != packed.Length)
                    {
                        return 0;
                    }

                    // Expected decompressed size: height * width * 3
                    int outSize = checked(height * width * 3);
                    byte[] raw = ZDecompress(packed, outSize);

                    int stride = checked(width * 3);
                    for (int y = 0; y < height; y++)
                    {
                        ReadOnlySpan<byte> line = new ReadOnlySpan<byte>(raw, y * stride, stride);
                        WriteConvertedScanlineToChunky(chunkyId, y, line, width, (ushort)destDepth, flipBgr: true);
                    }

                    return 1;
                }

                return 0;
            }

            // Non-24-bit path: the decompile mostly reads the payload into the chunky and optionally decompresses.
            // Supported here:
            // - 8-bit: raw or RLE (DeCompress) or z (ZDeCompress)
            // - 16-bit: raw or z (ZDeCompress) (best effort)
            int finalDepth = (byte)sourceDepthBits;
            if (!InitChunkyManaged(chunkyId, width, height, finalDepth))
            {
                return 0;
            }

            ChunkySurface? dst = ChunkyAddress(chunkyId);
            if (dst == null)
            {
                return 0;
            }

            int dstBytes = checked(width * height * (finalDepth >> 3));
            byte[] payload = new byte[checked(packedSize)];
            int readPayload = DexterFile.FileRead(file, payload, 0, payload.Length, 0);
            if (readPayload != payload.Length)
            {
                return 0;
            }

            if (compressionFlags == 0)
            {
                CopyPayloadToChunky(dst, payload);
                return 1;
            }

            // compressed
            if (finalDepth == 8)
            {
                // Prefer z if size suggests it, else RLE
                byte[] raw8 = ZDecompress(payload, checked(width * height));
                dst.Pixels8.Clear();
                raw8.AsSpan(0, checked(width * height)).CopyTo(dst.Pixels8);
                return 1;
            }

            if (finalDepth == 16)
            {
                byte[] raw16 = ZDecompress(payload, checked(width * height * 2));
                WriteRaw16LittleEndian(dst, raw16);
                return 1;
            }

            return 0;
        }

        // --------------------------------------------------------------------
        // DexterGFX::LoadChunkyPCX(unsigned int, PHYSFS_File*, unsigned char)
        // Returns 1 on success, 0 on failure.
        //
        // Supports: 8-bit PCX with 256-color palette at end.
        // --------------------------------------------------------------------
        internal uint LoadChunkyPCX(uint chunkyId, DexterFile.FileHandle file, byte flags)
        {
            if (!file.IsValid)
            {
                return 0;
            }

            int fileSize = DexterFile.FileSize(file);
            if (fileSize <= 0)
            {
                return 0;
            }

            byte manufacturer = DexterFile.FileReadByte(file);
            byte version = DexterFile.FileReadByte(file);
            byte encoding = DexterFile.FileReadByte(file);
            byte bpp = DexterFile.FileReadByte(file);

            _ = DexterEndian.FileReadWordLSB(file); // xmin
            _ = DexterEndian.FileReadWordLSB(file); // ymin
            short xmax = (short)DexterEndian.FileReadWordLSB(file);
            short ymax = (short)DexterEndian.FileReadWordLSB(file);

            _ = DexterEndian.FileReadWordLSB(file); // hdpi
            _ = DexterEndian.FileReadWordLSB(file); // vdpi

            // Skip palette16(48)
            byte[] skip48 = new byte[0x30];
            _ = DexterFile.FileRead(file, skip48, 0, skip48.Length, 0);

            _ = DexterFile.FileReadByte(file); // reserved
            _ = DexterFile.FileReadByte(file); // planes
            _ = DexterEndian.FileReadWordLSB(file); // bytesPerLine
            _ = DexterEndian.FileReadWordLSB(file); // paletteInfo

            // Minimal header validation: 0x0A, 0x05, 0x01, 0x08
            if (manufacturer != 0x0A || version != 0x05 || encoding != 0x01 || bpp != 0x08)
            {
                return 0;
            }

            int width = xmax + 1;
            int height = ymax + 1;

            if (width <= 0 || height <= 0)
            {
                return 0;
            }

            if (!InitChunkyManaged(chunkyId, (ushort)width, (ushort)height, 8))
            {
                return 0;
            }

            // Read full file to memory for easy RLE decode + palette
            DexterFile.FileRewind(file);
            byte[] all = new byte[fileSize];
            int got = DexterFile.FileRead(file, all, 0, all.Length, 0);
            if (got != all.Length)
            {
                return 0;
            }

            // Palette is last 0x300 bytes. Many PCX files have a 0x0C marker before it, but not all.
            int paletteStart = fileSize - 0x300;
            if (paletteStart < 0)
            {
                return 0;
            }

            if ((flags & 1) != 0)
            {
                for (int i = 0; i < 256; i++)
                {
                    int p = paletteStart + (i * 3);
                    byte r = all[p + 0];
                    byte g = all[p + 1];
                    byte b = all[p + 2];
                    SetPaletteEntry(i, r, g, b);
                }
            }

            ChunkySurface? dst = ChunkyAddress(chunkyId);
            if (dst == null)
            {
                return 0;
            }

            // Decode image stream from offset 0x80 to paletteStart
            int src = 0x80;
            int dstPos = 0;

            int pixelCount = checked(width * height);
            Span<byte> outPx = dst.Pixels8;

            while (src < paletteStart && dstPos < pixelCount)
            {
                byte v = all[src++];
                int run = 1;

                if ((v & 0xC0) == 0xC0)
                {
                    run = v & 0x3F;
                    if (src >= paletteStart)
                    {
                        break;
                    }

                    v = all[src++];
                }

                int toWrite = run;
                int remaining = pixelCount - dstPos;
                if (toWrite > remaining)
                {
                    toWrite = remaining;
                }

                outPx.Slice(dstPos, toWrite).Fill(v);
                dstPos += toWrite;
            }

            return dstPos == pixelCount ? 1u : 0u;
        }

        // --------------------------------------------------------------------
        // DexterGFX::LoadChunkyBMP(unsigned int, PHYSFS_File*, unsigned char)
        // Returns 1 on success, 0 on failure.
        //
        // Supports:
        // - 8-bit paletted BMP (BI_RGB) bottom-up or top-down
        // - 24-bit BMP (BI_RGB) bottom-up or top-down, scanline-converted to requested depth
        // --------------------------------------------------------------------
        internal uint LoadChunkyBMP(uint chunkyId, DexterFile.FileHandle file, byte flags)
        {
            if (!file.IsValid)
            {
                return 0;
            }

            byte b0 = DexterFile.FileReadByte(file);
            byte b1 = DexterFile.FileReadByte(file);

            if (b0 != (byte)'B' || b1 != (byte)'M')
            {
                return 0;
            }

            _ = DexterEndian.FileReadLongLSB(file); // file size
            _ = DexterEndian.FileReadLongLSB(file); // reserved
            uint pixelOffset = DexterEndian.FileReadLongLSB(file);

            uint dibSize = DexterEndian.FileReadLongLSB(file);
            if (dibSize < 40)
            {
                return 0;
            }

            int width = unchecked((int)DexterEndian.FileReadLongLSB(file));
            int height = unchecked((int)DexterEndian.FileReadLongLSB(file));
            _ = DexterEndian.FileReadWordLSB(file); // planes
            ushort bpp = DexterEndian.FileReadWordLSB(file);
            uint compression = DexterEndian.FileReadLongLSB(file);

            _ = DexterEndian.FileReadLongLSB(file); // imageSize
            _ = DexterEndian.FileReadLongLSB(file); // xppm
            _ = DexterEndian.FileReadLongLSB(file); // yppm
            _ = DexterEndian.FileReadLongLSB(file); // clrUsed
            _ = DexterEndian.FileReadLongLSB(file); // clrImportant

            if (compression != 0)
            {
                return 0; // BI_RGB only
            }

            if (width <= 0 || height == 0)
            {
                return 0;
            }

            int absHeight = height < 0 ? -height : height;
            bool topDown = height < 0;

            // Destination depth selection (same idea as DXP/TGA)
            int renderDepth = 8;
            if ((flags & 2) == 0)
            {
                renderDepth = Target.BitsPerPixel;
            }

            int preferred = 0x18;
            if ((flags & 8) == 0)
            {
                preferred = renderDepth;
            }

            int destDepth = 0x10;
            if ((flags & 4) == 0)
            {
                destDepth = preferred;
            }

            if ((flags & 0x20) != 0)
            {
                destDepth = bpp;
            }

            // Palette (if 8bpp)
            if (bpp == 8)
            {
                if ((flags & 1) != 0)
                {
                    // BMP palette entries are BGRA (4 bytes each)
                    for (int i = 0; i < 256; i++)
                    {
                        byte bb = DexterFile.FileReadByte(file);
                        byte bg = DexterFile.FileReadByte(file);
                        byte br = DexterFile.FileReadByte(file);
                        _ = DexterFile.FileReadByte(file);
                        SetPaletteEntry(i, br, bg, bb);
                    }
                }
                else
                {
                    // Skip palette if present (256*4 is typical)
                    DexterFile.FileSeek(file, 256 * 4, 1);
                }
            }

            DexterFile.FileSeek(file, unchecked((int)pixelOffset), 0);

            if (bpp == 8)
            {
                if (!InitChunkyManaged(chunkyId, (ushort)width, (ushort)absHeight, 8))
                {
                    return 0;
                }

                ChunkySurface? dst = ChunkyAddress(chunkyId);
                if (dst == null)
                {
                    return 0;
                }

                int rowStride = AlignUp(width, 4);
                byte[] row = new byte[rowStride];

                for (int y = 0; y < absHeight; y++)
                {
                    int got = DexterFile.FileRead(file, row, 0, row.Length, 0);
                    if (got != row.Length)
                    {
                        return 0;
                    }

                    int dstY = topDown ? y : (absHeight - 1 - y);
                    row.AsSpan(0, width).CopyTo(dst.Pixels8.Slice(dstY * width, width));
                }

                return 1;
            }

            if (bpp == 24)
            {
                if (!InitChunkyManaged(chunkyId, (ushort)width, (ushort)absHeight, destDepth))
                {
                    return 0;
                }

                int srcRowBytes = checked(width * 3);
                int rowStride = AlignUp(srcRowBytes, 4);
                byte[] row = new byte[rowStride];

                for (int y = 0; y < absHeight; y++)
                {
                    int got = DexterFile.FileRead(file, row, 0, row.Length, 0);
                    if (got != row.Length)
                    {
                        return 0;
                    }

                    int dstY = topDown ? y : (absHeight - 1 - y);
                    WriteConvertedScanlineToChunky(chunkyId, dstY, row.AsSpan(0, srcRowBytes), (ushort)width, (ushort)destDepth, flipBgr: true);
                }

                return 1;
            }

            return 0;
        }

        // --------------------------------------------------------------------
        // DexterGFX::LoadChunkyTGA(unsigned int, PHYSFS_File*, unsigned char)
        // Returns 1 on success, 0 on failure.
        //
        // Supports:
        // - type 2 (uncompressed truecolor) 24/32
        // - type 1 (uncompressed colormapped) 8
        // - best-effort palette handling
        // --------------------------------------------------------------------
        internal uint LoadChunkyTGA(uint chunkyId, DexterFile.FileHandle file, byte flags)
        {
            if (!file.IsValid)
            {
                return 0;
            }

            byte idLength = DexterFile.FileReadByte(file);
            byte colorMapType = DexterFile.FileReadByte(file);
            byte imageType = DexterFile.FileReadByte(file);

            _ = DexterEndian.FileReadWordLSB(file); // color map first entry
            ushort colorMapLength = DexterEndian.FileReadWordLSB(file);
            byte colorMapEntrySize = DexterFile.FileReadByte(file);

            _ = DexterEndian.FileReadWordLSB(file); // x-origin
            _ = DexterEndian.FileReadWordLSB(file); // y-origin
            ushort width = DexterEndian.FileReadWordLSB(file);
            ushort height = DexterEndian.FileReadWordLSB(file);

            byte bpp = DexterFile.FileReadByte(file);
            byte descriptor = DexterFile.FileReadByte(file);

            // Only type 1 or 2 in the decompile path (it checks (type-1)<2).
            if (imageType < 1 || imageType > 2)
            {
                return 0;
            }

            if (idLength != 0)
            {
                DexterFile.FileSeek(file, idLength, 1);
            }

            // Destination depth selection
            int renderDepth = 8;
            if ((flags & 2) == 0)
            {
                renderDepth = Target.BitsPerPixel;
            }

            int preferred = 0x18;
            if ((flags & 8) == 0)
            {
                preferred = renderDepth;
            }

            int destDepth = 0x10;
            if ((flags & 4) == 0)
            {
                destDepth = preferred;
            }

            if ((flags & 0x20) != 0)
            {
                destDepth = bpp;
            }

            bool originTop = (descriptor & 0x20) != 0;

            // Paletted 8-bit
            if (bpp == 8)
            {
                if (colorMapType != 0)
                {
                    // Skip color map if present (unless palette requested and 24-bit entries)
                    int mapBytesPerEntry = colorMapEntrySize >> 3;
                    int mapBytes = checked(colorMapLength * mapBytesPerEntry);

                    if (mapBytesPerEntry == 3 && (flags & 1) != 0)
                    {
                        int max = colorMapLength;
                        if (max > 256)
                        {
                            max = 256;
                        }

                        for (int i = 0; i < max; i++)
                        {
                            byte bb = DexterFile.FileReadByte(file);
                            byte bg = DexterFile.FileReadByte(file);
                            byte br = DexterFile.FileReadByte(file);
                            SetPaletteEntry(i, br, bg, bb);
                        }

                        int consumed = max * 3;
                        if (mapBytes > consumed)
                        {
                            DexterFile.FileSeek(file, mapBytes - consumed, 1);
                        }
                    }
                    else
                    {
                        DexterFile.FileSeek(file, mapBytes, 1);
                    }
                }

                if (!InitChunkyManaged(chunkyId, width, height, 8))
                {
                    return 0;
                }

                ChunkySurface? dst = ChunkyAddress(chunkyId);
                if (dst == null)
                {
                    return 0;
                }

                byte[] row = new byte[width];

                for (int y = 0; y < height; y++)
                {
                    int got = DexterFile.FileRead(file, row, 0, row.Length, 0);
                    if (got != row.Length)
                    {
                        return 0;
                    }

                    int dstY = originTop ? y : (height - 1 - y);
                    row.AsSpan().CopyTo(dst.Pixels8.Slice(dstY * width, width));
                }

                return 1;
            }

            // Truecolor 24/32
            if (bpp == 0x18 || bpp == 0x20)
            {
                if (colorMapType != 0)
                {
                    // Skip color map
                    int mapBytesPerEntry = colorMapEntrySize >> 3;
                    int mapBytes = checked(colorMapLength * mapBytesPerEntry);
                    DexterFile.FileSeek(file, mapBytes, 1);
                }

                if (!InitChunkyManaged(chunkyId, width, height, destDepth))
                {
                    return 0;
                }

                int srcBytesPerPixel = bpp >> 3;
                int srcRowBytes = checked(width * srcBytesPerPixel);
                byte[] row = new byte[srcRowBytes];

                if (srcBytesPerPixel == 3)
                {
                    for (int y = 0; y < height; y++)
                    {
                        int got = DexterFile.FileRead(file, row, 0, row.Length, 0);
                        if (got != row.Length)
                        {
                            return 0;
                        }

                        int dstY = originTop ? y : (height - 1 - y);
                        WriteConvertedScanlineToChunky(chunkyId, dstY, row.AsSpan(), width, (ushort)destDepth, flipBgr: true);
                    }

                    return 1;
                }

                // 32-bit: drop alpha
                byte[] tmp = new byte[checked(width * 3)];

                for (int y = 0; y < height; y++)
                {
                    int got = DexterFile.FileRead(file, row, 0, row.Length, 0);
                    if (got != row.Length)
                    {
                        return 0;
                    }

                    for (int x = 0; x < width; x++)
                    {
                        int s = x * 4;
                        int d = x * 3;
                        tmp[d + 0] = row[s + 0];
                        tmp[d + 1] = row[s + 1];
                        tmp[d + 2] = row[s + 2];
                    }

                    int dstY = originTop ? y : (height - 1 - y);
                    WriteConvertedScanlineToChunky(chunkyId, dstY, tmp.AsSpan(), width, (ushort)destDepth, flipBgr: true);
                }

                return 1;
            }

            return 0;
        }

        // --------------------------------------------------------------------
        // DexterGFX::ScanLineToBuffer(void*, unsigned char*, unsigned short, unsigned short, unsigned char)
        // Converts a 24-bit scanline to either 8-bit palette index or 16-bit pixel.
        //
        // param_5 == '\n' means input is BGR (BMP/TGA style) otherwise RGB.
        // Dithering state is reset on color key hits (matches decompile behavior).
        // --------------------------------------------------------------------
        private void ScanLineToBuffer(Span<byte> dst8, ReadOnlySpan<byte> src24, ushort pixelCount, ushort outDepthBits, byte srcIsBgrMarker)
        {
            if (pixelCount == 0)
            {
                return;
            }

            int rErr = 0;
            int gErr = 0;
            int bErr = 0;

            for (int i = 0; i < pixelCount; i++)
            {
                int s = i * 3;

                byte r;
                byte g;
                byte b;

                if (srcIsBgrMarker == (byte)'\n')
                {
                    b = src24[s + 0];
                    g = src24[s + 1];
                    r = src24[s + 2];
                }
                else
                {
                    r = src24[s + 0];
                    g = src24[s + 1];
                    b = src24[s + 2];
                }

                // Error diffusion: same integer rounding style as decompile
                int rHalf = rErr / 2;
                int gHalf = gErr / 2;
                int bHalf = bErr / 2;

                byte idx = Make8BitPixel(ClampToByte(r + rHalf), ClampToByte(g + gHalf), ClampToByte(b + bHalf));

                if (idx == _colourKey8)
                {
                    dst8[i] = idx;
                    rErr = 0;
                    gErr = 0;
                    bErr = 0;
                    continue;
                }

                dst8[i] = idx;

                // Recompute error against palette entry actually used
                byte pr = _paletteR[idx];
                byte pg = _paletteG[idx];
                byte pb = _paletteB[idx];

                rErr = (r + rHalf) - pr;
                gErr = (g + gHalf) - pg;
                bErr = (b + bHalf) - pb;
            }
        }

        private void ScanLineToBuffer(Span<ushort> dst16, ReadOnlySpan<byte> src24, ushort pixelCount, ushort outDepthBits, byte srcIsBgrMarker)
        {
            if (pixelCount == 0)
            {
                return;
            }

            int rErr = 0;
            int gErr = 0;
            int bErr = 0;

            for (int i = 0; i < pixelCount; i++)
            {
                int s = i * 3;

                byte r;
                byte g;
                byte b;

                if (srcIsBgrMarker == (byte)'\n')
                {
                    b = src24[s + 0];
                    g = src24[s + 1];
                    r = src24[s + 2];
                }
                else
                {
                    r = src24[s + 0];
                    g = src24[s + 1];
                    b = src24[s + 2];
                }

                int rHalf = rErr / 2;
                int gHalf = gErr / 2;
                int bHalf = bErr / 2;

                byte rr = ClampToByte(r + rHalf);
                byte gg = ClampToByte(g + gHalf);
                byte bb = ClampToByte(b + bHalf);

                ushort px = Pack16(new DexCol(rr, gg, bb));

                if (px == _colourKey16)
                {
                    dst16[i] = px;
                    rErr = 0;
                    gErr = 0;
                    bErr = 0;
                    continue;
                }

                dst16[i] = px;

                // Back-calc approximate RGB from the packed 16-bit value to get dithering error.
                Unpack16(px, out byte pr, out byte pg, out byte pb);

                rErr = rr - pr;
                gErr = gg - pg;
                bErr = bb - pb;
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::SaveChunky(unsigned int, char*, unsigned char, signed char)
        // Dispatcher: 0 => BMP, 1 => DXP, 2 => TGA (matches decompile).
        // Returns 1/0.
        // --------------------------------------------------------------------
        internal uint SaveChunky(uint chunkyId, string path, byte format, sbyte dxpLevel)
        {
            if (string.IsNullOrEmpty(path))
            {
                return 0;
            }

            if (!ValidChunky(chunkyId))
            {
                return 0;
            }

            DexterFile.FileHandle file = DexterFile.FileOpen(path, 0x01);
            if (!file.IsValid)
            {
                return 0;
            }

            try
            {
                if (format == 0)
                {
                    // Not present in this GFX10 excerpt as meaningful saver (BMP saver is earlier in file).
                    return 0;
                }

                if (format == 2)
                {
                    SaveChunkyTGA(chunkyId, file);
                    return 1;
                }

                if (format == 1)
                {
                    SaveChunkyDXP(chunkyId, file, dxpLevel);
                    return 1;
                }

                return 0;
            }
            finally
            {
                DexterFile.FileClose(file);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::SaveChunkyTGA(unsigned int, PHYSFS_File*)
        // Managed: writes palette and pixel data bottom-up, 8-bit or 16-bit.
        // --------------------------------------------------------------------
        internal void SaveChunkyTGA(uint chunkyId, DexterFile.FileHandle file)
        {
            if (!file.IsValid)
            {
                return;
            }

            ChunkySurface? c = ChunkyAddress(chunkyId);
            if (c == null)
            {
                return;
            }

            byte bpp = (byte)c.BitsPerPixel;

            // Header
            DexterFile.FileWriteByte(file, 0); // id length
            DexterFile.FileWriteByte(file, 1); // color map type
            DexterFile.FileWriteByte(file, (byte)(c.BitsPerPixel < 9 ? 1 : 2)); // image type (1=paletted, 2=truecolor)

            DexterEndian.FileWriteWordLSB(file, 0);
            DexterEndian.FileWriteWordLSB(file, 0x100);
            DexterFile.FileWriteByte(file, 0x18); // color map entry size (24)

            DexterEndian.FileWriteWordLSB(file, 0); // x origin
            DexterEndian.FileWriteWordLSB(file, 0); // y origin
            DexterEndian.FileWriteWordLSB(file, (ushort)c.Width);
            DexterEndian.FileWriteWordLSB(file, (ushort)c.Height);

            DexterFile.FileWriteByte(file, bpp);
            DexterFile.FileWriteByte(file, 0);

            // Palette as BGR
            for (int i = 0; i < 256; i++)
            {
                DexterFile.FileWriteByte(file, _paletteB[i]);
                DexterFile.FileWriteByte(file, _paletteG[i]);
                DexterFile.FileWriteByte(file, _paletteR[i]);
            }

            int bytesPerRow = checked(c.Width * (c.BitsPerPixel >> 3));
            byte[] row = new byte[bytesPerRow];

            // Write bottom-up (as in decompile)
            for (int y = c.Height - 1; y >= 0; y--)
            {
                if (c.BitsPerPixel == 8)
                {
                    c.Pixels8.Slice(y * c.Width, c.Width).CopyTo(row);
                    _ = DexterFile.FileWrite(file, row, 0, row.Length);
                    continue;
                }

                // 16-bit little-endian
                Span<ushort> src = c.Pixels16.Slice(y * c.Width, c.Width);
                for (int x = 0; x < c.Width; x++)
                {
                    ushort v = src[x];
                    int o = x * 2;
                    row[o + 0] = (byte)(v & 0xFF);
                    row[o + 1] = (byte)(v >> 8);
                }

                _ = DexterFile.FileWrite(file, row, 0, row.Length);
            }
        }

        // --------------------------------------------------------------------
        // DexterGFX::SaveChunkyDXP(unsigned int, PHYSFS_File*, signed char)
        // Managed: writes 'DXP1' + palette (optional) + optionally z-compressed payload.
        // Note: decompile contains extra bit-masking for max depth; this is kept best-effort.
        // --------------------------------------------------------------------
        internal void SaveChunkyDXP(uint chunkyId, DexterFile.FileHandle file, sbyte level)
        {
            if (!file.IsValid)
            {
                return;
            }

            ChunkySurface? c = ChunkyAddress(chunkyId);
            if (c == null)
            {
                return;
            }

            bool is8 = c.BitsPerPixel == 8;
            bool compress = is8 || level != 0;

            // DXP on disk is little-endian.
            DexterEndian.FileWriteLongLSB(file, 0x31505844u); // 'DXP1'
            DexterFile.FileWriteByte(file, 0); // version
            DexterFile.FileWriteByte(file, (byte)(is8 ? 1 : 0)); // palette present flag like decompile

            DexterEndian.FileWriteWordLSB(file, (ushort)c.Width);
            DexterEndian.FileWriteWordLSB(file, (ushort)c.Height);

            DexterFile.FileWriteByte(file, (byte)c.BitsPerPixel);
            DexterFile.FileWriteByte(file, (byte)(compress ? 1 : 0));

            byte[] raw = GetChunkyRawBytes(c);

            // Apply MaxDepthDXP style masking for 24-bit payload (best-effort)
            if (c.BitsPerPixel == 24 && _maxDepthDxp < 24)
            {
                int shift = 24 - _maxDepthDxp;
                if (shift > 0)
                {
                    for (int i = 0; i + 2 < raw.Length; i += 3)
                    {
                        raw[i + 0] = (byte)(raw[i + 0] & (byte)(0xFE << (shift - 1)));
                        raw[i + 1] = (byte)(raw[i + 1] & (byte)(0xFF << shift));
                        raw[i + 2] = (byte)(raw[i + 2] & (byte)(0xFF << shift));
                    }
                }
            }

            byte[] payload = compress ? ZCompress(raw, level) : raw;

            DexterEndian.FileWriteLongLSB(file, unchecked((uint)payload.Length));

            // Palette block for 8-bit
            if (is8)
            {
                for (int i = 0; i < 256; i++)
                {
                    DexterFile.FileWriteByte(file, _paletteR[i]);
                    DexterFile.FileWriteByte(file, _paletteG[i]);
                    DexterFile.FileWriteByte(file, _paletteB[i]);
                }
            }

            _ = DexterFile.FileWrite(file, payload, 0, payload.Length);
        }

        // --------------------------------------------------------------------
        // Helpers (managed equivalents)
        // --------------------------------------------------------------------

        private bool InitChunkyManaged(uint chunkyId, ushort width, ushort height, int bitsPerPixel)
        {
            if (bitsPerPixel != 8 && bitsPerPixel != 16 && bitsPerPixel != 24)
            {
                // This port only supports 8/16 for ChunkySurface. 24-bit is converted into dest depth earlier.
                return false;
            }

            // Existing engine stores chunkies in _chunkies[] and uses AllocateChunky/UseChunky.
            // Here: overwrite or allocate fresh.
            if (bitsPerPixel == 24)
            {
                // No 24-bit chunky surface in this port.
                return false;
            }

            if ((uint)_chunkies.Length <= chunkyId)
            {
                return false;
            }

            AllocateChunky((int)chunkyId, width, height, bitsPerPixel);
            return true;
        }

        // DexterGFX::ChunkyAddress(unsigned int)
        // Managed replacement: returns the surface object (pixel address is not exposed in managed code).
        internal ChunkySurface? ChunkyAddress(uint chunkyId)
        {
            if (chunkyId >= (uint)_chunkies.Length)
            {
                return null;
            }

            return _chunkies[chunkyId];
        }

        private static int AlignUp(int value, int align)
        {
            int mask = align - 1;
            return (value + mask) & ~mask;
        }

        private byte Make8BitPixel(byte r, byte g, byte b)
        {
            // Uses the existing nearest palette mapping used by your engine.
            return FindClosestPaletteIndex(r, g, b);
        }

        private void Unpack16(ushort value, out byte r, out byte g, out byte b)
        {
            if (_pixelFormat16 == PixelFormat16.Rgb565)
            {
                int rr = (value >> 11) & 0x1F;
                int gg = (value >> 5) & 0x3F;
                int bb = value & 0x1F;

                r = (byte)((rr * 255 + 15) / 31);
                g = (byte)((gg * 255 + 31) / 63);
                b = (byte)((bb * 255 + 15) / 31);
                return;
            }

            // RGB555
            int r5 = (value >> 10) & 0x1F;
            int g5 = (value >> 5) & 0x1F;
            int b5 = value & 0x1F;

            r = (byte)((r5 * 255 + 15) / 31);
            g = (byte)((g5 * 255 + 15) / 31);
            b = (byte)((b5 * 255 + 15) / 31);
        }

        private void WriteConvertedScanlineToChunky(uint chunkyId, int y, ReadOnlySpan<byte> src24, ushort width, ushort destDepthBits, bool flipBgr)
        {
            ChunkySurface? c = ChunkyAddress(chunkyId);
            if (c == null)
            {
                return;
            }

            if ((uint)y >= (uint)c.Height)
            {
                return;
            }

            byte marker = flipBgr ? (byte)'\n' : (byte)0;

            if (c.BitsPerPixel == 8)
            {
                Span<byte> dst = c.Pixels8.Slice(y * c.Width, width);
                ScanLineToBuffer(dst, src24, width, destDepthBits, marker);
                return;
            }

            if (c.BitsPerPixel == 16)
            {
                Span<ushort> dst = c.Pixels16.Slice(y * c.Width, width);
                ScanLineToBuffer(dst, src24, width, destDepthBits, marker);
            }
        }

        private static void CopyPayloadToChunky(ChunkySurface dst, byte[] payload)
        {
            if (dst.BitsPerPixel == 8)
            {
                payload.AsSpan(0, dst.Width * dst.Height).CopyTo(dst.Pixels8);
                return;
            }

            if (dst.BitsPerPixel == 16)
            {
                WriteRaw16LittleEndian(dst, payload);
            }
        }

        private static void WriteRaw16LittleEndian(ChunkySurface dst, byte[] raw)
        {
            int count = dst.Width * dst.Height;
            if (raw.Length < count * 2)
            {
                return;
            }

            Span<ushort> px = dst.Pixels16;
            for (int i = 0; i < count; i++)
            {
                int o = i * 2;
                px[i] = (ushort)(raw[o + 0] | (raw[o + 1] << 8));
            }
        }

        private static byte[] GetChunkyRawBytes(ChunkySurface c)
        {
            if (c.BitsPerPixel == 8)
            {
                byte[] raw = new byte[checked(c.Width * c.Height)];
                c.Pixels8.CopyTo(raw);
                return raw;
            }

            // 16-bit
            byte[] raw16 = new byte[checked(c.Width * c.Height * 2)];
            Span<ushort> px = c.Pixels16;

            for (int i = 0; i < px.Length; i++)
            {
                ushort v = px[i];
                int o = i * 2;
                raw16[o + 0] = (byte)(v & 0xFF);
                raw16[o + 1] = (byte)(v >> 8);
            }

            return raw16;
        }

        // --------------------------------------------------------------------
        // Managed compression: zlib/deflate compatible (best-effort).
        // This replaces DexterCompress::ZCompress/ZDeCompress for now.
        // --------------------------------------------------------------------

        private static byte[] ZCompress(byte[] raw, sbyte level)
        {
            using MemoryStream ms = new();
            using (System.IO.Compression.DeflateStream ds = new(ms, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            {
                ds.Write(raw, 0, raw.Length);
            }
            return ms.ToArray();
        }

        private static byte[] ZDecompress(byte[] packed, int expectedSize)
        {
            using MemoryStream input = new(packed, writable: false);
            using System.IO.Compression.DeflateStream ds = new(input, System.IO.Compression.CompressionMode.Decompress);
            using MemoryStream output = new(expectedSize > 0 ? expectedSize : 0);
            ds.CopyTo(output);
            byte[] raw = output.ToArray();

            // If expected size is known and differs, still return what is available (matches robust engine behavior).
            return raw;
        }

        // --------------------------------------------------------------------
        // Little-endian read/write helpers.
        // Keep these local helpers so call sites stay clean and no MSB/LSB mix-ups happen.
        // --------------------------------------------------------------------

        private static uint ReadUInt32LSB(DexterFile.FileHandle file)
        {
            return DexterEndian.FileReadLongLSB(file);
        }

        private static void WriteUInt16LSB(DexterFile.FileHandle file, ushort value)
        {
            DexterEndian.FileWriteWordLSB(file, value);
        }

        private static void WriteUInt32LSB(DexterFile.FileHandle file, uint value)
        {
            DexterEndian.FileWriteLongLSB(file, value);
        }

        private static void WriteInt32LSB(DexterFile.FileHandle file, int value)
        {
            DexterEndian.FileWriteLongLSB(file, unchecked((uint)value));
        }
    }
}