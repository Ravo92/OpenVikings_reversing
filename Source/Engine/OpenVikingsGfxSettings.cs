namespace OpenVikings.Engine
{
    public sealed class OpenVikingsGFXSettings
    {
        /// <summary>
        /// Target callback time (ms). Special values:
        /// 0x7777 => end application
        /// 0x6666 => reset FPS and return
        /// </summary>
        public uint CallbackTimeMs { get; set; }
    }
}