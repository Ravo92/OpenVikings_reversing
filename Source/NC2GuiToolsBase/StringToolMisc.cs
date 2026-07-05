using OpenVikings.Dexter;

namespace OpenVikings.NC2GuiToolsBase
{
    internal static class StringToolMisc
    {
        internal static int StringSortCompare(string a, string b)
        {
            // The original treats certain extended chars specially (likely ÄÖÜäöü) when they occur as first char.
            // It normalizes them to A/O/U or a/o/u for comparison.
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            {
                return LowStringCompare(a ?? string.Empty, b ?? string.Empty, 999);
            }

            char a0 = a[0];
            char b0 = b[0];

            bool special = IsSpecialGermanChar(a0) || IsSpecialGermanChar(b0);
            if (special)
            {
                string aa = NormalizeFirstGermanChar(a);
                string bb = NormalizeFirstGermanChar(b);
                return LowStringCompare(aa, bb, 999);
            }

            return LowStringCompare(a, b, 999);
        }

        private static bool IsSpecialGermanChar(char c)
        {
            return c == 'Ä' || c == 'Ö' || c == 'Ü' || c == 'ä' || c == 'ö' || c == 'ü';
        }

        private static string NormalizeFirstGermanChar(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            char first = s[0];
            char mapped = first;

            if (first == 'Ä') mapped = 'A';
            else if (first == 'Ö') mapped = 'O';
            else if (first == 'Ü') mapped = 'U';
            else if (first == 'ä') mapped = 'a';
            else if (first == 'ö') mapped = 'o';
            else if (first == 'ü') mapped = 'u';

            if (mapped == first)
            {
                return s;
            }

            if (s.Length == 1)
            {
                return new string(mapped, 1);
            }

            return mapped + s.Substring(1);
        }

        // Keep the original compare function name; assume an existing implementation exists elsewhere.
        internal static int LowStringCompare(string left, string right, int maxLength)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            int compareLength = Math.Min(maxLength, Math.Min(left.Length, right.Length));

            int result = string.Compare(
                left,
                0,
                right,
                0,
                compareLength,
                StringComparison.OrdinalIgnoreCase);

            if (result != 0)
            {
                return result;
            }

            if (maxLength > compareLength)
            {
                if (left.Length < right.Length)
                {
                    return -1;
                }

                if (left.Length > right.Length)
                {
                    return 1;
                }
            }

            return 0;
        }
    }
}
