using System.Diagnostics;

namespace OpenVikings.SystemHandles
{
    internal class SaveFolderHandler
    {
        private readonly string savesFolderPath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, ConstantsHandler.SAVES_FOLDER_NAME);
        internal static readonly char[] separator = [' '];
        internal static readonly Dictionary<string, string> defaultConfigurations = new()
            {
            { "engine_lod", "0" },
            { "music_mode", "2" },
            { "fx_volume", "100" },
            { "fx_quality", "2" },
            { "fx_jingles_off", "1" },
            { "dm_volume", "70" },
            { "gui_scroll_speed", "2" },
            { "gui_main_mode", "1" },
            { "gui_expert_flag", "0" },
            { "gui_tooltipsoff_flag", "0" },
            { "gui_scroll_on_third_button", "1" },
            { "gui_scroll_on_border", "1" },
            { "gui_mouse_software", "0" }
        };

        internal bool SetSaveFolder()
        {
            try
            {
                if (!Directory.Exists(savesFolderPath))
                    Directory.CreateDirectory(savesFolderPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to create directory: {savesFolderPath}. Exception: {ex.Message}");
                return false;
            }

            return true;
        }

        internal bool SetOptionsGameSettingsINIFile(string fileName = "opt_game.ini")
        {
            string filePathWithININame = Path.Combine(savesFolderPath, fileName);

            if (!File.Exists(filePathWithININame))
            {
                try
                {
                    File.Create(filePathWithININame).Dispose();

                    Dictionary<string, string> defaultConfigurations = GetDefaultConfigurations();

                    using StreamWriter writer = new(filePathWithININame);
                    foreach (KeyValuePair<string, string> config in defaultConfigurations)
                    {
                        writer.WriteLine($"{config.Key} {config.Value}");
                    }
                }
                catch (Exception)
                {
                    Debug.WriteLine("Failed to create {0}", filePathWithININame);
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<string, string> GetDefaultConfigurations()
        {
            return defaultConfigurations;
        }

        internal static Dictionary<string, string> GetConfiguration(string dateiPfad, string key)
        {
            Dictionary<string, string> optionSetting = ParseINIFile(dateiPfad);
            if (optionSetting.TryGetValue(key, out string? value))
                return new Dictionary<string, string> { { key, value } };
            else
                Debug.WriteLine($"The key '{key}' was not found in the configuration file.");

            return defaultConfigurations;
        }

        internal static Dictionary<string, string> ParseINIFile(string filePath)
        {
            Dictionary<string, string> optionSetting = [];

            foreach (string line in File.ReadLines(filePath))
            {
                string cleanedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(cleanedLine))
                    continue;

                string[] entries = cleanedLine.Split(separator, StringSplitOptions.RemoveEmptyEntries);

                if (entries.Length == 2)
                {
                    string key = entries[0];
                    string value = entries[1];

                    optionSetting[key] = value;
                }
            }

            return optionSetting;
        }

        internal void InitializeConfigurations()
        {
            if (SetOptionsGameSettingsINIFile("opt_game.ini"))
            {
                Debug.WriteLine("Default configurations are set successfully.");
            }
            else
            {
                Debug.WriteLine("Failed to set default configurations.");
            }
        }
    }
}