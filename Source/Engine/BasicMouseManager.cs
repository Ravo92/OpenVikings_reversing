using OpenVikings.Interfaces;

namespace OpenVikings.Engine
{
    // Minimal mouse manager implementation to keep the wiring complete.
    // Replace with real in-game mouse handling later.
    internal sealed class BasicMouseManager : IMouseManager
    {
        internal bool LeftDown { get; private set; }
        internal bool MiddleDown { get; private set; }
        internal bool RightDown { get; private set; }
        internal int WheelStepsAccumulated { get; private set; }

        public void UpdateMouseButtonState(bool leftDown, bool middleDown, bool rightDown)
        {
            LeftDown = leftDown;
            MiddleDown = middleDown;
            RightDown = rightDown;
        }

        public void UpdateMouseWheelState(int deltaSteps)
        {
            WheelStepsAccumulated += deltaSteps;
        }

        public void UpdateMouseState()
        {
            // Hook point for per-frame processing.
        }
    }
}