using OpenVikings.Interfaces;

namespace OpenVikings.Engine
{
    // In this project the Win32 window thread runs its own native message loop.
    // Therefore, PumpOnce() only needs to report whether the window requested quit.
    internal sealed class Win32MessagePump : IMessagePump
    {
        private readonly InputQueue _inputQueue;

        internal Win32MessagePump(InputQueue inputQueue)
        {
            _inputQueue = inputQueue ?? throw new ArgumentNullException(nameof(inputQueue));
        }

        public bool PumpOnce()
        {
            // If WM_DESTROY / WM_QUIT was observed in WndProc, WindowHandler requests quit on the queue.
            return !_inputQueue.IsQuitRequested;
        }
    }
}