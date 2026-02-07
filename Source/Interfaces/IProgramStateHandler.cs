namespace OpenVikings.Interfaces
{
    internal interface IProgramStateHandler
    {
        void Startup();
        void Shutdown();

        // Returns true to continue running, false to request application termination.
        bool Update();
    }
}