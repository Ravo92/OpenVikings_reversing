namespace OpenVikings.Engine
{
    internal sealed class EngineContext
    {
        internal int ResolutionWidth { get; set; }
        internal int ResolutionHeight { get; set; }
        internal int ColorDepthBits { get; set; }

        internal bool CustomCursorEnabled { get; set; }
        internal nint CursorHandle { get; set; }

        internal int State { get; set; }
    }

}