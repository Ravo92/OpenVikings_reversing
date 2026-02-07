namespace OpenVikings.Interfaces
{
    internal interface IMouseManager
    {
        void UpdateMouseButtonState(bool leftDown, bool middleDown, bool rightDown);
        void UpdateMouseWheelState(int deltaSteps);
        void UpdateMouseState();
    }
}