using OpenVikings.NXBasics;

namespace OpenVikings.NC2E2
{
    internal sealed class CE2Manager
    {
        private static readonly CE2Manager _instance = new();

        // Mirrors: DAT_1003a5690
        private CBitmap _desktopBitmap;

        internal static CE2Manager Instance
        {
            get
            {
                return _instance;
            }
        }

        private CE2Manager()
        {
            _desktopBitmap = null;
        }

        // NC2E2::CE2Manager::Inform_DesktopXTructed(int width, int height, unsigned int depth)
        internal void InformDesktopXTructed(int width, int height, uint depth)
        {
            if (width == 0)
            {
                // if (DAT_1003a5690 != 0) vcall +0x20 (cleanup)
                // Best C# equivalent for "virtual cleanup" is Dispose().
                _desktopBitmap?.Dispose();

                // DAT_1003a5690 = 0
                _desktopBitmap = null;
                return;
            }

            // this = operator_new(0x78); CBitmap::CBitmap(this, 100, 100, (uchar)depth)
            CBitmap bitmap = new(100, 100, checked((byte)depth));
            _desktopBitmap = bitmap;

            _ = height; // height is not used by the shown RE snippet (kept for signature parity)
        }
    }
}