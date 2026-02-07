using static OpenVikings.Engine.InputEvents;

namespace OpenVikings.Interfaces
{
    internal interface IInputQueue
    {
        // Returns true if an event was dequeued.
        bool TryDequeueMouseButton(out MouseButtonEvent mouseButtonEvent);
        bool TryDequeueMouseWheel(out MouseWheelEvent mouseWheelEvent);
        bool TryDequeueMouseMove(out MouseMoveEvent mouseMoveEvent);
        bool TryDequeueKey(out KeyEvent keyEvent);
        bool TryDequeueText(out TextInputEvent textInputEvent);
    }
}
