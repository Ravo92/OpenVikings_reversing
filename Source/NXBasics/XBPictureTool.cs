namespace OpenVikings.NXBasics
{
    internal static class XBPictureTool
    {
        internal static CBitmap? LoadBitmapOutOfPicture(string filename)
        {
            CPicture picture = new(filename);
            try
            {
                // RE: CBitmap::SetPalettePtr(*(CBitmap**)this, 0)
                CBitmap? bitmap = picture.Bitmap;
                bitmap?.SetPalettePtr(null);

                // RE: take bitmap pointer, then set picture->bitmap = 0
                CBitmap? extracted = picture.ExtractBitmap();

                // RE then frees palette (because picture still owns it)
                // In C#: Dispose of picture will free remaining owned resources (palette).
                return extracted;
            }
            finally
            {
                picture.Dispose();
            }
        }

        internal static CPalette? LoadPaletteOutOfPicture(string filename)
        {
            CPicture picture = new(filename);
            try
            {
                // RE: if bitmap != 0 then CBitmap::SetPalettePtr(bitmap, 0)
                CBitmap? bitmap = picture.Bitmap;
                bitmap?.SetPalettePtr(null);

                // RE: take palette pointer, then set picture->palette = 0
                CPalette? extracted = picture.ExtractPalette();

                // RE then frees bitmap (because picture still owns it),
                // and would free palette only if it wasn't extracted.
                // In C#: Dispose frees remaining owned resources (bitmap).
                return extracted;
            }
            finally
            {
                picture.Dispose();
            }
        }
    }
}