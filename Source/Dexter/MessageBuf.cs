namespace OpenVikings.Dexter
{
    internal sealed class MessageBuf
    {
        private const int MaxQueueEntries = 0x32; // 50

        private readonly DexEvent[] _events;
        private int _write;
        private int _read;

        internal MessageBuf()
        {
            _events = new DexEvent[MaxQueueEntries];
            _write = 0;
            _read = 0;
        }

        internal void AddMessage(DexEvent ev)
        {
            if (ev.Type == 0)
            {
                return;
            }

            if (_events[_write].Type != 0)
            {
                return;
            }

            _events[_write] = ev;
            _write = (_write + 1) % MaxQueueEntries;
        }

        internal DexEvent PeekMessage()
        {
            return _events[_read];
        }

        internal DexEvent GetMessage()
        {
            DexEvent ev = _events[_read];
            if (ev.Type != 0)
            {
                _events[_read] = default;
                _read = (_read + 1) % MaxQueueEntries;
            }
            return ev;
        }

        internal readonly struct DexEvent
        {
            internal DexEvent(short type, int a, int b, short c, short d, short e, short f)
            {
                Type = type;
                A = a;
                B = b;
                C = c;
                D = d;
                E = e;
                F = f;
            }

            internal short Type { get; }
            internal int A { get; }
            internal int B { get; }
            internal short C { get; }
            internal short D { get; }
            internal short E { get; }
            internal short F { get; }
        }
    }
}
