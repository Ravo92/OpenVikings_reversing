using OpenVikings.NXBasics;

namespace OpenVikings.NC2GuiToolsBase
{
    // Thin adapters to keep CGuiBaseDataManager readable.
    // Mirrors the original NXBasics helper functions.
    internal static class NXBasicsApi
    {
        internal static T XB_Storable_LoadObject<T>(string path) where T : class
        {
            return (T)XBStorable.LoadObject(path);
        }

        internal static CMemory XB_Storable_LoadObject(CFile file)
        {
            return (CMemory)XBStorable.LoadObject(file);
        }

        internal static CPalette XB_PictureTool_LoadPaletteOutOfPicture(string path)
        {
            return XBPictureTool.LoadPaletteOutOfPicture(path);
        }

        internal static CBitmap XB_PictureTool_LoadBitmapOutOfPicture(string path)
        {
            return XBPictureTool.LoadBitmapOutOfPicture(path);
        }
    }
}