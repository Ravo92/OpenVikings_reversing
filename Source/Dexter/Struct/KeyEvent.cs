namespace OpenVikings.Dexter.Struct
{
    internal readonly struct KeyEvent
    {
        internal KeyEvent(byte type, byte key, int duration, ushort qualifiers)
        {
            Type = type;
            Key = key;
            Duration = duration;
            Qualifiers = qualifiers;
        }

        internal byte Type { get; }
        internal byte Key { get; }
        internal ushort Qualifiers { get; }
        internal int Duration { get; }
    }
}
