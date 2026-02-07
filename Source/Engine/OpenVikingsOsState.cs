namespace OpenVikings.Engine
{
    internal sealed class OpenVikingsOsState
    {
        /// <summary>
        /// Time reset reference used by the original timing formula (ms).
        /// The value may change during OpenVikingsMain.RunOnce().
        /// </summary>
        public int TimeCheckResetMs { get; set; }
    }
}