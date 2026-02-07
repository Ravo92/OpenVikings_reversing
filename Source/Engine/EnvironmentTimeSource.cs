using OpenVikings.Interfaces;

namespace OpenVikings.Engine
{
    internal class EnvironmentTimeSource : ITimeSource
    {
        public int TimeMilliseconds()
        {
            // Environment.TickCount wraps around; this mirrors typical legacy timing behavior.
            return Environment.TickCount;
        }
    }
}