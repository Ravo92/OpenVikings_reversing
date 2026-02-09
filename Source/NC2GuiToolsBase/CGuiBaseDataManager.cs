using OpenVikings.NXBaseGui;
using OpenVikings.NXBasics;

namespace OpenVikings.NC2GuiToolsBase
{
    internal sealed class CGuiBaseDataManager : IDisposable
    {
        // RE: sTheObjectPtr = this;
        internal static CGuiBaseDataManager sTheObjectPtr;

        // ===== Static "globals" from the dump (renamed to meaningful fields) =====

        // Fonts
        internal static CFont sFont08;         // mStaticVars in dump (first assignment)
        internal static CFont sFont10;         // DAT_1003a4440
        internal static CFont sFont12;         // DAT_1003a4448
        internal static CFont sFontDebug;      // DAT_1003a4450

        // Font palettes
        internal static CPalette sPalFontWhite;   // DAT_1003a4458
        internal static CPalette sPalFontDimmed;  // DAT_1003a4460
        internal static CPalette sPalFontRed;     // DAT_1003a4468
        internal static CPalette sPalFontDark;    // DAT_1003a4470

        // GUI bobs + palettes
        internal static object sGuiWindowBobs;                          // DAT_1003a4478 (type unknown from snippet)
        internal static CPalette sPalFrame;        // DAT_1003a4480
        internal static CPalette sPalBgNormal;     // DAT_1003a4488
        internal static CPalette sPalPapyrus;      // DAT_1003a4490
        internal static CPalette sPalBarHitpoints; // DAT_1003a4498
        internal static CPalette sPalBarStandard;  // DAT_1003a44a0
        internal static CPalette sPalBarDisabled;  // DAT_1003a44a8
        internal static CPalette sPalIconsLeft;    // DAT_1003a44b0
        internal static CPalette sPalContext;      // DAT_1003a44b8

        // Dynamic data flag + dynamic bitmaps (loaded by DynamicData_Load)
        internal static byte sDynamicDataLoaded;                         // DAT_1003a44c0 (0/1)
        internal static CBitmap sBmpBg;             // DAT_1003a44c8
        internal static CBitmap sBmpBgButton;       // DAT_1003a44d0
        internal static CBitmap sBmpBgSelected;     // DAT_1003a44d8
        internal static CBitmap sBmpBgButtonHilite; // DAT_1003a44e0
        internal static CBitmap sBmpBgHeadline;     // DAT_1003a44e8

        private bool _disposed;

        // NC2GuiToolsBase::CGuiBaseDataManager::CGuiBaseDataManager()
        internal CGuiBaseDataManager()
        {
            sTheObjectPtr = this;

            // DexterMemory::MemorySet(this,'\0',1);
            // In managed code there is nothing meaningful to zero out here; state is tracked via fields.

            // DexterMemory::MemorySet(&mStaticVars,'\0',0xb8);
            ResetStaticState();

            // Load fonts
            sFont08 = (CFont)XB_Storable_LoadObject("data\\gui\\fonts\\font08.fnt");
            sFont10 = (CFont)XB_Storable_LoadObject("data\\gui\\fonts\\font10.fnt");
            sFont12 = (CFont)XB_Storable_LoadObject("data\\gui\\fonts\\font12.fnt");
            sFontDebug = (CFont)XB_Storable_LoadObject("data\\gui\\fonts\\fontdebug.fnt");

            // *(undefined4 *)(DAT_1003a4440 + 0x10) = 0xffffffff;
            // This is a field write inside CFont (offset +0x10). It needs a real CFont API.
            // If CFont already exposes this, wire it here. Otherwise keep as TODO.
            TrySetFontSpaceValue(sFont10, unchecked((int)0xFFFFFFFF));

            // Property_DoesExists(..., "font_space_small")
            bool fontSpaceSmall = NMasterPropertyManager.CPropertyManager.Property_DoesExists(OpenVikings.NMasterPropertyManager.CPropertyManager.sTheObjectPtr, "font_space_small");
            if (fontSpaceSmall)
            {
                TrySetFontSpaceValue(sFont08, unchecked((int)0xFFFFFFFF));
                TrySetFontSpaceValue(sFont10, unchecked((int)0xFFFFFFFE));
                TrySetFontSpaceValue(sFont12, unchecked((int)0xFFFFFFFE));
            }

            // Load font palettes
            sPalFontWhite = (CPalette)XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\font_white.pcx");
            sPalFontDimmed = (CPalette)XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\font_dimmed.pcx");
            sPalFontRed = (CPalette)XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\font_red.pcx");
            sPalFontDark = (CPalette)XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\font_dark.pcx");

            // SetPalettePtr(font, white)
            CFont.SetPalettePtr(sFont08, sPalFontWhite);
            CFont.SetPalettePtr(sFont10, sPalFontWhite);
            CFont.SetPalettePtr(sFont12, sPalFontWhite);
            CFont.SetPalettePtr(sFontDebug, sPalFontWhite);

            // LanguageTool_GetFilename(..., local_128, false); DAT_1003a4478 = XB_Storable_LoadObject(local_128)
            string bobsPath;
            OpenVikings.NC2Logic.LanguageTool.GetFilename("data\\gui\\lang\\%s\\bobs\\ls_gui_window.bmd", out bobsPath, false);
            sGuiWindowBobs = XB_Storable_LoadObject(bobsPath);

            // Load GUI palettes
            sPalFrame = (CPalette)XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\frame.pcx");
            sPalBgNormal = (CPalette)XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\bg_normal.pcx");
            sPalPapyrus = (CPalette)XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\papyrus.pcx");
            sPalBarHitpoints = (CPalette)XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\bar_hitpoints.pcx");
            sPalBarStandard = (CPalette)XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\bar_standart.pcx");
            sPalBarDisabled = (CPalette)XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\bar_disabled.pcx");
            sPalIconsLeft = (CPalette)XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\IconsLeft.pcx");
            sPalContext = (CPalette)XB_PictureTool_LoadPaletteOutOfPicture("data\\gui\\palettes\\Context.pcx");
        }

        // NC2GuiToolsBase::CGuiBaseDataManager::~CGuiBaseDataManager()
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            DynamicData_Free();

            ReleaseObject(sPalContext);
            ReleaseObject(sPalIconsLeft);
            ReleaseObject(sPalBarDisabled);
            ReleaseObject(sPalBarStandard);
            ReleaseObject(sPalBarHitpoints);
            ReleaseObject(sPalPapyrus);
            ReleaseObject(sPalBgNormal);
            ReleaseObject(sPalFrame);
            ReleaseObject(sGuiWindowBobs);
            ReleaseObject(sPalFontDark);
            ReleaseObject(sPalFontRed);
            ReleaseObject(sPalFontDimmed);
            ReleaseObject(sPalFontWhite);
            ReleaseObject(sFontDebug);
            ReleaseObject(sFont12);
            ReleaseObject(sFont10);
            ReleaseObject(sFont08);

            ResetStaticState();
            sTheObjectPtr = null;

            _disposed = true;
        }

        // NC2GuiToolsBase::CGuiBaseDataManager::DynamicData_Free()
        internal static void DynamicData_Free()
        {
            // PaletteChanged calls
            CPalette.PaletteChanged(sPalFontWhite);
            CPalette.PaletteChanged(sPalFontDimmed);
            CPalette.PaletteChanged(sPalFontRed);
            CPalette.PaletteChanged(sPalFontDark);
            CPalette.PaletteChanged(sPalFrame);
            CPalette.PaletteChanged(sPalBgNormal);
            CPalette.PaletteChanged(sPalPapyrus);

            if (sDynamicDataLoaded != 0)
            {
                ReleaseObject(sBmpBgHeadline);
                ReleaseObject(sBmpBgButtonHilite);
                ReleaseObject(sBmpBgSelected);
                ReleaseObject(sBmpBgButton);
                ReleaseObject(sBmpBg);

                sBmpBg = null;
                sBmpBgButton = null;
                sBmpBgSelected = null;
                sBmpBgButtonHilite = null;
                sBmpBgHeadline = null;

                sDynamicDataLoaded = 0;
            }
        }

        // NC2GuiToolsBase::CGuiBaseDataManager::DynamicData_Load()
        internal static void DynamicData_Load(byte bpp)
        {
            DynamicData_Free();

            // uVar1 = *(uchar *)(*(long *)(NXBaseGui::CDesktop::sTheObjectPtr + 0x10) + 8);
            // The exact field chain needs the real Desktop/Bitmap API. This is kept as a single call-site hook.

            LoadPictureIntoBitmap("data\\gui\\bitmaps\\bg.pcx", bpp, out sBmpBg);
            LoadPictureIntoBitmap("data\\gui\\bitmaps\\bg_button.pcx", bpp, out sBmpBgButton);
            LoadPictureIntoBitmap("data\\gui\\bitmaps\\bg_selected.pcx", bpp, out sBmpBgSelected);
            LoadPictureIntoBitmap("data\\gui\\bitmaps\\bg_button_hilite.pcx", bpp, out sBmpBgButtonHilite);
            LoadPictureIntoBitmap("data\\gui\\bitmaps\\bg_headline.pcx", bpp, out sBmpBgHeadline);

            sDynamicDataLoaded = 1;
        }

        private static void LoadPictureIntoBitmap(string picturePath, byte bpp, out CBitmap bitmap)
        {
            // CPicture pic("..."); bitmap = new CBitmap(pic.Width, pic.Height, bpp); CopyIntoBitmap(pic, bitmap, 0, 0); ~CPicture
            CPicture picture = new(picturePath);

            try
            {
                uint width = picture.Width;
                uint height = picture.Height;

                bitmap = new CBitmap(width, height, bpp);
                CBitmap.CopyIntoBitmap(picture, bitmap, 0, 0);
            }
            finally
            {
                picture.Dispose();
            }
        }

        private static void ResetStaticState()
        {
            sFont08 = null;
            sFont10 = null;
            sFont12 = null;
            sFontDebug = null;

            sPalFontWhite = null;
            sPalFontDimmed = null;
            sPalFontRed = null;
            sPalFontDark = null;

            sGuiWindowBobs = null;

            sPalFrame = null;
            sPalBgNormal = null;
            sPalPapyrus = null;
            sPalBarHitpoints = null;
            sPalBarStandard = null;
            sPalBarDisabled = null;
            sPalIconsLeft = null;
            sPalContext = null;

            sDynamicDataLoaded = 0;
            sBmpBg = null;
            sBmpBgButton = null;
            sBmpBgSelected = null;
            sBmpBgButtonHilite = null;
            sBmpBgHeadline = null;
        }

        private static void ReleaseObject(object obj)
        {
            if (obj is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        private static void TrySetFontSpaceValue(CFont font, int value)
        {
            if (font == null)
            {
                return;
            }

            // The dump writes to font + 0x10. If CFont exposes that as a property/field, wire it here.
            // Example (placeholder):
            // font.SpaceValue = value;
        }

        private static byte GetDesktopBpp()
        {
            CDesktop desktop = CDesktop.sTheObjectPtr;
            if (desktop == null)
            {
                return 0;
            }

            CBitmap screen = desktop.ScreenBitmap;
            if (screen == null)
            {
                return 0;
            }

            return screen.Bpp;
        }

    }
}