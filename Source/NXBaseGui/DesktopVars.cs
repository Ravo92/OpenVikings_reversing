using OpenVikings.NXBasics;

namespace OpenVikings.NXBaseGui
{
    internal sealed class DesktopVars : IDisposable
    {
        // Elements - Background
        internal readonly CListBase<CBaseElement> BackgroundDraw;          // +0x18
        internal readonly CListBase<CBaseElement> BackgroundLogical;       // +0x40

        // Elements - Overlay
        internal readonly CListBase<CBaseElement> OverlayDraw;             // +0x68
        internal readonly CListBase<CBaseElement> OverlayLogical;          // +0x90

        // Windows (normal)
        internal readonly CListBase<CBaseWindow> WindowsAll;                 // +0xB8
        internal readonly CListBase<CBaseWindow> WindowsFrontA;              // +0xE0
        internal readonly CListBase<CBaseWindow> WindowsDraw;                // +0x108
        internal readonly CListBase<CBaseWindow> WindowsFrontB;              // +0x130

        // Windows (overlay)
        internal readonly CListBase<CBaseWindow> OverlayWindowsAll;          // +0x158
        internal readonly CListBase<CBaseWindow> OverlayWindowsFrontA;       // +0x180
        internal readonly CListBase<CBaseWindow> OverlayWindowsDraw;         // +0x1A8
        internal readonly CListBase<CBaseWindow> OverlayWindowsFrontB;       // +0x1D0

        // ToClose
        internal readonly CListBase<CBaseWindow> WindowsToClose;             // +0x220

        internal DesktopVars()
        {
            BackgroundDraw = new CListBase<CBaseElement>();
            BackgroundLogical = new CListBase<CBaseElement>();
            OverlayDraw = new CListBase<CBaseElement>();
            OverlayLogical = new CListBase<CBaseElement>();

            WindowsAll = new CListBase<CBaseWindow>();
            WindowsFrontA = new CListBase<CBaseWindow>();
            WindowsDraw = new CListBase<CBaseWindow>();
            WindowsFrontB = new CListBase<CBaseWindow>();

            OverlayWindowsAll = new CListBase<CBaseWindow>();
            OverlayWindowsFrontA = new CListBase<CBaseWindow>();
            OverlayWindowsDraw = new CListBase<CBaseWindow>();
            OverlayWindowsFrontB = new CListBase<CBaseWindow>();

            WindowsToClose = new CListBase<CBaseWindow>();
        }

        internal void ClearListsOnly()
        {
            BackgroundDraw.DeleteAllElements();
            BackgroundLogical.DeleteAllElements();
            OverlayDraw.DeleteAllElements();
            OverlayLogical.DeleteAllElements();

            WindowsAll.DeleteAllElements();
            WindowsFrontA.DeleteAllElements();
            WindowsDraw.DeleteAllElements();
            WindowsFrontB.DeleteAllElements();

            OverlayWindowsAll.DeleteAllElements();
            OverlayWindowsFrontA.DeleteAllElements();
            OverlayWindowsDraw.DeleteAllElements();
            OverlayWindowsFrontB.DeleteAllElements();

            WindowsToClose.DeleteAllElements();
        }

        public void Dispose()
        {
            // In pseudo SVars::~SVars deletes list elements conditionally.
            // In managed design, CDesktop owns element/window lifetimes and already Dispose()'d them.
            ClearListsOnly();
        }
    }
}
