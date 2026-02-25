namespace OpenVikings.Dexter.Struct
{
    internal readonly struct TextEvent
    {
        internal TextEvent(bool inUse, byte value)
        {
            InUse = inUse;
            Value = value;
        }

        internal bool InUse { get; }
        internal byte Value { get; }
    }
}