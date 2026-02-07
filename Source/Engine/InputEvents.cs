namespace OpenVikings.Engine
{
    internal class InputEvents
    {
        internal enum MouseButton
        {
            Left = 0,
            Right = 1,
            Middle = 2
        }

        internal enum ButtonAction
        {
            Down = 1,
            Up = 2
        }

        internal readonly struct MouseButtonEvent(MouseButton button, ButtonAction action)
        {
            public MouseButton Button { get; } = button;
            public ButtonAction Action { get; } = action;
        }

        internal readonly struct MouseWheelEvent(int deltaSteps)
        {
            public int DeltaSteps { get; } = deltaSteps;
        }

        internal readonly struct MouseMoveEvent(int deltaX, int deltaY)
        {
            public int DeltaX { get; } = deltaX;
            public int DeltaY { get; } = deltaY;
        }

        internal readonly struct KeyEvent(byte virtualKey, bool isDown)
        {
            public byte VirtualKey { get; } = virtualKey;
            public bool IsDown { get; } = isDown;
        }

        internal readonly struct TextInputEvent(char character)
        {
            public char Character { get; } = character;
        }

        internal enum InputEventType
        {
            MouseButton,
            MouseWheel,
            MouseMove,
            Key,
            Text
        }

        internal readonly struct InputEvent(InputEventType type, int a, int b, int c)
        {
            public InputEventType Type { get; } = type;
            public int A { get; } = a;
            public int B { get; } = b;
            public int C { get; } = c;
        }
    }
}