namespace OpenVikings.SDL2
{
    // Installs basic process signal hooks so SDL can be shut down gracefully.
    // This mirrors the intent of typical SDL "signal" helpers without relying on platform-specific APIs.
    internal static class SignalHooks
    {
        private static bool _installed;
        private static SilkSdlApi? _api;

        internal static void Install(SilkSdlApi api)
        {
            if (_installed)
            {
                return;
            }

            _installed = true;
            _api = api;

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Console.CancelKeyPress += OnCancelKeyPress;
        }

        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            // Let the process exit normally after Ctrl+C, but note the intent.
            e.Cancel = false;
            TryLog("CancelKeyPress");
        }

        private static void OnProcessExit(object? sender, EventArgs e)
        {
            TryLog("ProcessExit");
        }

        private static void TryLog(string reason)
        {
            SilkSdlApi? api = _api;
            if (api == null)
            {
                return;
            }

            // Category 0 is "application" in SDL's default categories.
            api.LogMessage(0, Silk.NET.SDL.LogPriority.LogPriorityInfo, "Shutdown requested ({0}).", reason);
        }
    }
}