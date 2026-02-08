using OpenVikings.Interfaces;

namespace OpenVikings.Engine
{
    internal sealed class AudioManagerAdapter : IAudioManager
    {
        internal AudioManagerAdapter()
        {
        }

        public bool IsAvailable
        {
            get
            {
                // TODO: Return whether audio subsystem is initialized/available.
                throw new NotImplementedException();
            }
        }

        public void StartTrack(int trackId)
        {
            // TODO: Forward to your audio subsystem track start.
            throw new NotImplementedException();
        }
    }
}