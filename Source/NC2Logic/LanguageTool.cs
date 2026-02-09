using OpenVikings.NXBasics;

namespace OpenVikings.NC2Logic
{
    internal enum TLanguageTypes : uint
    {
        // Values are intentionally not named here (unknown in the dump).
        // The numeric range is what matters for the original logic (0..0x13).
    }

    internal sealed class LanguageTool
    {
        private const uint MaxLanguageTypes = 0x14;

        // RE: globals in the original
        private static uint s_languageTypeCurrent;
        private static string[] s_languageDataArray = new string[MaxLanguageTypes];

        // Optional: allow the array to be injected from elsewhere (e.g., engine init).
        internal static void SetLanguageDataArray(string[] languageDataArray)
        {
            if (languageDataArray == null || languageDataArray.Length < MaxLanguageTypes)
            {
                // Keep existing array if invalid input is provided.
                return;
            }

            s_languageDataArray = languageDataArray;
        }

        // NC2Logic::LanguageTool_SetCurrentLanguage(NC2Logic::TLanguageTypes)
        internal static void SetCurrentLanguage(uint languageType)
        {
            if (languageType < MaxLanguageTypes)
            {
                s_languageTypeCurrent = languageType;
            }
        }

        internal static void SetCurrentLanguage(TLanguageTypes languageType)
        {
            SetCurrentLanguage((uint)languageType);
        }

        // NC2Logic::LanguageTool_GetCurrentLanguage()
        internal static uint GetCurrentLanguage()
        {
            return s_languageTypeCurrent;
        }

        internal static TLanguageTypes GetCurrentLanguageTyped()
        {
            return (TLanguageTypes)s_languageTypeCurrent;
        }

        // NC2Logic::LanguageTool_GetIONamePtr()
        internal static string GetIOName()
        {
            return GetIOName(s_languageTypeCurrent);
        }

        // NC2Logic::LanguageTool_GetIONamePtr(NC2Logic::TLanguageTypes)
        internal static string GetIOName(uint languageType)
        {
            if (languageType >= MaxLanguageTypes)
            {
                return string.Empty;
            }

            string value = s_languageDataArray[languageType];
            return value ?? string.Empty;
        }

        internal static string GetIOName(TLanguageTypes languageType)
        {
            return GetIOName((uint)languageType);
        }

        // NC2Logic::LanguageTool_GetFilename(char const*, char*, bool)
        // Ported to C#: returns true/false and outputs the resolved filename.
        internal static bool GetFilename(string template, out string filename, bool tryIniToCif)
        {
            // The original writes into param_2 (buffer). Here it is an out string.
            // template is param_1, typically something like "path\\%s\\file.ini" or "path\\{0}\\file.ini".
            filename = ApplyLanguageTemplate(template, GetIOName());

            if (FileExists(filename))
            {
                return true;
            }

            if (tryIniToCif && TrySwapIniToCifAndExists(template, GetIOName(), out filename))
            {
                return true;
            }

            filename = ApplyLanguageTemplate(template, "eng");
            if (FileExists(filename))
            {
                return true;
            }

            if (tryIniToCif && TrySwapIniToCifAndExists(template, "eng", out filename))
            {
                return true;
            }

            filename = ApplyLanguageTemplate(template, "ger");
            if (FileExists(filename))
            {
                return true;
            }

            if (tryIniToCif && TrySwapIniToCifAndExists(template, "ger", out filename))
            {
                return true;
            }

            // Not found; keep last attempted filename (matches the original behavior of leaving the buffer filled).
            return false;
        }

        // NC2Logic::LanguageTool_GetDirectoryName(char const*, char*, char const*)
        // Ported to C#: returns true/false and outputs the resolved directory name.
        internal static bool GetDirectoryName(string baseTemplate, out string directoryName, string subPath)
        {
            // Original does: StringPrint(local, baseTemplate, lang) + "\\" + subPath, then FileExists(local, true)
            string lang = GetIOName();
            string baseDir = ApplyLanguageTemplate(baseTemplate, lang);
            string probe = AttachPath(baseDir, subPath);

            if (FileExists(probe))
            {
                directoryName = baseDir;
                return true;
            }

            baseDir = ApplyLanguageTemplate(baseTemplate, "eng");
            probe = AttachPath(baseDir, subPath);
            if (FileExists(probe))
            {
                directoryName = baseDir;
                return true;
            }

            baseDir = ApplyLanguageTemplate(baseTemplate, "ger");
            probe = AttachPath(baseDir, subPath);
            if (FileExists(probe))
            {
                directoryName = baseDir;
                return true;
            }

            // Original fallback: write "eng" into output buffer and return 0.
            directoryName = ApplyLanguageTemplate(baseTemplate, "eng");
            return false;
        }

        private static bool TrySwapIniToCifAndExists(string template, string language, out string filename)
        {
            filename = ApplyLanguageTemplate(template, language);

            if (!EndsWithIni(filename))
            {
                return false;
            }

            filename = ReplaceExtensionToCif(filename);
            return FileExists(filename);
        }

        private static string ApplyLanguageTemplate(string template, string language)
        {
            if (template == null)
            {
                return string.Empty;
            }

            if (language == null)
            {
                language = string.Empty;
            }

            // Supports two common patterns:
            // - C-style: "...%s..."
            // - C#-style: "...{0}..."
            if (template.Contains("{0}"))
            {
                return string.Format(template, language);
            }

            if (template.IndexOf("%s", StringComparison.Ordinal) >= 0)
            {
                return template.Replace("%s", language);
            }

            // If no placeholder exists, mimic "best effort" behavior.
            return template + language;
        }

        private static string AttachPath(string basePath, string suffix)
        {
            if (string.IsNullOrEmpty(basePath))
            {
                return suffix ?? string.Empty;
            }

            if (string.IsNullOrEmpty(suffix))
            {
                return basePath;
            }

            if (basePath.EndsWith("\\", StringComparison.Ordinal) || basePath.EndsWith("/", StringComparison.Ordinal))
            {
                return basePath + suffix;
            }

            return basePath + "\\" + suffix;
        }

        private static bool EndsWithIni(string path)
        {
            return path != null && path.EndsWith(".ini", StringComparison.OrdinalIgnoreCase);
        }

        private static string ReplaceExtensionToCif(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            if (path.Length < 4)
            {
                return path;
            }

            return string.Concat(path.AsSpan(0, path.Length - 4), ".cif");
        }

        // Mirrors: NXBasics::CFile::FileSystem_Tool_FileExists(path, true)
        // Assumes an existing signature like: static byte/char/int FileSystem_Tool_FileExists(string, bool)
        private static bool FileExists(string path) => CFile.FileSystem_Tool_FileExists(path, true);
    }
}