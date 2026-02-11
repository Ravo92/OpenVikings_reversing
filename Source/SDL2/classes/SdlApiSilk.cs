using Silk.NET.SDL;

namespace OpenVikings.SDL2.classes
{
    internal sealed unsafe class SdlApiSilk : ISdlApi
    {
        private readonly Sdl _sdl;

        internal SdlApiSilk(Sdl sdl)
        {
            _sdl = sdl;
        }

        internal Sdl Raw => _sdl;

        public void SetWindowTitle(nint windowHandle, string title)
        {
            // SDL expects UTF-8 string.
            _sdl.SetWindowTitle((Window*)windowHandle, title);
        }

        public uint GetTicks()
        {
            return _sdl.GetTicks();
        }

        public void Delay(uint milliseconds)
        {
            _sdl.Delay(milliseconds);
        }
    }
}