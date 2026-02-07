using OpenVikings.Interfaces;
using static OpenVikings.Engine.InputEvents;

namespace OpenVikings.Engine
{
    // Consumes queued input events and forwards them to the engine's input managers.
    internal sealed class ApplicationMessageProcessor
    {
        private bool _leftDown;
        private bool _middleDown;
        private bool _rightDown;

        private readonly IMessagePump _messagePump;
        private readonly InputQueue _inputQueue;
        private readonly IMouseManager _mouseManager;
        private readonly IKeyManager _keyManager;

        internal ApplicationMessageProcessor(IMessagePump messagePump, InputQueue inputQueue, IMouseManager mouseManager, IKeyManager keyManager)
        {
            _messagePump = messagePump ?? throw new ArgumentNullException(nameof(messagePump));
            _inputQueue = inputQueue ?? throw new ArgumentNullException(nameof(inputQueue));
            _mouseManager = mouseManager ?? throw new ArgumentNullException(nameof(mouseManager));
            _keyManager = keyManager ?? throw new ArgumentNullException(nameof(keyManager));
        }

        // Processes OS messages and forwards input events into engine managers.
        // Returns false if the application should terminate.
        internal bool DoMessages()
        {
            // Pump OS messages into our input queue.
            if (!_messagePump.PumpOnce())
            {
                return false;
            }

            // Consume queued mouse button events.
            while (_inputQueue.TryDequeueMouseButton(out MouseButtonEvent mouseButtonEvent))
            {
                ApplyMouseButton(mouseButtonEvent);
            }

            // Consume wheel events.
            while (_inputQueue.TryDequeueMouseWheel(out MouseWheelEvent mouseWheelEvent))
            {
                _mouseManager.UpdateMouseWheelState(mouseWheelEvent.DeltaSteps);
            }

            // Consume mouse move events.
            while (_inputQueue.TryDequeueMouseMove(out MouseMoveEvent mouseMoveEvent))
            {
                // The legacy decompile looked like a "delta" API.
                // If your input system expects deltas, feed these values as deltas.
                // If it expects absolute positions, you should translate here.
                _ = mouseMoveEvent;
            }

            // Push aggregated button state to mouse manager.
            _mouseManager.UpdateMouseButtonState(_leftDown, _middleDown, _rightDown);

            // Consume text input.
            while (_inputQueue.TryDequeueText(out TextInputEvent textInputEvent))
            {
                _keyManager.PushText(textInputEvent.Character);
            }

            // Consume key up/down.
            while (_inputQueue.TryDequeueKey(out KeyEvent keyEvent))
            {
                _keyManager.PushKey(keyEvent.VirtualKey, isText: false, isDown: keyEvent.IsDown);
            }

            // Finalize mouse state (mirrors UpdateMouseState in the legacy code).
            _mouseManager.UpdateMouseState();

            return true;
        }

        private void ApplyMouseButton(MouseButtonEvent e)
        {
            bool isDown = e.Action == ButtonAction.Down;

            if (e.Button == MouseButton.Left)
            {
                _leftDown = isDown;
                return;
            }

            if (e.Button == MouseButton.Middle)
            {
                _middleDown = isDown;
                return;
            }

            if (e.Button == MouseButton.Right)
            {
                _rightDown = isDown;
            }
        }
    }
}
