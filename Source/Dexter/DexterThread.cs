namespace OpenVikings.Dexter
{
    internal class DexterThread
    {
        // -1 means "no cooperative thread"
        internal static long CooperativeThread { get; set; } = -1;

        internal static int ThreadCount { get; set; } = 0;

        // Optional: compatibility for the decompiled "Clear()"
        internal static void Clear()
        {
            CooperativeThread = -1;
            ThreadCount = 0;
        }
    }
}