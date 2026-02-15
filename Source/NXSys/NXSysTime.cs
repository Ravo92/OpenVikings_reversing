namespace OpenVikings.NXSys
{
    internal static class NXSysTime
    {
        private static Func<uint>? _getMs;
        private static Action<uint>? _waitMs;

        // Call once during engine init (e.g., after OSEnvironment exists)
        internal static void Initialize(Func<uint> getMilliseconds, Action<uint> waitMilliseconds)
        {
            _getMs = getMilliseconds;
            _waitMs = waitMilliseconds;
        }

        // NXSysTime::XWS_Time_GetMilliSeconds()
        internal static uint XWS_Time_GetMilliSeconds()
        {
            if (_getMs == null)
            {
                throw new InvalidOperationException("NXSysTime.Initialize must be called before using time functions.");
            }

            return _getMs();
        }

        // NXSysTime::XWS_Time_WaitMilliSeconds(unsigned int)
        internal static void XWS_Time_WaitMilliSeconds(uint milliseconds)
        {
            if (_waitMs == null)
            {
                throw new InvalidOperationException("NXSysTime.Initialize must be called before using time functions.");
            }

            _waitMs(milliseconds);
        }
    }
}