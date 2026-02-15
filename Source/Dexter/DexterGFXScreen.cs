namespace OpenVikings.Dexter
{
    internal sealed class DexterGFXScreen
    {
        private readonly DexterGFXState _state;

        internal DexterGFXScreen(DexterGFXState state, int renderBitDepth, bool screenLocked)
        {
            _state = state;
            RenderBitDepth = renderBitDepth;
            ScreenLocked = screenLocked;
        }

        internal int RenderWidth => _state.RenderWidth;
        internal int RenderHeight => _state.RenderHeight;
        internal int WindowWidth => _state.WindowWidth;
        internal int WindowHeight => _state.WindowHeight;

        internal int RenderBitDepth { get; private set; }
        internal bool ScreenLocked { get; private set; }

        internal void SetWindowSize(int width, int height)
        {
            _state.SetWindowSize(width, height);
        }

        internal void SetScreenMode(int renderWidth, int renderHeight, int renderBitDepth, bool lockScreen)
        {
            _state.SetRenderSize(renderWidth, renderHeight);
            RenderBitDepth = renderBitDepth;
            ScreenLocked = lockScreen;
        }
    }
}