namespace OpenVikings.Dexter
{
    // DexterDebug
    // Conversion of DexterDebug.cxx as provided (mostly no-op / stubbed in the decompile).
    internal static class DexterDebug
    {
        // WARNING in decompile: globals overlapping smaller symbols.
        // In managed code this is represented as a simple backing field.
        private static ushort _debugMode;

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

        // DexterDebug::UpdateFPS(int)
        internal static void UpdateFPS(int value)
        {
            // No-op in the provided decompile.
        }
    }
}