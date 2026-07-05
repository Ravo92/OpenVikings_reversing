using OpenVikings.NXBasics;
using OpenVikings.NXBasics.Structs;
using System.Globalization;

namespace OpenVikings.NC2GuiToolsBase
{
    internal static class TextTool
    {
        internal static void PrintStringX(CBitmap target, int x, int y, CFont font, SColorRGB color, string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            SColorRGB currentColor = color;

            int cursorX = x;
            int cursorY = y;

            int lineStep = GetLineStep(font);

            int i = 0;

            while (i < text.Length)
            {
                char c = text[i];

                if (c == '{')
                {
                    int close = text.IndexOf('}', i + 1);
                    if (close >= 0)
                    {
                        string token = text[(i + 1)..close];
                        HandleInlineToken(font, x, y, ref cursorX, ref cursorY, ref currentColor, token, lineStep);
                        i = close + 1;
                        continue;
                    }
                }

                if (c == '\0')
                {
                    return;
                }

                if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i += 2;
                    }
                    else
                    {
                        i += 1;
                    }

                    cursorX = x;
                    cursorY += lineStep;
                    continue;
                }

                if (c == '\t' || c == ' ')
                {
                    cursorX += font.GetCharacterWidth((byte)'i');
                    i += 1;
                    continue;
                }

                if (c > 0x1F)
                {
                    byte ch8 = unchecked((byte)c);

                    int w = font.GetCharacterWidth(ch8);
                    font.PrintCharacter(in currentColor, target, ch8, cursorX, cursorY);
                    cursorX += w;
                }

                i += 1;
            }
        }

        internal static int PrintStringWrapping(CBitmap target, int x, int y, int maxX, CFont font, SColorRGB color, string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int lineStep = GetLineStep(font);

            int cursorX = x;
            int cursorY = y;

            int i = 0;

            while (i < text.Length)
            {
                char c = text[i];

                if (c == '{')
                {
                    int close = text.IndexOf('}', i + 1);
                    if (close >= 0)
                    {
                        string token = text[(i + 1)..close];

                        if (token.StartsWith("newline", StringComparison.Ordinal))
                        {
                            cursorX = x;
                            cursorY += lineStep;
                        }

                        i = close + 1;
                        continue;
                    }
                }

                if (c == '\0')
                {
                    break;
                }

                if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i += 2;
                    }
                    else
                    {
                        i += 1;
                    }

                    cursorX = x;
                    cursorY += lineStep;
                    continue;
                }

                if (c == ' ' || c == '\t')
                {
                    cursorX += font.GetCharacterWidth((byte)'i');
                    i += 1;
                    continue;
                }

                if (c < 0x20)
                {
                    i += 1;
                    continue;
                }

                int wordStart = i;
                int wordLen = 0;

                while (wordStart + wordLen < text.Length && wordLen < 100)
                {
                    char wc = text[wordStart + wordLen];

                    if (wc < 0x20 || wc == ' ' || wc == '\t' || wc == '{')
                    {
                        break;
                    }

                    wordLen += 1;
                }

                if (wordLen <= 0)
                {
                    i += 1;
                    continue;
                }

                string word = text.Substring(wordStart, wordLen);
                int wordWidth = font.GetPixelWidth(word);

                if (cursorX + wordWidth < maxX)
                {
                    target.Text_Print(font, color, cursorX, cursorY, word);
                    cursorX += wordWidth;
                }
                else
                {
                    cursorX = x;
                    cursorY += lineStep;

                    target.Text_Print(font, color, cursorX, cursorY, word);
                    cursorX += wordWidth;
                }

                i = wordStart + wordLen;
            }

            return (cursorY + lineStep) - y;
        }

        private static void HandleInlineToken(CFont font, int baseX, int baseY, ref int cursorX, ref int cursorY, ref SColorRGB currentColor, string token, int lineStep)
        {
            if (token.StartsWith("setx:", StringComparison.Ordinal))
            {
                int delta = ParseIntSafe(token[5..]);
                cursorX = baseX + delta;
                return;
            }

            if (token.StartsWith("sety:", StringComparison.Ordinal))
            {
                int delta = ParseIntSafe(token[5..]);
                cursorY = baseY + delta;
                return;
            }

            if (token.StartsWith("setfontcolor:", StringComparison.Ordinal))
            {
                string payload = token[13..];

                int c1 = payload.IndexOf(',');
                if (c1 < 0)
                {
                    return;
                }

                int c2 = payload.IndexOf(',', c1 + 1);
                if (c2 < 0)
                {
                    return;
                }

                int r = ParseIntSafe(payload[..c1]);
                int g = ParseIntSafe(payload[(c1 + 1)..c2]);
                int b = ParseIntSafe(payload[(c2 + 1)..]);

                currentColor = new SColorRGB(unchecked((byte)r), unchecked((byte)g), unchecked((byte)b));
                return;
            }

            if (token.StartsWith("newline", StringComparison.Ordinal))
            {
                cursorX = baseX;
                cursorY += lineStep;
                return;
            }

            _ = font;
        }

        private static int GetLineStep(CFont font)
        {
            int h = font.GetCharacterHeight((byte)'A');

            if (h <= 0)
            {
                h = font.GetPixelHeight("A");
            }

            if (h <= 0)
            {
                h = 10;
            }

            return h + 2;
        }

        private static int ParseIntSafe(string s)
        {

            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return value;
            }

            return 0;
        }
    }
}