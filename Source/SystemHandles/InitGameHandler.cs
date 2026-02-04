using OpenVikings.Engine;
using System.Runtime.InteropServices;
using static OpenVikings.SystemHandles.MutexHandler;

namespace OpenVikings.SystemHandles
{
    internal class InitGameHandler
    {
        private static nint data_5697ec;
        private static nint data_50f6a4;
        private static nint data_50f698;

        private static DLLCalls.Gedx8musicdrv.IGedx8MusicDriver dmDriver = DLLCalls.Gedx8musicdrv.Gedx8musicdrvFacade.GetInterface2_Managed();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern nint LoadCursorFromFileW(string lpFileName);

        [DllImport("user32.dll")]
        private static extern nint SetCursor(nint hCursor);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetWindowTextW(nint hWnd, string lpString);

        internal static EngineContext CreateEngineContext()
        {
            EngineContext engineContext = new()
            {
                CustomCursorEnabled = false,
                CursorHandle = 0,
                State = 0
            };
            return engineContext;
        }

        internal static async Task InitGame(EngineContext engineContext)
        {
            SaveFolderHandler saveFolderHandler = new();
            saveFolderHandler.InitializeConfigurations();

            _ = await WindowHandler.CreateWindowedWindowAsync("Weltwunder", engineContext.ResolutionWidth, engineContext.ResolutionHeight, true);
        }

        internal static bool TryEnsureSingleInstanceOrExit(string mutexName)
        {
            if (TryCreateMutex(mutexName) == MutexCreationResult.Success)
            {
                return true;
            }

            Environment.Exit(0);
            return false;
        }

        internal static void EnsureFoldersAndIniFiles()
        {
            PathHandler.GetFolderPath("logs");
            PathHandler.GetFolderPath("Saves");
            SaveFolderHandler saveFolderHandler = new();
            saveFolderHandler.SetOptionsGameSettingsINIFile("game.ini");
        }

        internal static bool DetectAndLoadDataFiles(Dictionary<string, string> config)
        {
            bool loadedAny = false;

            for (int index = 9; index >= 0; index--)
            {
                if (!TryResolveDataFilePathFromSetting(index, config, out string dataFilePath))
                {
                    continue;
                }

                // TODO: Add actual "datafile load" logic here.
                // LoadAndRegisterDataFile(dataFilePath);

                loadedAny = true;
            }

            return loadedAny;
        }

        internal static void LoadFallbackDataFiles()
        {
            string baseDir = Path.GetDirectoryName(Environment.ProcessPath)!;

            for (int i = 10; i >= 1; i--)
            {
                string fallbackPath = Path.Combine(baseDir, "datax", "libs", $"data{i:D4}.lib");
                if (!File.Exists(fallbackPath))
                {
                    continue;
                }

                // TODO: Add actual "datafile load" logic here.
                // LoadAndRegisterDataFile(fallbackPath);
            }
        }

        internal static bool TryResolveDataFilePathFromSetting(int index, Dictionary<string, string> config, out string dataFilePath)
        {
            dataFilePath = string.Empty;

            string key = $"use_data_file_{index}";

            if (!config.TryGetValue(key, out string? raw) || raw == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string trimmed = raw.Trim();

            // Case A: INI contains “1”/‘true’ => Activate standard library “dataXXXX.lib”.
            if (trimmed == "1" || trimmed.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                string baseDir = Path.GetDirectoryName(Environment.ProcessPath)!;
                string candidate = Path.Combine(baseDir, "datax", "libs", $"data{index:D4}.lib");

                if (!File.Exists(candidate))
                {
                    return false;
                }

                dataFilePath = candidate;
                return true;
            }

            // Case B: “0”/“false” => disabled
            if (trimmed == "0" || trimmed.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Case C: INI contains a specific path/file name => check
            if (File.Exists(trimmed))
            {
                dataFilePath = trimmed;
                return true;
            }

            string basePath2 = Path.GetDirectoryName(Environment.ProcessPath)!;
            string combined = Path.Combine(basePath2, trimmed);

            if (File.Exists(combined))
            {
                dataFilePath = combined;
                return true;
            }

            return false;
        }

        internal static Dictionary<string, string> LoadMergedConfigFromSaves()
        {
            string baseDir = Path.GetDirectoryName(Environment.ProcessPath)!;
            string savesDir = Path.Combine(baseDir, ConstantsHandler.SAVES_FOLDER_NAME);

            string optGlobPath = Path.Combine(savesDir, "opt_glob.ini");
            string optGamePath = Path.Combine(savesDir, ConstantsHandler.OPT_GAME_INI_NAME);

            Dictionary<string, string> merged = new(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(optGlobPath))
            {
                Dictionary<string, string> glob = SaveFolderHandler.ParseINIFile(optGlobPath);
                foreach (KeyValuePair<string, string> kv in glob)
                {
                    merged[kv.Key] = kv.Value;
                }
            }

            if (File.Exists(optGamePath))
            {
                Dictionary<string, string> game = SaveFolderHandler.ParseINIFile(optGamePath);
                foreach (KeyValuePair<string, string> kv in game)
                {
                    merged[kv.Key] = kv.Value;
                }
            }

            return merged;
        }


        // SetCursorIfExists("datax\\mouse\\mousepressed.cur");
        // SetCursorIfExists("datax\\mouse\\mouseright.cur");
        internal static void ApplyDefaultCursors()
        {
            string fullPath = Path.Combine(PathHandler.GameRoot, "datax\\mouse\\mousenormal.cur");
            if (!File.Exists(fullPath))
            {
                return;
            }

            nint cursor = LoadCursorFromFileW(fullPath);
            if (cursor == 0)
            {
                // optional: Debug LastError
                return;
            }

            WindowHandler.SetClientCursor(cursor);
        }

        internal static void ApplyLanguageSetting()
        {
            int language = sub_4e163b("set_language");
            sub_405ac8(language);
        }

        internal static void InitializeWindowAndSubsystems(EngineContext engineContext)
        {
            if (engineContext == null)
            {
                throw new ArgumentNullException(nameof(engineContext));
            }

            ApplyVideoMode(engineContext);
            ApplyWindowTitle(WindowHandler.WindowHandle);

            sub_4016ee(engineContext);

            if (engineContext.CustomCursorEnabled && engineContext.CursorHandle != 0)
            {
                sub_478b53(engineContext.CursorHandle);
            }

            sub_401750(engineContext);

            InitializeFxSystemIfEnabled();
            InitializeDmSystemAndVolume();

            engineContext.State = 2;

            IntroOutroHandler.ShowIntroBmpAndArmSkip();
        }

        internal static void ApplyVideoMode(EngineContext engineContext)
        {
            engineContext.ResolutionWidth = 800;
            engineContext.ResolutionHeight = 600;
            engineContext.ColorDepthBits = 32;
        }

        internal static void ApplyWindowTitle(nint hWnd)
        {
            if (hWnd == 0)
            {
                return;
            }

            SetWindowTextW(hWnd, "Weltwunder");
        }

        internal static void InitializeFxSystemIfEnabled()
        {
            sub_4e163b("fx_quality");
            sub_404aac(data_50f6a4, sub_4e163b("fx_volume"));
        }

        internal static void InitializeDmSystemAndVolume()
        {
            if (sub_4e163b("music_mode") == 2)
            {
                sub_4e167d(data_5697ec, "music_mode", 1, 1, 0);
            }

            string dmPath = BuildDmPath();

            dmDriver = DLLCalls.Gedx8musicdrv.Gedx8musicdrvFacade.GetInterface2_Managed();
            dmDriver.SetBasePath(dmPath);
            dmDriver.Initialize();

            InitializeDmSynthesizer();

            int dmVolume = sub_4e163b("dm_volume");
            sub_403932(data_50f698, dmVolume);

            if (sub_4e163b("music_mode") == 3)
            {
                sub_4e167d(data_5697ec, "music_mode", 1, 1, 0);
            }
        }

        internal static string BuildDmPath()
        {
            string baseDir = Path.GetDirectoryName(Environment.ProcessPath!)!;
            string dmDir = Path.Combine(baseDir, "datax", "dm2") + "\\";
            return dmDir;
        }

        internal static void InitializeDmSynthesizer()
        {
            int mode = 0;

            int sampleRate;
            int bytesOrBits;

            if (mode == 1)
            {
                sampleRate = 22050;
                bytesOrBits = 16;
            }
            else if (mode == 2)
            {
                sampleRate = 11025;
                bytesOrBits = 8;
            }
            else
            {
                sampleRate = 44100;
                bytesOrBits = 64;
            }

            int instance = dmDriver.CreateInstance();

            _ = dmDriver.InitSynthesizer(
                instance,
                new DLLCalls.Gedx8musicdrv.Gedx8InitParams(sampleRate, bytesOrBits));
        }

        private static void sub_4e167d(nint arg1, string arg2, int arg3, int arg4, nint arg5) { }
        private static int sub_4e163b(string arg) { return 0; }
        private static void sub_405ac8(int arg) { }
        private static void sub_4016ee(EngineContext engineContext) { }
        private static void sub_478b53(nint arg) { }
        private static void sub_401750(EngineContext engineContext) { }
        private static void sub_404aac(nint arg1, int arg2) { }
        private static void sub_403932(nint arg1, int arg2) { }
    }
}