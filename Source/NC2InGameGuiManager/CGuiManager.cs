using OpenVikings.Dexter;
using OpenVikings.NC2Logic;
using OpenVikings.NXBasics;
using OpenVikings.NXSys;
using OpenVikings.NXSysMouseManager;

namespace OpenVikings.NC2InGameGuiManager
{
    // NC2InGameGuiManager::CGuiManager
    internal sealed partial class CGuiManager : IDisposable
    {
        // "sTheObjectPtr"
        internal static CGuiManager? sTheObjectPtr;

        // C++ "mStaticVars" (size 0x14 in decompile) -> represent as a managed flag-bag.
        private static readonly StaticVarsBag _staticVars = new();

        // ---- Core state (selected offsets mapped by meaning) ----------------------------

        // *(int*)(this + 0x1c0) gui_scroll_speed
        private int _guiScrollSpeed;

        // *(int*)(this + 0x1c4) gui_main_mode
        private int _guiMainMode;

        // this[0x1c8] gui_expert_flag
        private bool _guiExpertFlag;

        // this[0x1c9] gui_tooltipsoff_flag
        private bool _guiTooltipsOffFlag;

        // this[0x1ca] gui_scroll_on_third_button
        private bool _guiScrollOnThirdButton;

        // this[0x1cb] gui_scroll_on_border
        private bool _guiScrollOnBorder;

        // this[0x1cc] software mouse flag
        private bool _softwareMouseFlag;

        // this[0x1cd] allow_cheats -> cached
        private bool _allowCheats;

        // this[0x1ce] gui_scroll_on_device_tilt
        private bool _guiTiltScrollFlag;

        // *(int*)(this + 0x1b0) selection action buttons checksum cached
        private int _selectionActionButtonsChecksum;

        // *(int*)this controlling player id
        private int _controllingPlayerId;

        // *(int*)(this + 0x128) last selection type snapshot
        private int _lastSelectionMode;

        // ---- GUI / Desktop references (based on offsets seen in Desktop_Open/Close) ----

        // (this + 8) -> CWorldDisplayElement*
        private NC2InGameGuiWorldElement.CWorldDisplayElement? _worldDisplayElement;

        // (this + 0x18) background gfx element
        private NC2InGameGuiBase.CBaseToolGfxElement? _toolBackground;

        // Various toolbar buttons (0x20..0x78 + 0x68 speed + 0x78 priority)
        private NC2InGameGuiBase.CBaseToolGfxButton? _btnHelp;      // +0x20 (string id 1)
        private NC2InGameGuiBase.CBaseToolGfxButton? _btnButton2;   // +0x28
        private NC2InGameGuiBase.CBaseToolGfxButton? _btnButton5;   // +0x30
        private NC2InGameGuiBase.CBaseToolGfxButton? _btnButton4;   // +0x38
        private NC2InGameGuiBase.CBaseToolGfxButton? _btnButton6;   // +0x40
        private NC2InGameGuiBase.CBaseToolGfxButton? _btnButton7;   // +0x48
        private NC2InGameGuiBase.CBaseToolGfxButton? _btnButton3;   // +0x50
        private NC2InGameGuiBase.CBaseToolGfxButton? _btnButton8;   // +0x58
        private NC2InGameGuiBase.CBaseToolGfxButton? _btnButton0;   // +0x60
        private NC2InGameGuiBase.CBaseToolGfxButton? _btnSpeed;     // +0x68
        private NC2InGameGuiBase.CBaseToolGfxElement? _priorityFrame; // +0x70
        private NC2InGameGuiBase.CBaseToolGfxButton? _btnPriority;  // +0x78

        // Windows
        private NC2InGameGuiMiscWindows.CMissionOnscreenInfoStaticGuiWindow? _missionOnscreenInfoWindow; // +0x10
        private NC2InGameGuiOverviewDisplay.CWorldOverviewStaticGuiWindow? _overviewWindow;              // +0x80
        private NXBaseGui.CBaseElement? _overviewPlaceholderElement;                                      // +0x88

        // Selection windows (0x130..0x180)
        private NXBaseGui.CBaseWindow? _wndSelSingleHuman;
        private NXBaseGui.CBaseWindow? _wndSelMultipleHuman;
        private NXBaseGui.CBaseWindow? _wndSelSingleAnimal;
        private NXBaseGui.CBaseWindow? _wndSelSingleVehicle;
        private NXBaseGui.CBaseWindow? _wndSelMultipleVehicles;
        private NXBaseGui.CBaseWindow? _wndSelHouse;
        private NXBaseGui.CBaseWindow? _wndSelHouseType;
        private NXBaseGui.CBaseWindow? _wndSelGuide;
        private NXBaseGui.CBaseWindow? _wndSelLandscapeType;
        private NXBaseGui.CBaseWindow? _wndSelRoadBuild;
        private NXBaseGui.CBaseWindow? _wndSelWallBuild;

        // Lists (C++ has multiple CListBase blocks: +0x188 action elements list, +0xb8 world displays, +0xe0 observation windows)
        private readonly CListBase<NXBaseGui.CBaseElement> _actionElementsList;
        private readonly CListBase<NC2InGameGuiWorldElement.CWorldDisplayElement> _worldDisplayList;
        private readonly CListBase<NXBaseGui.CBaseWindow> _observationWindowList;
        private readonly CListBase<NXBaseGui.CBaseElement> _actionButtonsList;
        private readonly CListBase<NXBaseGui.CBaseElement> _selectionActionButtonsList;
        private readonly CListBase<NXBaseGui.CBaseElement> _frameCallList;

        // Frame rate calc (this + 0x1f8)
        private readonly CFrameRateCalculator _frameRateCalculator;

        // Default message handler element (this + 0x290)
        private readonly NC2InGameGuiManagerElements.CGuiManagerDefaultMessageHandlerElement _defaultMessageHandlerElement;

        // String input handler (this + 0x2d0)
        private readonly NC2InGameGuiBaseTextInputElement.CStringInputHandler _stringInputHandler;

        private bool _disposed;

        // Checksum used by l_Selection_ActionButtons_CheckUpdate() (Part 1).
        private uint _actionButtonsChecksum; // (this + 0x1b0)

        // Stored mouse position at bring-up time (this + 0x1b4 / 0x1b8).
        private int _actionButtonsMouseX;
        private int _actionButtonsMouseY;

        // Default message handler element used as parent/receiver (this + 0x290).
        private readonly CGuiManagerDefaultMessageHandlerElement _defaultMessageHandler;

        // --------------------------------------------------------------------
        // MiscWindows_CloseAllLargeOnes(bool)::sWindowSearchCommandIdArray
        // Size 0x28 bytes => 10 entries.
        // The actual IDs should match the original binary; fill in with the real values if already known elsewhere.
        // --------------------------------------------------------------------
        private static readonly uint[] sWindowSearchCommandIdArray =
        [
            0x000007D8u, // Options
            0x000007D9u, // Global settings
            0x000007E3u, // Diplomacy
            0x000007E4u, // Human list
            0x000007E5u, // Statistics
            0x000007E6u, // Mission info
            0x00000000u,
            0x00000000u,
            0x00000000u,
            0x00000000u
        ];

        // 0x1B4 / 0x1B8 (mouse position snapshot when menu was opened)
        private int _selectionActionButtonsMouseX;
        private int _selectionActionButtonsMouseY;

        // 0x290
        private readonly NC2InGameGuiManagerDefaultMessageHandlerElement.CGuiManagerDefaultMessageHandlerElement _defaultMessageHandler;

        // 0x80 / 0x88
        private NXBaseGui.CBaseWindow? _overviewWindowPtr;
        private NC2InGameGuiBase.CBaseToolGfxButton? _overviewToggleButton;

        // 0x1C0
        private int _optionsScrollSpeedIndex;

        // 0x1D0 (10 markers)
        private readonly uint[] _guiMarkers = new uint[10];

        // 0x280 / 0x288 (large window pause handling)
        private bool _largeWindowOpenedPauseActive;
        private double _savedGameSpeedBeforeLargeWindow;

        // Debug gate (mStaticVars in decompile)
        private static bool sDebugDrawEnabled;

        internal CGuiManager()
        {
            // C++: SGuiElementPtr ctor at (this+8) and list base at (this+0x188), plus more.
            // In managed code, explicit fields are initialized here.

            _actionElementsList = new CListBase<NXBaseGui.CBaseElement>(false);
            _worldDisplayList = new CListBase<NC2InGameGuiWorldElement.CWorldDisplayElement>(false);
            _observationWindowList = new CListBase<NXBaseGui.CBaseWindow>(false);

            _actionButtonsList = new CListBase<NXBaseGui.CBaseElement>(false);
            _selectionActionButtonsList = new CListBase<NXBaseGui.CBaseElement>(false);
            _frameCallList = new CListBase<NXBaseGui.CBaseElement>(false);

            _frameRateCalculator = new NXSysTime.CFrameRateCalculator();
            _defaultMessageHandlerElement = new NC2InGameGuiManagerElements.CGuiManagerDefaultMessageHandlerElement();
            _stringInputHandler = new NC2InGameGuiBaseTextInputElement.CStringInputHandler();

            sTheObjectPtr = this;

            // C++ does memset(this,0,0x290) and memset(mStaticVars,0,0x14) AFTER constructing some subobjects.
            // In C#, fields are already zero/default; clear the static bag explicitly.
            _staticVars.Reset();

            // Construct singletons (C++ operator_new + ctor)
            _ = new NC2GuiToolsBase.CGuiBaseStringsManager();
            _ = new NC2GuiToolsBase.CGuiBaseDataManager();
            _ = new NC2InGameGuiKeyAssignmentManager.CKeyAssignmentManager();

            // Apply options from property manager (with defaults if missing)
            _guiScrollSpeed = ReadIntOptionOrDefault("gui_scroll_speed", 1);
            _guiMainMode = ReadIntOptionOrDefault("gui_main_mode", 1);
            _guiExpertFlag = ReadBoolOptionOrDefault("gui_expert_flag", false);
            _guiTooltipsOffFlag = ReadBoolOptionOrDefault("gui_tooltipsoff_flag", false);
            _guiScrollOnThirdButton = ReadBoolOptionOrDefault("gui_scroll_on_third_button", true);
            _guiScrollOnBorder = ReadBoolOptionOrDefault("gui_scroll_on_border", true);

            // gui_scroll_on_device_tilt also mirrors into DexterOS::TiltScroll
            _guiTiltScrollFlag = ReadBoolOptionOrDefault("gui_scroll_on_device_tilt", false);
            DexterOS.TiltScroll = _guiTiltScrollFlag;

            // C++ forces software mouse flag to true once and saves immediately if it was not 1.
            if (_softwareMouseFlag != true)
            {
                _softwareMouseFlag = true;
                NMasterPropertyManager.CPropertyManager.Options_Save_InGame(NMasterPropertyManager.CPropertyManager.sTheObjectPtr);
            }

            // allow_cheats -> if exists and !=0 then set
            _allowCheats = ReadBoolOptionOrDefault("allow_cheats", false);

            // CStringInputHandler::Init(this+0x2d0, 0x3c, false, false, 7, '\0')
            _stringInputHandler.Init(0x3c, false, false, 7, 0);

            // Other ctor state in decompile is either list vtables or padding; ignored in managed port.
        }

        internal void Options_ScrollSpeed_Set(int scrollSpeed, bool saveOptions)
        {
            if (_guiScrollSpeed != scrollSpeed)
            {
                _guiScrollSpeed = scrollSpeed;
                if (saveOptions)
                {
                    NMasterPropertyManager.CPropertyManager.Options_Save_InGame(NMasterPropertyManager.CPropertyManager.sTheObjectPtr);
                }
            }
        }

        internal void Options_GuiMisc_SetMainMode(int mainMode, bool saveOptions)
        {
            if (_guiMainMode != mainMode)
            {
                _guiMainMode = mainMode;
                if (saveOptions)
                {
                    NMasterPropertyManager.CPropertyManager.Options_Save_InGame(NMasterPropertyManager.CPropertyManager.sTheObjectPtr);
                }
            }
        }

        internal void Options_GuiMisc_SetExpertFlag(bool expertFlag, bool saveOptions)
        {
            if (_guiExpertFlag != expertFlag)
            {
                _guiExpertFlag = expertFlag;
                if (saveOptions)
                {
                    NMasterPropertyManager.CPropertyManager.Options_Save_InGame(NMasterPropertyManager.CPropertyManager.sTheObjectPtr);
                }
            }
        }

        internal void Options_GuiMisc_SetTooltipsOffFlag(bool tooltipsOff, bool saveOptions)
        {
            if (_guiTooltipsOffFlag != tooltipsOff)
            {
                _guiTooltipsOffFlag = tooltipsOff;
                if (saveOptions)
                {
                    NMasterPropertyManager.CPropertyManager.Options_Save_InGame(NMasterPropertyManager.CPropertyManager.sTheObjectPtr);
                }
            }
        }

        internal void Options_Scrolling_SetThirdMouseButtonScrollFlag(bool enabled, bool saveOptions)
        {
            if (_guiScrollOnThirdButton != enabled)
            {
                _guiScrollOnThirdButton = enabled;
                if (saveOptions)
                {
                    NMasterPropertyManager.CPropertyManager.Options_Save_InGame(NMasterPropertyManager.CPropertyManager.sTheObjectPtr);
                }
            }
        }

        internal void Options_Scrolling_SetBorderScrollFlag(bool enabled, bool saveOptions)
        {
            if (_guiScrollOnBorder != enabled)
            {
                _guiScrollOnBorder = enabled;
                if (saveOptions)
                {
                    NMasterPropertyManager.CPropertyManager.Options_Save_InGame(NMasterPropertyManager.CPropertyManager.sTheObjectPtr);
                }
            }
        }

        internal void Options_Scrolling_SetTiltScrollFlag(bool enabled, bool saveOptions)
        {
            if (_guiTiltScrollFlag != enabled)
            {
                _guiTiltScrollFlag = enabled;
                DexterOS.TiltScroll = enabled;

                if (saveOptions)
                {
                    NMasterPropertyManager.CPropertyManager.Options_Save_InGame(NMasterPropertyManager.CPropertyManager.sTheObjectPtr);
                }
            }
        }

        internal void Options_Scrolling_SetSoftwareMouseFlag(bool enabled, bool saveOptions)
        {
            if (_softwareMouseFlag != enabled)
            {
                _softwareMouseFlag = enabled;
                if (saveOptions)
                {
                    NMasterPropertyManager.CPropertyManager.Options_Save_InGame(NMasterPropertyManager.CPropertyManager.sTheObjectPtr);
                }
            }
        }

        public void Dispose()
        {
            // Ensure desktop is closed while dependencies still exist.
            Desktop_Close();

            NC2InGameGuiKeyAssignmentManager.CKeyAssignmentManager.sTheObjectPtr?.Dispose();

            NC2GuiToolsBase.CGuiBaseDataManager.sTheObjectPtr?.Dispose();

            NC2GuiToolsBase.CGuiBaseStringsManager.sTheObjectPtr?.Dispose();

            sTheObjectPtr = null;

            // Clear list-managed elements owned by this manager.
            if (_actionElementsList.HasAnyElements())
            {
                _actionElementsList.DeleteAllElements();
            }

            _stringInputHandler.Dispose();
            _defaultMessageHandlerElement.Dispose();

            _actionElementsList.Dispose();
            _worldDisplayList.Dispose();
            _observationWindowList.Dispose();
        }

        private static int ReadIntOptionOrDefault(string key, int defaultValue)
        {
            if (NMasterPropertyManager.CPropertyManager.sTheObjectPtr == null)
            {
                return defaultValue;
            }

            bool exists = NMasterPropertyManager.CPropertyManager.Property_DoesExists(NMasterPropertyManager.CPropertyManager.sTheObjectPtr, key);
            if (!exists)
            {
                return defaultValue;
            }

            return NMasterPropertyManager.CPropertyManager.Property_GetIntegerValue(NMasterPropertyManager.CPropertyManager.sTheObjectPtr, key);
        }

        private static bool ReadBoolOptionOrDefault(string key, bool defaultValue)
        {
            if (NMasterPropertyManager.CPropertyManager.sTheObjectPtr == null)
            {
                return defaultValue;
            }

            bool exists = NMasterPropertyManager.CPropertyManager.Property_DoesExists(NMasterPropertyManager.CPropertyManager.sTheObjectPtr, key);
            if (!exists)
            {
                return defaultValue;
            }

            int value = NMasterPropertyManager.CPropertyManager.Property_GetIntegerValue(NMasterPropertyManager.CPropertyManager.sTheObjectPtr, key);
            return value != 0;
        }

        private sealed class StaticVarsBag
        {
            internal bool DesktopOpenFlag; // C++ mStaticVars != 0

            internal void Reset()
            {
                DesktopOpenFlag = false;
            }
        }


        internal void System_UpdateGui()
        {
            NXBaseGui.CDesktop? desktop = NXBaseGui.CDesktop.sTheObjectPtr;
            if (desktop == null)
            {
                // Desktop is not open; nothing to update safely.
                _staticVars.DesktopOpenFlag = false;
                return;
            }

            // Decompile: if (*(int*)(this+0x98)!=0) iterate list at this+0xa0 and send message 0x7ed.
            DispatchFrameCallsIfAny(0x7ed);

            l_Selection_ActionButtons_CheckUpdate();

            _frameRateCalculator.CalculateFrameRate();

            MiscButtons_SpeedButton_Update(false);
            MiscButtons_MessagePriorityButton_Update(false);

            // DesktopOpenFlag is treated as a one-frame "desktop just opened" / "force refresh" flag.
            NXBaseGui.CDesktop.Desktop_DoWork(desktop, false, _staticVars.DesktopOpenFlag);
            _staticVars.DesktopOpenFlag = false;

            NXBaseGui.CBaseElement? underMouse = NXBaseGui.CDesktop.Element_GetUnderMousePtr(desktop);
            if (underMouse != null && ReferenceEquals(underMouse, _worldDisplayElement))
            {
                NXBaseGui.CDesktop.Element_Activate(desktop, underMouse);
            }
        }

        internal void FrameCall_Do()
        {
            // Decompile: same iteration as above but message 0x7ee.
            DispatchFrameCallsIfAny(0x7ee);
        }

        private void DispatchFrameCallsIfAny(int messageId)
        {
            // Placeholder hook: in the original, an internal list is iterated and Message() invoked on entries.
            // Keep this as a single choke-point for later exact reconstruction.
            _ = messageId;
        }

        private void l_Selection_ActionButtons_CheckUpdate()
        {
            if (_selectionActionButtonsChecksum == 0)
            {
                return;
            }

            int checksumNow = l_Selection_ActionButtons_GetChecksum();
            if (checksumNow == _selectionActionButtonsChecksum)
            {
                return;
            }

            // Remove all elements from the action element list and delete them from the desktop.
            NXBaseGui.CBaseElement? element = _actionElementsList.l_Base_GetStartElement();
            while (element != null)
            {
                _actionElementsList.l_Base_RemoveElement(element);
                NXBaseGui.CDesktop.Element_Delete(NXBaseGui.CDesktop.sTheObjectPtr, element);
                element = _actionElementsList.l_Base_GetStartElement();
            }

            _selectionActionButtonsChecksum = 0;
            l_Selection_ActionButtons_BringUp();
        }

        private int l_Selection_ActionButtons_GetChecksum()
        {
            // TODO: implement exact checksum logic when the selection/action button model is ported.
            return 0;
        }

        private void l_Selection_ActionButtons_BringUp()
        {
            // TODO: implement exact action button rebuild.
        }

        private void MiscButtons_SpeedButton_Update(bool force)
        {
            if (_btnSpeed == null)
            {
                return;
            }

            uint gfxId = 0x31;
            uint speedFactor = 0xFFFFFFFFu;

            if (!NC2InGameGuiGlobals.DAT_1003a64dd)
            {
                speedFactor = (uint)(NC2InGameGuiGlobals.DAT_1003a6488 / 12.0);
                if (speedFactor == 3)
                {
                    gfxId = 0x35;
                }
                else if (speedFactor == 2)
                {
                    gfxId = 0x34;
                }
                else if (speedFactor == 0)
                {
                    gfxId = 0x36;
                }
            }

            NC2InGameGuiBase.CBaseToolGfxButton.SetGraphicsId(_btnSpeed, gfxId);

            if (force || NC2InGameGuiGlobals.SpeedButton_LastSpeedFactor != speedFactor)
            {
                NC2InGameGuiGlobals.SpeedButton_LastSpeedFactor = speedFactor;

                string tooltip;
                if (speedFactor == 0xFFFFFFFFu)
                {
                    tooltip = "MAX!";
                }
                else
                {
                    string baseText = NC2GuiToolsBase.StringTool.StringTool_GetGameGuiMainStringPtr(0x0D);
                    tooltip = string.Format("{0} (*{1})", baseText, speedFactor);
                }

                NC2InGameGuiBase.CBaseToolGfxButton.SetToolTipString(_btnSpeed, tooltip);

                // Decompile: *(byte*)(button+0x68)=1 (dirty flag)
                _btnSpeed.ToolTipDirtyFlag = true;
            }
        }

        private void MiscButtons_MessagePriorityButton_Update(bool force)
        {
            if (_btnPriority == null)
            {
                return;
            }

            int priority = NC2InGameGuiMessages.CMessageManager.sTheObjectPtr.GetPriorityMode();
            uint gfxId = 0x42;
            if (priority != 2)
            {
                gfxId = (uint)((priority == 1 ? 1 : 0) | 0x40);
            }

            NC2InGameGuiBase.CBaseToolGfxButton.SetGraphicsId(_btnPriority, gfxId);

            if (force || NC2InGameGuiGlobals.PriorityButton_LastPriority != priority)
            {
                NC2InGameGuiGlobals.PriorityButton_LastPriority = priority;

                string text = NC2GuiToolsBase.StringTool.StringTool_GetGameGuiMainStringPtr(0x0E);
                if (priority == 1)
                {
                    text = NC2GuiToolsBase.StringTool.StringTool_GetGameGuiMainStringPtr(0x0F);
                }
                if (priority == 2)
                {
                    text = NC2GuiToolsBase.StringTool.StringTool_GetGameGuiMainStringPtr(0x10);
                }

                NC2InGameGuiBase.CBaseToolGfxButton.SetToolTipString(_btnPriority, text);
                _btnPriority.ToolTipDirtyFlag = true;
            }
        }

        internal void EngineWorldDisplayGuiElement_Activate()
        {
            if (_worldDisplayElement == null)
            {
                return;
            }

            NXBaseGui.CDesktop.Element_Activate(NXBaseGui.CDesktop.sTheObjectPtr, _worldDisplayElement);
        }

        internal static void System_StartGame()
        {
            _ = new NC2InGameGuiInputManager.CInputManager();
            _ = new NC2InGameGuiMessages.CMessageManager();
            _ = new NC2InGameGuiGroups.CObjectGroupManager();
            _ = new NC2InGameGuiDeadHumanDataManager.CDeadHumanDataManager();

            CCallbackManager.RegisterCallback(CCallbackManager.sTheObjectPtr, 0x19, NC2InGameGuiWorldGameCursor.IGG_DrawWorldGameCursor_LogicCallback, 0, 10);

            NC2InGameGuiMiscWindows.CStatisticsWindow.Statics_Init();
            NC2InGameGuiMiscWindows.CHumanListWindow.Statics_Init();
            NC2InGameGuiMiscWindows.CDiplomacyWindow.Statics_Init();
            NC2InGameGuiSelection.CSelectionSingleHumanWindow.Statics_Init();
            NC2InGameGuiSelection.CSelectionSingleVehicleWindow.Statics_Init();
            NC2InGameGuiSelection.CSelectionHouseWindow.Statics_Init();
            NC2InGameGuiMiscWindows.CTechTreeWindow.Statics_Init();
            NC2InGameGuiMiscWindows.CMissionInfoWindow.MissionBriefingHistory_Init();
        }

        internal static void System_EndGame()
        {
            CCallbackManager.UnRegisterCallback(CCallbackManager.sTheObjectPtr, 0x19, NC2InGameGuiWorldGameCursor.IGG_DrawWorldGameCursor_LogicCallback, 0);

            if (NC2InGameGuiDeadHumanDataManager.CDeadHumanDataManager.sTheObjectPtr != null)
            {
                NC2InGameGuiDeadHumanDataManager.CDeadHumanDataManager.sTheObjectPtr.Dispose();
            }

            if (NC2InGameGuiGroups.CObjectGroupManager.sTheObjectPtr != null)
            {
                NC2InGameGuiGroups.CObjectGroupManager.sTheObjectPtr.Dispose();
            }

            if (NC2InGameGuiMessages.CMessageManager.sTheObjectPtr != null)
            {
                NC2InGameGuiMessages.CMessageManager.sTheObjectPtr.Dispose();
            }

            if (NC2InGameGuiInputManager.CInputManager.sTheObjectPtr != null)
            {
                NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Dispose();
            }
        }

        // Decompile: ls_Logic_CallbackFunction(TCallbackTypes, ..., uint param_3, int param_4)
        internal static long ls_Logic_CallbackFunction(int callbackType, long param2, uint param3, int param4)
        {
            _ = param2;

            CGuiManager? mgr = sTheObjectPtr;
            if (mgr == null)
            {
                return 1;
            }

            switch (callbackType)
            {
                case 1:
                    mgr.Selection_UpdateWindows();
                    mgr.MiscButtons_SpeedButton_Update(false);
                    mgr.MiscButtons_MessagePriorityButton_Update(false);
                    break;

                case 0x14:
                    mgr.l_IO_Savegame_Save(CIoHelper.FromHandle(param3));
                    break;

                case 0x15:
                    mgr.l_IO_Savegame_Load(CIoHelper.FromHandle(param3));
                    break;

                case 0x16:
                    mgr.DispatchFrameCallsIfAny(0x7ee);
                    if (param3 != 0)
                    {
                        NC2InGameGuiMiscWindows.CMissionInfoWindow.MissionBriefingHistory_Init();
                    }
                    if (mgr._worldDisplayElement != null)
                    {
                        mgr._worldDisplayElement.FollowMode_End();
                    }
                    break;

                case 0x1b:
                    mgr.MiscWindows_MissionInfoWindow_Open(param3, param4 != 0);
                    break;

                case 0x2b:
                case 0x2c:
                    NC2InGameGuiEntryPoints.C2IGG_EntryPoint_MainDisplay_SetControllingPlayerCenterPosition();
                    NXBaseGui.CBaseWindow? wnd = NXBaseGui.CDesktop.Message_SendToAllWindows(NXBaseGui.CDesktop.sTheObjectPtr, 0x7e6, 0, 0, 0);
                    wnd?.Message(0x800, 0xc351, 0, 0, 0);

                    mgr.ObservationWindow_CloseAll();
                    break;

                case 0x2d:
                    NC2InGameGuiEntryPoints.C2IGG_EntryPoint_MainDisplay_SetControllingPlayerCenterPosition();
                    mgr.ObservationWindow_CloseAll();
                    break;

                case 0x2f:
                    long msg = NXBaseGui.CDesktop.Message_SendToAllWindowsRaw(NXBaseGui.CDesktop.sTheObjectPtr, 0x7eb, 2, 0, 0);
                    if (msg == 0)
                    {
                        NC2InGameGuiMiscWindows.CGameSafetyBoxWindow safety = new NC2InGameGuiMiscWindows.CGameSafetyBoxWindow();
                        NC2InGameGuiBase.BaseToolDesktop_AddWindow(safety, false);
                    }
                    break;
            }

            return 1;
        }

        private void l_IO_Savegame_Save(NC2IO.CIoHelper io)
        {
            // TODO: implement save logic binding.
            _ = io;
        }

        private void l_IO_Savegame_Load(NC2IO.CIoHelper io)
        {
            // TODO: implement load logic binding.
            _ = io;
        }

        private void MiscWindows_MissionInfoWindow_Open(uint arg, bool flag)
        {
            // TODO: implement mission info window open.
            _ = arg;
            _ = flag;
        }

        internal void Desktop_Open(int width, int height, uint renderByteDepth)
        {
            if (NXBaseGui.CDesktop.sTheObjectPtr != null)
            {
                // Desktop already exists.
                return;
            }

            // Create desktop
            NXBaseGui.CDesktop desktop = new(width, height, (byte)renderByteDepth);

            // Dynamic GUI data
            NC2GuiToolsBase.CGuiBaseDataManager.DynamicData_Load();

            // Mouse manager "is mouse stopped" callback
            CMouseManager.sTheObjectPtr?.IsMouseStoppedCallback = sl_IsMouseStoppedCallback;

            // Primary message handler
            NC2InGameGuiManagerElements.CGuiManagerPrimaryMessageHandlerElement primaryHandler = new NC2InGameGuiManagerElements.CGuiManagerPrimaryMessageHandlerElement();
            NXBaseGui.CDesktop.PrimaryMessageHandler_Set(desktop, primaryHandler);

            // Software mouse pointer element
            NC2InGameGuiManagerElements.CGuiManagerMousePointer mousePointer = new NC2InGameGuiManagerElements.CGuiManagerMousePointer();
            desktop.MousePointer = mousePointer;

            // Register callbacks
            CCallbackManager.RegisterCallback(CCallbackManager.sTheObjectPtr, 1, ls_Logic_CallbackFunction, 0, 10);
            CCallbackManager.RegisterCallback(CCallbackManager.sTheObjectPtr, 0x14, ls_Logic_CallbackFunction, 0, 5);
            CCallbackManager.RegisterCallback(CCallbackManager.sTheObjectPtr, 0x15, ls_Logic_CallbackFunction, 0, 5);
            CCallbackManager.RegisterCallback(CCallbackManager.sTheObjectPtr, 0x16, ls_Logic_CallbackFunction, 0, 5);
            CCallbackManager.RegisterCallback(CCallbackManager.sTheObjectPtr, 0x1b, ls_Logic_CallbackFunction, 0, 5);
            CCallbackManager.RegisterCallback(CCallbackManager.sTheObjectPtr, 0x2b, ls_Logic_CallbackFunction, 0, 5);
            CCallbackManager.RegisterCallback(CCallbackManager.sTheObjectPtr, 0x2c, ls_Logic_CallbackFunction, 0, 5);
            CCallbackManager.RegisterCallback(CCallbackManager.sTheObjectPtr, 0x2d, ls_Logic_CallbackFunction, 0, 5);
            CCallbackManager.RegisterCallback(CCallbackManager.sTheObjectPtr, 0x2f, ls_Logic_CallbackFunction, 0, 5);
            CCallbackManager.RegisterCallback(CCallbackManager.sTheObjectPtr, 0x30, ls_Logic_CallbackFunction, 0, 5);

            NC2E2.CE2Manager.Inform_DesktopXTructed(width, height, renderByteDepth);

            // World overview handler (singleton)
            _ = new NC2InGameGuiOverviewDisplay.CWorldOverviewElementHandler();

            // World display element
            SRectangle rect = new(0, 0, width, height);
            _worldDisplayElement = new NC2InGameGuiWorldElement.CWorldDisplayElement(rect, true, true);
            NC2InGameGuiBase.BaseToolDesktop_AddBackgroundElement(_worldDisplayElement);

            // Background frame element
            SRectangle bgRect = new(0, 10, 0x32, 0x1b1);
            _toolBackground = new NC2InGameGuiBase.CBaseToolGfxElement(bgRect, 0x33);
            NC2InGameGuiBase.CBaseToolGfxElement.SetCenterGraphicsFlag(_toolBackground, false);
            NC2InGameGuiBase.CBaseToolGfxElement.SetPalettePtr(_toolBackground, NC2InGameGuiGlobals.DAT_1003a44b0);
            NC2InGameGuiBase.BaseToolDesktop_AddBackgroundElement(_toolBackground);

            // Buttons block
            NXBaseGui.CBaseElement messageHandler = _defaultMessageHandlerElement;

            _btnButton2 = CreateToolButton(new SRectangle(0, 0x29, 0x28, 0x23), 0x2a, 2, 0xf3e, messageHandler);
            _btnButton5 = CreateToolButton(new SRectangle(0, 0x49, 0x28, 0x23), 0x2d, 5, 0xf3d, messageHandler);
            _btnButton3 = CreateToolButton(new SRectangle(0, 0x75, 0x28, 0x23), 0x2e, 3, 0xf42, messageHandler);
            _btnButton4 = CreateToolButton(new SRectangle(0, 0x97, 0x28, 0x23), 0x2c, 4, 0xf3f, messageHandler);
            _btnButton7 = CreateToolButton(new SRectangle(0, 0xb0, 0x28, 0x23), 0x32, 7, 0xf41, messageHandler);
            _btnButton6 = CreateToolButton(new SRectangle(0, 0xcc, 0x28, 0x23), 0x2b, 6, 0xf40, messageHandler);
            _btnButton8 = CreateToolButton(new SRectangle(0, 0xee, 0x28, 0x23), 0x38, 8, 0xf43, messageHandler);
            _btnHelp = CreateToolButton(new SRectangle(0, 0x127, 0x28, 0x23), 0x2f, 1, 0xf3c, messageHandler);
            _btnButton0 = CreateToolButton(new SRectangle(0, 0x149, 0x28, 0x23), 0x30, 0, 0xf44, messageHandler);

            _btnSpeed = new NC2InGameGuiBase.CBaseToolGfxButton(new SRectangle(0, 0x175, 0x28, 0x23), 0x31, 0xf46, messageHandler);
            NC2InGameGuiBase.CBaseToolGfxButton.SetCenterGraphicsFlag(_btnSpeed, false);
            NC2InGameGuiBase.CBaseToolGfxButton.SetPalettePtr(_btnSpeed, NC2InGameGuiGlobals.DAT_1003a44b0);
            NC2InGameGuiBase.BaseToolDesktop_AddBackgroundElement(_btnSpeed);

            MiscButtons_SpeedButton_Update(true);

            _priorityFrame = new NC2InGameGuiBase.CBaseToolGfxElement(new SRectangle(0x18, 0, 0x7e, 0x29), 0x3f);
            NC2InGameGuiBase.CBaseToolGfxElement.SetCenterGraphicsFlag(_priorityFrame, false);
            NC2InGameGuiBase.CBaseToolGfxElement.SetPalettePtr(_priorityFrame, NC2InGameGuiGlobals.DAT_1003a44b0);
            NC2InGameGuiBase.BaseToolDesktop_AddBackgroundElement(_priorityFrame);

            _btnPriority = new NC2InGameGuiBase.CBaseToolGfxButton(new SRectangle(0x6a, 3, 0x25, 0x1f), 0x40, 0xf47, messageHandler);
            NC2InGameGuiBase.CBaseToolGfxButton.SetCenterGraphicsFlag(_btnPriority, false);
            NC2InGameGuiBase.CBaseToolGfxButton.SetPalettePtr(_btnPriority, NC2InGameGuiGlobals.DAT_1003a44b0);
            NC2InGameGuiBase.BaseToolDesktop_AddBackgroundElement(_btnPriority);

            MiscButtons_MessagePriorityButton_Update(true);

            if (_overviewWindow == null)
            {
                _overviewWindow = new NC2InGameGuiOverviewDisplay.CWorldOverviewStaticGuiWindow();
                NC2InGameGuiBase.BaseToolDesktop_AddWindow(_overviewWindow, false);

                if (_overviewPlaceholderElement != null)
                {
                    NXBaseGui.CDesktop.Element_Delete(NXBaseGui.CDesktop.sTheObjectPtr, _overviewPlaceholderElement);
                    _overviewPlaceholderElement = null;
                }
            }

            if (_missionOnscreenInfoWindow == null)
            {
                _missionOnscreenInfoWindow = new NC2InGameGuiMiscWindows.CMissionOnscreenInfoStaticGuiWindow();
                NC2InGameGuiBase.BaseToolDesktop_AddWindow(_missionOnscreenInfoWindow, false);
            }

            // One-frame "just opened" flag for Desktop_DoWork().
            _staticVars.DesktopOpenFlag = true;

            NC2InGameGuiGlobals.DAT_1003a6128 = true;

            InputMode_Changed();
            _lastSelectionMode = 0;
            Selection_UpdateWindows();

            NC2E2.CE2Manager.Inform_ControllingPlayerChanged(_controllingPlayerId);
            NC2InGameGuiOverviewDisplay.CWorldOverviewElementHandler.sTheObjectPtr.System_ControllingPlayerChanged();
            NC2InGameGuiMessages.CMessageManager.sTheObjectPtr.Message_RemoveAll(false);
        }

        internal void Desktop_Close()
        {
            if (NXBaseGui.CDesktop.sTheObjectPtr == null)
            {
                // Desktop is already closed.
                _staticVars.DesktopOpenFlag = false;
                return;
            }

            l_Selection_RemoveAllGuiElements();

            NXBaseGui.CDesktop.sTheObjectPtr?.Dispose();

            // Clear desktop-owned references (mirrors memset(this+8,0,0x120))
            _worldDisplayElement = null;

            _toolBackground = null;

            _btnHelp = null;
            _btnButton2 = null;
            _btnButton5 = null;
            _btnButton4 = null;
            _btnButton6 = null;
            _btnButton7 = null;
            _btnButton3 = null;
            _btnButton8 = null;
            _btnButton0 = null;

            _btnSpeed = null;
            _priorityFrame = null;
            _btnPriority = null;

            _missionOnscreenInfoWindow = null;
            _overviewWindow = null;
            _overviewPlaceholderElement = null;

            NC2InGameGuiOverviewDisplay.CWorldOverviewElementHandler.sTheObjectPtr?.Dispose();

            NC2E2.CE2Manager.Inform_DesktopXTructed(0, 0, 0);

            // Unregister callbacks
            CCallbackManager.UnRegisterCallback(CCallbackManager.sTheObjectPtr, 0x30, ls_Logic_CallbackFunction, 0);
            CCallbackManager.UnRegisterCallback(CCallbackManager.sTheObjectPtr, 0x2f, ls_Logic_CallbackFunction, 0);
            CCallbackManager.UnRegisterCallback(CCallbackManager.sTheObjectPtr, 0x2d, ls_Logic_CallbackFunction, 0);
            CCallbackManager.UnRegisterCallback(CCallbackManager.sTheObjectPtr, 0x2c, ls_Logic_CallbackFunction, 0);
            CCallbackManager.UnRegisterCallback(CCallbackManager.sTheObjectPtr, 0x2b, ls_Logic_CallbackFunction, 0);
            CCallbackManager.UnRegisterCallback(CCallbackManager.sTheObjectPtr, 0x1b, ls_Logic_CallbackFunction, 0);
            CCallbackManager.UnRegisterCallback(CCallbackManager.sTheObjectPtr, 0x16, ls_Logic_CallbackFunction, 0);
            CCallbackManager.UnRegisterCallback(CCallbackManager.sTheObjectPtr, 0x15, ls_Logic_CallbackFunction, 0);
            CCallbackManager.UnRegisterCallback(CCallbackManager.sTheObjectPtr, 0x14, ls_Logic_CallbackFunction, 0);
            CCallbackManager.UnRegisterCallback(CCallbackManager.sTheObjectPtr, 1, ls_Logic_CallbackFunction, 0);

            if (NXSysMouseManager.CMouseManager.sTheObjectPtr != null)
            {
                CMouseManager.sTheObjectPtr.IsMouseStoppedCallback = null;
            }

            // Ensure the one-frame open flag is cleared.
            _staticVars.DesktopOpenFlag = false;
        }

        internal void l_RemoveAllGuiElements()
        {
            l_Selection_RemoveAllGuiElements();
        }

        internal void l_Selection_RemoveAllGuiElements()
        {
            // Remove all action elements
            NXBaseGui.CBaseElement? element = _actionElementsList.l_Base_GetStartElement();
            while (element != null)
            {
                _actionElementsList.l_Base_RemoveElement(element);
                NXBaseGui.CDesktop.Element_Delete(NXBaseGui.CDesktop.sTheObjectPtr, element);
                element = _actionElementsList.l_Base_GetStartElement();
            }

            _selectionActionButtonsChecksum = 0;

            CloseAndNullSelectionWindow(ref _wndSelSingleHuman);
            CloseAndNullSelectionWindow(ref _wndSelMultipleHuman);
            CloseAndNullSelectionWindow(ref _wndSelSingleAnimal);
            CloseAndNullSelectionWindow(ref _wndSelSingleVehicle);
            CloseAndNullSelectionWindow(ref _wndSelMultipleVehicles);
            CloseAndNullSelectionWindow(ref _wndSelHouse);
            CloseAndNullSelectionWindow(ref _wndSelHouseType);
            CloseAndNullSelectionWindow(ref _wndSelGuide);
            CloseAndNullSelectionWindow(ref _wndSelLandscapeType);
            CloseAndNullSelectionWindow(ref _wndSelRoadBuild);
            CloseAndNullSelectionWindow(ref _wndSelWallBuild);
        }

        private static void CloseAndNullSelectionWindow(ref NXBaseGui.CBaseWindow? window)
        {
            if (window == null)
            {
                return;
            }

            NXBaseGui.CDesktop.Window_Close(NXBaseGui.CDesktop.sTheObjectPtr, window);
            window = null;
        }

        internal void MiscWindows_OverviewWindow_Open()
        {
            if (_overviewWindow != null)
            {
                return;
            }

            _overviewWindow = new NC2InGameGuiOverviewDisplay.CWorldOverviewStaticGuiWindow();
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(_overviewWindow, false);

            if (_overviewPlaceholderElement != null)
            {
                NXBaseGui.CDesktop.Element_Delete(NXBaseGui.CDesktop.sTheObjectPtr, _overviewPlaceholderElement);
                _overviewPlaceholderElement = null;
            }
        }

        internal void InputMode_Changed()
        {
            bool buildMode = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.IsBuildMode();

            if (!buildMode)
            {
                UnhideToolElement(_toolBackground);
                UnhideToolElement(_btnHelp);
                UnhideToolElement(_btnButton2);
                UnhideToolElement(_btnButton5);
                UnhideToolElement(_btnButton4);
                UnhideToolElement(_btnButton6);
                UnhideToolElement(_btnButton7);
                UnhideToolElement(_btnButton3);
                UnhideToolElement(_btnButton8);
                UnhideToolElement(_btnButton0);
                UnhideToolElement(_btnSpeed);
                UnhideToolElement(_priorityFrame);
                UnhideToolElement(_btnPriority);
            }
            else
            {
                HideToolElement(_toolBackground);
                HideToolElement(_btnHelp);
                HideToolElement(_btnButton2);
                HideToolElement(_btnButton5);
                HideToolElement(_btnButton4);
                HideToolElement(_btnButton6);
                HideToolElement(_btnButton7);
                HideToolElement(_btnButton3);
                HideToolElement(_btnButton8);
                HideToolElement(_btnButton0);
                HideToolElement(_btnSpeed);
                HideToolElement(_priorityFrame);
                HideToolElement(_btnPriority);
            }

            NC2InGameGuiBase.BaseToolDesktop_SelectionActiveChanged();
        }

        private static void HideToolElement(NXBaseGui.CBaseElement? element)
        {
            if (element == null)
            {
                return;
            }

            NXBaseGui.CDesktop.Element_Hide(NXBaseGui.CDesktop.sTheObjectPtr, element);
        }

        private static void UnhideToolElement(NXBaseGui.CBaseElement? element)
        {
            if (element == null)
            {
                return;
            }

            NXBaseGui.CDesktop.Element_UnHide(NXBaseGui.CDesktop.sTheObjectPtr, element);
        }

        internal void Selection_UpdateWindows()
        {
            int selectionChecksum = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.GetSelectionChecksum();
            if (_lastSelectionMode == selectionChecksum)
            {
                return;
            }

            _lastSelectionMode = selectionChecksum;

            l_Selection_RemoveAllGuiElements();

            NXBaseGui.CDesktop.Message_SendToAllWindows(NXBaseGui.CDesktop.sTheObjectPtr, 0x7ec, 0, 0, 0);

            int selectionType = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.GetSelectionType();

            NXBaseGui.CBaseWindow? window = null;

            switch (selectionType)
            {
                case 1:
                    window = new NC2InGameGuiSelection.CSelectionSingleHumanWindow();
                    _wndSelSingleHuman = window;
                    break;

                case 2:
                    window = new NC2InGameGuiSelection.CSelectionMultipleHumanWindow();
                    _wndSelMultipleHuman = window;
                    break;

                case 3:
                    window = new NC2InGameGuiSelection.CSelectionSingleAnimalWindow();
                    _wndSelSingleAnimal = window;
                    break;

                case 4:
                    window = new NC2InGameGuiSelection.CSelectionSingleVehicleWindow();
                    _wndSelSingleVehicle = window;
                    break;

                case 5:
                    window = new NC2InGameGuiSelection.CSelectionMultipleVehiclesWindow();
                    _wndSelMultipleVehicles = window;
                    break;

                case 6:
                    window = new NC2InGameGuiSelection.CSelectionHouseWindow();
                    _wndSelHouse = window;
                    break;

                case 7:
                    window = new NC2InGameGuiSelection.CSelectionHouseTypeWindow();
                    _wndSelHouseType = window;
                    break;

                case 8:
                    window = new NC2InGameGuiSelection.CSelectionGuideWindow();
                    _wndSelGuide = window;
                    break;

                case 9:
                    window = new NC2InGameGuiSelection.CSelectionLandscapeTypeWindow();
                    _wndSelLandscapeType = window;
                    break;

                case 10:
                    window = new NC2InGameGuiSelection.CSelectionRoadBuildWindow();
                    _wndSelRoadBuild = window;
                    break;

                case 11:
                    window = new NC2InGameGuiSelection.CSelectionWallBuildWindow();
                    _wndSelWallBuild = window;
                    break;

                default:
                    return;
            }

            NC2InGameGuiBase.BaseToolDesktop_AddWindow(window, false);
        }

        internal void ControllingPlayer_Set(int playerId, bool force)
        {
            if (_controllingPlayerId == playerId && !force)
            {
                return;
            }

            _controllingPlayerId = playerId;

            NC2E2.CE2Manager.Inform_ControllingPlayerChanged(playerId);
            NC2InGameGuiOverviewDisplay.CWorldOverviewElementHandler.sTheObjectPtr.System_ControllingPlayerChanged();
            NC2InGameGuiMessages.CMessageManager.sTheObjectPtr.Message_RemoveAll(false);
        }

        internal void EngineWorldDisplayGuiElement_Register(NC2InGameGuiWorldElement.CWorldDisplayElement element)
        {
            _worldDisplayList.InsertAtEnd(element);
        }

        internal void EngineWorldDisplayGuiElement_UnRegister(NC2InGameGuiWorldElement.CWorldDisplayElement element)
        {
            _worldDisplayList.l_Base_RemoveElement(element);
        }

        internal static void ObservationWindow_CreateNewOne()
        {
            NC2InGameGuiWorldWindow.CWorldObserveDisplayWindow wnd = new NC2InGameGuiWorldWindow.CWorldObserveDisplayWindow();
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(wnd, false);
        }

        internal void ObservationWindow_Register(NC2InGameGuiWorldWindow.CWorldObserveDisplayWindow wnd)
        {
            _observationWindowList.InsertAtEnd(wnd);
        }

        internal void ObservationWindow_UnRegister(NC2InGameGuiWorldWindow.CWorldObserveDisplayWindow wnd)
        {
            _observationWindowList.l_Base_RemoveElement(wnd);
        }

        private void ClearAndDeleteActionButtonElements()
        {
            NXBaseGui.CBaseElement? element = _actionButtonsList.l_Base_GetStartElement();
            while (element != null)
            {
                _actionButtonsList.l_Base_RemoveElement(element);
                NXBaseGui.CDesktop.Element_Delete(NXBaseGui.CDesktop.sTheObjectPtr, element);
                element = _actionButtonsList.l_Base_GetStartElement();
            }
        }

        internal void Exploration_Set(bool enabled)
        {
            NC2E2.CE2Manager.Inform_SetExplorationState(enabled);
            NC2InGameGuiOverviewDisplay.CWorldOverviewElementHandler.sTheObjectPtr.System_SetExplorationState(enabled);
        }

        internal void Exploration_Toggle()
        {
            bool enabled = true;

            if (_worldDisplayElement != null)
            {
                enabled = !_worldDisplayElement.IsExplorationEnabled();
            }

            NC2E2.CE2Manager.Inform_SetExplorationState(enabled);
            NC2InGameGuiOverviewDisplay.CWorldOverviewElementHandler.sTheObjectPtr.System_SetExplorationState(enabled);
        }

        internal bool Exploration_GetFlag()
        {
            if (_worldDisplayElement == null)
            {
                return false;
            }

            return _worldDisplayElement.IsExplorationEnabled();
        }

        internal static bool IsMouseStoppedCallback()
        {
            CGuiManager guiManager = sTheObjectPtr;
            if (guiManager == null)
            {
                return false;
            }

            if (!guiManager._isMouseStoppedCallbackEnabled) // entspricht +0x1CA
            {
                return false;
            }

            CMouseManager mouse = CMouseManager.sTheObjectPtr;
            if (mouse == null)
            {
                return false;
            }

            return mouse.MiddleDown; // entspricht byte[9] != 0
        }

        private static bool sl_IsMouseStoppedCallback()
        {
            // TODO: wire to the original logic when available.
            return false;
        }

        private static NC2InGameGuiBase.CBaseToolGfxButton CreateToolButton(SRectangle rect, uint gfxId, int stringId, int messageId, NXBaseGui.CBaseElement messageHandler)
        {
            string label = NC2GuiToolsBase.StringTool.StringTool_GetGameGuiMainStringPtr(stringId);

            NC2InGameGuiBase.CBaseToolGfxButton btn = new NC2InGameGuiBase.CBaseToolGfxButton(rect, gfxId, label, messageId, messageHandler);
            NC2InGameGuiBase.CBaseToolGfxButton.SetCenterGraphicsFlag(btn, false);
            NC2InGameGuiBase.CBaseToolGfxButton.SetPalettePtr(btn, NC2InGameGuiGlobals.DAT_1003a44b0);
            NC2InGameGuiBase.BaseToolDesktop_AddBackgroundElement(btn);
            return btn;
        }

        // --------------------------------------------------------------------
        // Selection_ActionButtons_BringUp()
        // --------------------------------------------------------------------
        internal void Selection_ActionButtons_BringUp()
        {
            ClearAndDeleteActionButtonElements();

            _actionButtonsChecksum = 0;

            // Decompile: *(undefined8 *)(this + 0x1b4) = *NXSysMouseManager::CMouseManager::sTheObjectPtr;
            // Interpreted as capturing mouse X/Y.
            NXSysMouseManager.CMouseManager mouse = NXSysMouseManager.CMouseManager.sTheObjectPtr;
            _actionButtonsMouseX = mouse.X;
            _actionButtonsMouseY = mouse.Y;

            l_Selection_ActionButtons_BringUp();
        }

        // --------------------------------------------------------------------
        // Selection_ActionButtons_Remove()
        // --------------------------------------------------------------------
        internal void Selection_ActionButtons_Remove()
        {
            ClearAndDeleteActionButtonElements();
            _actionButtonsChecksum = 0;
        }

        // --------------------------------------------------------------------
        // l_Selection_ActionButtons_BringUp()
        // This is the heavy builder from the decompile. The original uses stack buffers and memmoves;
        // here the same behavior is expressed with lists and explicit filtering.
        // --------------------------------------------------------------------
        private void l_Selection_ActionButtons_BringUp()
        {
            // In the original, the behavior depends on input selection mode:
            // 1/2 humans (single/multi) => radial gfx buttons grouped by group-type (0..4)
            // 4/5 vehicles (single/multi) => vertical text buttons
            // 6 house => vertical text buttons

            int selectionType = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectionType();

            if (selectionType == 1 || selectionType == 2)
            {
                BuildHumanActionButtons(selectionType == 2);
                _actionButtonsChecksum = l_Selection_ActionButtons_GetChecksum();
                return;
            }

            if (selectionType == 4 || selectionType == 5)
            {
                BuildVehicleActionButtons(selectionType == 5);
                _actionButtonsChecksum = l_Selection_ActionButtons_GetChecksum();
                return;
            }

            if (selectionType == 6)
            {
                BuildHouseActionButtons();
                _actionButtonsChecksum = l_Selection_ActionButtons_GetChecksum();
                return;
            }

            _actionButtonsChecksum = l_Selection_ActionButtons_GetChecksum();
        }

        // --------------------------------------------------------------------
        // l_Selection_ActionButtons_GetChecksum() const
        // The original uses XB_GetMemoryChecksum over handler memory and XOR folding for multi selection.
        // This managed version computes a stable checksum from the allowed command IDs.
        // --------------------------------------------------------------------
        private uint l_Selection_ActionButtons_GetChecksum()
        {
            int selectionType = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectionType();

            if (selectionType == 1)
            {
                int humanId = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedSingleHumanId();
                if (!Logic_UserControl_GetHumanCommands(humanId, out SUserCommandsForHumanHandler handler, false, false))
                {
                    return 0;
                }

                return ComputeCommandListChecksum(handler);
            }

            if (selectionType == 2)
            {
                uint checksum = 0;

                int firstId = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedMultipleHumanId(0);
                if (!Logic_UserControl_GetHumanCommands(firstId, out SUserCommandsForHumanHandler handler0, true, false))
                {
                    return 0;
                }

                checksum = ComputeCommandListChecksum(handler0);

                uint count = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedMultipleHumanNumber();
                if (count < 2)
                {
                    return checksum;
                }

                for (uint i = 1; i < count; i++)
                {
                    int humanId = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedMultipleHumanId(i);
                    if (!Logic_UserControl_GetHumanCommands(humanId, out SUserCommandsForHumanHandler handlerN, true, false))
                    {
                        return checksum;
                    }

                    uint c = ComputeCommandListChecksum(handlerN);
                    checksum = checksum ^ (c + 10u);
                }

                return checksum;
            }

            if (selectionType == 4)
            {
                int vehicleId = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedSingleVehicleId();
                if (!Logic_UserControl_GetVehicleCommands(vehicleId, out SUserCommandsForVehicleHandler handler, false))
                {
                    return 0;
                }

                return ComputeCommandListChecksum(handler);
            }

            if (selectionType == 5)
            {
                uint checksum = 0;

                int firstId = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedMultipleVehicleId(0);
                if (!Logic_UserControl_GetVehicleCommands(firstId, out SUserCommandsForVehicleHandler handler0, true))
                {
                    return 0;
                }

                checksum = ComputeCommandListChecksum(handler0);

                uint count = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedMultipleVehiclesNumber();
                if (count < 2)
                {
                    return checksum;
                }

                for (uint i = 1; i < count; i++)
                {
                    int vehicleId = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedMultipleVehicleId(i);
                    if (!Logic_UserControl_GetVehicleCommands(vehicleId, out SUserCommandsForVehicleHandler handlerN, true))
                    {
                        return checksum;
                    }

                    uint c = ComputeCommandListChecksum(handlerN);
                    checksum = checksum ^ (c + 10u);
                }

                return checksum;
            }

            if (selectionType == 6)
            {
                uint houseId = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedHouseId();
                if (!Logic_UserControl_GetHouseCommands(houseId, this, out SUserCommandsForHouseHandler handler))
                {
                    return 0;
                }

                return ComputeCommandListChecksum(handler);
            }

            return 0;
        }

        // --------------------------------------------------------------------
        // Selection_CheckKey_Action(unsigned int, char)
        // Returns 1/0 in the original.
        // --------------------------------------------------------------------
        internal bool Selection_CheckKey_Action(uint keyCode, bool isDown)
        {
            // Original only handles single-human selection type == 1.
            int selectionType = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectionType();
            if (selectionType != 1)
            {
                return false;
            }

            int humanId = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedSingleHumanId();
            if (!Logic_UserControl_GetHumanCommands(humanId, out SUserCommandsForHumanHandler handler, false, false))
            {
                return false;
            }

            NC2InGameGuiKeyAssignmentManager.CKeyAssignmentManager keyMgr = NC2InGameGuiKeyAssignmentManager.CKeyAssignmentManager.sTheObjectPtr;

            // The decompile checks a small set of command slots (0x18, 0x10, 0x14, 10) and key groups (0x26, 0x27, 0x28).
            // On match it sends a GUI message with (commandId + 3000) to the default message handler.
            if (handler.IsCommandAllowed(0x18) && keyMgr.CheckKey(0x26, keyCode, isDown))
            {
                _defaultMessageHandler.XGui_BE_Message_Handle(0x800, 3000 + handler.GetCommandIdByIndex(0x18), 0, 0, 0);
                return true;
            }

            if (handler.IsCommandAllowed(0x10) && keyMgr.CheckKey(0x27, keyCode, isDown))
            {
                _defaultMessageHandler.XGui_BE_Message_Handle(0x800, 3000 + handler.GetCommandIdByIndex(0x10), 0, 0, 0);
                return true;
            }

            if (handler.IsCommandAllowed(0x14) && keyMgr.CheckKey(0x27, keyCode, isDown))
            {
                _defaultMessageHandler.XGui_BE_Message_Handle(0x800, 3000 + handler.GetCommandIdByIndex(0x14), 0, 0, 0);
                return true;
            }

            if (handler.IsCommandAllowed(10) && keyMgr.CheckKey(0x28, keyCode, isDown))
            {
                _defaultMessageHandler.XGui_BE_Message_Handle(0x800, 3000 + handler.GetCommandIdByIndex(10), 0, 0, 0);
                return true;
            }

            return false;
        }

        // --------------------------------------------------------------------
        // MiscWindows_OptionsWindow_Toggle(bool, bool)
        // --------------------------------------------------------------------
        internal void MiscWindows_OptionsWindow_Toggle(bool param1, bool param2)
        {
            NXBaseGui.CBaseWindow existing = NXBaseGui.CDesktop.Message_SendToAllWindows(
                NXBaseGui.CDesktop.sTheObjectPtr,
                0x7D8,
                param1 ? 1u : 0u,
                param2 ? 1u : 0u,
                0u);

            if (existing != null)
            {
                NXBaseGui.CDesktop.Window_AddToCloseList(NXBaseGui.CDesktop.sTheObjectPtr, existing);
                return;
            }

            MiscWindows_OptionsWindow_Open(param1, param2);
        }

        // --------------------------------------------------------------------
        // MiscWindows_OptionsWindow_Open(bool, bool)
        // --------------------------------------------------------------------
        internal void MiscWindows_OptionsWindow_Open(bool param1, bool param2)
        {
            ClearAndDeleteActionButtonElements();
            _actionButtonsChecksum = 0;

            NXBaseGui.CBaseWindow existing = NXBaseGui.CDesktop.Message_SendToAllWindows(
                NXBaseGui.CDesktop.sTheObjectPtr,
                0x7D8,
                param1 ? 1u : 0u,
                param2 ? 1u : 0u,
                0u);

            if (existing != null)
            {
                return;
            }

            MiscWindows_CloseAllLargeOnes(false);
            NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.InputMode_Set(0);

            NC2InGameGuiMiscWindows.COptionsWindow window = new NC2InGameGuiMiscWindows.COptionsWindow(param1, param2);
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(window, false);
        }

        // --------------------------------------------------------------------
        // MiscWindows_CloseAllLargeOnes(bool)
        // --------------------------------------------------------------------
        internal void MiscWindows_CloseAllLargeOnes(bool resetInputMode)
        {
            for (int i = 0; i < sWindowSearchCommandIdArray.Length; i++)
            {
                uint cmd = sWindowSearchCommandIdArray[i];
                if (cmd == 0)
                {
                    continue;
                }

                NXBaseGui.CBaseWindow wnd = NXBaseGui.CDesktop.Message_SendToAllWindows(
                    NXBaseGui.CDesktop.sTheObjectPtr,
                    cmd,
                    0u,
                    0u,
                    0u);

                if (wnd != null)
                {
                    NXBaseGui.CDesktop.Window_Close(NXBaseGui.CDesktop.sTheObjectPtr, wnd);
                }
            }

            if (resetInputMode)
            {
                NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.InputMode_Set(0);
            }
        }

        // --------------------------------------------------------------------
        // MiscWindows_GlobalGameSettingsWindow_Toggle(bool, bool)
        // --------------------------------------------------------------------
        internal void MiscWindows_GlobalGameSettingsWindow_Toggle(bool param1, bool param2)
        {
            NXBaseGui.CBaseWindow existing = NXBaseGui.CDesktop.Message_SendToAllWindows(
                NXBaseGui.CDesktop.sTheObjectPtr,
                0x7D9,
                param1 ? 1u : 0u,
                param2 ? 1u : 0u,
                0u);

            if (existing != null)
            {
                NXBaseGui.CDesktop.Window_AddToCloseList(NXBaseGui.CDesktop.sTheObjectPtr, existing);
                return;
            }

            MiscWindows_GlobalGameSettingsWindow_Open(param1, param2);
        }

        // --------------------------------------------------------------------
        // MiscWindows_GlobalGameSettingsWindow_Open(bool, bool)
        // --------------------------------------------------------------------
        internal void MiscWindows_GlobalGameSettingsWindow_Open(bool param1, bool param2)
        {
            ClearAndDeleteActionButtonElements();
            _actionButtonsChecksum = 0;

            NXBaseGui.CBaseWindow existing = NXBaseGui.CDesktop.Message_SendToAllWindows(
                NXBaseGui.CDesktop.sTheObjectPtr,
                0x7D9,
                param1 ? 1u : 0u,
                param2 ? 1u : 0u,
                0u);

            if (existing != null)
            {
                return;
            }

            MiscWindows_CloseAllLargeOnes(false);
            NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.InputMode_Set(0);

            NC2InGameGuiMiscWindows.CGlobalGameSettingsWindow window = new NC2InGameGuiMiscWindows.CGlobalGameSettingsWindow(param1, param2);
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(window, false);
        }

        // --------------------------------------------------------------------
        // MiscWindows_DiplomacyWindow_Toggle()
        // --------------------------------------------------------------------
        internal void MiscWindows_DiplomacyWindow_Toggle()
        {
            NXBaseGui.CBaseWindow existing = NXBaseGui.CDesktop.Message_SendToAllWindows(
                NXBaseGui.CDesktop.sTheObjectPtr,
                0x7E3,
                0u,
                0u,
                0u);

            if (existing != null)
            {
                NXBaseGui.CDesktop.Window_AddToCloseList(NXBaseGui.CDesktop.sTheObjectPtr, existing);
                return;
            }

            MiscWindows_DiplomacyWindow_Open();
        }

        // --------------------------------------------------------------------
        // MiscWindows_DiplomacyWindow_Open()
        // --------------------------------------------------------------------
        internal void MiscWindows_DiplomacyWindow_Open()
        {
            ClearAndDeleteActionButtonElements();
            _actionButtonsChecksum = 0;

            NXBaseGui.CBaseWindow existing = NXBaseGui.CDesktop.Message_SendToAllWindows(
                NXBaseGui.CDesktop.sTheObjectPtr,
                0x7E3,
                0u,
                0u,
                0u);

            if (existing != null)
            {
                return;
            }

            MiscWindows_CloseAllLargeOnes(false);

            // Decompile sometimes calls InputMode_Set() without args; treated as InputMode_Set(0).
            NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.InputMode_Set(0);

            NC2InGameGuiMiscWindows.CDiplomacyWindow window = new NC2InGameGuiMiscWindows.CDiplomacyWindow();
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(window, false);
        }

        // --------------------------------------------------------------------
        // MiscWindows_HumanListWindow_Toggle(TJobTypes, TJobTypes)
        // --------------------------------------------------------------------
        internal void MiscWindows_HumanListWindow_Toggle(uint jobTypeA, uint jobTypeB)
        {
            NXBaseGui.CBaseWindow existing = NXBaseGui.CDesktop.Message_SendToAllWindows(
                NXBaseGui.CDesktop.sTheObjectPtr,
                0x7E4,
                jobTypeA,
                jobTypeB,
                0u);

            if (existing != null)
            {
                NXBaseGui.CDesktop.Window_AddToCloseList(NXBaseGui.CDesktop.sTheObjectPtr, existing);
                return;
            }

            MiscWindows_HumanListWindow_Open(jobTypeA, jobTypeB);
        }

        // --------------------------------------------------------------------
        // MiscWindows_HumanListWindow_Open(TJobTypes, TJobTypes)
        // --------------------------------------------------------------------
        internal void MiscWindows_HumanListWindow_Open(uint jobTypeA, uint jobTypeB)
        {
            ClearAndDeleteActionButtonElements();
            _actionButtonsChecksum = 0;

            NXBaseGui.CBaseWindow existing = NXBaseGui.CDesktop.Message_SendToAllWindows(
                NXBaseGui.CDesktop.sTheObjectPtr,
                0x7E4,
                jobTypeA,
                jobTypeB,
                0u);

            if (existing != null)
            {
                return;
            }

            MiscWindows_CloseAllLargeOnes(false);
            NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.InputMode_Set(0);

            NC2InGameGuiMiscWindows.CHumanListWindow window = new NC2InGameGuiMiscWindows.CHumanListWindow(jobTypeA, jobTypeB);
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(window, false);
        }

        // --------------------------------------------------------------------
        // MiscWindows_StatisticsWindow_Toggle()
        // --------------------------------------------------------------------
        internal void MiscWindows_StatisticsWindow_Toggle()
        {
            NXBaseGui.CBaseWindow existing = NXBaseGui.CDesktop.Message_SendToAllWindows(
                NXBaseGui.CDesktop.sTheObjectPtr,
                0x7E5,
                0u,
                0u,
                0u);

            if (existing != null)
            {
                NXBaseGui.CDesktop.Window_AddToCloseList(NXBaseGui.CDesktop.sTheObjectPtr, existing);
                return;
            }

            MiscWindows_StatisticsWindow_Open();
        }

        // --------------------------------------------------------------------
        // MiscWindows_StatisticsWindow_Open()
        // --------------------------------------------------------------------
        internal void MiscWindows_StatisticsWindow_Open()
        {
            ClearAndDeleteActionButtonElements();
            _actionButtonsChecksum = 0;

            NXBaseGui.CBaseWindow existing = NXBaseGui.CDesktop.Message_SendToAllWindows(
                NXBaseGui.CDesktop.sTheObjectPtr,
                0x7E5,
                0u,
                0u,
                0u);

            if (existing != null)
            {
                return;
            }

            MiscWindows_CloseAllLargeOnes(false);

            // Decompile sometimes calls InputMode_Set() without args; treated as InputMode_Set(0).
            NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.InputMode_Set(0);

            NC2InGameGuiMiscWindows.CStatisticsWindow window = new NC2InGameGuiMiscWindows.CStatisticsWindow();
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(window, false);
        }

        // --------------------------------------------------------------------
        // MiscWindows_MissionInfoWindow_Toggle(unsigned int, bool)
        // --------------------------------------------------------------------
        internal void MiscWindows_MissionInfoWindow_Toggle(uint missionId, bool somethingFlag)
        {
            NXBaseGui.CBaseWindow existing = NXBaseGui.CDesktop.Message_SendToAllWindows(
                NXBaseGui.CDesktop.sTheObjectPtr,
                0x7E6,
                missionId,
                somethingFlag ? 1u : 0u,
                0u);

            if (existing != null)
            {
                NXBaseGui.CDesktop.Window_AddToCloseList(NXBaseGui.CDesktop.sTheObjectPtr, existing);
                return;
            }

            MiscWindows_MissionInfoWindow_Open(missionId, somethingFlag);
        }

        // --------------------------------------------------------------------
        // MiscWindows_MissionInfoWindow_Open(unsigned int, bool)
        // --------------------------------------------------------------------
        internal void MiscWindows_MissionInfoWindow_Open(uint missionId, bool somethingFlag)
        {
            ClearAndDeleteActionButtonElements();
            _actionButtonsChecksum = 0;

            NXBaseGui.CBaseWindow existing = NXBaseGui.CDesktop.Message_SendToAllWindows(
                NXBaseGui.CDesktop.sTheObjectPtr,
                0x7E6,
                0u,
                0u,
                0u);

            if (existing != null)
            {
                // Decompile: call into vtable with message 50000 and payload (missionId, flag).
                // Here: forward a message to that window in a managed way.
                existing.XGui_BE_Message_Handle(0x800, 50000, missionId, somethingFlag ? 1u : 0u, 0u);
                return;
            }

            MiscWindows_CloseAllLargeOnes(false);
            NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.InputMode_Set(0);

            NC2InGameGuiMiscWindows.CMissionInfoWindow window = new NC2InGameGuiMiscWindows.CMissionInfoWindow(missionId, somethingFlag);
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(window, false);
        }

        // --------------------------------------------------------------------
        // MiscWindows_MissionInfoWindow_ReloadStrings()
        // --------------------------------------------------------------------
        internal static void MiscWindows_MissionInfoWindow_ReloadStrings()
        {
            NXBaseGui.CBaseWindow existing = NXBaseGui.CDesktop.Message_SendToAllWindows(
                NXBaseGui.CDesktop.sTheObjectPtr,
                0x7E6,
                0u,
                0u,
                0u);

            if (existing != null)
            {
                existing.XGui_BE_Message_Handle(0x800, 0xC351, 0u, 0u, 0u);
            }
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private void ClearAndDeleteActionButtonElements()
        {
            NXBaseGui.CBaseElement element = (NXBaseGui.CBaseElement)_actionButtonsList.l_Base_GetStartElement();
            while (element != null)
            {
                _actionButtonsList.l_Base_RemoveElement(element);
                NXBaseGui.CDesktop.Element_Delete(NXBaseGui.CDesktop.sTheObjectPtr, element);
                element = (NXBaseGui.CBaseElement)_actionButtonsList.l_Base_GetStartElement();
            }
        }

        private static uint ComputeCommandListChecksum(SUserCommandsForHumanHandler handler)
        {
            // Stable FNV-1a over allowed command IDs in handler order.
            uint hash = 2166136261u;
            int count = handler.Count;

            for (int i = 0; i < count; i++)
            {
                int cmd = handler.GetCommandIdByIndex(i);
                unchecked
                {
                    hash ^= (uint)cmd;
                    hash *= 16777619u;
                }
            }

            // Include count to reduce collisions.
            unchecked
            {
                hash ^= (uint)count;
                hash *= 16777619u;
            }

            return hash;
        }

        private static uint ComputeCommandListChecksum(SUserCommandsForVehicleHandler handler)
        {
            uint hash = 2166136261u;
            int count = handler.Count;

            for (int i = 0; i < count; i++)
            {
                int cmd = handler.GetCommandIdByIndex(i);
                unchecked
                {
                    hash ^= (uint)cmd;
                    hash *= 16777619u;
                }
            }

            unchecked
            {
                hash ^= (uint)count;
                hash *= 16777619u;
            }

            return hash;
        }

        private static uint ComputeCommandListChecksum(SUserCommandsForHouseHandler handler)
        {
            uint hash = 2166136261u;
            int count = handler.Count;

            for (int i = 0; i < count; i++)
            {
                int cmd = handler.GetCommandIdByIndex(i);
                unchecked
                {
                    hash ^= (uint)cmd;
                    hash *= 16777619u;
                }
            }

            unchecked
            {
                hash ^= (uint)count;
                hash *= 16777619u;
            }

            return hash;
        }

        // ====================================================================
        // Builders (human/vehicle/house)
        // ====================================================================

        private void BuildHumanActionButtons(bool isMultiSelection)
        {
            // 1) Get command set for selection (intersection for multi).
            SUserCommandsForHumanHandler commands = GetHumanCommandsForCurrentSelection(isMultiSelection, out bool ok);
            if (!ok || commands.Count == 0)
            {
                return;
            }

            // 2) Sort by priority like the original.
            commands.SortCommandsByPriority();

            // 3) Count per group-type (0..4) like original local_a8[5] counters.
            int[] groupCounts = new int[5];
            int count = commands.Count;

            for (int i = 0; i < count; i++)
            {
                int commandId = commands.GetCommandIdByIndex(i);
                uint group = Tool_UserHumanCommandTypes_GetGroupType(commandId);
                if (group < (uint)groupCounts.Length)
                {
                    groupCounts[group] = groupCounts[group] + 1;
                }
            }

            // 4) Define base rect around mouse and clamp inside desktop.
            SRectangle rect = new SRectangle(_actionButtonsMouseX - 0x74, _actionButtonsMouseY - 0x74, 0xE8, 0xE8);
            rect.PlaceInside(NXBaseGui.CDesktop.sTheObjectPtr);

            // 5) For each group 0..4: compute starting anchor and stepping vectors and create gfx buttons.
            // Matches decompile constants (100 px offsets, 0x20 steps, +/-5 corner nudges).
            for (uint groupIndex = 0; groupIndex < 5; groupIndex++)
            {
                int centerX = rect.X + (rect.Width / 2);
                int centerY = rect.Y + (rect.Height / 2);

                int x = centerX;
                int y = centerY;

                int stepX = 0;
                int stepY = 0;
                int cornerBiasX = 0;
                int cornerBiasY = 0;

                int groupCount = groupCounts[groupIndex];

                if (groupIndex == 0)
                {
                    y = centerY + 100;
                    x = centerX + (groupCount * -0x10) + 0x10;
                    stepX = 0x20;
                    cornerBiasY = -5;
                }
                else if (groupIndex == 1)
                {
                    y = centerY - 100;
                    x = centerX + (groupCount * 0x10) - 0x10;
                    stepX = -0x20;
                    cornerBiasY = 5;
                }
                else if (groupIndex == 2)
                {
                    x = centerX + 100;
                    y = centerY + (groupCount * -0x10) + 0x10;
                    stepY = 0x20;
                    cornerBiasX = -5;
                }
                else if (groupIndex == 3)
                {
                    x = (centerX - 100);
                    y = centerY + (groupCount * 0x10) - 0x10;
                    stepY = -0x20;
                    cornerBiasX = 5;
                }
                else
                {
                    x = (centerX - 0x44);
                    y = centerY + (groupCount * 0x10) - 0x10;
                    stepY = -0x20;
                    cornerBiasX = 5;
                }

                int createdInGroup = 0;

                for (int i = 0; i < count; i++)
                {
                    int commandId = commands.GetCommandIdByIndex(i);
                    uint group = Tool_UserHumanCommandTypes_GetGroupType(commandId);
                    if (group != groupIndex)
                    {
                        continue;
                    }

                    string tooltip = NC2GuiToolsBase.StringTool_GetLogicUserCommandsForHumanTypeNamePtr(commandId);

                    uint gfxId = 0x6Bu;
                    if ((uint)(commandId - 3) < 0x2B)
                    {
                        gfxId = NC2InGameGuiBaseTables.ToolCommandToGfxId(commandId);
                    }

                    SRectangle buttonRect = new SRectangle(x - 0x10, y - 0x10, 0x20, 0x20);

                    // Corner nudges on first/last in a group (matches local_d4 logic in decompile).
                    if (createdInGroup == 0 || createdInGroup == groupCount - 1)
                    {
                        buttonRect.X = buttonRect.X + cornerBiasX;
                        buttonRect.Y = buttonRect.Y + cornerBiasY;
                    }

                    NC2InGameGuiBase.CBaseToolGfxButton button = new NC2InGameGuiBase.CBaseToolGfxButton(
                        buttonRect,
                        gfxId,
                        tooltip,
                        commandId + 3000,
                        _defaultMessageHandler);

                    button.SetCenterGraphicsFlag(true);
                    button.SetPalettePtr(NC2InGameGuiBaseTables.ToolPaletteOverlayButtons);

                    NC2InGameGuiBase.BaseToolDesktop_AddOverlayElement(button);
                    _actionButtonsList.InsertAtEnd(button);

                    x = x + stepX;
                    y = y + stepY;

                    createdInGroup++;
                }
            }
        }

        private void BuildVehicleActionButtons(bool isMultiSelection)
        {
            SUserCommandsForVehicleHandler commands = GetVehicleCommandsForCurrentSelection(isMultiSelection, out bool ok);
            if (!ok || commands.Count == 0)
            {
                return;
            }

            // Decompile layout:
            // rect: x = mouseX - 0x104, y = mouseY + (count * -0xB), w = 0xF0, h = count * 0x16
            SRectangle rect = new SRectangle(_actionButtonsMouseX - 0x104, _actionButtonsMouseY - (commands.Count * 0x0B), 0xF0, commands.Count * 0x16);
            rect.PlaceInside(NXBaseGui.CDesktop.sTheObjectPtr);

            int yStep = 0x16;

            for (int i = 0; i < commands.Count; i++)
            {
                int commandId = commands.GetCommandIdByIndex(i);
                string text = NC2GuiToolsBase.StringTool_GetLogicUserCommandsForVehicleTypeNamePtr(commandId);

                SRectangle rowRect = new SRectangle(rect.X, rect.Y + (i * yStep), rect.Width, yStep);

                NC2InGameGuiBase.CBaseToolTextButton button = new NC2InGameGuiBase.CBaseToolTextButton(
                    rowRect,
                    text,
                    commandId + 0xE10,
                    _defaultMessageHandler);

                NC2InGameGuiBase.BaseToolDesktop_AddOverlayElement(button);
                _actionButtonsList.InsertAtEnd(button);
            }
        }

        private void BuildHouseActionButtons()
        {
            uint houseId = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedHouseId();
            if (!Logic_UserControl_GetHouseCommands(houseId, this, out SUserCommandsForHouseHandler commands))
            {
                return;
            }

            if (commands.Count == 0)
            {
                return;
            }

            SRectangle rect = new SRectangle(_actionButtonsMouseX - 0x104, _actionButtonsMouseY - (commands.Count * 0x0B), 0xF0, commands.Count * 0x16);
            rect.PlaceInside(NXBaseGui.CDesktop.sTheObjectPtr);

            int yStep = 0x16;

            for (int i = 0; i < commands.Count; i++)
            {
                int commandId = commands.GetCommandIdByIndex(i);
                string text = NC2GuiToolsBase.StringTool_GetLogicUserCommandsForHouseTypeNamePtr(commandId);

                SRectangle rowRect = new SRectangle(rect.X, rect.Y + (i * yStep), rect.Width, yStep);

                NC2InGameGuiBase.CBaseToolTextButton button = new NC2InGameGuiBase.CBaseToolTextButton(
                    rowRect,
                    text,
                    commandId + 0xCE4,
                    _defaultMessageHandler);

                NC2InGameGuiBase.BaseToolDesktop_AddOverlayElement(button);
                _actionButtonsList.InsertAtEnd(button);
            }
        }

        private SUserCommandsForHumanHandler GetHumanCommandsForCurrentSelection(bool isMultiSelection, out bool ok)
        {
            ok = false;

            if (!isMultiSelection)
            {
                int humanId = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedSingleHumanId();
                if (!Logic_UserControl_GetHumanCommands(humanId, out SUserCommandsForHumanHandler handler, false, false))
                {
                    return default;
                }

                ok = true;
                return handler;
            }

            int firstId = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedMultipleHumanId(0);
            if (!Logic_UserControl_GetHumanCommands(firstId, out SUserCommandsForHumanHandler merged, true, false))
            {
                return default;
            }

            uint count = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedMultipleHumanNumber();
            if (count > 1)
            {
                for (uint i = 1; i < count; i++)
                {
                    int humanId = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedMultipleHumanId(i);
                    if (!Logic_UserControl_GetHumanCommands(humanId, out SUserCommandsForHumanHandler other, true, false))
                    {
                        ok = false;
                        return default;
                    }

                    merged.IntersectAllowedWith(other);
                }

                // Decompile removes commandId == 0x0C from multi-human sets.
                merged.RemoveCommandId(0x0C);
            }

            ok = merged.Count > 0;
            return merged;
        }

        private SUserCommandsForVehicleHandler GetVehicleCommandsForCurrentSelection(bool isMultiSelection, out bool ok)
        {
            ok = false;

            if (!isMultiSelection)
            {
                int vehicleId = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedSingleVehicleId();
                if (!Logic_UserControl_GetVehicleCommands(vehicleId, out SUserCommandsForVehicleHandler handler, false))
                {
                    return default;
                }

                ok = true;
                return handler;
            }

            int firstId = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedMultipleVehicleId(0);
            if (!Logic_UserControl_GetVehicleCommands(firstId, out SUserCommandsForVehicleHandler merged, true))
            {
                return default;
            }

            uint count = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedMultipleVehiclesNumber();
            if (count > 1)
            {
                for (uint i = 1; i < count; i++)
                {
                    int vehicleId = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.Selection_GetSelectedMultipleVehicleId(i);
                    if (!Logic_UserControl_GetVehicleCommands(vehicleId, out SUserCommandsForVehicleHandler other, true))
                    {
                        ok = false;
                        return default;
                    }

                    merged.IntersectAllowedWith(other);
                }
            }

            ok = merged.Count > 0;
            return merged;
        }

        private void Selection_ActionButtons_ClearListAndDeleteElements()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseElement? element = _selectionActionButtonsList.l_Base_GetStartElement();
            while (element != null)
            {
                _selectionActionButtonsList.l_Base_RemoveElement(element);
                desktop.Element_Delete(element);
                element = _selectionActionButtonsList.l_Base_GetStartElement();
            }
        }

        private static void IntersectAllowedHumanCommands(ref SUserCommandsForHumanHandler baseHandler, in SUserCommandsForHumanHandler otherHandler)
        {
            int i = 0;
            while (i < baseHandler.CommandCount)
            {
                int commandType = baseHandler.GetCommandTypeAt(i);
                if (!otherHandler.IsCommandAllowed(commandType))
                {
                    baseHandler.RemoveCommandAt(i);
                    continue;
                }

                i++;
            }
        }

        private static void IntersectAllowedVehicleCommands(ref SUserCommandsForVehicleHandler baseHandler, in SUserCommandsForVehicleHandler otherHandler)
        {
            int i = 0;
            while (i < baseHandler.CommandCount)
            {
                int commandType = baseHandler.GetCommandTypeAt(i);
                if (!otherHandler.IsCommandAllowed(commandType))
                {
                    baseHandler.RemoveCommandAt(i);
                    continue;
                }

                i++;
            }
        }

        private static void RemoveHumanCommandType(ref SUserCommandsForHumanHandler handler, int commandTypeToRemove)
        {
            int i = 0;
            while (i < handler.CommandCount)
            {
                if (handler.GetCommandTypeAt(i) == commandTypeToRemove)
                {
                    handler.RemoveCommandAt(i);
                    continue;
                }

                i++;
            }
        }

        private static int GetHumanCommandIconId(int humanCommandType)
        {
            // Decompile: default 0x6B, else lookup table DAT_1003337c8 indexed by (type - 3), range < 0x2B.
            // Replace with the real table when available.
            if ((uint)(humanCommandType - 3) < 0x2B)
            {
                return sHumanCommandTypeToIconId[humanCommandType - 3];
            }

            return 0x6B;
        }

        // Placeholder: must be filled with the real values from the binary/data.
        private static readonly int[] sHumanCommandTypeToIconId = new int[0x2B]
        {
            // TODO: fill with DAT_1003337c8 contents
            0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,
            0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,
            0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,
            0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,0x6B,
            0x6B,0x6B,0x6B
        };

        // Positions per “group” around the cursor, matching the switch(uVar25) layout in spirit.
        private static void GetRadialGroupLayout(int groupIndex, int groupCount, int centerX, int centerY, out int startX, out int startY, out int stepX, out int stepY, out int edgeAdjustX, out int edgeAdjustY)
        {
            // The original does 5 groups (0..4) and uses +/-100 offsets and 0x20 step, plus -5/+5 edge tweaks.
            // This is a faithful simplification that produces the same overall layout.
            startX = centerX;
            startY = centerY;
            stepX = 0;
            stepY = 0;
            edgeAdjustX = 0;
            edgeAdjustY = 0;

            // Each button is 0x20x0x20, the code builds local rect at (pos-0x10, pos-0x10).
            // Here we work in “center of button” coordinates.
            if (groupIndex == 0)
            {
                startY = centerY + 100;
                startX = centerX - (groupCount * 16) + 16;
                stepX = 32;
                edgeAdjustX = -5;
            }
            else if (groupIndex == 1)
            {
                startY = centerY - 100;
                startX = centerX + (groupCount * 16) - 16;
                stepX = -32;
                edgeAdjustX = 5;
            }
            else if (groupIndex == 2)
            {
                startX = centerX + 100;
                startY = centerY - (groupCount * 16) + 16;
                stepY = 32;
                edgeAdjustY = -5;
            }
            else if (groupIndex == 3)
            {
                startX = centerX - 100;
                startY = centerY + (groupCount * 16) - 16;
                stepY = -32;
                edgeAdjustY = 5;
            }
            else if (groupIndex == 4)
            {
                startX = centerX - 0x44; // special “offset left” group in the original
                startY = centerY + (groupCount * 16) - 16;
                stepY = -32;
                edgeAdjustY = 5;
            }
        }

        // --------------------------------------------------------------------
        // Part 2: Selection action buttons
        // --------------------------------------------------------------------

        internal void Selection_ActionButtons_BringUp()
        {
            Selection_ActionButtons_ClearListAndDeleteElements();

            _selectionActionButtonsChecksum = 0;

            NXSysMouseManager.CMouseManager? mouse = NXSysMouseManager.CMouseManager.sTheObjectPtr;
            if (mouse != null)
            {
                _selectionActionButtonsMouseX = mouse.X;
                _selectionActionButtonsMouseY = mouse.Y;
            }

            l_Selection_ActionButtons_BringUp();
        }

        internal void Selection_ActionButtons_Remove()
        {
            Selection_ActionButtons_ClearListAndDeleteElements();
            _selectionActionButtonsChecksum = 0;
        }

        private void l_Selection_ActionButtons_BringUp()
        {
            NC2InGameGuiInputManager.CInputManager input = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr;
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            int selectionMode = input.SelectionMode_Get(); // decompile: *(sTheObjectPtr+4)

            if (selectionMode == 1 || selectionMode == 2)
            {
                SUserCommandsForHumanHandler commands = default;

                int firstHumanId;
                bool multi;
                if (selectionMode == 1)
                {
                    firstHumanId = input.Selection_GetSelectedSingleHumanId();
                    multi = false;
                    if (!Logic_UserControl_GetHumanCommands(firstHumanId, ref commands, false, false))
                    {
                        return;
                    }
                }
                else
                {
                    firstHumanId = input.Selection_GetSelectedMultipleHumanId(0);
                    multi = true;
                    if (!Logic_UserControl_GetHumanCommands(firstHumanId, ref commands, true, false))
                    {
                        return;
                    }

                    uint count = input.Selection_GetSelectedMultipleHumanNumber();
                    if (count > 1)
                    {
                        for (uint i = 1; i < count; i++)
                        {
                            int humanId = input.Selection_GetSelectedMultipleHumanId(i);

                            SUserCommandsForHumanHandler other = default;
                            if (!Logic_UserControl_GetHumanCommands(humanId, ref other, true, false))
                            {
                                return;
                            }

                            IntersectAllowedHumanCommands(ref commands, in other);
                            if (commands.CommandCount == 0)
                            {
                                return;
                            }
                        }

                        // Decompile removes command type 0x0C after intersection.
                        RemoveHumanCommandType(ref commands, 0x0C);
                        if (commands.CommandCount == 0)
                        {
                            return;
                        }
                    }
                }

                commands.SortCommandsByPriority();

                // Count per group type (0..4).
                int[] groupCounts = new int[5];
                for (int i = 0; i < commands.CommandCount; i++)
                {
                    int cmdType = commands.GetCommandTypeAt(i);
                    int group = Tool_UserHumanCommandTypes_GetGroupType(cmdType);
                    if ((uint)group < (uint)groupCounts.Length)
                    {
                        groupCounts[group]++;
                    }
                }

                // Popup rect around stored mouse position: (x-0x74, y-0x74, 0xE8, 0xE8), then clamped inside desktop.
                SRectangle menuRect = new SRectangle(_selectionActionButtonsMouseX - 0x74, _selectionActionButtonsMouseY - 0x74, 0xE8, 0xE8);
                menuRect.PlaceInside(desktop);

                int centerX = menuRect.X + (menuRect.Width / 2);
                int centerY = menuRect.Y + (menuRect.Height / 2);

                // Create buttons per group in “radial” layout.
                for (int groupIndex = 0; groupIndex < 5; groupIndex++)
                {
                    int countInGroup = groupCounts[groupIndex];
                    if (countInGroup <= 0)
                    {
                        continue;
                    }

                    GetRadialGroupLayout(groupIndex, countInGroup, centerX, centerY, out int cursorX, out int cursorY, out int stepX, out int stepY, out int edgeAdjustX, out int edgeAdjustY);

                    int createdInGroup = 0;

                    for (int i = 0; i < commands.CommandCount; i++)
                    {
                        int humanCommandType = commands.GetCommandTypeAt(i);
                        int group = Tool_UserHumanCommandTypes_GetGroupType(humanCommandType);
                        if (group != groupIndex)
                        {
                            continue;
                        }

                        string tooltip = NC2GuiToolsBase.StringTool_GetLogicUserCommandsForHumanTypeName(humanCommandType);

                        int iconId = GetHumanCommandIconId(humanCommandType);

                        // Button rect is 0x20x0x20 centered on (cursorX, cursorY).
                        SRectangle buttonRect = new SRectangle(cursorX - 0x10, cursorY - 0x10, 0x20, 0x20);

                        // Decompile: if first or last in group, apply +/-5 adjustment on one axis.
                        if (createdInGroup == 0 || createdInGroup == countInGroup - 1)
                        {
                            buttonRect = new SRectangle(buttonRect.X + edgeAdjustY, buttonRect.Y + edgeAdjustX, buttonRect.Width, buttonRect.Height);
                        }

                        NC2InGameGuiBase.CBaseToolGfxButton button = new NC2InGameGuiBase.CBaseToolGfxButton(buttonRect, iconId, tooltip, humanCommandType + 3000, _defaultMessageHandler);

                        button.SetCenterGraphicsFlag(true);

                        // Decompile: this_00[0x68]=1 and SetPalettePtr(DAT_1003a44b8)
                        button.SetSomeInternalFlag_0x68(true);
                        button.SetPalettePtr(NC2InGameGuiBase.Palettes.ActionButtonPalette);

                        NC2InGameGuiBase.BaseToolDesktop_AddOverlayElement(button);
                        _selectionActionButtonsList.InsertAtEnd(button);

                        cursorX += stepX;
                        cursorY += stepY;
                        createdInGroup++;
                    }
                }

                _selectionActionButtonsChecksum = l_Selection_ActionButtons_GetChecksum();
                return;
            }

            if (selectionMode == 4 || selectionMode == 5 || selectionMode == 6)
            {
                // Vehicle/House: the original creates a vertical list of text buttons (0xF0 wide, row height 0x16).
                // For multi-selection it intersects allowed commands similarly.
                // For house it asks Logic_UserControl_GetHouseCommands(..., *sTheObjectPtr, &handler).

                int rowHeight = 0x16;

                if (selectionMode == 4)
                {
                    int vehicleId = input.Selection_GetSelectedSingleVehicleId();
                    SUserCommandsForVehicleHandler veh = default;
                    if (!Logic_UserControl_GetVehicleCommands(vehicleId, ref veh, false))
                    {
                        return;
                    }

                    CreateVehicleOrHouseTextButtons(desktop, in veh, rowHeight, isHouse: false);
                    _selectionActionButtonsChecksum = l_Selection_ActionButtons_GetChecksum();
                    return;
                }

                if (selectionMode == 5)
                {
                    int firstVehicleId = input.Selection_GetSelectedMultipleVehicleId(0);
                    SUserCommandsForVehicleHandler veh = default;
                    if (!Logic_UserControl_GetVehicleCommands(firstVehicleId, ref veh, true))
                    {
                        return;
                    }

                    uint count = input.Selection_GetSelectedMultipleVehiclesNumber();
                    if (count > 1)
                    {
                        for (uint i = 1; i < count; i++)
                        {
                            int vehicleId = input.Selection_GetSelectedMultipleVehicleId(i);
                            SUserCommandsForVehicleHandler other = default;
                            if (!Logic_UserControl_GetVehicleCommands(vehicleId, ref other, true))
                            {
                                return;
                            }

                            IntersectAllowedVehicleCommands(ref veh, in other);
                            if (veh.CommandCount == 0)
                            {
                                return;
                            }
                        }
                    }

                    CreateVehicleOrHouseTextButtons(desktop, in veh, rowHeight, isHouse: false);
                    _selectionActionButtonsChecksum = l_Selection_ActionButtons_GetChecksum();
                    return;
                }

                if (selectionMode == 6)
                {
                    uint houseId = input.Selection_GetSelectedHouseId();
                    SUserCommandsForHouseHandler house = default;

                    // decompile: Logic_UserControl_GetHouseCommands(houseId, *sTheObjectPtr, &local_140)
                    if (!Logic_UserControl_GetHouseCommands(houseId, CGuiManager.sTheObjectPtr, ref house))
                    {
                        return;
                    }

                    CreateVehicleOrHouseTextButtons(desktop, in house, rowHeight, isHouse: true);
                    _selectionActionButtonsChecksum = l_Selection_ActionButtons_GetChecksum();
                    return;
                }
            }

            _selectionActionButtonsChecksum = l_Selection_ActionButtons_GetChecksum();
        }

        private void CreateVehicleOrHouseTextButtons(NXBaseGui.CDesktop desktop, in IUserCommandsTextList handler, int rowHeight, bool isHouse)
        {
            // Rect: (mouseX-0x104, mouseY + (count*-0xB), 0xF0, count*0x16), then PlaceInside.
            int count = handler.CommandCount;
            if (count <= 0)
            {
                return;
            }

            SRectangle rect = new SRectangle(_selectionActionButtonsMouseX - 0x104, (_selectionActionButtonsMouseY + (count * -0x0B)), 0xF0, count * rowHeight);
            rect.PlaceInside(desktop);

            int y = rect.Y;

            for (int i = 0; i < count; i++)
            {
                int cmdType = handler.GetCommandTypeAt(i);

                string label;
                int commandId;
                if (isHouse)
                {
                    label = NC2GuiToolsBase.StringTool_GetLogicUserCommandsForHouseTypeName(cmdType);
                    commandId = cmdType + 0x0CE4;
                }
                else
                {
                    label = NC2GuiToolsBase.StringTool_GetLogicUserCommandsForVehicleTypeName(cmdType);
                    commandId = cmdType + 0x0E10;
                }

                SRectangle rowRect = new SRectangle(rect.X, y, rect.Width, rowHeight);

                NC2InGameGuiBase.CBaseToolTextButton button = new NC2InGameGuiBase.CBaseToolTextButton(rowRect, label, commandId, _defaultMessageHandler);
                NC2InGameGuiBase.BaseToolDesktop_AddOverlayElement(button);
                _selectionActionButtonsList.InsertAtEnd(button);

                y += rowHeight;
            }
        }

        private uint l_Selection_ActionButtons_GetChecksum()
        {
            NC2InGameGuiInputManager.CInputManager input = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr;
            int selectionMode = input.SelectionMode_Get();

            if (selectionMode == 1)
            {
                int humanId = input.Selection_GetSelectedSingleHumanId();
                SUserCommandsForHumanHandler h = default;
                if (!Logic_UserControl_GetHumanCommands(humanId, ref h, false, false))
                {
                    return 0;
                }

                return XB_GetMemoryChecksum(h.AsByteSpan());
            }

            if (selectionMode == 2)
            {
                int humanId = input.Selection_GetSelectedMultipleHumanId(0);
                SUserCommandsForHumanHandler h0 = default;
                if (!Logic_UserControl_GetHumanCommands(humanId, ref h0, true, false))
                {
                    return 0;
                }

                uint checksum = XB_GetMemoryChecksum(h0.AsByteSpan());

                uint count = input.Selection_GetSelectedMultipleHumanNumber();
                if (count < 2)
                {
                    return checksum;
                }

                for (uint i = 1; i < count; i++)
                {
                    int id = input.Selection_GetSelectedMultipleHumanId(i);
                    SUserCommandsForHumanHandler h = default;
                    if (!Logic_UserControl_GetHumanCommands(id, ref h, true, false))
                    {
                        return checksum;
                    }

                    uint c = XB_GetMemoryChecksum(h.AsByteSpan());
                    checksum = checksum ^ (c + 10U);
                }

                return checksum;
            }

            if (selectionMode == 4)
            {
                int vehicleId = input.Selection_GetSelectedSingleVehicleId();
                SUserCommandsForVehicleHandler v = default;
                if (!Logic_UserControl_GetVehicleCommands(vehicleId, ref v, false))
                {
                    return 0;
                }

                return XB_GetMemoryChecksum(v.AsByteSpan());
            }

            if (selectionMode == 5)
            {
                int vehicleId = input.Selection_GetSelectedMultipleVehicleId(0);
                SUserCommandsForVehicleHandler v0 = default;
                if (!Logic_UserControl_GetVehicleCommands(vehicleId, ref v0, true))
                {
                    return 0;
                }

                uint checksum = XB_GetMemoryChecksum(v0.AsByteSpan());

                uint count = input.Selection_GetSelectedMultipleVehiclesNumber();
                if (count < 2)
                {
                    return checksum;
                }

                for (uint i = 1; i < count; i++)
                {
                    int id = input.Selection_GetSelectedMultipleVehicleId(i);
                    SUserCommandsForVehicleHandler v = default;
                    if (!Logic_UserControl_GetVehicleCommands(id, ref v, true))
                    {
                        return checksum;
                    }

                    uint c = XB_GetMemoryChecksum(v.AsByteSpan());
                    checksum = checksum ^ (c + 10U);
                }

                return checksum;
            }

            if (selectionMode == 6)
            {
                uint houseId = input.Selection_GetSelectedHouseId();
                SUserCommandsForHouseHandler h = default;
                if (!Logic_UserControl_GetHouseCommands(houseId, CGuiManager.sTheObjectPtr, ref h))
                {
                    return 0;
                }

                return XB_GetMemoryChecksum(h.AsByteSpan());
            }

            return 0;
        }

        internal bool Selection_CheckKey_Action(uint key, char keyChar)
        {
            NC2InGameGuiInputManager.CInputManager input = NC2InGameGuiInputManager.CInputManager.sTheObjectPtr;
            NC2InGameGuiKeyAssignmentManager.CKeyAssignmentManager keys = NC2InGameGuiKeyAssignmentManager.CKeyAssignmentManager.sTheObjectPtr;

            if (input.SelectionMode_Get() != 1)
            {
                return false;
            }

            int humanId = input.Selection_GetSelectedSingleHumanId();

            SUserCommandsForHumanHandler commands = default;
            if (!Logic_UserControl_GetHumanCommands(humanId, ref commands, false, false))
            {
                return false;
            }

            // Decompile tries command slots in this order:
            // 0x18 -> assignment 0x26
            // 0x10 -> assignment 0x27
            // 0x14 -> assignment 0x27
            // 10   -> assignment 0x28
            // and then sends message with a command-id mapping pointer.
            // In managed code: resolve which “action” key triggered and send the corresponding command id.

            int commandToSend;
            if (commands.IsCommandAllowed(0x18) && keys.CheckKey(0x26, key, keyChar))
            {
                commandToSend = SelectionKeyCommandIds.CommandA; // TODO: DAT mapping
            }
            else if (commands.IsCommandAllowed(0x10) && keys.CheckKey(0x27, key, keyChar))
            {
                commandToSend = SelectionKeyCommandIds.CommandB; // TODO
            }
            else if (commands.IsCommandAllowed(0x14) && keys.CheckKey(0x27, key, keyChar))
            {
                commandToSend = SelectionKeyCommandIds.CommandC; // TODO
            }
            else if (commands.IsCommandAllowed(10) && keys.CheckKey(0x28, key, keyChar))
            {
                commandToSend = SelectionKeyCommandIds.CommandD; // TODO
            }
            else
            {
                return false;
            }

            _defaultMessageHandler.XGui_BE_Message_Handle(0x800, commandToSend + 3000, 0, 0, 0, 0);
            return true;
        }

        // Placeholder for the DAT_1003336a8/... mapping in the decompile.
        private static class SelectionKeyCommandIds
        {
            internal const int CommandA = 0; // TODO
            internal const int CommandB = 1; // TODO
            internal const int CommandC = 2; // TODO
            internal const int CommandD = 3; // TODO
        }

        // --------------------------------------------------------------------
        // Part 2: Misc windows (Options / GlobalSettings / Diplomacy / HumanList / Statistics / MissionInfo)
        // --------------------------------------------------------------------

        internal void MiscWindows_OptionsWindow_Toggle(bool a, bool b)
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7D8, a ? 1U : 0U, b ? 1U : 0U, 0);
            if (win != null)
            {
                desktop.Window_AddToCloseList(win);
                return;
            }

            MiscWindows_OptionsWindow_Open(a, b);
        }

        internal void MiscWindows_OptionsWindow_Open(bool a, bool b)
        {
            Selection_ActionButtons_ClearListAndDeleteElements();
            _selectionActionButtonsChecksum = 0;

            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;
            if (desktop.Message_SendToAllWindows(0x7D8, a ? 1U : 0U, b ? 1U : 0U, 0) != 0)
            {
                return;
            }

            MiscWindows_CloseAllLargeOnes(true);

            NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.InputMode_Set(0);

            NC2InGameGuiMiscWindows.COptionsWindow win = new NC2InGameGuiMiscWindows.COptionsWindow(a, b);
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(win, false);
        }

        internal void MiscWindows_CloseAllLargeOnes(bool setInputModeTo0)
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            for (int i = 0; i < sWindowSearchCommandIdArray.Length; i++)
            {
                uint searchId = sWindowSearchCommandIdArray[i];
                NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(searchId, 0, 0, 0);
                if (win != null)
                {
                    desktop.Window_Close(win);
                }
            }

            if (setInputModeTo0)
            {
                NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.InputMode_Set(0);
            }
        }

        internal void MiscWindows_GlobalGameSettingsWindow_Toggle(bool a, bool b)
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7D9, a ? 1U : 0U, b ? 1U : 0U, 0);
            if (win != null)
            {
                desktop.Window_AddToCloseList(win);
                return;
            }

            MiscWindows_GlobalGameSettingsWindow_Open(a, b);
        }

        internal void MiscWindows_GlobalGameSettingsWindow_Open(bool a, bool b)
        {
            Selection_ActionButtons_ClearListAndDeleteElements();
            _selectionActionButtonsChecksum = 0;

            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;
            if (desktop.Message_SendToAllWindows(0x7D9, a ? 1U : 0U, b ? 1U : 0U, 0) != 0)
            {
                return;
            }

            MiscWindows_CloseAllLargeOnes(true);

            NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.InputMode_Set(0);

            NC2InGameGuiMiscWindows.CGlobalGameSettingsWindow win = new NC2InGameGuiMiscWindows.CGlobalGameSettingsWindow(a, b);
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(win, false);
        }

        internal void MiscWindows_DiplomacyWindow_Toggle()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7E3, 0, 0, 0);
            if (win != null)
            {
                desktop.Window_AddToCloseList(win);
                return;
            }

            MiscWindows_DiplomacyWindow_Open();
        }

        internal void MiscWindows_DiplomacyWindow_Open()
        {
            Selection_ActionButtons_ClearListAndDeleteElements();
            _selectionActionButtonsChecksum = 0;

            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;
            if (desktop.Message_SendToAllWindows(0x7E3, 0, 0, 0) != 0)
            {
                return;
            }

            MiscWindows_CloseAllLargeOnes(true);

            NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.InputMode_Set(0);

            NC2InGameGuiMiscWindows.CDiplomacyWindow win = new NC2InGameGuiMiscWindows.CDiplomacyWindow();
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(win, false);
        }

        internal void MiscWindows_HumanListWindow_Toggle(uint jobA, uint jobB)
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7E4, jobA, jobB, 0);
            if (win != null)
            {
                desktop.Window_AddToCloseList(win);
                return;
            }

            MiscWindows_HumanListWindow_Open(jobA, jobB);
        }

        internal void MiscWindows_HumanListWindow_Open(uint jobA, uint jobB)
        {
            Selection_ActionButtons_ClearListAndDeleteElements();
            _selectionActionButtonsChecksum = 0;

            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;
            if (desktop.Message_SendToAllWindows(0x7E4, jobA, jobB, 0) != 0)
            {
                return;
            }

            MiscWindows_CloseAllLargeOnes(true);

            NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.InputMode_Set(0);

            NC2InGameGuiMiscWindows.CHumanListWindow win = new NC2InGameGuiMiscWindows.CHumanListWindow(jobA, jobB);
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(win, false);
        }

        internal void MiscWindows_StatisticsWindow_Toggle()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7E5, 0, 0, 0);
            if (win != null)
            {
                desktop.Window_AddToCloseList(win);
                return;
            }

            MiscWindows_StatisticsWindow_Open();
        }

        internal void MiscWindows_StatisticsWindow_Open()
        {
            Selection_ActionButtons_ClearListAndDeleteElements();
            _selectionActionButtonsChecksum = 0;

            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;
            if (desktop.Message_SendToAllWindows(0x7E5, 0, 0, 0) != 0)
            {
                return;
            }

            MiscWindows_CloseAllLargeOnes(false);

            NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.InputMode_Set(0);

            NC2InGameGuiMiscWindows.CStatisticsWindow win = new NC2InGameGuiMiscWindows.CStatisticsWindow();
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(win, false);
        }

        internal void MiscWindows_MissionInfoWindow_Toggle(uint missionId, bool modeFlag)
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7E6, missionId, modeFlag ? 1U : 0U, 0);
            if (win != null)
            {
                desktop.Window_AddToCloseList(win);
                return;
            }

            MiscWindows_MissionInfoWindow_Open(missionId, modeFlag);
        }

        internal void MiscWindows_MissionInfoWindow_Open(uint missionId, bool modeFlag)
        {
            Selection_ActionButtons_ClearListAndDeleteElements();
            _selectionActionButtonsChecksum = 0;

            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? existing = desktop.Message_SendToAllWindows_FindFirst(0x7E6, 0, 0, 0);
            if (existing != null)
            {
                existing.XGui_BE_Message_Handle(0x800, 50000, missionId, modeFlag ? 1U : 0U, 0, 0);
                return;
            }

            MiscWindows_CloseAllLargeOnes(true);

            NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.InputMode_Set(0);

            NC2InGameGuiMiscWindows.CMissionInfoWindow win = new NC2InGameGuiMiscWindows.CMissionInfoWindow(missionId, modeFlag);
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(win, false);
        }

        internal void MiscWindows_MissionInfoWindow_ReloadStrings()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;
            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7E6, 0, 0, 0);
            if (win != null)
            {
                win.XGui_BE_Message_Handle(0x800, 0xC351, 0, 0, 0, 0);
            }
        }

        // Placeholder: the decompile iterates 0x28 bytes (10 entries) of window-search command ids.
        private static readonly uint[] sWindowSearchCommandIdArray = new uint[10]
        {
            // TODO: fill with MiscWindows_CloseAllLargeOnes::sWindowSearchCommandIdArray
            0,0,0,0,0,0,0,0,0,0
        };

        // --------------------------------------------------------------------
        // Part 3: TechTree / Help / LargeOverview / ConstructionSelection / StringOutput / Change-* windows / OverviewWindow / Options / FrameCall / Pause / Markers / Debug
        // --------------------------------------------------------------------

        internal void MiscWindows_TechTreeWindow_Toggle()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7E7, 0, 0, 0);
            if (win != null)
            {
                desktop.Window_AddToCloseList(win);
                return;
            }

            MiscWindows_TechTreeWindow_Open();
        }

        internal void MiscWindows_TechTreeWindow_Open()
        {
            Selection_ActionButtons_ClearListAndDeleteElements();
            _selectionActionButtonsChecksum = 0;

            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;
            if (desktop.Message_SendToAllWindows(0x7E7, 0, 0, 0) != 0)
            {
                return;
            }

            MiscWindows_CloseAllLargeOnes(false);

            NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.InputMode_Set();

            NC2InGameGuiMiscWindows.CTechTreeWindow win = new NC2InGameGuiMiscWindows.CTechTreeWindow();
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(win, false);
        }

        internal void MiscWindows_HelpWindow_Toggle()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7E8, 0, 0, 0);
            if (win != null)
            {
                desktop.Window_AddToCloseList(win);
                return;
            }

            MiscWindows_HelpWindow_Open();
        }

        internal void MiscWindows_HelpWindow_Open()
        {
            Selection_ActionButtons_ClearListAndDeleteElements();
            _selectionActionButtonsChecksum = 0;

            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;
            if (desktop.Message_SendToAllWindows(0x7E8, 0, 0, 0) != 0)
            {
                return;
            }

            MiscWindows_CloseAllLargeOnes(false);

            NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.InputMode_Set();

            NC2InGameGuiMiscWindows.CHyperLinkTextWindow win = new NC2InGameGuiMiscWindows.CHyperLinkTextWindow();
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(win, false);
        }

        internal void MiscWindows_HelpWindow_Open_HouseInfo(int houseType, uint houseSubType)
        {
            MiscWindows_HelpWindow_Open();

            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;
            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7E8, 0, 0, 0);
            if (win != null)
            {
                win.XGui_BE_Message_Handle(0x800, 20000, 0, (uint)houseType, houseSubType, 0);
            }
        }

        internal void MiscWindows_HelpWindow_Open_GoodInfo(long goodId)
        {
            MiscWindows_HelpWindow_Open();

            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;
            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7E8, 0, 0, 0);
            if (win != null)
            {
                win.XGui_BE_Message_Handle(0x800, 0x4E21, 0, (uint)goodId, 0, 0);
            }
        }

        internal void MiscWindows_LargeOverviewWindow_Toggle()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7E9, 0, 0, 0);
            if (win != null)
            {
                desktop.Window_AddToCloseList(win);
                return;
            }

            MiscWindows_LargeOverviewWindow_Open(false);
        }

        internal void MiscWindows_LargeOverviewWindow_Open(bool keepOpenFlag)
        {
            Selection_ActionButtons_ClearListAndDeleteElements();
            _selectionActionButtonsChecksum = 0;

            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;
            if (desktop.Message_SendToAllWindows(0x7E9, 0, 0, 0) != 0)
            {
                return;
            }

            MiscWindows_CloseAllLargeOnes(true);

            NC2InGameGuiInputManager.CInputManager.sTheObjectPtr.InputMode_Set(0);

            NC2InGameGuiOverviewDisplay.CWorldOverviewLargeGuiWindow win = new NC2InGameGuiOverviewDisplay.CWorldOverviewLargeGuiWindow(keepOpenFlag);
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(win, keepOpenFlag);
        }

        internal void MiscWindows_ConstructionSelectionWindow_Toggle(uint specialItemType, uint param)
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7DB, specialItemType, param, 0);
            if (win != null)
            {
                desktop.Window_AddToCloseList(win);
                return;
            }

            // Decompile redundantly checks twice then allocates.
            MiscWindows_ConstructionSelectionWindow_Open(specialItemType, param);
        }

        internal void MiscWindows_ConstructionSelectionWindow_Open(uint specialItemType, uint param)
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            if (desktop.Message_SendToAllWindows(0x7DB, specialItemType, param, 0) != 0)
            {
                return;
            }

            NC2InGameGuiMiscWindows.CConstructionSelectionWindow win = new NC2InGameGuiMiscWindows.CConstructionSelectionWindow(specialItemType, param);
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(win, false);
        }

        internal void MiscWindows_ConstructionSelectionWindow_Close()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7DA, 0, 0, 0);
            if (win != null)
            {
                desktop.Window_Close(win);
            }
        }

        internal ulong MiscWindows_ConstructionSelectionWindow_SendKey(uint key, char keyChar)
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7DA, 0, 0, 0);
            if (win != null)
            {
                return win.XGui_BE_Message_Handle(0x800, 0x8233, key, (uint)keyChar, 0, 0);
            }

            return 0;
        }

        internal static void MiscWindows_StringOutputWindow_Open()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            if (desktop.Message_SendToAllWindows(0x7DC, 0, 0, 0) != 0)
            {
                return;
            }

            for (int i = 0; i < sWindowSearchCommandIdArray.Length; i++)
            {
                uint searchId = sWindowSearchCommandIdArray[i];
                NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(searchId, 0, 0, 0);
                if (win != null)
                {
                    desktop.Window_Close(win);
                }
            }

            NC2InGameGuiMiscWindows.CStringOutputWindow winOut = new NC2InGameGuiMiscWindows.CStringOutputWindow();
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(winOut, false);
        }

        internal static void MiscWindows_StringOutputWindow_Close()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7DC, 0, 0, 0);
            if (win != null)
            {
                desktop.Window_Close(win);
            }
        }

        internal void MiscWindows_StringOutputWindow_PrintString(string text)
        {
            MiscWindows_StringOutputWindow_Open();

            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;
            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7DC, 0, 0, 0);
            if (win != null)
            {
                win.XGui_BE_Message_Handle(0x800, 20000, StringPool.Intern(text), 0, 0, 0);
            }
        }

        internal void MiscWindows_StringOutputWindow_PrintStringV(string format, params object[] args)
        {
            string composed = FormatTools.Vsprintf(format, args);
            MiscWindows_StringOutputWindow_PrintString(composed);
        }

        internal static void MiscWindows_StringOutputWindow_ClearAll()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7DC, 0, 0, 0);
            if (win != null)
            {
                win.XGui_BE_Message_Handle(0x800, 0x4E21, 0, 0, 0, 0);
            }
        }

        internal static void MiscWindows_HumanChangeJobWindow_Open()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            if (desktop.Message_SendToAllWindows(0x7DD, 0, 0, 0) != 0)
            {
                return;
            }

            NC2InGameGuiSelectedAction.CSelectedSingleHumanChangeJobWindow win = new NC2InGameGuiSelectedAction.CSelectedSingleHumanChangeJobWindow();
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(win, false);
        }

        internal static void MiscWindows_HumanChangeJobWindow_Close()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7DD, 0, 0, 0);
            if (win != null)
            {
                desktop.Window_Close(win);
            }
        }

        internal static void MiscWindows_HumanChangeNameWindow_Open()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            if (desktop.Message_SendToAllWindows(0x7DE, 0, 0, 0) == 0)
            {
                NC2InGameGuiSelectedAction.CSelectedSingleHumanChangeNameWindow win = new NC2InGameGuiSelectedAction.CSelectedSingleHumanChangeNameWindow();
                NC2InGameGuiBase.BaseToolDesktop_AddWindow(win, false);

                NXBaseGui.CBaseWindow? found = desktop.Message_SendToAllWindows_FindFirst(0x7DE, 0, 0, 0);
                if (found != null)
                {
                    desktop.Window_ToFront(found);
                }
            }
        }

        internal static void MiscWindows_HumanChangeNameWindow_Close()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7DE, 0, 0, 0);
            if (win != null)
            {
                desktop.Window_Close(win);
            }
        }

        internal ulong MiscWindows_HumanChangeNameWindow_SendKey(uint key, char keyChar)
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7DE, 0, 0, 0);
            if (win != null)
            {
                return win.XGui_BE_Message_Handle(0x800, 0x8233, key, (uint)keyChar, 0, 0);
            }

            return 0;
        }

        internal static void MiscWindows_HumanChangeProducedGoodWindow_Open()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            if (desktop.Message_SendToAllWindows(0x7DF, 0, 0, 0) != 0)
            {
                return;
            }

            NC2InGameGuiSelectedAction.CSelectedSingleHumanChangeProducedGoodWindow win = new NC2InGameGuiSelectedAction.CSelectedSingleHumanChangeProducedGoodWindow();
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(win, false);
        }

        internal static void MiscWindows_HumanChangeProducedGoodWindow_Close()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7DF, 0, 0, 0);
            if (win != null)
            {
                desktop.Window_Close(win);
            }
        }

        internal static void MiscWindows_HumanChangeEquippedGoodsWindow_Open(uint equipmentType, uint slot)
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            if (desktop.Message_SendToAllWindows(0x7E0, equipmentType, slot, 0) != 0)
            {
                return;
            }

            NC2InGameGuiSelectedAction.CSelectedSingleHumanChangeEquippedGoodsWindow win = new NC2InGameGuiSelectedAction.CSelectedSingleHumanChangeEquippedGoodsWindow(equipmentType, slot);
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(win, false);
        }

        internal static void MiscWindows_HumanChangeEquippedGoodsWindow_Close()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7E0, 0, 0, 0);
            if (win != null)
            {
                desktop.Window_Close(win);
            }
        }

        internal static void MiscWindows_HumanLearnJobWindow_Open()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            if (desktop.Message_SendToAllWindows(0x7E1, 0, 0, 0) != 0)
            {
                return;
            }

            NC2InGameGuiSelectedAction.CSelectedSingleHumanLearnJobWindow win = new NC2InGameGuiSelectedAction.CSelectedSingleHumanLearnJobWindow();
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(win, false);
        }

        internal static void MiscWindows_HumanLearnJobWindow_Close()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7E1, 0, 0, 0);
            if (win != null)
            {
                desktop.Window_Close(win);
            }
        }

        internal static void MiscWindows_HouseRemoveWindow_Open()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            if (desktop.Message_SendToAllWindows(0x7EA, 0, 0, 0) != 0)
            {
                return;
            }

            NC2InGameGuiSelectedAction.CSelectedHouseRemoveWindow win = new NC2InGameGuiSelectedAction.CSelectedHouseRemoveWindow();
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(win, false);
        }

        internal static void MiscWindows_HouseRemoveWindow_Close()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7EA, 0, 0, 0);
            if (win != null)
            {
                desktop.Window_Close(win);
            }
        }

        internal static void MiscWindows_GameSafetyBoxWindow_Open(uint mode)
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            if (desktop.Message_SendToAllWindows(0x7EB, mode, 0, 0) != 0)
            {
                return;
            }

            NC2InGameGuiMiscWindows.CGameSafetyBoxWindow win = new NC2InGameGuiMiscWindows.CGameSafetyBoxWindow(mode);
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(win, false);
        }

        internal static void MiscWindows_GameSafetyBoxWindow_Close()
        {
            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;

            NXBaseGui.CBaseWindow? win = desktop.Message_SendToAllWindows_FindFirst(0x7EB, 0, 0, 0);
            if (win != null)
            {
                desktop.Window_Close(win);
            }
        }

        internal void MiscWindows_OverviewWindow_Close()
        {
            if (_overviewWindowPtr != null)
            {
                NXBaseGui.CDesktop.sTheObjectPtr.Window_AddToCloseList(_overviewWindowPtr);
                MiscWindows_OverviewWindow_Closed();
            }
        }

        internal void MiscWindows_OverviewWindow_Closed()
        {
            _overviewWindowPtr = null;

            if (_overviewToggleButton == null)
            {
                SRectangle rect = new SRectangle(0, NC2InGameGuiBase.Layout.TopBarY - 0x26, 0x26, 0x26);
                string tip = NC2GuiToolsBase.StringTool_GetGameGuiMainString(0x0C);

                NC2InGameGuiBase.CBaseToolGfxButton btn = new NC2InGameGuiBase.CBaseToolGfxButton(rect, 0x91, tip, 0x0F45, _defaultMessageHandler);
                _overviewToggleButton = btn;

                btn.SetPalettePtr(NC2InGameGuiBase.Palettes.TopBarPalette);
                NC2InGameGuiBase.BaseToolDesktop_AddBackgroundElement(btn);
            }
        }

        internal void MiscWindows_OverviewWindow_Toggle()
        {
            if (_overviewWindowPtr != null)
            {
                NXBaseGui.CDesktop.sTheObjectPtr.Window_AddToCloseList(_overviewWindowPtr);
                MiscWindows_OverviewWindow_Closed();
                return;
            }

            NC2InGameGuiOverviewDisplay.CWorldOverviewStaticGuiWindow win = new NC2InGameGuiOverviewDisplay.CWorldOverviewStaticGuiWindow();
            _overviewWindowPtr = win;
            NC2InGameGuiBase.BaseToolDesktop_AddWindow(win, false);

            if (_overviewToggleButton != null)
            {
                NXBaseGui.CDesktop.sTheObjectPtr.Element_Delete(_overviewToggleButton);
                _overviewToggleButton = null;
            }
        }

        internal NXBaseGui.CBaseWindow? MiscWindows_OverviewWindow_GetPtr()
        {
            return _overviewWindowPtr;
        }

        internal int Options_ScrollSpeed_Get()
        {
            return _optionsScrollSpeedIndex;
        }

        internal int Options_ScrollSpeed_GetInPixelPerSecond()
        {
            // Decompile: lookup array indexed by _optionsScrollSpeedIndex.
            return sScrollSpeedPixelsPerSecond[_optionsScrollSpeedIndex];
        }

        private static readonly int[] sScrollSpeedPixelsPerSecond = new int[]
        {
            // TODO: fill Options_ScrollSpeed_GetInPixelPerSecond::ls_ScrollSpeedArray
            200, 300, 400, 500, 600
        };

        internal void FrameCall_Register(NXBaseGui.CBaseElement element)
        {
            _frameCallList.InsertAtEnd(element);
        }

        internal void FrameCall_UnRegister(NXBaseGui.CBaseElement element)
        {
            _frameCallList.l_Base_RemoveElement(element);
        }

        internal void Misc_GameTime_Stop_LargeWindowOpened()
        {
            // Decompile:
            // if (!flag && 0.01 <= globalSpeed) { saved = globalSpeed; if (specialFlag) saved=12.0; set flag; send network cmd 0x87 }
            if (!_largeWindowOpenedPauseActive && (CGame.GameSpeed_Current >= 0.01))
            {
                double speed = CGame.GameSpeed_Current;
                if (CGame.IsMaxSpeedCheatEnabled)
                {
                    speed = 12.0;
                }

                _savedGameSpeedBeforeLargeWindow = speed;

                CGame.GameTime_MaximizeGameSpeed_SetFlag(false);

                _largeWindowOpenedPauseActive = true;

                CUserNetworkCommand cmd = CUserNetworkCommand.Create(0x87);
                cmd.Param0 = 0;
                CUserNetworkCommandManager.sTheObjectPtr.AddCommand(cmd, false);
            }
        }

        internal void Misc_GameTime_Continue_LargeWindowClosed()
        {
            // Decompile:
            // if (flag && globalSpeed < 0.01) { send cmd 0x87 with param = savedSpeed }
            if (_largeWindowOpenedPauseActive && (CGame.GameSpeed_Current < 0.01))
            {
                CUserNetworkCommand cmd = CUserNetworkCommand.Create(0x87);
                cmd.Param0 = (int)_savedGameSpeedBeforeLargeWindow;
                CUserNetworkCommandManager.sTheObjectPtr.AddCommand(cmd, false);
            }

            _largeWindowOpenedPauseActive = false;
        }

        internal void GuiMarker_Set(uint index, in SMapMigPoint point)
        {
            if (index < 10)
            {
                _guiMarkers[index] = point.Raw;
            }
        }

        internal void GuiMarker_Get(uint index, out SMapMigPoint point)
        {
            uint raw = 0;
            if (index < 10)
            {
                raw = _guiMarkers[index];
            }

            point = new SMapMigPoint(raw);
        }

        internal void Debug_PrintTextToDesktop(int x, int y, string text)
        {
            if (!sDebugDrawEnabled)
            {
                return;
            }

            NXBaseGui.CDesktop desktop = NXBaseGui.CDesktop.sTheObjectPtr;
            CBitmap target = desktop.GetBackBufferBitmap();
            target.Text_Print(NC2InGameGuiBase.Fonts.DebugFont, x, y, text);
        }

        // --------------------------------------------------------------------
        // SVars::SGuiElementPtr ctor/dtor (semantic port)
        // --------------------------------------------------------------------

        internal sealed class SGuiElementPtr
        {
            internal readonly CListBase ListA;
            internal readonly CListBase ListB;
            internal readonly CListBase ListC;

            internal bool OwnsListAElements;
            internal bool OwnsListBElements;
            internal bool OwnsListCElements;

            internal SGuiElementPtr()
            {
                ListA = new CListBase(false);
                ListB = new CListBase(false);
                ListC = new CListBase(false);
            }

            internal void DisposeLists()
            {
                if (OwnsListCElements)
                {
                    ListC.DeleteAllElements();
                }

                if (OwnsListBElements)
                {
                    ListB.DeleteAllElements();
                }

                if (OwnsListAElements)
                {
                    ListA.DeleteAllElements();
                }

                ListC.Dispose();
                ListB.Dispose();
                ListA.Dispose();
            }
        }

        // --------------------------------------------------------------------
        // Savegame IO (semantic port)
        // --------------------------------------------------------------------

        internal static void l_IO_Savegame_Save(CIoHelper io)
        {
            io.IO_Tool_Chunk_StartCreation(0x58475549, 1); // 'XGUI'
            io.IO_Tool_Chunk_StartCreation(0x58647372, 1); // 'Xdsr'

            SMapMigPoint center = NC2E2.C2DEngineDisplay.DE_Misc_GetDisplayedCenterMapPosition(CGuiManager.sTheObjectPtr.GetEngineDisplay());
            io.IO_Memory_WriteLong(center.GetLong());

            io.IO_Tool_Chunk_FinishAndWrite();

            NC2InGameGuiMiscWindows.CStatisticsWindow.IO_Savegame_Save(io);
            NC2InGameGuiMiscWindows.CHumanListWindow.IO_Savegame_Save(io);
            NC2InGameGuiMiscWindows.CDiplomacyWindow.IO_Savegame_Save(io);

            NC2InGameGuiMessages.CMessageManager.sTheObjectPtr.IO_Savegame_Save(io);
            NC2InGameGuiGroups.CObjectGroupManager.sTheObjectPtr.IO_Savegame_Save(io);
            NC2InGameGuiDeadHumanDataManager.CDeadHumanDataManager.sTheObjectPtr.IO_Savegame_Save(io);

            NC2InGameGuiMiscWindows.CMissionInfoWindow.MissionBriefingHistory_IO_Savegame_Save(io);

            io.IO_Tool_Chunk_StartCreation(0x586D6C65, 1); // 'Xmle'
            io.IO_Memory_WriteLong(NC2InGameGuiMessages.CMessageManager.sTheObjectPtr.MessagePriority_Get());
            io.IO_Tool_Chunk_FinishAndWrite();

            io.IO_Tool_Chunk_FinishAndWrite();
            io.IO_Tool_Chunk_WriteEndChunk();
        }

        internal static void l_IO_Savegame_Load(CIoHelper io)
        {
            if (!io.IO_File_Chunk_CheckMarkerAndGetVersion(0x58475549, out uint version))
            {
                io.IO_File_Chunk_Skip();
                return;
            }

            if (!io.IO_File_Chunk_ReadHeader())
            {
                return;
            }

            while (true)
            {
                int marker = io.CurrentChunkMarker;
                if (marker == 0)
                {
                    break;
                }

                if (marker == 0x58647372) // 'Xdsr'
                {
                    uint raw = io.IO_File_ReadLong();
                    SMapMigPoint point = new SMapMigPoint(raw);
                    NC2E2.C2DEngineDisplay.DE_SetWantedPositionToMapPoint(CGuiManager.sTheObjectPtr.GetEngineDisplay(), point);
                }
                else if (marker == 0x58737461) // 'Xsta'
                {
                    NC2InGameGuiMiscWindows.CStatisticsWindow.IO_Savegame_Load(io);
                }
                else if (marker == 0x5868756C) // 'Xhul'
                {
                    NC2InGameGuiMiscWindows.CHumanListWindow.IO_Savegame_Load(io);
                }
                else if (marker == 0x58646970) // 'Xdip'
                {
                    NC2InGameGuiMiscWindows.CDiplomacyWindow.IO_Savegame_Load(io);
                }
                else if (marker == 0x5864686D) // 'Xdhm'
                {
                    NC2InGameGuiDeadHumanDataManager.CDeadHumanDataManager.sTheObjectPtr.IO_Savegame_Load(io);
                }
                else if (marker == 0x586D6268) // 'Xmbh'
                {
                    NC2InGameGuiMiscWindows.CMissionInfoWindow.MissionBriefingHistory_IO_Savegame_Load(io);
                }
                else if (marker == 0x586D6C65) // 'Xmle'
                {
                    uint prio = io.IO_File_ReadLong();
                    NC2InGameGuiMessages.CMessageManager.sTheObjectPtr.MessagePriority_Set(prio);
                }
                else if (marker == 0x586D6D61) // 'Xmma'
                {
                    NC2InGameGuiMessages.CMessageManager.sTheObjectPtr.IO_Savegame_Load(io);
                }
                else if (marker == 0x586F676D) // 'Xogm'
                {
                    NC2InGameGuiGroups.CObjectGroupManager.sTheObjectPtr.IO_Savegame_Load(io);
                }
                else
                {
                    io.IO_File_Chunk_Skip();
                }

                if (!io.IO_File_Chunk_ReadHeader())
                {
                    break;
                }
            }
        }
    }
}