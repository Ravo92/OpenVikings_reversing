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
        private bool _interactive;

        internal WorldDisplayElement(SRectangle rect, bool interactive, bool flag) : base(rect, layerOrType: 1, elementId: 0x01A7)
        {
            _interactive = interactive;

            // Mirrors: memset of multiple internal regions
            ResetInternalState();

            // Mirrors: new C2DEngineDisplay(bitmap, interactive, CGuiBaseDataManager::mStaticVars)
            CBitmap desktopBitmap = CDesktop.Current.BackBuffer;

            _engineDisplay = new EngineDisplay2D(desktopBitmap, interactive, CGuiBaseDataManager.StaticVars);

            // Mirrors: this[0x50] = param_3
            // (stored but not used yet – keep for correctness)
            bool secondaryFlag = flag;
            _ = secondaryFlag;

            // Mirrors: UserInteractionTargetData init
            _interactionData = new UserInteractionTargetData();
            _interactionData.Initialize();

            if (interactive)
            {
                // Mirrors:
                //   engineDisplay[0xb8] = 1
                //   engineDisplay->targetData = this_00
                _engineDisplay.EnableInteraction(_interactionData);
            }
            else
            {
                bool exploration = NC2MainMenuGuiManager.Instance.GetExplorationFlag();

                // Mirrors: DE_ToDraw_SetState(pCVar2,4,bVar1)
                _engineDisplay.SetDrawState(4, exploration);
            }

            // Mirrors:
            //   EngineWorldDisplayGuiElement_Register(this)
            //   FrameCall_Register(this)
            NC2MainMenuGuiManager.Instance.RegisterWorldDisplayElement(this);
            NC2MainMenuGuiManager.Instance.RegisterFrameCall(this);
        }

        private void ResetInternalState()
        {
            // Intentionally empty for now.
            // Native code clears large internal blocks which we model as managed objects.
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Mirrors unregister order in destructor
            NC2MainMenuGuiManager.Instance.UnregisterFrameCall(this);
            NC2MainMenuGuiManager.Instance.UnregisterWorldDisplayElement(this);

            _engineDisplay.Dispose();

            _disposed = true;
        }
    }
}