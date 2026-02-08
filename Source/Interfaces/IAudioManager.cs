namespace OpenVikings.Interfaces
{
    internal interface IAudioManager
    {
        bool IsAvailable { get; }
        void StartTrack(int trackId);
    }
}