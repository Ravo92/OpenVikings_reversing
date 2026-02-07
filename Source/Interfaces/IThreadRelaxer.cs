namespace OpenVikings.Interfaces
{
    public interface IThreadRelaxer
    {
        /// <summary>
        /// Yields/sleeps briefly to reduce CPU usage and let other threads run.
        /// </summary>
        void Relax();
    }
}
