using OpenVikings.Interfaces;

namespace OpenVikings.Engine
{
    internal sealed class GameIoAdapter : IGameIo
    {
        internal GameIoAdapter()
        {
        }

        public void CleanmapLoad(string mapPath, bool clearWorld, bool resetState, bool keepPlayer)
        {
            // TODO: Forward to the clean map loading routine.
            // This probably maps to: load map file -> optional clear world -> optional reset -> optional keep player state.
            throw new NotImplementedException();
        }
    }
}