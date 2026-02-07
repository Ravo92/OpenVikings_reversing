namespace OpenVikings.Interfaces
{
    internal interface IMessagePump
    {
        // Returns false when the app should terminate (e.g., WM_QUIT).
        bool PumpOnce();
    }
}