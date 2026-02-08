using OpenVikings.Interfaces;
using System.Diagnostics;

namespace OpenVikings.Engine
{
    internal class OpenVikingsDebug : IOpenVikingsDebug
    {
        private long _frameCount;
        private long _lastReportTick;

        internal OpenVikingsDebug()
        {
            _frameCount = 0;
            _lastReportTick = Stopwatch.GetTimestamp();
        }

        public void UpdateFps(uint frameTimeMs)
        {
            // This is only a placeholder. Can be replaced with real profiling / overlay logic.
            _frameCount++;

            long now = Stopwatch.GetTimestamp();
            long frequency = Stopwatch.Frequency;

            // Report roughly every 2 seconds.
            long elapsedTicks = now - _lastReportTick;
            if (elapsedTicks >= frequency * 2)
            {
                double seconds = (double)elapsedTicks / frequency;
                double fps = _frameCount / seconds;

                Debug.WriteLine("FPS: " + fps.ToString("0.0") + " | frameTimeMs=" + frameTimeMs);

                _frameCount = 0;
                _lastReportTick = now;
            }
        }
    }
}