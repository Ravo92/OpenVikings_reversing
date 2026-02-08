using OpenVikings.Interfaces;

namespace OpenVikings.Engine
{
    internal sealed class MainMenuHandler : IMainMenuHandler
    {
        private const string MainMenuMapPath = "data\\maps\\demo_mainmenu_10";
        private const string StartCampaign3PropertyKey = "start_campaign_3_screen";

        private const int Campaign3ScreenId = 5;
        private const int Campaign3ScreenParam = 0;

        private const int MainMenuMusicTrackId = 2;

        private readonly EngineContext _engineContext;
        private readonly IApplicationLifecycle _application;
        private readonly IGuiManager _guiManager;
        private readonly IProgressBar _progressBar;
        private readonly IGameEngine _gameEngine;
        private readonly IGameIo _gameIo;
        private readonly IAudioManager _audioManager;
        private readonly IPropertyManager _propertyManager;
        private readonly ITimeSource _timeSource;

        private uint _startupTimestampMs;

        internal MainMenuHandler(
            EngineContext engineContext,
            IApplicationLifecycle application,
            IGuiManager guiManager,
            IProgressBar progressBar,
            IGameEngine gameEngine,
            IGameIo gameIo,
            IAudioManager audioManager,
            IPropertyManager propertyManager,
            ITimeSource timeSource)
        {
            _engineContext = engineContext;
            _application = application;
            _guiManager = guiManager;
            _progressBar = progressBar;
            _gameEngine = gameEngine;
            _gameIo = gameIo;
            _audioManager = audioManager;
            _propertyManager = propertyManager;
            _timeSource = timeSource;

            _startupTimestampMs = 0;
        }

        public void Startup()
        {
            // Mirrors: ProgressBar_Init(this, this[0x28] == 0);
            // If you need a specific condition, inject it (e.g., cold start flag).
            _progressBar.Init(true);

            // Mirrors: GameEngine_StartUpEngine(this);
            _gameEngine.StartUpEngine();

            // Mirrors: Desktop_Open(GetDisplayWidth(), GetDisplayHeight(), GetDisplayDepth())
            int width = _engineContext.ResolutionWidth;
            int height = _engineContext.ResolutionHeight;
            uint depth = (uint)_engineContext.ColorDepthBits;

            _guiManager.DesktopOpen(width, height, depth);

            // Mirrors: MemorySet(this + 0x170, 0, 4);
            _startupTimestampMs = 0;

            // Mirrors: GameIO_Cleanmap_Load("data\\maps\\demo_mainmenu_10", true, true, false);
            _gameIo.CleanmapLoad(MainMenuMapPath, true, true, false);

            // Mirrors: EngineWorldDisplayGuiElement_CenterDisplay(...)
            _guiManager.CenterEngineWorldDisplay();

            // Mirrors: *(this+0x170) = TimeGetMilliseconds();
            _startupTimestampMs = _timeSource.GetMilliseconds();

            // Mirrors: ProgressBar_Exit(this);
            _progressBar.Exit();

            // Mirrors: if audio manager exists -> StartTrack(2);
            _audioManager?.StartTrack(MainMenuMusicTrackId);

            // Mirrors: if property exists -> Screen_ChangeTo(5,0) and remove property
            bool hasFlag = _propertyManager.Exists(StartCampaign3PropertyKey);
            if (hasFlag)
            {
                _guiManager.ChangeScreen(Campaign3ScreenId, Campaign3ScreenParam);
                _propertyManager.Remove(StartCampaign3PropertyKey);
            }
        }

        public bool Update()
        {
            // Your per-frame main menu logic goes here.
            return true;
        }

        public void Shutdown()
        {
            // Optional: undo/unload resources, stop music, close UI, etc.
        }
    }
}