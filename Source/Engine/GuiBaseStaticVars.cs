using OpenVikings.NXBasics;

namespace OpenVikings.Engine
{
    internal sealed class GuiBaseStaticVars
    {
        internal CBitmap BackgroundBitmap { get; }
        internal CBitmap BackgroundButtonBitmap { get; }
        internal CBitmap BackgroundSelectedBitmap { get; }
        internal CBitmap BackgroundButtonHiliteBitmap { get; }
        internal CBitmap BackgroundHeadlineBitmap { get; }

        internal GuiBaseStaticVars(
            CBitmap backgroundBitmap,
            CBitmap backgroundButtonBitmap,
            CBitmap backgroundSelectedBitmap,
            CBitmap backgroundButtonHiliteBitmap,
            CBitmap backgroundHeadlineBitmap)
        {
            BackgroundBitmap = backgroundBitmap;
            BackgroundButtonBitmap = backgroundButtonBitmap;
            BackgroundSelectedBitmap = backgroundSelectedBitmap;
            BackgroundButtonHiliteBitmap = backgroundButtonHiliteBitmap;
            BackgroundHeadlineBitmap = backgroundHeadlineBitmap;
        }
    }
}