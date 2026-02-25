using System.Globalization;
using System.Text;

namespace OpenVikings.Dexter
{
    // Managed port of DexterString helpers (ASCII / C-string style).
    // Notes:
    // - These functions treat buffers as null-terminated ('\0') character arrays.
    // - Character classification is ASCII-focused, matching the original intent.
    internal static class DexterString
    {
        // --------------------------------------------------------------------
        // Char classification (ASCII)
        // --------------------------------------------------------------------

        internal static ulong IsAlpha(char c)
        {
            if (c >= 0x7F)
            {
                return 0;
            }

            // The original uses a CharacterTypes lookup with mask 0x103.
            // For a practical C# port, treat ASCII letters and '_' as alpha-like.
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_')
            {
                return 1;
            }

            return 0;
        }

        internal static ulong IsDigit(char c)
        {
            if (c > 0x7E)
            {
                return 0;
            }

            if (c < '0' || c > '9')
            {
                return 0;
            }

            return 1;
        }

        internal static bool IsAlphaNum(char c)
        {
            if (c >= 0x7F)
            {
                return false;
            }

            if (IsAlpha(c) != 0)
            {
                return true;
            }

            if (c >= '0' && c <= '9')
            {
                return true;
            }

            return false;
        }

        internal static char ToLower(char c)
        {
            // Original:
            // cLower = c + ' ';
            // if (0x19 < (byte)(c + 0xbfU)) cLower = c;
            // This is a compact ASCII A..Z check.
            if (c >= 'A' && c <= 'Z')
            {
                return (char)(c + 0x20);
            }

            return c;
        }

        // --------------------------------------------------------------------
        // C-string style buffer operations (null-terminated Span<char>)
        // --------------------------------------------------------------------

        internal static void StringToLower(Span<char> buffer)
        {
            int i = 0;
            while (i < buffer.Length)
            {
                char c = buffer[i];
                if (c == '\0')
                {
                    break;
                }

                buffer[i] = ToLower(c);
                i++;
            }
        }

        /// <summary>
        /// Searches for <paramref name="needle"/> in <paramref name="haystack"/>.
        /// If <paramref name="caseSensitive"/> is true: exact match. If false: ASCII case-insensitive.
        /// Returns the start index, or -1 if not found.
        /// </summary>
        internal static int StringSearch(ReadOnlySpan<char> haystack, ReadOnlySpan<char> needle, bool caseSensitive)
        {
            int needleLen = CStrLen(needle);
            if (needleLen == 0)
            {
                return 0;
            }

            int hayLen = CStrLen(haystack);
            if (hayLen == 0)
            {
                return -1;
            }

            for (int start = 0; start < hayLen; start++)
            {
                if (start + needleLen > hayLen)
                {
                    return -1;
                }

                bool match = true;
                for (int j = 0; j < needleLen; j++)
                {
                    char a = haystack[start + j];
                    char b = needle[j];

                    if (caseSensitive)
                    {
                        if (a != b)
                        {
                            match = false;
                            break;
                        }
                    }
                    else
                    {
                        if (ToLower(a) != ToLower(b))
                        {
                            match = false;
                            break;
                        }
                    }
                }

                if (match)
                {
                    return start;
                }
            }

            return -1;
        }

        /// <summary>
        /// Replaces all occurrences of <paramref name="find"/> with <paramref name="replace"/> in-place.
        /// Output is truncated to fit the destination buffer (always null-terminated if possible).
        /// </summary>
        internal static void StringReplace(Span<char> buffer, ReadOnlySpan<char> find, ReadOnlySpan<char> replace)
        {
            if (buffer.Length == 0)
            {
                return;
            }

            int findLen = CStrLen(find);
            if (findLen == 0)
            {
                return;
            }

            int srcLen = CStrLen(buffer);

            // Build into a temporary array of the same capacity (like the original stack temp).
            char[] tmp = new char[buffer.Length];
            int w = 0;

            int i = 0;
            while (i < srcLen)
            {
                bool isMatch = false;

                if (i + findLen <= srcLen)
                {
                    isMatch = true;
                    for (int j = 0; j < findLen; j++)
                    {
                        if (buffer[i + j] != find[j])
                        {
                            isMatch = false;
                            break;
                        }
                    }
                }

                if (isMatch)
                {
                    int repLen = CStrLen(replace);
                    for (int k = 0; k < repLen; k++)
                    {
                        if (w >= tmp.Length - 1)
                        {
                            break;
                        }

                        tmp[w++] = replace[k];
                    }

                    i += findLen;
                    continue;
                }

                if (w >= tmp.Length - 1)
                {
                    break;
                }

                tmp[w++] = buffer[i];
                i++;
            }

            // Null-terminate.
            tmp[w] = '\0';

            // Copy back.
            int cpy = Math.Min(buffer.Length, tmp.Length);
            for (int t = 0; t < cpy; t++)
            {
                buffer[t] = tmp[t];
                if (tmp[t] == '\0')
                {
                    break;
                }
            }

            // Ensure terminator even if we broke without copying '\0'.
            if (buffer.Length > 0 && buffer[^1] != '\0')
            {
                buffer[^1] = '\0';
            }
        }

        internal static int StringLength(ReadOnlySpan<char> cstr)
        {
            return CStrLen(cstr);
        }

        internal static void StringCopy(Span<char> dst, ReadOnlySpan<char> src)
        {
            int i = 0;
            while (i < dst.Length)
            {
                char c = (i < src.Length) ? src[i] : '\0';
                dst[i] = c;
                i++;

                if (c == '\0')
                {
                    break;
                }
            }

            if (dst.Length > 0 && dst[^1] != '\0')
            {
                dst[^1] = '\0';
            }
        }

        /// <summary>
        /// C strncpy behavior: copies up to maxCount characters; zero-fills remainder if src ends early.
        /// Always attempts to null-terminate if there is space.
        /// </summary>
        internal static void StringCopy(Span<char> dst, ReadOnlySpan<char> src, int maxCount)
        {
            if (maxCount <= 0 || dst.Length == 0)
            {
                return;
            }

            int limit = Math.Min(dst.Length, maxCount);

            int i = 0;
            for (; i < limit; i++)
            {
                char c = (i < src.Length) ? src[i] : '\0';
                dst[i] = c;

                if (c == '\0')
                {
                    i++;
                    break;
                }
            }

            // Zero-fill the rest up to limit.
            for (; i < limit; i++)
            {
                dst[i] = '\0';
            }

            // If we filled without writing '\0' and we still have capacity, enforce termination.
            if (dst.Length > 0 && dst[^1] != '\0')
            {
                dst[^1] = '\0';
            }
        }

        /// <summary>
        /// Returns 1 if equal, else 0. If caseSensitive is false, compares ASCII case-insensitively.
        /// </summary>
        internal static bool StringCompare(string a, string b, bool caseSensitive)
        {
            return string.Equals(a, b, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Strips leading and trailing spaces and tabs (in-place), repeating until stable (like the original loop).
        /// </summary>
        internal static string StringStripWhitespace(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            // Original entfernt nur ' ' und '\t' (nicht alle Unicode-Whitespaces).
            int start = 0;
            int end = text.Length - 1;

            while (start <= end && (text[start] == ' ' || text[start] == '\t'))
            {
                start++;
            }

            while (end >= start && (text[end] == ' ' || text[end] == '\t'))
            {
                end--;
            }

            if (start == 0 && end == text.Length - 1)
            {
                return text;
            }

            if (end < start)
            {
                return string.Empty;
            }

            return text.Substring(start, end - start + 1);
        }

        /// <summary>
        /// Appends src to dst (null-terminated). Returns dst for parity with C.
        /// </summary>
        internal static Span<char> StringAttach(Span<char> dst, ReadOnlySpan<char> src)
        {
            int dstLen = CStrLen(dst);
            int w = dstLen;

            int i = 0;
            while (w < dst.Length)
            {
                char c = (i < src.Length) ? src[i] : '\0';
                dst[w] = c;

                if (c == '\0')
                {
                    break;
                }

                w++;
                i++;
                if (w >= dst.Length - 1)
                {
                    dst[^1] = '\0';
                    break;
                }
            }

            if (dst.Length > 0 && dst[^1] != '\0')
            {
                dst[^1] = '\0';
            }

            return dst;
        }

        /// <summary>
        /// Inserts prefix at the start of buffer (shifts existing content right).
        /// If capacity is insufficient, the result is truncated.
        /// </summary>
        internal static void StringInsert(Span<char> buffer, ReadOnlySpan<char> prefix)
        {
            int prefixLen = CStrLen(prefix);
            if (prefixLen <= 0)
            {
                return;
            }

            int srcLen = CStrLen(buffer);
            if (srcLen <= 0)
            {
                return;
            }

            // Total desired length without counting terminator.
            int total = prefixLen + srcLen;
            int maxWritable = Math.Max(0, buffer.Length - 1);
            int finalLen = Math.Min(total, maxWritable);

            // Shift right in-place from end, like memmove.
            // We need to move up to min(srcLen, finalLen - prefixLen) characters of original.
            int movedSrcLen = Math.Min(srcLen, Math.Max(0, finalLen - prefixLen));

            for (int i = movedSrcLen - 1; i >= 0; i--)
            {
                buffer[prefixLen + i] = buffer[i];
            }

            // Copy prefix into beginning.
            int copyPrefix = Math.Min(prefixLen, finalLen);
            for (int i = 0; i < copyPrefix; i++)
            {
                buffer[i] = prefix[i];
            }

            // Null-terminate.
            if (buffer.Length > 0)
            {
                buffer[finalLen] = '\0';
            }
        }

        /// <summary>
        /// Writes formatted text into buffer. Supports a small subset of printf:
        /// %d %i %u %x %X %s %c and %%.
        /// Output is truncated to fit and null-terminated.
        /// </summary>
        internal static void StringPrint(Span<char> buffer, string format, params object[] args)
        {
            if (buffer.Length == 0)
            {
                return;
            }

            string rendered = PrintfMini(format, args);

            int max = buffer.Length - 1;
            int count = Math.Min(max, rendered.Length);

            for (int i = 0; i < count; i++)
            {
                buffer[i] = rendered[i];
            }

            buffer[count] = '\0';
        }

        internal static int StringToInteger(ReadOnlySpan<char> cstr)
        {
            int i = 0;

            // Skip spaces and tabs.
            while (i < cstr.Length)
            {
                char c = cstr[i];
                if (c == '\0')
                {
                    return 0;
                }

                if (c != ' ' && c != '\t')
                {
                    break;
                }

                i++;
            }

            bool negative = false;

            if (i < cstr.Length)
            {
                char sign = cstr[i];
                if (sign == '+')
                {
                    i++;
                }
                else if (sign == '-')
                {
                    negative = true;
                    i++;
                }
            }

            int value = 0;
            while (i < cstr.Length)
            {
                char c = cstr[i];
                if (c == '\0')
                {
                    break;
                }

                if (c < '0' || c > '9')
                {
                    break;
                }

                value = (value * 10) + (c - '0');
                i++;
            }

            return negative ? -value : value;
        }

        internal static string IntegerToString(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------

        private static int CStrLen(ReadOnlySpan<char> s)
        {
            int i = 0;
            while (i < s.Length && s[i] != '\0')
            {
                i++;
            }

            return i;
        }

        private static void ShiftLeft(Span<char> buffer, int count)
        {
            if (count <= 0)
            {
                return;
            }

            int len = CStrLen(buffer);
            int remaining = Math.Max(0, len - count);

            for (int i = 0; i < remaining; i++)
            {
                buffer[i] = buffer[i + count];
            }

            // Null-terminate and clear the rest (optional, but keeps it tidy).
            int t = remaining;
            if (t < buffer.Length)
            {
                buffer[t] = '\0';
                t++;
            }

            for (; t < buffer.Length; t++)
            {
                buffer[t] = '\0';
            }
        }

        private static void TrimRightChar(Span<char> buffer, char ch)
        {
            int len = CStrLen(buffer);
            while (len > 0 && buffer[len - 1] == ch)
            {
                buffer[len - 1] = '\0';
                len--;
            }
        }

        private static string PrintfMini(string format, object[] args)
        {
            StringBuilder sb = new(format.Length + 32);
            int argIndex = 0;

            for (int i = 0; i < format.Length; i++)
            {
                char c = format[i];
                if (c != '%')
                {
                    sb.Append(c);
                    continue;
                }

                if (i + 1 >= format.Length)
                {
                    sb.Append('%');
                    break;
                }

                char spec = format[i + 1];
                i++;

                if (spec == '%')
                {
                    sb.Append('%');
                    continue;
                }

                object arg = argIndex < args.Length ? args[argIndex] : string.Empty;
                argIndex++;

                switch (spec)
                {
                    case 'd':
                    case 'i':
                        sb.Append(ToInt64(arg).ToString(CultureInfo.InvariantCulture));
                        break;

                    case 'u':
                        sb.Append(ToUInt64(arg).ToString(CultureInfo.InvariantCulture));
                        break;

                    case 'x':
                        sb.Append(ToUInt64(arg).ToString("x", CultureInfo.InvariantCulture));
                        break;

                    case 'X':
                        sb.Append(ToUInt64(arg).ToString("X", CultureInfo.InvariantCulture));
                        break;

                    case 's':
                        sb.Append(arg?.ToString() ?? string.Empty);
                        break;

                    case 'c':
                        sb.Append(ToChar(arg));
                        break;

                    default:
                        // Unknown specifier: keep it readable.
                        sb.Append('%').Append(spec);
                        break;
                }
            }

            return sb.ToString();
        }

        private static long ToInt64(object value)
        {
            if (value is long v64) return v64;
            if (value is int v32) return v32;
            if (value is short v16) return v16;
            if (value is sbyte v8) return v8;
            if (value is ulong vu64) return unchecked((long)vu64);
            if (value is uint vu32) return vu32;
            if (value is ushort vu16) return vu16;
            if (value is byte vu8) return vu8;
            if (value is string s && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)) return parsed;

            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static ulong ToUInt64(object value)
        {
            if (value is ulong v64) return v64;
            if (value is uint v32) return v32;
            if (value is ushort v16) return v16;
            if (value is byte v8) return v8;
            if (value is long vs64) return unchecked((ulong)vs64);
            if (value is int vs32) return unchecked((ulong)vs32);
            if (value is short vs16) return unchecked((ulong)vs16);
            if (value is sbyte vs8) return unchecked((ulong)vs8);
            if (value is string s && ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong parsed)) return parsed;

            return Convert.ToUInt64(value, CultureInfo.InvariantCulture);
        }

        private static char ToChar(object value)
        {
            if (value is char ch) return ch;
            if (value is byte b) return (char)b;
            if (value is sbyte sb) return (char)unchecked((byte)sb);
            if (value is int i) return (char)i;
            if (value is string s && s.Length > 0) return s[0];
            return '\0';
        }
    }
}