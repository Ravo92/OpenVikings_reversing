namespace OpenVikings.NXSys
{
    // NXSysTime::CFrameRateCalculator
    internal sealed class CFrameRateCalculator
    {
        // Layout mapping (based on the decompile):
        // +0x00: last time (uint)
        // +0x04..+0x78: 30 uint deltas (30 * 4 = 120 bytes)
        // +0x7C: index (int)
        // +0x80: frame rate (float)
        private uint _lastTimeMs;
        private readonly uint[] _frameDeltasMs;
        private int _index;
        private float _frameRate;

        // NXSysTime::CFrameRateCalculator::CFrameRateCalculator()
        internal CFrameRateCalculator()
        {
            _frameDeltasMs = new uint[30];
            _index = 0;
            _frameRate = 0.0f;

            _lastTimeMs = DexterOS.Time();
        }

        // NXSysTime::CFrameRateCalculator::CalculateFrameRate()
        internal void CalculateFrameRate()
        {
            uint nowMs = DexterOS.Time();
            CalculateFrameRate(nowMs);
        }

        // NXSysTime::CFrameRateCalculator::CalculateFrameRate(unsigned int)
        internal void CalculateFrameRate(uint nowMs)
        {
            uint prevMs = _lastTimeMs;

            // Keep original behavior: time must not go backwards.
            if (nowMs < prevMs)
            {
                nowMs = prevMs;
            }

            uint deltaMs = nowMs - prevMs;

            _frameDeltasMs[_index] = deltaMs;

            int nextIndex = _index + 1;
            if (nextIndex == 30)
            {
                nextIndex = 0;
            }

            _index = nextIndex;
            _lastTimeMs = nowMs;

            // Original code: fps = 1.0 / (((sum/30) / 1000.0))
            // => fps = 30000 / sum, where sum is total milliseconds across 30 samples.
            uint sumMs = 0;
            int i = 0;
            while (i < 30)
            {
                sumMs += _frameDeltasMs[i];
                i++;
            }

            if (sumMs == 0)
            {
                _frameRate = 0.0f;
                return;
            }

            double fps = (30000.0 / (double)sumMs);
            _frameRate = (float)fps;
        }

        internal float GetFrameRate()
        {
            return _frameRate;
        }
    }
}
