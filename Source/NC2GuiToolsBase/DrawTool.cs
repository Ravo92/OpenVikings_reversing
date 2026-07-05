using OpenVikings.NC2GuiToolsBase.enums;
using OpenVikings.NXBasics;

namespace OpenVikings.NC2GuiToolsBase
{
    internal static class DrawTool
    {
        // These were globals in the original binary (DAT_...).
        // Wire them up to the real singletons in the port.
        internal static CBobManager? BobManager { get; set; }
        internal static CPalette? PaletteFrame { get; set; }         // DAT_1003a4488 in snippet
        internal static CPalette? PaletteFrameCorners { get; set; }  // DAT_1003a4480 in snippet

        // Original: PTR_mStaticVars + offsets that select a fill bitmap.
        // In the port this should be explicit.
        internal static CBitmap? FillBitmapMode1 { get; set; }
        internal static CBitmap? FillBitmapMode3 { get; set; }
        internal static CBitmap? FillBitmapMode4 { get; set; }
        internal static CBitmap? FillBitmapMode5 { get; set; }

        internal static void DrawTool_FillArea(NXBaseGui.CBaseElement? element, CBitmap target, SRectangle area, TBaseDrawModes mode)
        {
            if (mode == TBaseDrawModes.None)
            {
                return;
            }

            CBitmap? pattern = GetFillPatternBitmap(mode);
            if (pattern == null)
            {
                return;
            }

            // --- This matches the intent of the RE code:
            // start with -(target.VirtualSrcX/Y), then walk VirtualParent chain and subtract each parent's VirtualSrcX/Y,
            // then modulo by pattern Width/Height.
            int scrollX = -target.VirtualSrcX;
            int scrollY = -target.VirtualSrcY;

            CBitmap? bmp = target.VirtualParent;
            while (bmp != null)
            {
                scrollX -= bmp.VirtualSrcX;
                scrollY -= bmp.VirtualSrcY;
                bmp = bmp.VirtualParent;
            }

            scrollX = PositiveModulo(scrollX - pattern.Width, pattern.Width);
            scrollY = PositiveModulo(scrollY - pattern.Height, pattern.Height);

            target.FillWithBitmap(area, pattern, scrollX, scrollY);
        }

        internal static void DrawTool_DrawFrame(NXBaseGui.CBaseElement? element, CBitmap target, SRectangle area, TBaseDrawModes mode)
        {
            using CBitmap sub = new(target, area);
            DrawTool_DrawFrame(sub, mode);
        }

        internal static void DrawTool_DrawFrame(CBitmap target, TBaseDrawModes mode)
        {
            if (mode == TBaseDrawModes.None)
            {
                return;
            }

            CBobManager? bobManager = BobManager;
            if (bobManager == null)
            {
                return;
            }

            CPalette? palEdges = CGuiBaseDataManager.PalFrame;
            CPalette? palCorners = CGuiBaseDataManager.PalFrame; // see note above

            if (palEdges == null || palCorners == null)
            {
                return;
            }

            int baseIndex = mode != TBaseDrawModes.Normal ? 8 : 0;

            // --- Horizontal tiling (top/bottom) ---
            if (target.Width > 0)
            {
                int x = 0;

                while (x < target.Width)
                {
                    bobManager.PrintBob((uint)(baseIndex + 5), target, x, 0, palEdges);
                    bobManager.PrintBob((uint)(baseIndex + 7), target, x, target.Height - 2, palEdges);

                    SRectangle? bobArea = bobManager.GetBobAreaRectangle((uint)(baseIndex + 5));
                    if (!bobArea.HasValue || bobArea.Value.Width <= 0)
                    {
                        break;
                    }

                    x += bobArea.Value.Width;
                }
            }

            // --- Vertical tiling (left/right) ---
            if (target.Height > 0)
            {
                int y = 0;

                while (y < target.Height)
                {
                    bobManager.PrintBob((uint)(baseIndex + 6), target, 0, y, palEdges);
                    bobManager.PrintBob((uint)(baseIndex + 8), target, target.Width - 2, y, palEdges);

                    SRectangle? bobArea = bobManager.GetBobAreaRectangle((uint)(baseIndex + 6));
                    if (!bobArea.HasValue || bobArea.Value.Height <= 0)
                    {
                        break;
                    }

                    y += bobArea.Value.Height;
                }
            }

            // --- Find top-most VirtualParent and accumulate VirtualSrc offsets ---
            int accumulatedX = 0;
            int accumulatedY = 0;

            CBitmap top = target;
            while (top.VirtualParent != null)
            {
                accumulatedX += top.VirtualSrcX;
                accumulatedY += top.VirtualSrcY;
                top = top.VirtualParent;
            }

            uint cornerBase = 9;
            if (mode == TBaseDrawModes.Mode5)
            {
                cornerBase = 0x15;
            }
            if (mode == TBaseDrawModes.Normal)
            {
                cornerBase = 0;
            }

            uint alpha = 255;

            bobManager.PrintBob_UsingTransparency(cornerBase + 0, top, alpha, accumulatedX, accumulatedY, palCorners);
            bobManager.PrintBob_UsingTransparency(cornerBase + 1, top, alpha, accumulatedX + target.Width - 1, accumulatedY, palCorners);
            bobManager.PrintBob_UsingTransparency(cornerBase + 2, top, alpha, accumulatedX + target.Width - 1, accumulatedY + target.Height - 1, palCorners);
            bobManager.PrintBob_UsingTransparency(cornerBase + 3, top, alpha, accumulatedX, accumulatedY + target.Height - 1, palCorners);
        }

        internal static void DrawIcon(uint bobId, CBitmap target, int x, int y)
        {
            CBobManager? bobManager = BobManager;
            CPalette? palette = PaletteFrame;
            if (bobManager == null || palette == null)
            {
                return;
            }

            int bobType = bobManager.GetBobType(bobId);
            if (bobType == 4)
            {
                bobManager.PrintBob_UsingShadedAlpha(bobId, target, x, y, palette, 0x80);
                return;
            }

            bobManager.PrintBob(bobId, target, x, y, palette);
        }

        private static CBitmap? GetFillPatternBitmap(TBaseDrawModes mode)
        {
            if (mode == TBaseDrawModes.Normal)
            {
                return FillBitmapMode1;
            }
            if (mode == TBaseDrawModes.Mode3)
            {
                return FillBitmapMode3;
            }
            if (mode == TBaseDrawModes.Mode4)
            {
                return FillBitmapMode4;
            }
            if (mode == TBaseDrawModes.Mode5)
            {
                return FillBitmapMode5;
            }

            return null;
        }

        private static int PositiveModulo(int value, int mod)
        {
            if (mod <= 0)
            {
                return 0;
            }

            int r = value % mod;
            if (r < 0)
            {
                r += mod;
            }

            return r;
        }
    }
}
