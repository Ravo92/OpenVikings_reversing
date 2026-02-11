namespace OpenVikings.Dexter
{
    internal sealed class DexterGFXState
    {
        internal DexterGFXState(int renderWidth, int renderHeight, int windowWidth, int windowHeight)
        {
            RenderWidth = renderWidth;
            RenderHeight = renderHeight;
            WindowWidth = windowWidth;
            WindowHeight = windowHeight;
        }

        internal int RenderWidth { get; private set; }
        internal int RenderHeight { get; private set; }

        internal int WindowWidth { get; private set; }
        internal int WindowHeight { get; private set; }

        internal void SetRenderSize(int width, int height)
        {
            RenderWidth = width;
            RenderHeight = height;
        }

        internal void SetWindowSize(int width, int height)
        {
            WindowWidth = width;
            WindowHeight = height;
        }

        internal int WindowToRenderX(int x)
        {
            if (WindowWidth <= 0 || RenderWidth <= 0) return x;
            if (WindowWidth == RenderWidth) return x;
            return x * RenderWidth / WindowWidth;
        }

        internal int WindowToRenderY(int y)
        {
            if (WindowHeight <= 0 || RenderHeight <= 0) return y;
            if (WindowHeight == RenderHeight) return y;
            return y * RenderHeight / WindowHeight;
        }
    }
}
