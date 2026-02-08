using OpenVikings.Interfaces;

namespace OpenVikings.Engine
{
    internal sealed class GuiManagerAdapter : IGuiManager
    {
        internal GuiManagerAdapter()
        {
        }

        public void DesktopOpen(int width, int height, uint depth)
        {
            // TODO: Forward to your CDesktop / GUI init routine.
            // Example (placeholder):
            // CDesktop desktop = ...;
            // desktop.Open(width, height, depth);

            throw new NotImplementedException();
        }

        public void CenterEngineWorldDisplay()
        {
            // TODO: Forward to engine world viewport centering.
            throw new NotImplementedException();
        }

        public void ChangeScreen(int screenId, int parameter)
        {
            // TODO: Forward to screen/state machine.
            throw new NotImplementedException();
        }
    }
}