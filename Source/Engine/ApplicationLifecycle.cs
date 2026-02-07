using OpenVikings.Interfaces;

namespace OpenVikings.Engine
{
    internal sealed class ApplicationLifecycle : IApplicationLifecycle
    {
        private int _ended;

        public bool HasEnded
        {
            get { return Volatile.Read(ref _ended) != 0; }
        }

        public void ApplicationEnded()
        {
            Volatile.Write(ref _ended, 1);
        }
    }
}
