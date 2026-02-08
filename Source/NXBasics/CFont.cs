using OpenVikings.NXBasics.Structs;

namespace OpenVikings.NXBasics
{
    // NXBasics::CFont
    internal sealed class CFont : IDisposable
    {
        // this + 0x08 (4 bytes)
        private uint _value08;

        // this + 0x0C (4 bytes)
        private uint _value0C;

        // this + 0x10 (4 bytes) -> used as additional spacing in width computation
        private int _spacing;

        // this + 0x18 (8 bytes)
        private CBobManager _bobManager;

        // this + 0x20 (8 bytes)
        private CPalette _palettePtr;

        private bool _disposed;

        // NXBasics::CFont::CFont()
        internal CFont()
        {
            L_InitObject();
        }

        // NXBasics::CFont::l_InitObject()
        internal void L_InitObject()
        {
            _value08 = 0;
            _value0C = 0;
            _spacing = 0;
            _bobManager = null;
            _palettePtr = null;
        }

        // NXBasics::CFont::CFont(NXBasics::CFile&, unsigned int)
        internal CFont(CFile file, uint version)
        {
            L_InitObject();

            _value08 = unchecked(file.ReadLong());
            _value0C = unchecked(file.ReadLong());

            CStorable storable = XB_Storable_LoadObject(file);
            _bobManager = storable as CBobManager;
        }

        // NXBasics::CFont::~CFont()
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _bobManager?.Dispose();
            _bobManager = null;

            _disposed = true;
        }

        // NXBasics::CFont::PrintCharacter(NXBasics::CBitmap const&, unsigned char, int, int) const
        internal void PrintCharacter(CBitmap target, byte character, int x, int y)
        {
            if (_bobManager == null)
            {
                return;
            }

            int bobIndex = character - 0x20;
            _bobManager.PrintBob(bobIndex, target, x, y, _palettePtr);
        }

        // NXBasics::CFont::PrintCharacter(NXBasics::CPalette&, NXBasics::CBitmap const&, unsigned char, int, int) const
        internal void PrintCharacter(CPalette palette, CBitmap target, byte character, int x, int y)
        {
            if (_bobManager == null)
            {
                return;
            }

            int bobIndex = character - 0x20;
            _bobManager.PrintBob(bobIndex, target, x, y, palette);
        }

        // NXBasics::CFont::PrintCharacter(NXBasics::SColorRGB const&, NXBasics::CBitmap const&, unsigned char, int, int) const
        internal void PrintCharacter(in SColorRGB transparentColor, CBitmap target, byte character, int x, int y)
        {
            if (_bobManager == null)
            {
                return;
            }

            int bobIndex = character - 0x20;
            _bobManager.PrintBobUsingTransparency(bobIndex, target, x, y, transparentColor);
        }

        // NXBasics::CFont::GetCharacterWidth(unsigned char) const
        internal int GetCharacterWidth(byte character)
        {
            if (_bobManager == null)
            {
                return 0;
            }

            int bobIndex = character - 0x20;

            if (!_bobManager.TryGetBobAreaRectangle(bobIndex, out SRectangle rect))
            {
                return 0;
            }

            return _spacing + rect.X + rect.Width + 1;
        }

        // NXBasics::CFont::GetCharacterHeight(unsigned char) const
        internal int GetCharacterHeight(byte character)
        {
            if (_bobManager == null)
            {
                return 0;
            }

            int bobIndex = character - 0x20;

            if (!_bobManager.TryGetBobAreaRectangle(bobIndex, out SRectangle rect))
            {
                return 0;
            }

            return rect.Height + rect.Y + 1;
        }

        // NXBasics::CFont::GetPixelWidth(char const*) const
        internal int GetPixelWidth(string text)
        {
            if (_bobManager == null || text == null)
            {
                return 0;
            }

            const int SpaceBobIndex = 0x49;

            int total = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '\0')
                {
                    break;
                }

                int bobIndex;
                if (ch == ' ' || ch == '\t')
                {
                    bobIndex = SpaceBobIndex;
                }
                else
                {
                    bobIndex = ((int)(byte)ch) - 0x20;
                }

                int w;
                if (!_bobManager.TryGetBobAreaRectangle(bobIndex, out SRectangle rect))
                {
                    w = 0;
                }
                else
                {
                    w = _spacing + rect.X + rect.Width + 1;
                }

                total += w;
            }

            return total;
        }

        // NXBasics::CFont::GetPixelHeight(char const*) const
        internal int GetPixelHeight(string text)
        {
            if (_bobManager == null || text == null)
            {
                return 0;
            }

            int maxHeight = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '\0')
                {
                    break;
                }

                if (ch == ' ' || ch == '\t')
                {
                    continue;
                }

                int bobIndex = ((int)(byte)ch) - 0x20;

                int h;
                if (!_bobManager.TryGetBobAreaRectangle(bobIndex, out SRectangle rect))
                {
                    h = 0;
                }
                else
                {
                    h = rect.Height + rect.Y + 1;
                }

                if (maxHeight < h)
                {
                    maxHeight = h;
                }
            }

            return maxHeight;
        }

        // NXBasics::CFont::SetPalettePtr(NXBasics::CPalette*)
        internal void SetPalettePtr(CPalette palette)
        {
            _palettePtr = palette;
        }

        // NXBasics::CFont::Storable_SaveData(NXBasics::CFile&)
        internal void Storable_SaveData(CFile file)
        {
            file.WriteLong(unchecked(_value08));
            file.WriteLong(unchecked(_value0C));

            if (_bobManager != null)
            {
                _bobManager.Storable_Save(file);
            }
            else
            {
                CStorable.Storable_SaveNull(file);
            }
        }

        // NXBasics::CFont::Storable_GetId() const
        internal static ulong Storable_GetId()
        {
            return 0x3F5UL;
        }

        // Placeholder for your existing loader hook (matches decompile naming).
        private static CStorable XB_Storable_LoadObject(CFile file)
        {
            return CStorable.XB_Storable_LoadObject(file);
        }
    }
}