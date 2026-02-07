namespace OpenVikings.SystemHandles
{
    internal static class PathHandler
    {
#if DEBUG
        internal static readonly string GameRoot = Directory.GetCurrentDirectory();
#else
        internal static readonly string GameRoot = Path.GetDirectoryName(Environment.ProcessPath!)!; 
#endif

        internal static string GetFolderPath(string folderName)
        {
            // TODO: This is a temporary solution. We should implement a more robust solution for handling save folders and other game data folders in the future.
            if (!Path.Exists(Path.Combine(GameRoot, folderName)))
            {
                SaveFolderHandler saveFolderHandler = new();
                saveFolderHandler.SetSaveFolder();
            }

            return Path.Combine(GameRoot, folderName);
        }

        internal static string GetDataXPath()
        {
            return Path.Combine(GameRoot, "DataX");
        }
    }
}