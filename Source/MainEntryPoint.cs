using OpenVikings.Engine;
using OpenVikings.Interfaces;
using OpenVikings.SystemHandles;

class Program
{
    private delegate void FunctionDelegate();

    [STAThread]
    internal static async Task Main(string[] args)
    {
        _ = args;

        // Basic startup steps (folders, mutex, config).
        InitGameHandler.TryEnsureSingleInstanceOrExit(ConstantsHandler.MUTEX_NAME);
        InitGameHandler.EnsureFoldersAndIniFiles();

        EngineContext engineContext = InitGameHandler.CreateEngineContext();
        InitGameHandler.ApplyVideoMode(engineContext);

        await InitGameHandler.InitGame(engineContext);

        // From here the Win32 window exists and its WndProc can receive input.
        InputQueue inputQueue = new();
        WindowHandler.AttachInputQueue(inputQueue);

        InitGameHandler.InitializeWindowAndSubsystems(engineContext);

        IMessagePump messagePump = new Win32MessagePump(inputQueue);
        IMouseManager mouseManager = new BasicMouseManager();
        IKeyManager keyManager = new BasicKeyManager();

        ApplicationMessageProcessor messageProcessor = new(messagePump, inputQueue, mouseManager, keyManager);

        MasterControlProgram masterControlProgram = new(new GameStateHandler(), new MainMenuHandler(), new TitleScreenHandler());

        OpenVikingsGfxSettings gfx = new()
        {
            // Default target: ~60 FPS. You can override this from config later.
            CallbackTimeMs = 16
        };

        OpenVikingsOsState osState = new()
        {
            TimeCheckResetMs = 0
        };

        ApplicationLifecycle lifecycle = new();
        IOpenVikingsDebug debug = new OpenVikingsDebug();

        IOpenVikingsMain OpenVikingsMain = new OpenVikingsMain(messageProcessor, masterControlProgram, gfx);

        OpenVikingsApp app = new(new EnvironmentTimeSource(), new YieldRelaxer(), OpenVikingsMain, debug, lifecycle, gfx, osState)
        {
            AppContinue = true
        };

        // Main loop. OpenVikingsApp implements the legacy frame pacing and special exit codes.
        while (!lifecycle.HasEnded)
        {
            bool keepRunning = app.MainThreadTick();
            if (!keepRunning)
            {
                break;
            }
        }
    }
}