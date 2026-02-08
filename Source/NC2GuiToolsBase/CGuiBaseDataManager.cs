using OpenVikings.NXBasics;
using OpenVikings.NXBaseGui;

namespace OpenVikings.NC2GuiToolsBase
{
    internal sealed class CGuiBaseDataManager : IDisposable
    {
        internal static CGuiBaseDataManager? sTheObjectPtr;

        // Fonts (RE: mStaticVars is actually the "font08" pointer)
        internal static CFont? Font08;
        internal static CFont? Font10;
        internal static CFont? Font12;
        internal static CFont? FontDebug;

        // Font palettes
        internal static CPalette? PaletteFontWhite;
        internal static CPalette? PaletteFontDimmed;
        internal static CPalette? PaletteFontRed;
        internal static CPalette? PaletteFontDark;

        // Window bob + other palettes
        internal static object? GuiWindowBobBmd; // type unknown in snippet; keep as object for now
        internal static CPalette? PaletteFrame;
        internal static CPalette? PaletteBgNormal;
        internal static CPalette? PalettePapyrus;
        internal static CPalette? PaletteBarHitpoints;
        internal static CPalette? PaletteBarStandart;
        internal static CPalette? PaletteBarDisabled;
        internal static CPalette? PaletteIconsLeft;
        internal static CPalette? PaletteContext;

        // Dynamic data flag + bitmaps (0x30 block in RE)
        private static bool _dynamicLoaded;
        internal static CBitmap? Bg;
        internal static CBitmap? BgButton;
        internal static CBitmap? BgSelected;
        internal static CBitmap? BgButtonHilite;
        internal static CBitmap? BgHeadline;

        private bool _disposed;

        internal CGuiBaseDataManager()
        {
            sTheObjectPtr = this;

            // RE does a few memsets over global blocks; in C# we explicitly initialize the statics.
            Font08 = NXBasicsApi.XB_Storable_LoadObject<CFont>("data\\gui\\fonts\\font08.fnt");
            Font10 = NXBasicsApi.XB_Storable_LoadObject<CFont>("data\\gui\\fonts\\font10.fnt");
            Font12 = NXBasicsApi.XB_Storable_LoadObject<CFont>("data\\gui\\fonts\\font12.fnt");
            FontDebug = NXBasicsApi.XB_Storable_LoadObject<CFont>("data\\gui\\fonts\\fontdebug.fnt");

            // *(undefined4 *)(Font10 + 0x10) = 0xffffffff;
            Font10?.SpaceAdjust = -1;

            // Property "font_space_small" changes spacing rules.
            bool fontSpaceSmall = NMasterPropertyManager.CPropertyManager.sTheObjectPtr
                .Property_DoesExists("font_space_small");

            if (fontSpaceSmall)
            {
                if (Font08 != null) { Font08.SpaceAdjust = -1; }
                if (Font10 != null) { Font10.SpaceAdjust = -2; }
                if (Font12 != null) { Font12.SpaceAdjust = -2; }
            }

            PaletteFontWhite = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\font_white.pcx");
            PaletteFontDimmed = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\font_dimmed.pcx");
            PaletteFontRed = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\font_red.pcx");
            PaletteFontDark = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\font_dark.pcx");

            // All fonts get the "white" font palette initially.
            if (Font08 != null && PaletteFontWhite != null) { Font08.SetPalettePtr(PaletteFontWhite); }
            if (Font10 != null && PaletteFontWhite != null) { Font10.SetPalettePtr(PaletteFontWhite); }
            if (Font12 != null && PaletteFontWhite != null) { Font12.SetPalettePtr(PaletteFontWhite); }
            if (FontDebug != null && PaletteFontWhite != null) { FontDebug.SetPalettePtr(PaletteFontWhite); }

            string windowBobPath = NC2Logic.LanguageTool_GetFilename("data\\gui\\lang\\%s\\bobs\\ls_gui_window.bmd", false);

            GuiWindowBobBmd = NXBasicsApi.XB_Storable_LoadObject<object>(windowBobPath);

            PaletteFrame = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\frame.pcx");
            PaletteBgNormal = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\bg_normal.pcx");
            PalettePapyrus = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\papyrus.pcx");
            PaletteBarHitpoints = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\bar_hitpoints.pcx");
            PaletteBarStandart = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\bar_standart.pcx");
            PaletteBarDisabled = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\bar_disabled.pcx");
            PaletteIconsLeft = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\IconsLeft.pcx");
            PaletteContext = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\Context.pcx");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            DynamicData_Free();

            // Release all statics (RE calls vtable+0x20). In managed code, Dispose is the equivalent.
            SafeDispose(PaletteContext);
            PaletteContext = null;

            SafeDispose(PaletteIconsLeft);
            PaletteIconsLeft = null;

            SafeDispose(PaletteBarDisabled);
            PaletteBarDisabled = null;

            SafeDispose(PaletteBarStandart);
            PaletteBarStandart = null;

            SafeDispose(PaletteBarHitpoints);
            PaletteBarHitpoints = null;

            SafeDispose(PalettePapyrus);
            PalettePapyrus = null;

            SafeDispose(PaletteBgNormal);
            PaletteBgNormal = null;

            SafeDispose(PaletteFrame);
            PaletteFrame = null;

            SafeDispose(GuiWindowBobBmd);
            GuiWindowBobBmd = null;

            SafeDispose(PaletteFontDark);
            PaletteFontDark = null;

            SafeDispose(PaletteFontRed);
            PaletteFontRed = null;

            SafeDispose(PaletteFontDimmed);
            PaletteFontDimmed = null;

            SafeDispose(PaletteFontWhite);
            PaletteFontWhite = null;

            SafeDispose(FontDebug);
            FontDebug = null;

            SafeDispose(Font12);
            Font12 = null;

            SafeDispose(Font10);
            Font10 = null;

            SafeDispose(Font08);
            Font08 = null;

            sTheObjectPtr = null;
        }

        internal static void DynamicData_Free()
        {
            // PaletteChanged calls exist to notify dependents; keep order as in RE.
            if (PaletteFontWhite != null) { CPalette.PaletteChanged(PaletteFontWhite); }
            if (PaletteFontDimmed != null) { CPalette.PaletteChanged(PaletteFontDimmed); }
            if (PaletteFontRed != null) { CPalette.PaletteChanged(PaletteFontRed); }
            if (PaletteFontDark != null) { CPalette.PaletteChanged(PaletteFontDark); }
            if (PaletteFrame != null) { CPalette.PaletteChanged(PaletteFrame); }
            if (PaletteBgNormal != null) { CPalette.PaletteChanged(PaletteBgNormal); }
            if (PalettePapyrus != null) { CPalette.PaletteChanged(PalettePapyrus); }

            if (!_dynamicLoaded)
            {
                return;
            }

            SafeDispose(BgHeadline);
            BgHeadline = null;

            SafeDispose(BgButtonHilite);
            BgButtonHilite = null;

            SafeDispose(BgSelected);
            BgSelected = null;

            SafeDispose(BgButton);
            BgButton = null;

            SafeDispose(Bg);
            Bg = null;

            _dynamicLoaded = false;
        }

        internal static void DynamicData_Load()
        {
            DynamicData_Free();

            // RE reads desktop->backbuffer->colorDepthByte
            CDesktop? desktop = CDesktop.Current;
            if (desktop == null || desktop.BackBuffer == null)
            {
                // In RE this would likely crash; in C# we fail fast with a clear error.
                throw new InvalidOperationException("CDesktop.Current.BackBuffer must be initialized before DynamicData_Load().");
            }

            byte colorDepthBits = desktop.BackBuffer.ColorDepthBits;

            Bg = LoadBitmapFromPcx("data\\gui\\bitmaps\\bg.pcx", colorDepthBits);
            BgButton = LoadBitmapFromPcx("data\\gui\\bitmaps\\bg_button.pcx", colorDepthBits);
            BgSelected = LoadBitmapFromPcx("data\\gui\\bitmaps\\bg_selected.pcx", colorDepthBits);
            BgButtonHilite = LoadBitmapFromPcx("data\\gui\\bitmaps\\bg_button_hilite.pcx", colorDepthBits);
            BgHeadline = LoadBitmapFromPcx("data\\gui\\bitmaps\\bg_headline.pcx", colorDepthBits);

            _dynamicLoaded = true;
        }

        private static CBitmap LoadBitmapFromPcx(string pcxPath, byte colorDepthBits)
        {
            // Mirrors: CPicture(pcx), new CBitmap(width,height,depth), CopyIntoBitmap, ~CPicture
            using CPicture picture = new(pcxPath);
            CBitmap bitmap = new(picture.Width, picture.Height, colorDepthBits);
            picture.CopyIntoBitmap(bitmap, 0, 0);
            return bitmap;
        }

        private static void SafeDispose(object? obj)
        {
            if (obj is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}