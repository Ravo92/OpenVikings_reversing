using OpenVikings.Interfaces;
using OpenVikings.NXBasics;

namespace OpenVikings.NC2GuiToolsBase
{
    // NC2GuiToolsBase::CGuiBaseDataManager
    // Notes:
    // - The original code uses a mostly-static resource container plus a "dynamic" subset (bitmaps) that is recreated on demand.
    // - Ownership is handled via IDisposable. Where the original uses vtable+0x20 destructors, Dispose() is used.
    // - The original constructor preloads fonts, palettes and a localized BMD. DynamicData_Load recreates background bitmaps.
    internal sealed class CGuiBaseDataManager : IDisposable
    {
        internal static CGuiBaseDataManager? sTheObjectPtr;

        // ---- Static fonts (mStaticVars / DAT_...) ----
        internal static CFont? Font08;       // mStaticVars
        internal static CFont? Font10;       // DAT_1003a4440
        internal static CFont? Font12;       // DAT_1003a4448
        internal static CFont? FontDebug;    // DAT_1003a4450

        // ---- Static palettes (DAT_...) ----
        internal static CPalette? PaletteFontWhite;     // DAT_1003a4458
        internal static CPalette? PaletteFontDimmed;    // DAT_1003a4460
        internal static CPalette? PaletteFontRed;       // DAT_1003a4468
        internal static CPalette? PaletteFontDark;      // DAT_1003a4470

        internal static CPalette? PaletteFrame;         // DAT_1003a4480
        internal static CPalette? PaletteBgNormal;      // DAT_1003a4488
        internal static CPalette? PalettePapyrus;       // DAT_1003a4490
        internal static CPalette? PaletteBarHitpoints;  // DAT_1003a4498
        internal static CPalette? PaletteBarStandard;   // DAT_1003a44a0
        internal static CPalette? PaletteBarDisabled;   // DAT_1003a44a8
        internal static CPalette? PaletteIconsLeft;     // DAT_1003a44b0
        internal static CPalette? PaletteContext;       // DAT_1003a44b8

        // Localized resource (DAT_1003a4478). Type is unknown in the decompile; treat as IDisposable.
        internal static IDisposable? GuiWindowBmd;

        // ---- Dynamic bitmaps (DAT_...) ----
        private static bool _dynamicLoaded; // DAT_1003a44c0
        internal static CBitmap? Bg;               // DAT_1003a44c8
        internal static CBitmap? BgButton;         // DAT_1003a44d0
        internal static CBitmap? BgSelected;       // DAT_1003a44d8
        internal static CBitmap? BgButtonHilite;   // DAT_1003a44e0
        internal static CBitmap? BgHeadline;       // DAT_1003a44e8

        private bool _disposed;

        internal CGuiBaseDataManager()
        {
            sTheObjectPtr = this;

            // Fonts
            Font08 = LoadObjectAs<CFont>("data\\gui\\fonts\\font08.fnt");
            Font10 = LoadObjectAs<CFont>("data\\gui\\fonts\\font10.fnt");
            Font12 = LoadObjectAs<CFont>("data\\gui\\fonts\\font12.fnt");
            FontDebug = LoadObjectAs<CFont>("data\\gui\\fonts\\fontdebug.fnt");

            // Mirrors: *(Font10 + 0x10) = 0xffffffff;
            // Exact meaning is unknown (likely spacing/kerning override). Keep as-is if the field exists.
            TrySetFontInt32(Font10, -1);

            // Property-driven spacing change
            bool hasSmallSpace = false;

            if (NMasterPropertyManager.CPropertyManager.sTheObjectPtr is IPropertyManager propertyManager)
            {
                hasSmallSpace = propertyManager.Exists("font_space_small");
            }

            if (hasSmallSpace)
            {
                TrySetFontInt32(Font08, -1);
                TrySetFontInt32(Font10, unchecked((int)0xFFFFFFFE));
                TrySetFontInt32(Font12, unchecked((int)0xFFFFFFFE));
            }

            // Font palettes
            PaletteFontWhite = LoadPalette("data\\gui\\palettes\\font_white.pcx");
            PaletteFontDimmed = LoadPalette("data\\gui\\palettes\\font_dimmed.pcx");
            PaletteFontRed = LoadPalette("data\\gui\\palettes\\font_red.pcx");
            PaletteFontDark = LoadPalette("data\\gui\\palettes\\font_dark.pcx");

            // Apply default font palette (white)
            if (PaletteFontWhite != null)
            {
                SetFontPalette(Font08, PaletteFontWhite);
                SetFontPalette(Font10, PaletteFontWhite);
                SetFontPalette(Font12, PaletteFontWhite);
                SetFontPalette(FontDebug, PaletteFontWhite);
            }

            // Localized BMD
            string bmdPath = LanguageTool_GetFilename("data\\gui\\lang\\%s\\bobs\\ls_gui_window.bmd", false);
            GuiWindowBmd = NXBasicsApi.XB_Storable_LoadObject(bmdPath) as IDisposable;

            // Other palettes
            PaletteFrame = LoadPalette("data\\gui\\palettes\\frame.pcx");
            PaletteBgNormal = LoadPalette("data\\gui\\palettes\\bg_normal.pcx");
            PalettePapyrus = LoadPalette("data\\gui\\palettes\\papyrus.pcx");
            PaletteBarHitpoints = LoadPalette("data\\gui\\palettes\\bar_hitpoints.pcx");
            PaletteBarStandard = LoadPalette("data\\gui\\palettes\\bar_standart.pcx");
            PaletteBarDisabled = LoadPalette("data\\gui\\palettes\\bar_disabled.pcx");
            PaletteIconsLeft = LoadPalette("data\\gui\\palettes\\IconsLeft.pcx");
            PaletteContext = LoadPalette("data\\gui\\palettes\\Context.pcx");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~CGuiBaseDataManager()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            // Mirrors: DynamicData_Free();
            DynamicData_Free();

            // Dispose static resources in roughly the original order
            DisposeAndNull(ref PaletteContext);
            DisposeAndNull(ref PaletteIconsLeft);
            DisposeAndNull(ref PaletteBarDisabled);
            DisposeAndNull(ref PaletteBarStandard);
            DisposeAndNull(ref PaletteBarHitpoints);
            DisposeAndNull(ref PalettePapyrus);
            DisposeAndNull(ref PaletteBgNormal);
            DisposeAndNull(ref PaletteFrame);
            DisposeAndNull(ref GuiWindowBmd);

            DisposeAndNull(ref PaletteFontDark);
            DisposeAndNull(ref PaletteFontRed);
            DisposeAndNull(ref PaletteFontDimmed);
            DisposeAndNull(ref PaletteFontWhite);

            DisposeAndNull(ref FontDebug);
            DisposeAndNull(ref Font12);
            DisposeAndNull(ref Font10);
            DisposeAndNull(ref Font08);

            sTheObjectPtr = null;
            _disposed = true;
        }

        // NC2GuiToolsBase::CGuiBaseDataManager::DynamicData_Free()
        internal static void DynamicData_Free()
        {
            // Mirrors: PaletteChanged(...) calls prior to freeing/overwriting bitmaps.
            PaletteChangedSafe(PaletteFontWhite);
            PaletteChangedSafe(PaletteFontDimmed);
            PaletteChangedSafe(PaletteFontRed);
            PaletteChangedSafe(PaletteFontDark);
            PaletteChangedSafe(PaletteFrame);
            PaletteChangedSafe(PaletteBgNormal);
            PaletteChangedSafe(PalettePapyrus);

            if (!_dynamicLoaded)
            {
                return;
            }

            DisposeAndNull(ref BgHeadline);
            DisposeAndNull(ref BgButtonHilite);
            DisposeAndNull(ref BgSelected);
            DisposeAndNull(ref BgButton);
            DisposeAndNull(ref Bg);

            _dynamicLoaded = false;
        }

        // NC2GuiToolsBase::CGuiBaseDataManager::DynamicData_Load()
        // In the original, bpp is read from the desktop bitmap. Here it is passed explicitly.
        internal static void DynamicData_Load(byte bpp)
        {
            DynamicData_Free();

            Bg = LoadBitmapFromPicture("data\\gui\\bitmaps\\bg.pcx", bpp);
            BgButton = LoadBitmapFromPicture("data\\gui\\bitmaps\\bg_button.pcx", bpp);
            BgSelected = LoadBitmapFromPicture("data\\gui\\bitmaps\\bg_selected.pcx", bpp);
            BgButtonHilite = LoadBitmapFromPicture("data\\gui\\bitmaps\\bg_button_hilite.pcx", bpp);
            BgHeadline = LoadBitmapFromPicture("data\\gui\\bitmaps\\bg_headline.pcx", bpp);

            _dynamicLoaded = true;
        }

        private static CPalette? LoadPalette(string path)
        {
            object? obj = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture(path);
            return obj as CPalette;
        }

        private static T? LoadObjectAs<T>(string path) where T : class
        {
            object? obj = NXBasicsApi.XB_Storable_LoadObject(path);
            return obj as T;
        }

        private static void SetFontPalette(CFont? font, CPalette palette)
        {
            if (font == null)
            {
                return;
            }

            CFont.SetPalettePtr(font, palette);
        }

        private static void PaletteChangedSafe(CPalette? palette)
        {
            if (palette == null)
            {
                return;
            }

            CPalette.PaletteChanged(palette);
        }

        private static void DisposeAndNull<T>(ref T? obj) where T : class
        {
            if (obj is IDisposable disposable)
            {
                disposable.Dispose();
            }

            obj = null;
        }

        private static CBitmap? LoadBitmapFromPicture(string pcxPath, byte bpp)
        {
            // Mirrors:
            //   CPicture pic("...pcx");
            //   CBitmap bmp(pic.Width, pic.Height, bpp);
            //   CopyIntoBitmap(pic, bmp, 0, 0);
            using CPicture picture = new(pcxPath);

            uint w = picture.Width;
            uint h = picture.Height;

            CBitmap bitmap = new(w, h, bpp);
            CBitmap.CopyIntoBitmap(picture, bitmap, 0, 0);

            return bitmap;
        }

        private static string LanguageTool_GetFilename(string format, bool useFallback)
        {
            // Mirrors: NC2Logic::LanguageTool_GetFilename("...%s...", local_128, false)
            // Assumes a managed wrapper exists that returns a string.
            return LanguageTool_GetFilename(format, useFallback);
        }

        private static void TrySetFontInt32(CFont? font, int value)
        {
            if (font == null)
            {
                return;
            }

            // The decompile writes to (font + 0x10). If a proper property exists, prefer it.
            // Keep this method as a single place to adjust once the real field is identified.
            font.TrySetInternalInt32_0x10(value);
        }
    }
}