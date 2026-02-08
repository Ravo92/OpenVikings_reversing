using OpenVikings.Interfaces;

namespace OpenVikings.Engine
{
    internal sealed class GameEngineAdapter : IGameEngine
    {
        internal GameEngineAdapter()
        {
        }

        public void StartUpEngine()
        {
            // TODO: Forward to your engine startup sequence.
            // Likely: DesktopOpen -> data load -> world init -> etc.
            throw new NotImplementedException();
        }
    }
}