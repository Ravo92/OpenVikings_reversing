namespace OpenVikings.Dexter
{
    internal static partial class DexterString
    {
        internal static bool IsAlpha(char value)
        {
            if (value >= (char)0x7F)
            {
                return false;
            }

            return IsAsciiUpper(value) || IsAsciiLower(value);
        }

        internal static bool IsDigit(char value)
        {
            byte b = (byte)value;
            if (b > 0x7E)
            {
                return false;
            }

            int diff = b - (byte)'0';
            return (uint)diff < 10u;
        }

        internal static bool IsAlphaNum(char value)
        {
            if (value >= (char)0x7F)
            {
                return false;
            }

            return IsAlpha(value) || IsDigit(value);
        }

        internal static char ToUpper(char value)
        {
            if (IsAsciiLower(value))
            {
                return (char)(value - 0x20);
            }

            return value;
        }

        internal static char ToLower(char value)
        {
            if (IsAsciiUpper(value))
            {
                return (char)(value + 0x20);
            }

            return value;
        }

        // ---------- Span<char> APIs ----------

        internal static int StringLength(ReadOnlySpan<char> buffer)
        {
            int i = 0;
            while (i < buffer.Length && buffer[i] != '\0')
            {
                i++;
            }

            return i;
        }

        internal static int StringLength(Span<char> buffer)
        {
            return StringLength((ReadOnlySpan<char>)buffer);
        }

        internal static void StringToUpper(Span<char> buffer)
        {
            int length = StringLength(buffer);
            for (int i = 0; i < length; i++)
            {
                buffer[i] = ToUpper(buffer[i]);
            }
        }

        internal static void StringToLower(Span<char> buffer)
        {
            int length = StringLength(buffer);
            for (int i = 0; i < length; i++)
            {
                buffer[i] = ToLower(buffer[i]);
            }
        }

        internal static void StringReplace(Span<char> destination, ReadOnlySpan<char> find, ReadOnlySpan<char> replace)
        {
            int destLen = StringLength(destination);
            int findLen = StringLength(find);
            int replLen = StringLength(replace);

            if (findLen == 0)
            {
                return;
            }

            char[] temp = new char[destination.Length];

            int write = 0;
            int read = 0;

            while (read < destLen)
            {
                bool match = false;

                if (read + findLen <= destLen)
                {
                    match = true;
                    for (int j = 0; j < findLen; j++)
                    {
                        if (destination[read + j] != find[j])
                        {
                            match = false;
                            break;
                        }
                    }
                }

                if (match)
                {
                    EnsureCanWrite(write, replLen, temp.Length);
                    for (int j = 0; j < replLen; j++)
                    {
                        temp[write + j] = replace[j];
                    }

                    write += replLen;
                    read += findLen;
                }
                else
                {
                    EnsureCanWrite(write, 1, temp.Length);
                    temp[write] = destination[read];
                    write++;
                    read++;
                }
            }

            EnsureCanWrite(write, 1, temp.Length);
            temp[write] = '\0';

            int copyCount = write + 1;
            for (int i = 0; i < copyCount; i++)
            {
                destination[i] = temp[i];
            }
        }

        // ---------- byte[] C-string APIs (ASCII, \0 terminated) ----------

        internal static int StringLength(byte[] buffer)
        {
            if (buffer == null)
            {
                return 0;
            }

            int i = 0;
            while (i < buffer.Length && buffer[i] != 0)
            {
                i++;
            }

            return i;
        }

        internal static void StringCopy(byte[] destination, byte[] source)
        {
            if (destination == null)
            {
                return;
            }

            if (source == null)
            {
                destination[0] = 0;
                return;
            }

            int i = 0;
            while (i < destination.Length)
            {
                byte b = i < source.Length ? source[i] : (byte)0;
                destination[i] = b;

                i++;
                if (b == 0)
                {
                    return;
                }
            }

            destination[^1] = 0;
        }

        internal static void StringCopy(byte[] destination, string source, int maxCount)
        {
            if (destination == null)
            {
                return;
            }

            if (maxCount <= 0)
            {
                return;
            }

            int count = maxCount;
            if (count > destination.Length)
            {
                count = destination.Length;
            }

            if (string.IsNullOrEmpty(source))
            {
                destination[0] = 0;
                for (int i = 1; i < count; i++)
                {
                    destination[i] = 0;
                }
                return;
            }

            for (int i = 0; i < count; i++)
            {
                if (i >= source.Length)
                {
                    destination[i] = 0;
                    for (int j = i + 1; j < count; j++)
                    {
                        destination[j] = 0;
                    }
                    return;
                }

                char c = source[i];
                destination[i] = c <= 0x7F ? (byte)c : (byte)'?';

                if (destination[i] == 0)
                {
                    for (int j = i + 1; j < count; j++)
                    {
                        destination[j] = 0;
                    }
                    return;
                }
            }

            if (count > 0)
            {
                destination[count - 1] = 0;
            }
        }

        internal static void StringCopy(byte[] destination, string source)
        {
            if (destination == null)
            {
                return;
            }

            StringCopy(destination, source, destination.Length);
        }

        internal static void StringAttach(byte[] destination, string source)
        {
            if (destination == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(source))
            {
                return;
            }

            int destLen = StringLength(destination);

            int available = destination.Length - destLen;
            if (available <= 1)
            {
                return;
            }

            int copy = source.Length;
            if (copy > available - 1)
            {
                copy = available - 1;
            }

            for (int i = 0; i < copy; i++)
            {
                char c = source[i];
                destination[destLen + i] = c <= 0x7F ? (byte)c : (byte)'?';
            }

            destination[destLen + copy] = 0;
        }

        internal static void StringAttach(byte[] destination, byte[] source)
        {
            if (destination == null)
            {
                return;
            }

            if (source == null)
            {
                return;
            }

            int destLen = StringLength(destination);
            int srcLen = StringLength(source);

            int available = destination.Length - destLen;
            if (available <= 1)
            {
                return;
            }

            int copy = srcLen;
            if (copy > available - 1)
            {
                copy = available - 1;
            }

            for (int i = 0; i < copy; i++)
            {
                destination[destLen + i] = source[i];
            }

            destination[destLen + copy] = 0;
        }

        internal static int StringSearch(byte[] haystack, string needle, bool caseSensitive)
        {
            if (haystack == null || needle == null)
            {
                return -1;
            }

            int hayLen = StringLength(haystack);
            int needleLen = needle.Length;

            if (needleLen == 0)
            {
                return 0;
            }

            for (int i = 0; i + needleLen <= hayLen; i++)
            {
                bool match = true;

                for (int j = 0; j < needleLen; j++)
                {
                    byte a = haystack[i + j];
                    char nb = needle[j];
                    byte b = nb <= 0x7F ? (byte)nb : (byte)'?';

                    if (!caseSensitive)
                    {
                        a = ToLowerAscii(a);
                        b = ToLowerAscii(b);
                    }

                    if (a != b)
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }

        internal static bool StringCompare(string a, string b, byte caseSensitiveFlag)
        {
            bool caseInsensitive = caseSensitiveFlag == 0;

            int index = 0;
            while (true)
            {
                char ca = index < a.Length ? a[index] : '\0';
                char cb = index < b.Length ? b[index] : '\0';

                if (caseInsensitive)
                {
                    char na = NormalizeAsciiLetterToLower(ca);
                    char nb = NormalizeAsciiLetterToLower(cb);

                    if (na != nb)
                    {
                        return false;
                    }
                }
                else
                {
                    if (ca != cb)
                    {
                        return false;
                    }
                }

                if (ca == '\0')
                {
                    return true;
                }

                index++;
            }
        }

        // ---------- helpers ----------

        private static char NormalizeAsciiLetterToLower(char c)
        {
            // Original logic: if c is 'A'..'Z' then add ' ' (0x20) -> make it lowercase.
            // Otherwise keep as-is.
            if (c >= 'A' && c <= 'Z')
            {
                return (char)(c + 0x20);
            }

            return c;
        }

        private static bool IsAsciiUpper(char c)
        {
            return c >= 'A' && c <= 'Z';
        }

        private static bool IsAsciiLower(char c)
        {
            return c >= 'a' && c <= 'z';
        }

        private static void EnsureCanWrite(int writeIndex, int writeCount, int capacity)
        {
            if (writeIndex < 0 || writeCount < 0 || writeIndex + writeCount > capacity)
            {
                throw new ArgumentOutOfRangeException("Result would exceed destination capacity.");
            }
        }

        private static byte ToLowerAscii(byte value)
        {
            if (value >= (byte)'A' && value <= (byte)'Z')
            {
                return (byte)(value + 0x20);
            }

            return value;
        }
    }
}