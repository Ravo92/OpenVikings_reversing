using OpenVikings.NC2Logic;
using OpenVikings.NXBaseGui;
using OpenVikings.NXBasics;

namespace OpenVikings.NC2GuiToolsBase
{
    // Port of: NC2GuiToolsBase::CGuiBaseDataManager
    internal sealed class CGuiBaseDataManager : IDisposable
    {
        internal static CGuiBaseDataManager? sTheObjectPtr;

        // Fonts (C++: mStaticVars / DAT_* globals)
        internal static CFont? Font08;
        internal static CFont? Font10;
        internal static CFont? Font12;
        internal static CFont? FontDebug;

        // Font palettes
        internal static CPalette? PalFontWhite;
        internal static CPalette? PalFontDimmed;
        internal static CPalette? PalFontRed;
        internal static CPalette? PalFontDark;

        // Language-dependent object (C++: DAT_1003a4478)
        internal static CStorable? GuiWindowBmd;

        // Other palettes
        internal static CPalette? PalFrame;
        internal static CPalette? PalBgNormal;
        internal static CPalette? PalPapyrus;
        internal static CPalette? PalBarHitpoints;
        internal static CPalette? PalBarStandard;
        internal static CPalette? PalBarDisabled;
        internal static CPalette? PalIconsLeft;
        internal static CPalette? PalContext;

        // Dynamic data (C++: DAT_1003a44c0..DAT_1003a44e8)
        private static bool _dynamicLoaded;

        internal static CBitmap? BmpBg;
        internal static CBitmap? BmpBgButton;
        internal static CBitmap? BmpBgSelected;
        internal static CBitmap? BmpBgButtonHilite;
        internal static CBitmap? BmpBgHeadline;

        private bool _disposed;

        // --------------------------------------------------------------------
        // Ctor
        // --------------------------------------------------------------------
        internal CGuiBaseDataManager()
        {
            sTheObjectPtr = this;

            ResetDynamicStaticsOnly();

            Font08 = NXBasicsApi.XB_Storable_LoadObject<CFont>("data\\gui\\fonts\\font08.fnt");
            Font10 = NXBasicsApi.XB_Storable_LoadObject<CFont>("data\\gui\\fonts\\font10.fnt");
            Font12 = NXBasicsApi.XB_Storable_LoadObject<CFont>("data\\gui\\fonts\\font12.fnt");
            FontDebug = NXBasicsApi.XB_Storable_LoadObject<CFont>("data\\gui\\fonts\\fontdebug.fnt");

            // C++: *(int*)(Font10 + 0x10) = 0xFFFFFFFF
            Font10?.SetSpacing(unchecked((int)0xFFFFFFFF));

            // C++: CPropertyManager::sTheObjectPtr->Property_DoesExists("font_space_small")
            bool hasSmallSpace = false;
            if (NMasterPropertyManager.CPropertyManager.sTheObjectPtr != null)
            {
                hasSmallSpace = NMasterPropertyManager.CPropertyManager.sTheObjectPtr.Property_DoesExists("font_space_small");
            }

            if (hasSmallSpace)
            {
                Font08?.SetSpacing(unchecked((int)0xFFFFFFFF));
                Font10?.SetSpacing(unchecked((int)0xFFFFFFFE));
                Font12?.SetSpacing(unchecked((int)0xFFFFFFFE));
            }

            PalFontWhite = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\font_white.pcx");
            PalFontDimmed = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\font_dimmed.pcx");
            PalFontRed = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\font_red.pcx");
            PalFontDark = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\font_dark.pcx");

            Font08?.SetPalettePtr(PalFontWhite);
            Font10?.SetPalettePtr(PalFontWhite);
            Font12?.SetPalettePtr(PalFontWhite);
            FontDebug?.SetPalettePtr(PalFontWhite);

            // C++: LanguageTool_GetFilename(template, buffer, false) (return ignored)
            _ = LanguageTool.GetFilename("data\\gui\\lang\\%s\\bobs\\ls_gui_window.bmd", out string bmdPath, false);
            GuiWindowBmd = NXBasicsApi.XB_Storable_LoadObject(bmdPath);

            PalFrame = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\frame.pcx");
            PalBgNormal = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\bg_normal.pcx");
            PalPapyrus = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\papyrus.pcx");
            PalBarHitpoints = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\bar_hitpoints.pcx");
            PalBarStandard = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\bar_standart.pcx");
            PalBarDisabled = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\bar_disabled.pcx");
            PalIconsLeft = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\IconsLeft.pcx");
            PalContext = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\Context.pcx");
        }

        // --------------------------------------------------------------------
        // Dispose (C++ dtor equivalent)
        // --------------------------------------------------------------------
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            DynamicData_Free();

            DisposeAndNull(ref PalContext);
            DisposeAndNull(ref PalIconsLeft);
            DisposeAndNull(ref PalBarDisabled);
            DisposeAndNull(ref PalBarStandard);
            DisposeAndNull(ref PalBarHitpoints);
            DisposeAndNull(ref PalPapyrus);
            DisposeAndNull(ref PalBgNormal);
            DisposeAndNull(ref PalFrame);

            DisposeAndNull(ref GuiWindowBmd);

            DisposeAndNull(ref PalFontDark);
            DisposeAndNull(ref PalFontRed);
            DisposeAndNull(ref PalFontDimmed);
            DisposeAndNull(ref PalFontWhite);

            DisposeAndNull(ref FontDebug);
            DisposeAndNull(ref Font12);
            DisposeAndNull(ref Font10);
            DisposeAndNull(ref Font08);

            ResetDynamicStaticsOnly();
            sTheObjectPtr = null;

            _disposed = true;
        }

        // --------------------------------------------------------------------
        // DynamicData_Free
        // --------------------------------------------------------------------
        internal static void DynamicData_Free()
        {
            PalFontWhite?.PaletteChanged();
            PalFontDimmed?.PaletteChanged();
            PalFontRed?.PaletteChanged();
            PalFontDark?.PaletteChanged();
            PalFrame?.PaletteChanged();
            PalBgNormal?.PaletteChanged();
            PalPapyrus?.PaletteChanged();

            if (_dynamicLoaded)
            {
                DisposeAndNull(ref BmpBgHeadline);
                DisposeAndNull(ref BmpBgButtonHilite);
                DisposeAndNull(ref BmpBgSelected);
                DisposeAndNull(ref BmpBgButton);
                DisposeAndNull(ref BmpBg);

                _dynamicLoaded = false;
            }
        }

        // --------------------------------------------------------------------
        // DynamicData_Load
        // --------------------------------------------------------------------
        internal static void DynamicData_Load()
        {
            DynamicData_Free();

            byte bpp = GetDesktopBitsPerPixel();

            // C++ loads CPicture and copies into newly allocated CBitmap.
            // In this codebase, there are two reasonable approaches:
            //  A) Use XBPictureTool.LoadBitmapOutOfPicture and treat it as already-final bitmap.
            //  B) Rebuild exact flow using CPicture+CopyIntoBitmap (if those types exist).
            //
            // Because NXBasicsApi already exposes XB_PictureTool_LoadBitmapOutOfPicture, prefer A)
            // unless exact behavior requires manual copy.
            //
            // If exact manual-copy is needed later, replace these lines with the CPicture flow.
            BmpBg = NXBasicsApi.XB_PictureTool_LoadBitmapOutOfPicture("data\\gui\\bitmaps\\bg.pcx");
            BmpBgButton = NXBasicsApi.XB_PictureTool_LoadBitmapOutOfPicture("data\\gui\\bitmaps\\bg_button.pcx");
            BmpBgSelected = NXBasicsApi.XB_PictureTool_LoadBitmapOutOfPicture("data\\gui\\bitmaps\\bg_selected.pcx");
            BmpBgButtonHilite = NXBasicsApi.XB_PictureTool_LoadBitmapOutOfPicture("data\\gui\\bitmaps\\bg_button_hilite.pcx");
            BmpBgHeadline = NXBasicsApi.XB_PictureTool_LoadBitmapOutOfPicture("data\\gui\\bitmaps\\bg_headline.pcx");

            // Ensure their bpp matches desktop if your loader doesn't already.
            // If your XBPictureTool enforces correct depth, this is not needed.
            _ = bpp;

            _dynamicLoaded = true;
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------
        private static void ResetDynamicStaticsOnly()
        {
            _dynamicLoaded = false;

            BmpBg = null;
            BmpBgButton = null;
            BmpBgSelected = null;
            BmpBgButtonHilite = null;
            BmpBgHeadline = null;
        }

        private static void DisposeAndNull<T>(ref T? obj) where T : class
        {
            if (obj == null)
            {
                return;
            }

            if (obj is IDisposable disposable)
            {
                disposable.Dispose();
            }

            obj = null;
        }

        private static byte GetDesktopBitsPerPixel()
        {
            // C++: reads a byte from the desktop's display-mode struct.
            // Your desktop exposes a backbuffer CBitmap which exposes BitsPerPixel.
            if (CDesktop.sTheObjectPtr == null)
            {
                return (byte)CBitmap.BitmapFormat.Indexed8;
            }

            return CDesktop.sTheObjectPtr.BackBuffer.BitsPerPixel;
        }
    }
}