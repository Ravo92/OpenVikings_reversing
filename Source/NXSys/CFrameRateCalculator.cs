namespace OpenVikings.NXSys
{
    // NXSysTime::CFrameRateCalculator
    internal sealed class CFrameRateCalculator
    {
        private const int SampleCount = 30;

        private readonly Func<uint> _getTimeMs;

        private uint _lastTimeMs;
        private readonly uint[] _frameDeltasMs;
        private int _index;
        private float _frameRate;

        // Mirrors: MemorySet(this, 0, 0x84) + lastTime = DexterOS::Time()
        internal CFrameRateCalculator(Func<uint> getTimeMs)
        {
            _getTimeMs = getTimeMs;

            _frameDeltasMs = new uint[SampleCount];
            _index = 0;
            _frameRate = 0.0f;

            _lastTimeMs = _getTimeMs();
        }

        // NXSysTime::CFrameRateCalculator::CalculateFrameRate()
        internal void CalculateFrameRate()
        {
            uint nowMs = _getTimeMs();
            CalculateFrameRate(nowMs);
        }

        // NXSysTime::CFrameRateCalculator::CalculateFrameRate(unsigned int)
        internal void CalculateFrameRate(uint nowMs)
        {
            uint prevMs = _lastTimeMs;
            if (nowMs < prevMs)
            {
                nowMs = prevMs;
            }

            _frameDeltasMs[_index] = nowMs - prevMs;

            int nextIndex = _index + 1;
            if (nextIndex == SampleCount)
            {
                nextIndex = 0;
            }

            _index = nextIndex;
            _lastTimeMs = nowMs;

            uint sumMs = 0;
            for (int i = 0; i < SampleCount; i++)
            {
                sumMs += _frameDeltasMs[i];
            }

            // Dump-treu: 1.0 / (((sum/30)/1000)) == 30000/sum.
            // If sumMs == 0, this yields +Infinity like in native floating division.
            _frameRate = (float)(30000.0 / sumMs);
        }

        internal float GetFrameRate()
        {
            return _frameRate;
        }
    }
}