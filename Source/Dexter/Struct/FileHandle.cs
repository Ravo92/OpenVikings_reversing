namespace OpenVikings.Dexter.Struct
{
    internal readonly struct FileHandle
    {
        internal readonly long Value;

        internal FileHandle(long value)
        {
            Value = value;
        }

        internal bool IsValid => Value != 0;

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            if (obj is FileHandle other)
            {
                return other.Value == Value;
            }

            return false;
        }

        public static bool operator ==(FileHandle a, FileHandle b)
        {
            return a.Value == b.Value;
        }

        public static bool operator !=(FileHandle a, FileHandle b)
        {
            return a.Value != b.Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
