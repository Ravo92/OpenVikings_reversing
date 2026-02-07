using OpenVikings.Interfaces;

namespace OpenVikings.Engine
{
    // Minimal key manager implementation to keep the wiring complete.
    // Replace with real key input logic later.
    internal sealed class BasicKeyManager : IKeyManager
    {
        private readonly Queue<(byte VirtualKey, bool IsDown)> _keys;
        private readonly Queue<char> _text;

        internal BasicKeyManager()
        {
            _keys = new Queue<(byte VirtualKey, bool IsDown)>();
            _text = new Queue<char>();
        }

        public void PushKey(byte virtualKey, bool isText, bool isDown)
        {
            // 'isText' is kept for parity with the legacy signature.
            _ = isText;
            _keys.Enqueue((virtualKey, isDown));
        }

        public void PushText(char character)
        {
            _text.Enqueue(character);
        }

        internal bool TryDequeueKey(out byte virtualKey, out bool isDown)
        {
            if (_keys.Count == 0)
            {
                virtualKey = 0;
                isDown = false;
                return false;
            }

            (byte VirtualKey, bool IsDown) item = _keys.Dequeue();
            virtualKey = item.VirtualKey;
            isDown = item.IsDown;
            return true;
        }

        internal bool TryDequeueText(out char c)
        {
            if (_text.Count == 0)
            {
                c = '\0';
                return false;
            }

            c = _text.Dequeue();
            return true;
        }
    }
}
