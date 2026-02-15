using Silk.NET.SDL;

namespace OpenVikings.SDL2
{
    internal static class SdlBootstrap
    {
        internal static SdlContext Create(string windowTitle, int width, int height)
        {
            Sdl sdl = Sdl.GetApi();

            if (sdl.Init(Sdl.InitVideo) != 0)
            {
                string error = sdl.GetErrorS();
                throw new InvalidOperationException("SDL_Init failed: " + error);
            }

            nint windowHandle = CreateWindow(sdl, windowTitle, width, height);

            SilkSdlApi api = new(sdl);
            OSEnvironment osEnvironment = new(api, windowHandle);

            return new SdlContext(sdl, windowHandle, osEnvironment, api);
        }

        private static unsafe nint CreateWindow(Sdl sdl, string windowTitle, int width, int height)
        {
            Window* window = sdl.CreateWindow(windowTitle, Sdl.WindowposUndefined, Sdl.WindowposUndefined, width, height, (uint)WindowFlags.Shown);
            if (window == null)
            {
                string error = sdl.GetErrorS();
                throw new InvalidOperationException("SDL_CreateWindow failed: " + error);
            }

            return (nint)window;
        }
    }
}