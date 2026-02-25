namespace OpenVikings.Dexter.Struct
{
    internal readonly struct MouseEvent
    {
        internal MouseEvent(byte type, byte button, int duration, short x, short y, ushort qualifiers)
        {
            Type = type;
            Button = button;
            Duration = duration;
            X = x;
            Y = y;
            Qualifiers = qualifiers;
        }

        internal byte Type { get; }
        internal byte Button { get; }
        internal ushort Qualifiers { get; }
        internal int Duration { get; }
        internal short X { get; }
        internal short Y { get; }
    }
}
