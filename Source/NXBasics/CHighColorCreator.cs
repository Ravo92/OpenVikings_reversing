using OpenVikings.NXBasics.Structs;

namespace OpenVikings.NXBasics
{
    internal sealed class CHighColorCreator : IDisposable
    {
        // These correspond to the byte fields read at offsets 0x20..0x34 in the original code.
        // Assumed to be already correctly initialized elsewhere (e.g., by ctor / setup code).
        private byte _blueShiftDown;   // this[0x20]
        private byte _greenShiftDown;  // this[0x24]
        private byte _redShiftDown;    // this[0x28]

        private byte _blueShiftUp;     // this[0x2C]
        private byte _greenShiftUp;    // this[0x30]
        private byte _redShiftUp;      // this[0x34]

        private bool _isValid;         // equivalent intent to "*this == 0" check in the decompile

        private bool _disposed;

        internal CHighColorCreator()
        {
            // If you RE the ctor later, initialize fields here.
            // For now, keep default values.
            _isValid = false;
        }

        internal bool IsEnabled
        {
            get
            {
                return !_disposed && _isValid;
            }
        }

        // NXBasics::CHighColorCreator::GetHighColorWord(NXBasics::SColorRGB const&)
        internal ushort GetHighColorWord(byte r, byte g, byte b)
        {
            if (_disposed || !_isValid)
            {
                return 0;
            }

            uint pr = (uint)(r >> (_redShiftDown & 0x1F)) << (_redShiftUp & 0x1F);
            uint pg = (uint)(g >> (_greenShiftDown & 0x1F)) << (_greenShiftUp & 0x1F);
            uint pb = (uint)(b >> (_blueShiftDown & 0x1F)) << (_blueShiftUp & 0x1F);

            uint packed = pr | pg | pb;
            return (ushort)(packed & 0xFFFFu);
        }

        internal ushort GetHighColorWord(in SColorRGB color)
        {
            return GetHighColorWord(color.R, color.G, color.B);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Put the original destructor logic here once RE'd.
            // If the original dtor only reset fields, mirror that here.

            _isValid = false;
            _disposed = true;
        }
    }
}