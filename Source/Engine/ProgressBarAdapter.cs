using OpenVikings.Interfaces;

namespace OpenVikings.Engine
{
    internal sealed class ProgressBarAdapter : IProgressBar
    {
        internal ProgressBarAdapter()
        {
        }

        public void Init(bool isColdStart)
        {
            // TODO: Hook into your loading/progress UI.
            throw new NotImplementedException();
        }

        public void Exit()
        {
            // TODO: Tear down progress UI.
            throw new NotImplementedException();
        }
    }
}