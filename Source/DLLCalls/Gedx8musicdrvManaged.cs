namespace OpenVikings.DLLCalls
{
    internal sealed class Gedx8MusicDriverManaged : Gedx8musicdrv.IGedx8MusicDriver
    {
        private string basePath;
        private bool initialized;
        private int nextInstanceId;
        private readonly HashSet<int> instances;
        private int volume;

        internal Gedx8MusicDriverManaged()
        {
            basePath = string.Empty;
            initialized = false;
            nextInstanceId = 1;
            instances = [];
            volume = 100;
        }

        public void SetBasePath(string basePath)
        {
            if (basePath == null)
            {
                this.basePath = string.Empty;
                return;
            }

            this.basePath = basePath;
        }

        public void Initialize()
        {
            initialized = true;
        }

        public int CreateInstance()
        {
            if (!initialized)
            {
                return 0;
            }

            int id = nextInstanceId;
            nextInstanceId++;
            instances.Add(id);
            return id;
        }

        public bool InitSynthesizer(int instanceId, Gedx8musicdrv.Gedx8InitParams initParams)
        {
            if (!initialized)
            {
                return false;
            }

            if (!instances.Contains(instanceId))
            {
                return false;
            }

            if (initParams.SampleRate <= 0)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(basePath) && !Directory.Exists(basePath))
            {
                return false;
            }

            return true;
        }

        public void SetVolume(int volume)
        {
            if (volume < 0)
            {
                this.volume = 0;
                return;
            }

            if (volume > 100)
            {
                this.volume = 100;
                return;
            }

            this.volume = volume;
        }
    }
}