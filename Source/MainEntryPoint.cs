using OpenVikings.Engine;
using OpenVikings.Interfaces;
using OpenVikings.NC2Logic;
using OpenVikings.SystemHandles;

class Program
{
    [STAThread]
    internal static async Task Main(string[] args)
    {
        _ = args;

        // ------------------------------------------------------------
        // Basic startup steps (folders, mutex, config)
        // ------------------------------------------------------------
        InitGameHandler.TryEnsureSingleInstanceOrExit(ConstantsHandler.MUTEX_NAME);
        InitGameHandler.EnsureFoldersAndIniFiles();

        EngineContext engineContext = InitGameHandler.CreateEngineContext();
        InitGameHandler.ApplyVideoMode(engineContext);

        await InitGameHandler.InitGame(engineContext);

        // ------------------------------------------------------------
        // Window & input system
        // ------------------------------------------------------------
        CCallbackManager callbackManager = new(maxCallbackTypes: 64);
        LogicTickDispatcher logicTickDispatcher = new(callbackManager);

        InputQueue inputQueue = new();
        WindowHandler.AttachInputQueue(inputQueue);

        InitGameHandler.InitializeWindowAndSubsystems(engineContext);

        IMessagePump messagePump = new Win32MessagePump(inputQueue);
        IMouseManager mouseManager = new BasicMouseManager();
        IKeyManager keyManager = new BasicKeyManager();

        ApplicationMessageProcessor messageProcessor = new(messagePump, inputQueue, mouseManager, keyManager);

        // ------------------------------------------------------------
        // Shared system services
        // ------------------------------------------------------------
        ApplicationLifecycle lifecycle = new();
        IApplicationLifecycle appLifecycle = lifecycle;

        ITimeSource timeSource = new EnvironmentTimeSource();
        IOpenVikingsDebug debug = new OpenVikingsDebug();

        // ------------------------------------------------------------
        // Engine adapters (bridge to legacy / engine internals)
        // ------------------------------------------------------------
        IGuiManager guiManager = new GuiManagerAdapter();
        IProgressBar progressBar = new ProgressBarAdapter();
        IGameEngine gameEngine = new GameEngineAdapter();
        IGameIo gameIo = new GameIoAdapter();
        IAudioManager audioManager = new AudioManagerAdapter();
        IPropertyManager propertyManager = new PropertyManagerAdapter();

        // ------------------------------------------------------------
        // State handlers
        // ------------------------------------------------------------
        IGameStateHandler gameStateHandler = new GameStateHandler();

        IMainMenuHandler mainMenuHandler = new MainMenuHandler(
            engineContext,
            appLifecycle,
            guiManager,
            progressBar,
            gameEngine,
            gameIo,
            audioManager,
            propertyManager,
            timeSource);

        ITitleScreenHandler titleScreenHandler = new TitleScreenHandler();

        MasterControlProgram masterControlProgram = new(gameStateHandler, mainMenuHandler, titleScreenHandler);

        // ------------------------------------------------------------
        // Application core
        // ------------------------------------------------------------
        OpenVikingsGfxSettings gfx = new()
        {
            CallbackTimeMs = 16 // ~60 FPS
        };

        OpenVikingsOsState osState = new()
        {
            TimeCheckResetMs = 0
        };

        IOpenVikingsMain openVikingsMain = new OpenVikingsMain(messageProcessor, masterControlProgram, gfx, logicTickDispatcher);

        OpenVikingsApp app = new(timeSource, new YieldRelaxer(), openVikingsMain, debug, lifecycle, gfx, osState)
        {
            AppContinue = true
        };

        // ------------------------------------------------------------
        // Main loop
        // ------------------------------------------------------------
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