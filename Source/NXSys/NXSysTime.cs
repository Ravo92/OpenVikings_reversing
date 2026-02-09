namespace OpenVikings.NXSys
{
    internal static class NXSysTime
    {
        // NXSysTime::XWS_Time_GetMilliSeconds()
        internal static uint XWS_Time_GetMilliSeconds()
        {
            return DexterOS.Time();
        }

        // NXSysTime::XWS_Time_WaitMilliSeconds(unsigned int)
        internal static void XWS_Time_WaitMilliSeconds(uint milliseconds)
        {
            DexterOS.Pause(milliseconds);
        }
    }
}