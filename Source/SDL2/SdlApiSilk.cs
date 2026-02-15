using Silk.NET.SDL;

namespace OpenVikings.SDL2
{
    internal sealed class SilkSdlApi : ISdlApi
    {
        private readonly Sdl _sdl;

        internal SilkSdlApi() : this(Sdl.GetApi())
        {
        }

        internal SilkSdlApi(Sdl sdl)
        {
            _sdl = sdl;
        }

        public void SetWindowTitle(nint windowHandle, string title)
        {
            // Window handle is not wired yet in the current port.
            // Once a real SDL_Window exists, it can be set here.
            _ = windowHandle;
            _ = title;
        }

        public uint GetTicks()
        {
            return _sdl.GetTicks();
        }

        public void Delay(uint milliseconds)
        {
            _sdl.Delay(milliseconds);
        }

        internal void LogSetAllPriority(LogPriority priority)
        {
            _sdl.LogSetAllPriority(priority);
        }

        internal void LogMessage(int category, LogPriority priority, string format, params object[] args)
        {
            string msg = string.Format(format, args);
            _sdl.LogMessage(category, priority, msg);
        }
    }
}