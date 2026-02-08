namespace OpenVikings.Interfaces
{
    public interface IOpenVikingsDebug
    {
        /// <summary>
        /// Updates FPS diagnostics (or frame timing) using the given frame time in milliseconds.
        /// </summary>
        void UpdateFps(uint frameTimeMs);
    }
}