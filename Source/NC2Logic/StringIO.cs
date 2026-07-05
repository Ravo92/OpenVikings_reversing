using OpenVikings.NXBasics;
using System.Globalization;

namespace OpenVikings.NC2Logic
{
    // Port of: NC2Logic::StringIO_*
    internal static class StringIO
    {
        // --------------------------------------------------------------------
        // StringIO_CreateOutOfIniFile
        // --------------------------------------------------------------------
        // RE behavior summary:
        // - Reads INI file.
        // - Optional [control] group: reads "mids" => multiplier (default 1).
        // - Required [text] group:
        //   - "stri" <string>                -> insert sequentially at 0,1,2,...
        //   - "strn" <int> <string>          -> insert at (int * multiplier), and advance sequential counter.
        // - Returns CStringArray or null.
        internal static CStringArray? CreateOutOfIniFile(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return null;
            }

            if (!CFile.FileSystem_Tool_FileExists(filename, true))
            {
                return null;
            }

            IniDocument? ini = IniDocument.TryLoad(filename);
            if (ini == null)
            {
                return null;
            }

            int multiplier = 1;

            IniSection? control = ini.TryGetSection("control");
            if (control != null)
            {
                for (int i = 0; i < control.Lines.Count; i += 1)
                {
                    IniLine line = control.Lines[i];

                    if (StringEqualsToken(line.Command, "mids"))
                    {
                        if (TryParseInt(line.Arg0, out int tmp))
                        {
                            multiplier = tmp;
                        }
                    }
                }
            }

            IniSection? text = ini.TryGetSection("text");
            if (text == null)
            {
                return null;
            }

            CStringArray result = new(false);

            uint sequentialIndex = 0;

            for (int i = 0; i < text.Lines.Count; i += 1)
            {
                IniLine line = text.Lines[i];

                if (StringEqualsToken(line.Command, "stri"))
                {
                    string value = line.Arg0 ?? string.Empty;
                    result.InsertString(sequentialIndex, value);
                    sequentialIndex += 1;
                    continue;
                }

                if (StringEqualsToken(line.Command, "strn"))
                {
                    if (!TryParseInt(line.Arg0, out int baseIndex))
                    {
                        continue;
                    }

                    string value = line.Arg1 ?? string.Empty;

                    uint mappedIndex = unchecked((uint)(baseIndex * multiplier));
                    result.InsertString(mappedIndex, value);

                    sequentialIndex += 1;
                    continue;
                }
            }

            return result;
        }

        // --------------------------------------------------------------------
        // StringIO_GetCurrentMissionStringArrayPtr
        // --------------------------------------------------------------------
        internal static CStringArray? GetCurrentMissionStringArrayPtr()
        {
            // RE:
            // info = CleanMapList_GetInfoPtrByGuid(..., DAT_1003a6460)
            // if info != null: return GetMissionStringArrayPtr(info)
            //
            // This wrapper assumes these types exist in your port.
            string? missionRoot = CCleanMapsManager.CleanMapList_GetInfoPtrByGuid(CCleanMapsManager.sTheObjectPtr, SCleanMapGuid.CurrentMissionGuid);
            if (missionRoot == null)
            {
                return null;
            }

            return GetMissionStringArrayPtr(missionRoot);
        }

        // --------------------------------------------------------------------
        // StringIO_GetMissionStringArrayPtr
        // --------------------------------------------------------------------
        internal static CStringArray? GetMissionStringArrayPtr(string missionRootDirectory)
        {
            if (string.IsNullOrEmpty(missionRootDirectory))
            {
                return null;
            }

            // RE: StringPrint(local_128, "%s\\text\\%%s\\strings.ini", param_1);
            // The template intentionally contains "%s" for LanguageTool.
            string template = missionRootDirectory + "\\text\\%s\\strings.ini";

            LanguageTool.GetFilename(template, out string resolved, true);

            CStringArray? arr = CreateOutOfIniFile(resolved);
            if (arr != null)
            {
                return arr;
            }

            // RE fallback: "currentusermap"
            template = "currentusermap\\text\\%s\\strings.ini";
            LanguageTool.GetFilename(template, out resolved, true);

            return CreateOutOfIniFile(resolved);
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------
        private static bool StringEqualsToken(string? a, string b)
        {
            if (a == null)
            {
                return false;
            }

            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseInt(string? s, out int value)
        {
            if (s == null)
            {
                value = 0;
                return false;
            }

            return int.TryParse(
                s,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value);
        }

        // --------------------------------------------------------------------
        // Minimal INI parser (enough for the RE format)
        // - Sections: [name]
        // - Lines: command + args (whitespace or '=' separated)
        // - Comments: ';' or '#'
        // --------------------------------------------------------------------
        private sealed class IniDocument
        {
            private readonly Dictionary<string, IniSection> _sections;

            private IniDocument(Dictionary<string, IniSection> sections)
            {
                _sections = sections;
            }

            internal IniSection? TryGetSection(string name)
            {
                if (_sections.TryGetValue(name, out IniSection section))
                {
                    return section;
                }

                return null;
            }

            internal static IniDocument? TryLoad(string filename)
            {
                string text;

                try
                {
                    text = File.ReadAllText(filename);
                }
                catch
                {
                    return null;
                }

                Dictionary<string, IniSection> sections = new(StringComparer.OrdinalIgnoreCase);

                IniSection? current = null;

                string[] lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i += 1)
                {
                    string raw = lines[i].Trim();
                    if (raw.Length == 0)
                    {
                        continue;
                    }

                    if (raw[0] == ';' || raw[0] == '#')
                    {
                        continue;
                    }

                    if (raw.Length >= 2 && raw[0] == '[' && raw[^1] == ']')
                    {
                        string name = raw[1..^1].Trim();

                        if (!sections.TryGetValue(name, out IniSection section))
                        {
                            section = new IniSection(name);
                            sections.Add(name, section);
                        }

                        current = section;
                        continue;
                    }

                    if (current == null)
                    {
                        continue;
                    }

                    if (IniLine.TryParse(raw, out IniLine parsed))
                    {
                        current.Lines.Add(parsed);
                    }
                }

                return new IniDocument(sections);
            }
        }

        private sealed class IniSection
        {
            internal string Name { get; }
            internal List<IniLine> Lines { get; }

            internal IniSection(string name)
            {
                Name = name;
                Lines = [];
            }
        }

        private readonly struct IniLine
        {
            internal string Command { get; }
            internal string? Arg0 { get; }
            internal string? Arg1 { get; }

            private IniLine(string command, string? arg0, string? arg1)
            {
                Command = command;
                Arg0 = arg0;
                Arg1 = arg1;
            }

            internal static bool TryParse(string raw, out IniLine line)
            {
                // Remove inline comments (best effort).
                int comment = raw.IndexOf(';');
                if (comment >= 0)
                {
                    raw = raw[..comment].Trim();
                }

                comment = raw.IndexOf('#');
                if (comment >= 0)
                {
                    raw = raw[..comment].Trim();
                }

                if (raw.Length == 0)
                {
                    line = default;
                    return false;
                }

                // Support "cmd=arg" and "cmd arg0 arg1..."
                string cmd;
                string rest;

                int eq = raw.IndexOf('=');
                if (eq >= 0)
                {
                    cmd = raw[..eq].Trim();
                    rest = raw[(eq + 1)..].Trim();
                }
                else
                {
                    int sp = raw.IndexOf(' ');
                    if (sp < 0)
                    {
                        cmd = raw.Trim();
                        rest = string.Empty;
                    }
                    else
                    {
                        cmd = raw[..sp].Trim();
                        rest = raw[(sp + 1)..].Trim();
                    }
                }

                if (cmd.Length == 0)
                {
                    line = default;
                    return false;
                }

                // Split rest into up to 2 args: Arg0 and Arg1 (Arg1 may contain spaces if quoted).
                string? a0 = null;
                string? a1 = null;

                if (rest.Length > 0)
                {
                    // Tokenize with simple quote support.
                    List<string> tokens = Tokenize(rest);

                    if (tokens.Count >= 1)
                    {
                        a0 = tokens[0];
                    }

                    if (tokens.Count >= 2)
                    {
                        // For "strn": arg0=int, arg1=string (possibly with spaces) -> keep only tokens[1] if quoted,
                        // otherwise join the remainder to preserve spaces.
                        if (tokens.Count == 2)
                        {
                            a1 = tokens[1];
                        }
                        else
                        {
                            a1 = string.Join(" ", tokens.GetRange(1, tokens.Count - 1));
                        }
                    }
                }

                line = new IniLine(cmd, a0, a1);
                return true;
            }

            private static List<string> Tokenize(string s)
            {
                List<string> tokens = [];

                int i = 0;
                while (i < s.Length)
                {
                    while (i < s.Length && char.IsWhiteSpace(s[i]))
                    {
                        i += 1;
                    }

                    if (i >= s.Length)
                    {
                        break;
                    }

                    if (s[i] == '"' || s[i] == '\'')
                    {
                        char quote = s[i];
                        i += 1;

                        int start = i;
                        while (i < s.Length && s[i] != quote)
                        {
                            i += 1;
                        }

                        string token = s[start..i];
                        tokens.Add(token);

                        if (i < s.Length && s[i] == quote)
                        {
                            i += 1;
                        }

                        continue;
                    }
                    else
                    {
                        int start = i;
                        while (i < s.Length && !char.IsWhiteSpace(s[i]))
                        {
                            i += 1;
                        }

                        string token = s[start..i];
                        tokens.Add(token);
                        continue;
                    }
                }

                return tokens;
            }
        }
    }

    // ------------------------------------------------------------------------
    // Minimal stubs for the mission lookup used by StringIO_GetCurrentMission...
    // Replace with your real types/fields.
    // ------------------------------------------------------------------------
    internal static class SCleanMapGuid
    {
        internal static string CurrentMissionGuid { get { return "CURRENT_MISSION_GUID"; } }
    }

    internal static class CCleanMapsManager
    {
        internal static object? sTheObjectPtr;

        internal static string? CleanMapList_GetInfoPtrByGuid(object? manager, string guid)
        {
            _ = manager;
            _ = guid;
            return null;
        }
    }
}