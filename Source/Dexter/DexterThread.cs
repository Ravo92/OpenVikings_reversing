namespace OpenVikings.Dexter
{
    internal class DexThread
    {
        // -1 means "no cooperative thread"
        internal static bool CooperativeThread { get; set; } = false;

        internal static int ThreadCount { get; set; } = 0;

        // Optional: compatibility for the decompiled "Clear()"
        internal static void Clear()
        {
            CooperativeThread = false;
            ThreadCount = 0;
        }
    }
}