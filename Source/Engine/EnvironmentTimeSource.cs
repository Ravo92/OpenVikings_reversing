using OpenVikings.Interfaces;

namespace OpenVikings.Engine
{
    internal class EnvironmentTimeSource : ITimeSource
    {
        public uint GetMilliseconds()
        {
            // Environment.TickCount wraps around; this mirrors typical legacy timing behavior.
            return unchecked((uint)Environment.TickCount);
        }
    }
}