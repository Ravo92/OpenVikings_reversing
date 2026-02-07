namespace OpenVikings.Interfaces
{
    public interface IApplicationLifecycle
    {
        /// <summary>
        /// Signals that the application has ended (shutdown hook).
        /// </summary>
        void ApplicationEnded();
    }
}
