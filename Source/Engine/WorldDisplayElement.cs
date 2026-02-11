using OpenVikings.NC2GuiToolsBase;
using OpenVikings.NXBaseGui;
using OpenVikings.NXBasics;

namespace OpenVikings.Engine
{
    internal sealed class WorldDisplayElement : CBaseElement, IDisposable
    {
        private readonly EngineDisplay2D _engineDisplay;
        private readonly UserInteractionTargetData _interactionData;

        private bool _disposed;
        private readonly bool _interactive;

        internal WorldDisplayElement(SRectangle rect, bool interactive, bool flag)
            : base(rect, 1, 0x01A7)
        {
            _interactive = interactive;

            // Mirrors: memset of multiple internal regions
            ResetInternalState();

            CDesktop? desktop = CDesktop.sTheObjectPtr ?? throw new InvalidOperationException("Desktop is not initialized.");

            // Mirrors: new C2DEngineDisplay(bitmap, interactive, CGuiBaseDataManager::mStaticVars)
            CBitmap desktopBitmap = desktop.BackBuffer;

            CFont? baseFont = CGuiBaseDataManager.Font08 ?? throw new InvalidOperationException("CGuiBaseDataManager.Font08 is not initialized.");
            _engineDisplay = new EngineDisplay2D(desktopBitmap, interactive, baseFont);

            // Mirrors: this[0x50] = param_3 (stored but not used yet – keep for correctness)
            bool secondaryFlag = flag;
            _ = secondaryFlag;

            _interactionData = new UserInteractionTargetData();
            _interactionData.Initialize();

            if (interactive)
            {
                _engineDisplay.EnableInteraction(_interactionData);
            }
            else
            {
                bool exploration = NC2MainMenuGuiManager.Instance.GetExplorationFlag();
                _engineDisplay.SetDrawState(4, exploration);
            }

            NC2MainMenuGuiManager.Instance.RegisterWorldDisplayElement(this);
            NC2MainMenuGuiManager.Instance.RegisterFrameCall(this);
        }

        private static void ResetInternalState()
        {
            // Intentionally empty for now.
            // Native code clears large internal blocks which are modeled as managed objects.
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            NC2MainMenuGuiManager.Instance.UnregisterFrameCall(this);
            NC2MainMenuGuiManager.Instance.UnregisterWorldDisplayElement(this);

            _engineDisplay.Dispose();

            _disposed = true;
        }
    }
}