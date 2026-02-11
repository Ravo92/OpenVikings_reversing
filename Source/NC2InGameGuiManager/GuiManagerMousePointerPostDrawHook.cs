using OpenVikings.Interfaces;
using OpenVikings.NXBasics;
using OpenVikings.NXSysMouseManager;

namespace OpenVikings.NC2InGameGuiManager
{
    internal sealed class GuiManagerMousePointerPostDrawHook : IDesktopPostDrawHook
    {
        private readonly CGuiManagerMousePointer _mousePointer;

        internal GuiManagerMousePointerPostDrawHook(CGuiManagerMousePointer mousePointer)
        {
            _mousePointer = mousePointer;
        }

        public void PostDraw(CBitmap backBuffer, CMouseManager? mouseManager)
        {
            if (mouseManager == null)
            {
                return;
            }

            SPoint pos = mouseManager.Position;
            _mousePointer.SetPosition(pos.X, pos.Y);

            // Rendering stub for now
            _mousePointer.Draw();
        }

        public void Dispose()
        {
            // Nothing to dispose currently
        }
    }
}
