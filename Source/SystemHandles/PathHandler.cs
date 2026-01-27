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
            return Path.Combine(GameRoot, folderName);
        }
    }
}
