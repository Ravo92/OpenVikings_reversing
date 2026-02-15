using OpenVikings.NXBasics;

namespace OpenVikings.NC2GuiToolsBase
{
    // Thin adapters to keep CGuiBaseDataManager readable.
    // Mirrors the original NXBasics helper functions.
    internal static class NXBasicsApi
    {
        internal static CStorable XB_Storable_LoadObject(string path)
        {
            return XBStorable.LoadObject(path);
        }

        internal static T XB_Storable_LoadObject<T>(string path) where T : class
        {
            CStorable storable = XBStorable.LoadObject(path);

            return storable is not T typed ? throw new InvalidOperationException($"Expected '{typeof(T).Name}', got '{storable.GetType().Name}' for '{path}'.") : typed;
        }

        internal static CStorable XB_Storable_LoadObject(CFile file)
        {
            return XBStorable.LoadObject(file);
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