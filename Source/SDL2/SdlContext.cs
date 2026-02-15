using Silk.NET.SDL;

namespace OpenVikings.SDL2
{
    internal sealed class SdlContext : IDisposable
    {
        private readonly Sdl _sdl;
        private readonly nint _windowHandle;

        internal SdlContext(Sdl sdl, nint windowHandle, OSEnvironment osEnvironment, SilkSdlApi api)
        {
            _sdl = sdl;
            _windowHandle = windowHandle;
            Environment = osEnvironment;
            Api = api;
        }

        internal OSEnvironment Environment { get; }
        internal SilkSdlApi Api { get; }

        public void Dispose()
        {
            // Always restore relative mouse mode before shutting down.
            if (GetRelativeMouseMode())
            {
                SetRelativeMouseMode(false);
            }

            DestroyWindow();
            _sdl.Quit();
        }

        internal void LogSetAllPriority(LogPriority priority)
        {
            _sdl.LogSetAllPriority(priority);
        }

        internal void SetEventEnabled(uint eventType, bool enabled)
        {
            _sdl.EventState(eventType, enabled ? (byte)1 : (byte)0);
        }

        internal bool GetRelativeMouseMode()
        {
            return _sdl.GetRelativeMouseMode() != 0;
        }

        internal void SetRelativeMouseMode(bool enabled)
        {
            _sdl.SetRelativeMouseMode(enabled ? SdlBool.True : SdlBool.False);
        }

        private unsafe void DestroyWindow()
        {
            if (_windowHandle == 0)
            {
                return;
            }

            Window* window = (Window*)_windowHandle;
            _sdl.DestroyWindow(window);
        }
    }
}