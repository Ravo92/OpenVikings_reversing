namespace OpenVikings.Interfaces
{
    internal interface IGuiManager
    {
        void DesktopOpen(int width, int height, uint depth);
        void CenterEngineWorldDisplay();
        void ChangeScreen(int screenId, int parameter);
    }
}
