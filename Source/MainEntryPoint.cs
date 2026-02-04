using OpenVikings.Engine;
using OpenVikings.SystemHandles;

class Program
{
    private delegate void FunctionDelegate();

    // sub_401000()
    [STAThread]
    static async Task Main(string[] args)
    {
        ArgumentHandler argumentHandler = new();
        argumentHandler.HandleArguments(args);

        if (!InitGameHandler.TryEnsureSingleInstanceOrExit("weltwunder"))
        {
            return;
        }

        EngineContext engineContext = InitGameHandler.CreateEngineContext();
        engineContext.ResolutionWidth = 1280;
        engineContext.ResolutionHeight = 720;

        await InitGameHandler.InitGame(engineContext);

        InitGameHandler.EnsureFoldersAndIniFiles();

        Dictionary<string, string> config = InitGameHandler.LoadMergedConfigFromSaves();

        bool anyDataFileLoaded = InitGameHandler.DetectAndLoadDataFiles(config);
        if (!anyDataFileLoaded)
        {
            InitGameHandler.LoadFallbackDataFiles();
        }

        InitGameHandler.ApplyDefaultCursors();
        InitGameHandler.ApplyLanguageSetting();
        InitGameHandler.InitializeWindowAndSubsystems(engineContext);
    }
}