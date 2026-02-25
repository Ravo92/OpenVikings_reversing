namespace OpenVikings.Dexter
{
    internal sealed class ChunkyObject : IDisposable
    {
        private int _unknown0;
        private ushort _width;
        private ushort _height;
        private byte _bytesPerPixel;
        private uint _dataSize;
        private byte[]? _pixelData;
        private byte[]? _alphaData;
        private uint _alphaSize;
        private bool _valid;

        public ChunkyObject()
        {
            _unknown0 = 0;
            _width = 0;
            _height = 0;
            _bytesPerPixel = 0;
            _dataSize = 0;
            _pixelData = null;
            _alphaData = null;
            _alphaSize = 0;
            _valid = false;
        }

        public ushort Width => _width;
        public ushort Height => _height;

        public int BitDepth => _bytesPerPixel << 3;

        public uint DataSize => _dataSize;

        public byte[]? PixelData => _pixelData;

        public byte[]? AlphaData => _alphaData;

        public bool IsValid => _valid;

        public bool Init(uint width, uint height, byte bitDepth)
        {
            uint bytesPerPixelU = (uint)((bitDepth + 1) >> 3);

            ushort w16 = (ushort)width;
            ushort h16 = (ushort)height;

            if (_pixelData != null)
            {
                uint currentCalc = (uint)_bytesPerPixel * _width * _height;
                if (currentCalc == _dataSize)
                {
                    FreePixelData_ResetLikeCpp();
                }
            }

            if (_alphaData != null)
            {
                _alphaData = null;
                _alphaSize = 0;
            }

            uint newSize = height * width * bytesPerPixelU;
            _dataSize = newSize;

            if (newSize > int.MaxValue)
            {
                _pixelData = null;
                _valid = false;
                return false;
            }

            try
            {
                _pixelData = new byte[(int)newSize];
            }
            catch (OutOfMemoryException)
            {
                _pixelData = null;
                _valid = false;
                return false;
            }

            _width = w16;
            _height = h16;
            _bytesPerPixel = (byte)bytesPerPixelU;

            _unknown0 = 0;
            _valid = true;

            return true;
        }

        private void FreePixelData_ResetLikeCpp()
        {
            _pixelData = null;
            _dataSize = 0;
            _bytesPerPixel = 0;
            _width = 0;
            _height = 0;
        }

        public void Dispose()
        {
            if (_pixelData != null)
            {
                uint currentCalc = (uint)_bytesPerPixel * (uint)_width * (uint)_height;
                if (currentCalc == _dataSize)
                {
                    FreePixelData_ResetLikeCpp();
                }
            }

            if (_alphaData != null)
            {
                _alphaData = null;
                _alphaSize = 0;
            }

            _valid = false;
            GC.SuppressFinalize(this);
        }

        ~ChunkyObject()
        {
            Dispose();
        }
    }
}