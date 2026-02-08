using OpenVikings.Interfaces;
using OpenVikings.NC2GuiToolsBase;
using OpenVikings.NC2InGameGuiManager;
using OpenVikings.NC2Logic;
using OpenVikings.NXBaseGui;
using OpenVikings.NXBasics;

namespace OpenVikings.Engine
{
    internal sealed class NC2MainMenuGuiManager : IGuiManager, IDisposable
    {
        private static NC2MainMenuGuiManager _instance;

        private readonly IGameTimeController _gameTime;
        private readonly IMainMenuScreenUpdater _screenUpdater;
        private readonly CCallbackManager _callbackManager;

        private bool _desktopInitialized;
        private bool _desktopActive;

        private int _desktopWidth;
        private int _desktopHeight;
        private uint _desktopDepth;

        private int _currentScreenId;
        private bool _isDisposed;

        private bool _explorationFlag;

        private WorldDisplayElement _worldDisplayElement;

        private readonly List<WorldDisplayElement> _worldDisplayElements = [];
        private readonly List<CBaseElement> _frameCallElements = [];


        internal static NC2MainMenuGuiManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new InvalidOperationException("NC2MainMenuGuiManager.Instance was accessed before initialization.");
                }

                return _instance;
            }
        }

        internal NC2MainMenuGuiManager(IGameTimeController gameTime, IMainMenuScreenUpdater screenUpdater, CCallbackManager callbackManager)
        {
            _gameTime = gameTime ?? throw new ArgumentNullException(nameof(gameTime));
            _screenUpdater = screenUpdater ?? throw new ArgumentNullException(nameof(screenUpdater));
            _callbackManager = callbackManager ?? throw new ArgumentNullException(nameof(callbackManager));

            if (_instance != null)
            {
                throw new InvalidOperationException("NC2MainMenuGuiManager was created more than once.");
            }

            _instance = this;

            _desktopInitialized = false;
            _desktopActive = false;

            _desktopWidth = 0;
            _desktopHeight = 0;
            _desktopDepth = 0;

            _currentScreenId = 0;
            _isDisposed = false;

            GraphicDataLoad();
        }

        public void DesktopOpen(int width, int height, uint depth)
        {
            // Mirrors: if (mStaticVars == '\0')
            if (_desktopInitialized)
            {
                return;
            }

            _desktopWidth = width;
            _desktopHeight = height;
            _desktopDepth = depth;

            // Mirrors: new CDesktop(width, height, (uchar)depth)
            _ = new CDesktop(width, height, (byte)depth);

            // Mirrors: CGuiBaseDataManager::DynamicData_Load()
            CGuiBaseDataManager.DynamicData_Load();

            // Mirrors: PrimaryMessageHandler_Set(...)
            // You can keep this as CBaseElement for now.
            CBaseElement primaryHandler = new CGuiManagerPrimaryMessageHandlerElement();
            CDesktop.Current.SetPrimaryMessageHandler(primaryHandler);

            // Mirrors: desktop + 0x250 = new MousePointer()
            // Typisiert später, wenn du dein MousePointer-Interface hast.
            object mousePointer = new CGuiManagerMousePointer();
            CDesktop.Current.AttachMousePointer(mousePointer);

            // Mirrors: RegisterCallback(...) list from RE
            RegisterInGameCallbacks();

            // Mirrors: CE2Manager::Inform_DesktopXTructed(width,height,depth)
            EngineEventBridge.InformDesktopCreated(width, height, depth);

            // Mirrors: new CWorldDisplayElement(rect, true, true)
            SRectangle fullScreenRect = new(0, 0, width, height);
            WorldDisplayElement worldDisplay = new(fullScreenRect, interactive: true, flag: true);
            _worldDisplayElement = worldDisplay;

            // Mirrors: BaseToolDesktop_AddBackgroundElement(worldDisplay)
            CDesktop.Current.ElementAddBackground(worldDisplay, insertAtEnd: true);

            _desktopInitialized = true;
            _desktopActive = true;

            // NOTE: MainMenu variant calls Screen_ChangeTo(this,1,0) at end.
            ChangeScreen(1, 0);
        }

        private static bool LogicCallbackFunction(uint callbackType, uint a, uint b, uint c, uint d)
        {
            _ = callbackType;
            _ = a;
            _ = b;
            _ = c;
            _ = d;

            // Mirrors: ls_Logic_CallbackFunction
            // Implement when RE is complete.
            return true;
        }

        private void RegisterInGameCallbacks()
        {
            _callbackManager.RegisterCallback(1, LogicCallbackFunction, 0, 10);

            _callbackManager.RegisterCallback(0x14, LogicCallbackFunction, 0, 5);
            _callbackManager.RegisterCallback(0x15, LogicCallbackFunction, 0, 5);
            _callbackManager.RegisterCallback(0x16, LogicCallbackFunction, 0, 5);
            _callbackManager.RegisterCallback(0x1B, LogicCallbackFunction, 0, 5);

            _callbackManager.RegisterCallback(0x2B, LogicCallbackFunction, 0, 5);
            _callbackManager.RegisterCallback(0x2C, LogicCallbackFunction, 0, 5);
            _callbackManager.RegisterCallback(0x2D, LogicCallbackFunction, 0, 5);

            _callbackManager.RegisterCallback(0x2F, LogicCallbackFunction, 0, 5);
            _callbackManager.RegisterCallback(0x30, LogicCallbackFunction, 0, 5);
        }

        public void ChangeScreen(int screenId, int parameter)
        {
            _ = parameter;

            if (_currentScreenId == screenId)
            {
                return;
            }

            _gameTime.SetSpeed(0.0f);
            _currentScreenId = screenId;
            _gameTime.SetSpeed(12.0f);

            _screenUpdater.UpdateGui(this);
        }

        public void CenterEngineWorldDisplay()
        {
            throw new NotSupportedException("CenterEngineWorldDisplay requires RE of EngineWorldDisplayGuiElement_CenterDisplay.");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            _ = disposing;

            if (_isDisposed)
            {
                return;
            }

            GraphicDataFree();

            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }

            _isDisposed = true;
        }

        ~NC2MainMenuGuiManager()
        {
            Dispose(false);
        }

        private static void GraphicDataLoad()
        {
            // TODO: ls_GraphicData_Load
        }

        private static void GraphicDataFree()
        {
            // TODO: ls_GraphicData_Free
        }

        internal bool GetExplorationFlag()
        {
            // Mirrors engine state used by world display draw state.
            // RE source not finalized yet.
            return _explorationFlag;
        }

        internal void SetExplorationFlag(bool value)
        {
            _explorationFlag = value;
        }

        internal void RegisterWorldDisplayElement(WorldDisplayElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (_worldDisplayElements.Contains(element))
            {
                return;
            }

            _worldDisplayElements.Add(element);

            // TODO: RE: EngineWorldDisplayGuiElement_Register(element)
        }

        internal void UnregisterWorldDisplayElement(WorldDisplayElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            _ = _worldDisplayElements.Remove(element);

            // TODO: RE: EngineWorldDisplayGuiElement_Unregister(element)
        }

        internal void RegisterFrameCall(CBaseElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            if (_frameCallElements.Contains(element))
            {
                return;
            }

            _frameCallElements.Add(element);

            // TODO: RE: FrameCall_Register(element)
        }

        internal void UnregisterFrameCall(CBaseElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            _ = _frameCallElements.Remove(element);

            // TODO: RE: FrameCall_Unregister(element)
        }
    }
}