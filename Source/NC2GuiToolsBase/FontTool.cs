using OpenVikings.NXBasics;
using OpenVikings.NXBasics.Structs;

namespace OpenVikings.NC2GuiToolsBase
{
    internal static class FontTool
    {
        internal static CFont? GetFont08Ptr()
        {
            return CGuiBaseDataManager.Font08;
        }

        internal static CFont? GetFont10Ptr()
        {
            return CGuiBaseDataManager.Font10;
        }

        internal static CPalette? GetFontPaletteNormal()
        {
            return CGuiBaseDataManager.PalFontWhite;
        }

        internal static CPalette? GetFontPaletteDark()
        {
            return CGuiBaseDataManager.PalFontDark;
        }

        internal static CPalette? GetFontPaletteDimmed()
        {
            return CGuiBaseDataManager.PalFontDimmed;
        }

        internal static CPalette? GetFontPaletteRed()
        {
            return CGuiBaseDataManager.PalFontRed;
        }
    }
}