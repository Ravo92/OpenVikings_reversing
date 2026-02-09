namespace OpenVikings.NXBaseGui
{
    internal readonly struct TXGuiMessageTypes
    {
        internal readonly uint Value;

        internal TXGuiMessageTypes(uint value)
        {
            Value = value;
        }

        internal bool Is(uint value)
        {
            return Value == value;
        }

        internal static TXGuiMessageTypes FromUInt32(uint value)
        {
            return new TXGuiMessageTypes(value);
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static implicit operator uint(TXGuiMessageTypes value)
        {
            return value.Value;
        }

        public static implicit operator TXGuiMessageTypes(uint value)
        {
            return new TXGuiMessageTypes(value);
        }

        public static bool operator ==(TXGuiMessageTypes left, TXGuiMessageTypes right)
        {
            return left.Value == right.Value;
        }

        public static bool operator !=(TXGuiMessageTypes left, TXGuiMessageTypes right)
        {
            return left.Value != right.Value;
        }

        public override bool Equals(object? obj)
        {
            if (obj is TXGuiMessageTypes other)
            {
                return other.Value == Value;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        // Known value found in decompile:
        internal static readonly TXGuiMessageTypes KeyDown = new(0x200u);
        internal static readonly TXGuiMessageTypes KeyUp = new(0x400u);
        internal static readonly TXGuiMessageTypes User = new(0x800u);
        internal static readonly TXGuiMessageTypes BroadcastToElements = new(0x1000u); // TODO
        internal static readonly TXGuiMessageTypes BroadcastToWindows = new(0x2000u);  // TODO

        // TODO: Verify real values in CDesktop::l_Message_HandleMouse (param_2 compares / call sites).
        internal static readonly TXGuiMessageTypes MouseMove = new(0x1100u);
        internal static readonly TXGuiMessageTypes MouseLeftDown = new(0x1101u);
        internal static readonly TXGuiMessageTypes MouseLeftUp = new(0x1102u);
        internal static readonly TXGuiMessageTypes MouseRightDown = new(0x1103u);
        internal static readonly TXGuiMessageTypes MouseRightUp = new(0x1104u);
        internal static readonly TXGuiMessageTypes MouseMiddleDown = new(0x1105u);
        internal static readonly TXGuiMessageTypes MouseMiddleUp = new(0x1106u);
        internal static readonly TXGuiMessageTypes MouseWheel = new(0x1107u);
        internal static readonly TXGuiMessageTypes MouseWheelEnd = new(0x1108u);
        internal static readonly TXGuiMessageTypes MouseDrag = new(0x1109u);
        internal static readonly TXGuiMessageTypes MouseLeftDoubleClick = new(0x110Au);
        internal static readonly TXGuiMessageTypes MouseRightClick = new(0x110Bu);
    }
}