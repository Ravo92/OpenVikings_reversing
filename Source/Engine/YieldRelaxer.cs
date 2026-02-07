using OpenVikings.Interfaces;

namespace OpenVikings.Engine
{
    internal class YieldRelaxer : IThreadRelaxer
    {
        public void Relax()
        {
            // Thread.Yield() is a simple, low-latency way to yield execution.
            Thread.Yield();
        }
    }
}
