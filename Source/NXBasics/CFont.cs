using OpenVikings.NXBasics.Structs;

namespace OpenVikings.NXBasics
{
    // NXBasics::CFont
    internal sealed class CFont : CStorable, IDisposable
    {
        // this + 0x08 (4 bytes)
        private uint _value08;

        // this + 0x0C (4 bytes)
        private uint _value0C;

        // this + 0x10 (4 bytes) -> additional spacing in width computation
        private int _spacing;

        // this + 0x18 (8 bytes)
        private CBobManager? _bobManager;

        // this + 0x20 (8 bytes)
        private CPalette? _palettePtr;

        private bool _disposed;

        private const int FirstPrintableChar = 0x20;
        private const uint SpaceBobId = 0x49u;

        internal void SetSpacing(int spacing)
        {
            _spacing = spacing;
        }

        // NXBasics::CFont::CFont()
        internal CFont()
        {
            L_InitObject();
        }

        // NXBasics::CFont::CFont(NXBasics::CFile&, unsigned int)
        internal CFont(CFile file, uint version)
        {
            ArgumentNullException.ThrowIfNull(file);

            L_InitObject();

            _value08 = file.ReadLong();
            _value0C = file.ReadLong();

            CStorable? storable = XBStorable.LoadObjectOrNull(file);
            _bobManager = storable as CBobManager;

            _ = version; // decompile param exists; not used
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

        // NXBasics::CFont::~CFont()
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _bobManager?.Dispose();
            _bobManager = null;

            _palettePtr = null;

            _disposed = true;
        }

        // NXBasics::CFont::PrintCharacter(NXBasics::CBitmap const&, unsigned char, int, int) const
        internal void PrintCharacter(CBitmap target, byte character, int x, int y)
        {
            if (_bobManager == null)
            {
                return;
            }

            uint bobId = GetBobIdForPrint(character);
            if (bobId == 0xFFFFFFFFu)
            {
                return;
            }

            _bobManager.PrintBob(bobId, target, x, y, _palettePtr);
        }

        // NXBasics::CFont::PrintCharacter(NXBasics::CPalette&, NXBasics::CBitmap const&, unsigned char, int, int) const
        internal void PrintCharacter(CPalette palette, CBitmap target, byte character, int x, int y)
        {
            ArgumentNullException.ThrowIfNull(palette);

            if (_bobManager == null)
            {
                return;
            }

            uint bobId = GetBobIdForPrint(character);
            if (bobId == 0xFFFFFFFFu)
            {
                return;
            }

            _bobManager.PrintBob(bobId, target, x, y, palette);
        }

        // NXBasics::CFont::PrintCharacter(NXBasics::SColorRGB const&, NXBasics::CBitmap const&, unsigned char, int, int) const
        internal void PrintCharacter(in SColorRGB transparentColor, CBitmap target, byte character, int x, int y)
        {
            if (_bobManager == null)
            {
                return;
            }

            uint bobId = GetBobIdForPrint(character);
            if (bobId == 0xFFFFFFFFu)
            {
                return;
            }

            // C++ calls: CBobManager::PrintBob_UsingTransparency(bobId, dst, x, y, SColorRGB const&)
            // The current C# CBobManager implements a different overload:
            // PrintBob_UsingTransparency(uint bobId, CBitmap destination, uint alpha, int dstX, int dstY, CPalette? palette)
            // Until the SColorRGB overload exists in CBobManager, the alpha-based one is used here.
            _bobManager.PrintBob_UsingTransparency(bobId, target, 0xFFu, x, y, _palettePtr);

            _ = transparentColor; // kept to preserve API parity; used once CBobManager has the matching overload
        }

        // NXBasics::CFont::GetCharacterWidth(unsigned char) const
        internal int GetCharacterWidth(byte character)
        {
            if (_bobManager == null)
            {
                return 0;
            }

            uint bobId = GetBobId_Default(character);
            if (bobId == 0xFFFFFFFFu)
            {
                return 0;
            }

            SRectangle? rect = _bobManager.GetBobAreaRectanglePtr(bobId);
            if (rect == null)
            {
                return 0;
            }

            // C++: spacing + rect.X + rect.Width + 1
            return _spacing + rect.Value.X + rect.Value.Width + 1;
        }

        // NXBasics::CFont::GetCharacterHeight(unsigned char) const
        internal int GetCharacterHeight(byte character)
        {
            if (_bobManager == null)
            {
                return 0;
            }

            uint bobId = GetBobId_Default(character);
            if (bobId == 0xFFFFFFFFu)
            {
                return 0;
            }

            SRectangle? rect = _bobManager.GetBobAreaRectanglePtr(bobId);
            if (rect == null)
            {
                return 0;
            }

            // C++: rect.Height + rect.Y + 1
            return rect.Value.Height + rect.Value.Y + 1;
        }

        // NXBasics::CFont::GetPixelWidth(char const*) const
        internal int GetPixelWidth(string? text)
        {
            if (_bobManager == null || text == null)
            {
                return 0;
            }

            int total = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '\0')
                {
                    break;
                }

                uint bobId;
                if (ch == ' ' || ch == '\t')
                {
                    bobId = SpaceBobId;
                }
                else
                {
                    int idx = (byte)ch - FirstPrintableChar;
                    if (idx < 0)
                    {
                        bobId = 0xFFFFFFFFu;
                    }
                    else
                    {
                        bobId = unchecked((uint)idx);
                    }
                }

                int w = 0;

                if (bobId != 0xFFFFFFFFu)
                {
                    SRectangle? rect = _bobManager.GetBobAreaRectanglePtr(bobId);
                    if (rect != null)
                    {
                        w = _spacing + rect.Value.X + rect.Value.Width + 1;
                    }
                }

                total += w;
            }

            return total;
        }

        // NXBasics::CFont::GetPixelHeight(char const*) const
        internal int GetPixelHeight(string? text)
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

                int idx = (byte)ch - FirstPrintableChar;
                if (idx < 0)
                {
                    continue;
                }

                uint bobId = unchecked((uint)idx);
                SRectangle? rect = _bobManager.GetBobAreaRectanglePtr(bobId);
                if (rect == null)
                {
                    continue;
                }

                int h = rect.Value.Height + rect.Value.Y + 1;
                if (maxHeight < h)
                {
                    maxHeight = h;
                }
            }

            return maxHeight;
        }

        // NXBasics::CFont::SetPalettePtr(NXBasics::CPalette*)
        internal void SetPalettePtr(CPalette? palette)
        {
            _palettePtr = palette;
        }

        // NXBasics::CFont::Storable_SaveData(NXBasics::CFile&)
        internal override void Storable_SaveData(CFile file)
        {
            ArgumentNullException.ThrowIfNull(file);

            file.WriteLong(_value08);
            file.WriteLong(_value0C);

            // C++ always saves the storable at +0x18. If null, write a null header (id=0, version=0).
            if (_bobManager == null)
            {
                file.WriteLong(0u);
                file.WriteLong(0u);
                return;
            }

            _bobManager.Storable_Save(file);
        }

        // NXBasics::CFont::Storable_GetId() const
        internal override uint Storable_GetId()
        {
            return 0x3F5u;
        }

        private static uint GetBobId_Default(byte character)
        {
            if (character < FirstPrintableChar)
            {
                return 0xFFFFFFFFu;
            }

            return unchecked((uint)(character - FirstPrintableChar));
        }

        private static uint GetBobIdForPrint(byte character)
        {
            if (character == (byte)' ' || character == (byte)'\t')
            {
                return SpaceBobId;
            }

            return GetBobId_Default(character);
        }
    }
}