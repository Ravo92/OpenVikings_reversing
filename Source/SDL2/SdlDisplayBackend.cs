using Silk.NET.SDL;

namespace OpenVikings.SDL2
{
    // SDL backend that owns SDL pointers. Keep unsafe confined here.
    internal sealed unsafe class SdlDisplayBackend : IDisposable
    {
        private readonly Sdl _sdl;

        private Window* _window;
        private Renderer* _renderer;
        private Texture* _texture;
        private Surface* _surface;

        private int _logicalWidth;
        private int _logicalHeight;

        private bool _ownsWindowAndRenderer;
        private bool _initialized;

        internal SdlDisplayBackend()
        {
            _sdl = Sdl.GetApi();

            _window = null;
            _renderer = null;
            _texture = null;
            _surface = null;

            _logicalWidth = 0;
            _logicalHeight = 0;

            _ownsWindowAndRenderer = true;
            _initialized = false;
        }

        public void Dispose()
        {
            Shutdown();
            GC.SuppressFinalize(this);
        }

        ~SdlDisplayBackend()
        {
            try
            {
                Shutdown();
            }
            catch
            {
                // Finalizer must not throw.
            }
        }

        internal bool IsInitialized => _initialized;

        internal (int width, int height) GetDesktopSize()
        {
            DisplayMode mode;
            int rc = _sdl.GetDesktopDisplayMode(0, &mode);
            if (rc != 0)
            {
                return (0, 0);
            }

            return (mode.W, mode.H);
        }

        internal bool InitWindowAndRenderer(string title, int windowWidth, int windowHeight, int logicalWidth, int logicalHeight, bool fullscreenDesktop, bool showCursor, bool ownsWindowAndRenderer)
        {
            if (string.IsNullOrEmpty(title))
            {
                title = "OpenVikings";
            }

            if (windowWidth <= 0 || windowHeight <= 0 || logicalWidth <= 0 || logicalHeight <= 0)
            {
                return false;
            }

            _ownsWindowAndRenderer = ownsWindowAndRenderer;

            if (!_initialized)
            {
                // Ensure SDL video is initialized. If your project does it elsewhere, Init will just no-op.
                // Silk.NET SDL: Init returns 0 on success.
                if (_sdl.WasInit(Sdl.InitVideo) == 0)
                {
                    int initRc = _sdl.Init(Sdl.InitVideo);
                    if (initRc != 0)
                    {
                        return false;
                    }
                }
            }

            if (_ownsWindowAndRenderer)
            {
                uint windowFlags = (uint)WindowFlags.Shown;
                if (fullscreenDesktop)
                {
                    windowFlags |= (uint)WindowFlags.FullscreenDesktop;
                }

                _window = _sdl.CreateWindow(title, Sdl.WindowposUndefined, Sdl.WindowposUndefined, windowWidth, windowHeight, windowFlags);
                if (_window == null)
                {
                    return false;
                }

                _renderer = _sdl.CreateRenderer(_window, -1, (uint)RendererFlags.Accelerated);
                if (_renderer == null)
                {
                    return false;
                }
            }
            else
            {
                // If ownership is external, window/renderer must already be set via BindExternalWindowAndRenderer.
                if (_window == null || _renderer == null)
                {
                    return false;
                }

                if (fullscreenDesktop)
                {
                    _sdl.SetWindowFullscreen(_window, (uint)WindowFlags.FullscreenDesktop);
                }
                else
                {
                    _sdl.SetWindowFullscreen(_window, 0);
                }

                _sdl.SetWindowSize(_window, windowWidth, windowHeight);
            }

            _logicalWidth = logicalWidth;
            _logicalHeight = logicalHeight;

            // Quality hint similar to common SDL usage.
            _sdl.SetHint("SDL_RENDER_SCALE_QUALITY", "linear");

            // Logical size to scale the render output.
            _sdl.RenderSetLogicalSize(_renderer, logicalWidth, logicalHeight);

            // Mouse cursor visibility
            _sdl.ShowCursor(showCursor ? 1 : 0);

            // Create 16-bit surface + streaming texture
            if (!RecreateSurfaceAndTexture(logicalWidth, logicalHeight))
            {
                return false;
            }

            _initialized = true;
            return true;
        }

        internal void BindExternalWindowAndRenderer(nint windowHandle, nint rendererHandle)
        {
            // This method allows reuse of externally-created SDL objects.
            // It is optional; only use if your project creates the window/renderer elsewhere.
            _window = (Window*)windowHandle;
            _renderer = (Renderer*)rendererHandle;
            _ownsWindowAndRenderer = false;
        }

        internal void SetWindowTitle(string title)
        {
            if (_window == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(title))
            {
                return;
            }

            _sdl.SetWindowTitle(_window, title);
        }

        internal void SetFullscreenDesktop(bool enabled)
        {
            if (_window == null)
            {
                return;
            }

            uint flags = enabled ? (uint)WindowFlags.FullscreenDesktop : 0;
            _sdl.SetWindowFullscreen(_window, flags);
        }

        internal void SetWindowSize(int width, int height)
        {
            if (_window == null)
            {
                return;
            }

            if (width <= 0 || height <= 0)
            {
                return;
            }

            _sdl.SetWindowSize(_window, width, height);
        }

        internal void SetShowCursor(bool show)
        {
            _sdl.ShowCursor(show ? 1 : 0);
        }

        internal bool RecreateSurfaceAndTexture(int logicalWidth, int logicalHeight)
        {
            if (_renderer == null)
            {
                return false;
            }

            if (logicalWidth <= 0 || logicalHeight <= 0)
            {
                return false;
            }

            // Dispose old
            if (_texture != null)
            {
                _sdl.DestroyTexture(_texture);
                _texture = null;
            }

            if (_surface != null)
            {
                _sdl.FreeSurface(_surface);
                _surface = null;
            }

            // Create 16-bit surface (masks 0 => let SDL pick a format)
            _surface = _sdl.CreateRGBSurface(0, logicalWidth, logicalHeight, 16, 0, 0, 0, 0);
            if (_surface == null)
            {
                return false;
            }

            // Streaming texture - RGB565 is a reasonable default for 16-bit output.
            uint pixelFormat = (uint)Sdl.PixelformatRgb565;
            _texture = _sdl.CreateTexture(_renderer, pixelFormat, (int)TextureAccess.Streaming, logicalWidth, logicalHeight);
            if (_texture == null)
            {
                return false;
            }

            _logicalWidth = logicalWidth;
            _logicalHeight = logicalHeight;
            return true;
        }

        internal bool LockSurface(out Span<byte> pixels, out int pitch)
        {
            pixels = default;
            pitch = 0;

            if (_surface == null)
            {
                return false;
            }

            // SDL_Surface may require locking
            bool mustLock = (_surface->Flags & 2) != 0;
            if (mustLock)
            {
                int rc = _sdl.LockSurface(_surface);
                if (rc < 0)
                {
                    return false;
                }
            }

            void* ptr = _surface->Pixels;
            pitch = _surface->Pitch;

            if (ptr == null || pitch <= 0 || _logicalHeight <= 0)
            {
                if (mustLock)
                {
                    _sdl.UnlockSurface(_surface);
                }

                return false;
            }

            int bytes = checked(pitch * _logicalHeight);
            pixels = new Span<byte>(ptr, bytes);
            return true;
        }

        internal void UnlockSurface()
        {
            if (_surface == null)
            {
                return;
            }

            bool mustLock = (_surface->Flags & 2) != 0;
            if (mustLock)
            {
                _sdl.UnlockSurface(_surface);
            }
        }

        internal void PresentSurface()
        {
            if (_surface == null || _renderer == null || _texture == null)
            {
                return;
            }

            void* ptr = _surface->Pixels;
            int pitch = _surface->Pitch;

            if (ptr == null || pitch <= 0)
            {
                return;
            }

            _sdl.UpdateTexture(_texture, null, ptr, pitch);
            _sdl.RenderClear(_renderer);
            _sdl.RenderCopy(_renderer, _texture, null, null);
            _sdl.RenderPresent(_renderer);
        }

        internal void Shutdown()
        {
            // Texture/surface always owned here
            if (_texture != null)
            {
                _sdl.DestroyTexture(_texture);
                _texture = null;
            }

            if (_surface != null)
            {
                _sdl.FreeSurface(_surface);
                _surface = null;
            }

            // Window/renderer only if owned
            if (_ownsWindowAndRenderer)
            {
                if (_renderer != null)
                {
                    _sdl.DestroyRenderer(_renderer);
                    _renderer = null;
                }

                if (_window != null)
                {
                    _sdl.DestroyWindow(_window);
                    _window = null;
                }
            }

            _initialized = false;
            _logicalWidth = 0;
            _logicalHeight = 0;
        }
    }
}