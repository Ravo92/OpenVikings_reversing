using System.Globalization;
using System.Text;

namespace OpenVikings.Dexter
{
    // Managed port of DexterString helpers (ASCII / C-string style).
    // Notes:
    // - Buffers are treated as null-terminated (0) byte arrays (C "char*" semantics).
    // - Case operations are ASCII-only.
    internal static class DexterString
    {
        // --------------------------------------------------------------------
        // Char/byte classification (ASCII)
        // --------------------------------------------------------------------

        internal static ulong IsAlpha(byte c)
        {
            if (c >= 0x7F)
            {
                return 0;
            }

            if ((c >= (byte)'A' && c <= (byte)'Z') || (c >= (byte)'a' && c <= (byte)'z') || c == (byte)'_')
            {
                return 1;
            }

            return 0;
        }

        internal static ulong IsDigit(byte c)
        {
            if (c > 0x7E)
            {
                return 0;
            }

            if (c < (byte)'0' || c > (byte)'9')
            {
                return 0;
            }

            return 1;
        }

        internal static bool IsAlphaNum(byte c)
        {
            if (c >= 0x7F)
            {
                return false;
            }

            if (IsAlpha(c) != 0)
            {
                return true;
            }

            return c >= (byte)'0' && c <= (byte)'9';
        }

        internal static byte ToLower(byte c)
        {
            if (c >= (byte)'A' && c <= (byte)'Z')
            {
                return (byte)(c + 0x20);
            }

            return c;
        }

        // --------------------------------------------------------------------
        // C-string style buffer operations (null-terminated byte buffers)
        // --------------------------------------------------------------------

        internal static void StringToLower(Span<byte> buffer)
        {
            int i = 0;
            while (i < buffer.Length)
            {
                byte c = buffer[i];
                if (c == 0)
                {
                    break;
                }

                buffer[i] = ToLower(c);
                i++;
            }
        }

        /// <summary>
        /// Returns the C-string length (up to the first 0 byte) within the span.
        /// </summary>
        internal static int StringLength(ReadOnlySpan<byte> cstr)
        {
            return CStrLen(cstr);
        }

        /// <summary>
        /// Converts a null-terminated byte buffer to a managed string (ASCII-ish).
        /// Bytes outside 0..127 are mapped to '?'.
        /// </summary>
        internal static string StringToString(ReadOnlySpan<byte> cstr)
        {
            int len = CStrLen(cstr);
            if (len == 0)
            {
                return string.Empty;
            }

            char[] chars = new char[len];
            for (int i = 0; i < len; i++)
            {
                byte b = cstr[i];
                chars[i] = b <= 0x7F ? (char)b : '?';
            }

            return new string(chars);
        }

        /// <summary>
        /// Searches for <paramref name="needleAscii"/> in <paramref name="haystack"/>.
        /// If <paramref name="caseSensitive"/> is true: exact match. If false: ASCII case-insensitive.
        /// Returns the start index, or -1 if not found.
        /// </summary>
        internal static int StringSearch(ReadOnlySpan<byte> haystack, string needleAscii, bool caseSensitive)
        {
            if (needleAscii == null)
            {
                return -1;
            }

            int needleLen = needleAscii.Length;
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
                    byte a = haystack[start + j];
                    char bch = needleAscii[j];

                    byte b = bch <= 0x7F ? (byte)bch : (byte)'?';

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
        /// strcpy-like: copies src into dst including terminator; truncates safely and always null-terminates if possible.
        /// </summary>
        internal static void StringCopy(Span<byte> dst, ReadOnlySpan<byte> src)
        {
            if (dst.Length == 0)
            {
                return;
            }

            int i = 0;

            while (i < dst.Length)
            {
                byte c = i < src.Length ? src[i] : (byte)0;
                dst[i] = c;
                i++;

                if (c == 0)
                {
                    return;
                }
            }

            dst[^1] = 0;
        }

        /// <summary>
        /// Copies a managed string (ASCII-ish) into dst including terminator; truncates safely.
        /// Characters outside 0..127 are mapped to '?'.
        /// </summary>
        internal static void StringCopy(Span<byte> dst, string src)
        {
            if (dst.Length == 0)
            {
                return;
            }

            if (src == null)
            {
                dst[0] = 0;
                return;
            }

            int maxWritable = dst.Length - 1;
            int count = src.Length < maxWritable ? src.Length : maxWritable;

            for (int i = 0; i < count; i++)
            {
                char ch = src[i];
                dst[i] = ch <= 0x7F ? (byte)ch : (byte)'?';
            }

            dst[count] = 0;
        }

        /// <summary>
        /// strncpy-like: copies up to maxCount bytes; if src ends early, zero-fills to maxCount.
        /// Always attempts to null-terminate if there is space.
        /// </summary>
        internal static void StringCopy(Span<byte> dst, string src, int maxCount)
        {
            if (maxCount <= 0 || dst.Length == 0)
            {
                return;
            }

            int limit = dst.Length < maxCount ? dst.Length : maxCount;

            int i = 0;
            int srcLen = src != null ? src.Length : 0;

            for (; i < limit; i++)
            {
                if (src == null || i >= srcLen)
                {
                    dst[i] = 0;
                    i++;
                    break;
                }

                char ch = src[i];
                dst[i] = ch <= 0x7F ? (byte)ch : (byte)'?';

                if (dst[i] == 0)
                {
                    i++;
                    break;
                }
            }

            for (; i < limit; i++)
            {
                dst[i] = 0;
            }

            if (dst.Length > 0 && dst[^1] != 0)
            {
                dst[^1] = 0;
            }
        }

        /// <summary>
        /// Appends src to dst (null-terminated). Truncates safely and ensures termination.
        /// </summary>
        internal static void StringAttach(Span<byte> dst, ReadOnlySpan<byte> src)
        {
            if (dst.Length == 0)
            {
                return;
            }

            int dstLen = CStrLen(dst);
            int w = dstLen;

            int i = 0;
            while (w < dst.Length)
            {
                byte c = i < src.Length ? src[i] : (byte)0;
                dst[w] = c;

                if (c == 0)
                {
                    return;
                }

                w++;
                i++;

                if (w >= dst.Length - 1)
                {
                    dst[^1] = 0;
                    return;
                }
            }

            dst[^1] = 0;
        }

        /// <summary>
        /// Appends a managed string (ASCII-ish) to dst (null-terminated). Truncates safely and ensures termination.
        /// </summary>
        internal static void StringAttach(Span<byte> dst, string src)
        {
            if (dst.Length == 0)
            {
                return;
            }

            if (src == null)
            {
                return;
            }

            int dstLen = CStrLen(dst);
            int maxWritable = dst.Length - 1;

            int w = dstLen;
            int i = 0;

            while (w < maxWritable && i < src.Length)
            {
                char ch = src[i];
                dst[w] = ch <= 0x7F ? (byte)ch : (byte)'?';
                w++;
                i++;
            }

            dst[w] = 0;
        }

        /// <summary>
        /// Replaces all occurrences of find with replace in-place. Output is truncated and null-terminated.
        /// </summary>
        internal static void StringReplace(Span<byte> buffer, string findAscii, string replaceAscii)
        {
            if (buffer.Length == 0)
            {
                return;
            }

            if (string.IsNullOrEmpty(findAscii))
            {
                return;
            }

            int srcLen = CStrLen(buffer);

            byte[] tmp = new byte[buffer.Length];
            int w = 0;

            int findLen = findAscii.Length;
            int i = 0;

            while (i < srcLen)
            {
                bool isMatch = false;

                if (i + findLen <= srcLen)
                {
                    isMatch = true;

                    for (int j = 0; j < findLen; j++)
                    {
                        byte a = buffer[i + j];
                        char bch = findAscii[j];
                        byte b = bch <= 0x7F ? (byte)bch : (byte)'?';

                        if (a != b)
                        {
                            isMatch = false;
                            break;
                        }
                    }
                }

                if (isMatch)
                {
                    int repLen = replaceAscii != null ? replaceAscii.Length : 0;

                    for (int k = 0; k < repLen; k++)
                    {
                        if (w >= tmp.Length - 1)
                        {
                            break;
                        }

                        char ch = replaceAscii[k];
                        tmp[w++] = ch <= 0x7F ? (byte)ch : (byte)'?';
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

            tmp[w] = 0;

            int cpy = buffer.Length < tmp.Length ? buffer.Length : tmp.Length;
            for (int t = 0; t < cpy; t++)
            {
                buffer[t] = tmp[t];
                if (tmp[t] == 0)
                {
                    break;
                }
            }

            if (buffer.Length > 0 && buffer[^1] != 0)
            {
                buffer[^1] = 0;
            }
        }

        /// <summary>
        /// Compares two managed strings (ordinal / ordinal-ignore-case).
        /// </summary>
        internal static bool StringCompare(string a, string b, bool caseSensitive)
        {
            return string.Equals(a, b, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Strips leading/trailing spaces and tabs (like the original).
        /// </summary>
        internal static string StringStripWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

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

            if (end < start)
            {
                return string.Empty;
            }

            if (start == 0 && end == text.Length - 1)
            {
                return text;
            }

            return text.Substring(start, end - start + 1);
        }

        /// <summary>
        /// Writes formatted text into buffer. Supports a small subset of printf:
        /// %d %i %u %x %X %s %c and %%.
        /// Output is truncated to fit and null-terminated.
        /// </summary>
        internal static void StringPrint(Span<byte> buffer, string format, params object[] args)
        {
            if (buffer.Length == 0)
            {
                return;
            }

            string rendered = PrintfMini(format, args);

            int max = buffer.Length - 1;
            int count = rendered.Length < max ? rendered.Length : max;

            for (int i = 0; i < count; i++)
            {
                char ch = rendered[i];
                buffer[i] = ch <= 0x7F ? (byte)ch : (byte)'?';
            }

            buffer[count] = 0;
        }

        internal static int StringToInteger(ReadOnlySpan<byte> cstr)
        {
            int i = 0;

            while (i < cstr.Length)
            {
                byte c = cstr[i];
                if (c == 0)
                {
                    return 0;
                }

                if (c != (byte)' ' && c != (byte)'\t')
                {
                    break;
                }

                i++;
            }

            bool negative = false;

            if (i < cstr.Length)
            {
                byte sign = cstr[i];
                if (sign == (byte)'+')
                {
                    i++;
                }
                else if (sign == (byte)'-')
                {
                    negative = true;
                    i++;
                }
            }

            int value = 0;

            while (i < cstr.Length)
            {
                byte c = cstr[i];
                if (c == 0)
                {
                    break;
                }

                if (c < (byte)'0' || c > (byte)'9')
                {
                    break;
                }

                value = (value * 10) + (c - (byte)'0');
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

        private static int CStrLen(ReadOnlySpan<byte> s)
        {
            int i = 0;
            while (i < s.Length && s[i] != 0)
            {
                i++;
            }

            return i;
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
                        sb.Append(arg != null ? arg.ToString() : string.Empty);
                        break;

                    case 'c':
                        sb.Append(ToChar(arg));
                        break;

                    default:
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

            if (value is string s && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
            {
                return parsed;
            }

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

            if (value is string s && ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong parsed))
            {
                return parsed;
            }

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