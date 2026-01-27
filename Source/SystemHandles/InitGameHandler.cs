namespace OpenVikings.SystemHandles
{
    internal class InitGameHandler
    {
        internal static void InitGame()
        {
            SaveFolderHandler saveFolderHandler = new();
            saveFolderHandler.InitializeConfigurations();

            WindowHandler.CreateFullScreenWindowAsync("Weltwunder");
        }
    }
}