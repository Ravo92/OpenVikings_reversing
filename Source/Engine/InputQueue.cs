using OpenVikings.Interfaces;
using System.Collections.Concurrent;
using static OpenVikings.Engine.InputEvents;

namespace OpenVikings.Engine
{
    // Thread-safe input event queue used by the Win32 window thread (producer)
    // and the engine thread (consumer).
    internal sealed class InputQueue : IInputQueue
    {
        private readonly ConcurrentQueue<InputEvent> _events;
        private int _quitRequested;

        internal InputQueue()
        {
            _events = new ConcurrentQueue<InputEvent>();
            _quitRequested = 0;
        }

        internal bool IsQuitRequested
        {
            get { return Volatile.Read(ref _quitRequested) != 0; }
        }

        internal void RequestQuit()
        {
            Volatile.Write(ref _quitRequested, 1);
        }

        internal void Enqueue(InputEvent ev)
        {
            _events.Enqueue(ev);
        }

        internal bool TryDequeueRaw(out InputEvent ev)
        {
            return _events.TryDequeue(out ev);
        }

        public bool TryDequeueMouseButton(out MouseButtonEvent mouseButtonEvent)
        {
            mouseButtonEvent = default;

            if (!TryDequeueRaw(out InputEvent ev))
            {
                return false;
            }

            if (ev.Type != InputEventType.MouseButton)
            {
                // Put it back by re-enqueueing at the end (stable enough for this use case).
                _events.Enqueue(ev);
                return false;
            }

            mouseButtonEvent = new MouseButtonEvent((MouseButton)ev.A, (ButtonAction)ev.B);
            return true;
        }

        public bool TryDequeueMouseWheel(out MouseWheelEvent mouseWheelEvent)
        {
            mouseWheelEvent = default;

            if (!TryDequeueRaw(out InputEvent ev))
            {
                return false;
            }

            if (ev.Type != InputEventType.MouseWheel)
            {
                _events.Enqueue(ev);
                return false;
            }

            mouseWheelEvent = new MouseWheelEvent(ev.A);
            return true;
        }

        public bool TryDequeueMouseMove(out MouseMoveEvent mouseMoveEvent)
        {
            mouseMoveEvent = default;

            if (!TryDequeueRaw(out InputEvent ev))
            {
                return false;
            }

            if (ev.Type != InputEventType.MouseMove)
            {
                _events.Enqueue(ev);
                return false;
            }

            mouseMoveEvent = new MouseMoveEvent(ev.A, ev.B);
            return true;
        }

        public bool TryDequeueKey(out KeyEvent keyEvent)
        {
            keyEvent = default;

            if (!TryDequeueRaw(out InputEvent ev))
            {
                return false;
            }

            if (ev.Type != InputEventType.Key)
            {
                _events.Enqueue(ev);
                return false;
            }

            keyEvent = new KeyEvent((byte)ev.A, ev.B != 0);
            return true;
        }

        public bool TryDequeueText(out TextInputEvent textInputEvent)
        {
            textInputEvent = default;

            if (!TryDequeueRaw(out InputEvent ev))
            {
                return false;
            }

            if (ev.Type != InputEventType.Text)
            {
                _events.Enqueue(ev);
                return false;
            }

            textInputEvent = new TextInputEvent((char)ev.A);
            return true;
        }
    }
}