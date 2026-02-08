using OpenVikings.Engine.NC2E2;

namespace OpenVikings.Engine
{
    internal static class EngineEventBridge
    {
        internal static void InformDesktopCreated(int width, int height, uint depth)
        {
            CE2Manager.Instance.InformDesktopXTructed(width, height, depth);
        }

        internal static void InformDesktopDestroyed(int height)
        {
            // RE uses param_1 == 0 to indicate destruction. param_2 is still passed through.
            CE2Manager.Instance.InformDesktopXTructed(0, height, 0);
        }
    }
}