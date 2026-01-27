namespace OpenVikings.DLLCalls
{
    internal class Gedx8musicdrv
    {
        internal interface IGedx8MusicDriver
        {
            void SetBasePath(string basePath);
            void Initialize();
            int CreateInstance();
            bool InitSynthesizer(int instanceId, Gedx8InitParams initParams);
            void SetVolume(int volume);
        }

        internal readonly struct Gedx8InitParams(int sampleRate, int bytesOrBits)
        {
            public int BytesOrBits { get; } = bytesOrBits;
            public int SampleRate { get; } = sampleRate;
        }

        internal static class Gedx8musicdrvFacade
        {
            internal static IGedx8MusicDriver GetInterface2_Managed()
            {
                return new Gedx8MusicDriverManaged();
            }
        }
    }
}