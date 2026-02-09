namespace OpenVikings
{
    internal class StructsCollection
    {
        // ---- Core Types / Stubs ----
        // These should be replaced with the existing engine types. Implementations here are minimal and pointer-free.

        [Flags]
        internal enum ElementFlags : uint
        {
            None = 0,
            AddedToDesktop = 1u << 11,          // 0x800 in pseudo
            DefaultMessageHandler = 1u << 7,    // 0x80 in pseudo
            CanReceiveKeyboardFocus = 1u << 2,  // 0x04 tests in pseudo
            IsRootWindow = 1u << 6,             // 0x40 tests in pseudo (root/has children)
            IgnoreMouseOver = 1u << 8,          // 0x100 tests in pseudo (mouse-over ignore)
            SpecialBit0x08 = 1u << 3            // maps to (byte)&0xF7 on 0x1d; exact meaning unknown
        }

        [Flags]
        internal enum HiddenFlags : byte
        {
            None = 0,
            Hidden = 1 << 1 // bit test on 0x1d & 2 in pseudo
        }

        [Flags]
        internal enum WindowLayerFlags : byte
        {
            None = 0,
            NormalWindow = 1 << 0,   // (byte)|1 in pseudo
            OverlayWindow = 1 << 1   // (byte)|2 in pseudo
        }

        [Flags]
        internal enum GuiMessageTypes : uint
        {
            None = 0,

            MouseLeftDown = 0x1,
            MouseLeftUp = 0x2,
            MouseRightDown = 0x4,
            MouseRightUp = 0x8,

            MouseMove = 0x10,
            MouseWheel = 0x20,
            MouseWheelEnd = 0x40,

            MouseDrag = 0x80,
            MouseExtraDown = 0x100,

            KeyDown = 0x200,
            KeyUp = 0x400,

            User = 0x800,

            BroadcastToElements = 0x1000,
            BroadcastToWindows = 0x2000
        }

        [Flags]
        internal enum MouseMessageFlags : uint
        {
            None = 0,

            Move = 0x0001,
            Wheel = 0x0002,

            LeftDown = 0x0004,
            LeftUp = 0x0008,

            MiddleDown = 0x0010,
            MiddleUp = 0x0020,

            RightDown = 0x0040,
            RightUp = 0x0080,

            LeftDoubleClick = 0x0100
        }
    }
}