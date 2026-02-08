namespace OpenVikings.NXBasics
{
    /// <summary>
    /// Port of NXBasics::CXBSystemManager ctor/dtor logic.
    /// Assumptions:
    /// - CFile.FileSystem_Initialize(), CPalette, CHighColorCreator, CTrueColorCreator exist.
    /// - CPalette and CHighColorCreator need Dispose() (or equivalent) to mirror destructor/vtable calls.
    /// - CTrueColorCreator has SetTrueColorMasks(uint rMask, uint gMask, uint bMask).
    /// </summary>
    internal sealed class CXBSystemManager : IDisposable
    {
        internal static CXBSystemManager sTheObjectPtr;
        internal static CPalette sPalettePtr;
        internal static CHighColorCreator sHighColorCreatorPtr;
        internal static CTrueColorCreator sTrueColorCreatorPtr;

        private bool _disposed;

        internal CXBSystemManager()
        {
            // Original behavior: if already constructed, ctor returns early without changing anything.
            if (sTheObjectPtr != null)
            {
                return;
            }

            sTheObjectPtr = this;

            CFile.FileSystem_Initialize();

            sPalettePtr = new CPalette();

            sHighColorCreatorPtr = new CHighColorCreator();

            sTrueColorCreatorPtr = new CTrueColorCreator();
            sTrueColorCreatorPtr.SetTrueColorMasks(0x00FF0000u, 0x0000FF00u, 0x000000FFu);
        }

        ~CXBSystemManager()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            // TrueColorCreator: no resource ownership -> just release reference.
            sTrueColorCreatorPtr = null;

            // HighColorCreator: explicit dtor + delete in original code -> Dispose in C#.
            sHighColorCreatorPtr?.Dispose();
            sHighColorCreatorPtr = null;

            // Palette: original does virtual call at vtable+0x20 -> treat as Dispose().
            sPalettePtr?.Dispose();
            sPalettePtr = null;

            sTheObjectPtr = null;

            _disposed = true;
        }
    }
}