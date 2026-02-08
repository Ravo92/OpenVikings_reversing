namespace OpenVikings.Interfaces
{
    public interface ITimeSource
    {
        /// <summary>
        /// Returns a monotonically increasing time value in milliseconds (best effort).
        /// </summary>
        uint GetMilliseconds();
    }
}