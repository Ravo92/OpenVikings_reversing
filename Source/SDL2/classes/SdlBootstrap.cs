using Silk.NET.SDL;

namespace OpenVikings.SDL2.classes
{
    internal static unsafe class SdlBootstrap
    {
        internal static OSEnvironment CreateOsEnvironment(string windowTitle, int width, int height)
        {
            Sdl sdl = Sdl.GetApi();

            if (sdl.Init(Sdl.InitVideo) != 0)
            {
                // Optional: sdl.GetErrorS()
                throw new InvalidOperationException("SDL_Init failed.");
            }

            Window* window = sdl.CreateWindow(windowTitle, Sdl.WindowposUndefined, Sdl.WindowposUndefined, width, height, (uint)WindowFlags.Shown);

            if (window == null)
            {
                throw new InvalidOperationException("SDL_CreateWindow failed.");
            }

            SdlApiSilk api = new(sdl);
            OSEnvironment osEnvironment = new(api, (nint)window);
            return osEnvironment;
        }

        internal static void DestroyWindowAndQuit(OSEnvironment osEnvironment)
        {
            // If a clean shutdown is needed, store Sdl + Window somewhere central.
            // OSEnvironment currently does not expose them, so shutdown ownership should be decided at a higher layer.
        }
    }
}