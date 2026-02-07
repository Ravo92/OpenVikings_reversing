namespace OpenVikings.Interfaces
{
    internal interface IMasterControlProgram
    {
        /// <summary>
        /// Performs one system update tick.
        /// Returns true if the app should continue running; false if it should stop.
        /// </summary>
        bool Update();
    }
}