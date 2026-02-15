namespace OpenVikings.Dexter
{
    // DexterDebug
    // Conversion of DexterDebug.cxx as provided (mostly no-op / stubbed in the decompile).
    internal static class DexterDebug
    {
        // WARNING in decompile: globals overlapping smaller symbols.
        // In managed code this is represented as a simple backing field.
        private static ushort _debugMode;

        // DexterDebug::OpenDebugFile()
        internal static void OpenDebugFile()
        {
            // No-op in the provided decompile.
        }

        // DexterDebug::CloseDebugFile()
        internal static void CloseDebugFile()
        {
            // No-op in the provided decompile.
        }

        // DexterDebug::SetDebugMode(unsigned short)
        internal static void SetDebugMode(ushort mode)
        {
            _debugMode = mode;
        }

        // DexterDebug::CheckDebugMode(unsigned short)
        internal static bool CheckDebugMode(ushort mask)
        {
            return (_debugMode & mask) != 0;
        }

        // DexterDebug::SetProfileMode(unsigned char)
        internal static void SetProfileMode(byte mode)
        {
            // No-op in the provided decompile.
        }

        // DexterDebug::InitFPS()
        internal static void InitFPS()
        {
            // No-op in the provided decompile.
        }

        // DexterDebug::UpdateFPS(int)
        internal static void UpdateFPS(int value)
        {
            // No-op in the provided decompile.
        }

        // DexterDebug::FrameTime()
        internal static ulong FrameTime()
        {
            // Decompile returns 0 (undefined8).
            return 0UL;
        }

        // DexterDebug::Fps()
        internal static ulong Fps()
        {
            // Decompile returns 0 (undefined8).
            return 0UL;
        }

        // DexterDebug::Idle()
        internal static ulong Idle()
        {
            // Decompile returns 0 (undefined8).
            return 0UL;
        }

        // DexterDebug::ProfileReport()
        internal static void ProfileReport()
        {
            // No-op in the provided decompile.
        }
    }
}