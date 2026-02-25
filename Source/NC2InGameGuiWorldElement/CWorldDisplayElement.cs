using OpenVikings.NC2GuiToolsBase;
using OpenVikings.NC2Logic;
using OpenVikings.NXBasics;

namespace OpenVikings.NC2InGameGuiWorldElement
{
    internal enum TWorldDisplayScrollTimeSyncedDirection : int
    {
        None = 0,
        Left = 1,
        Down = 2,
        Right = 3,
        Up = 4
    }

    // This is a best-effort managed replacement for the unknown 0x14-byte command struct.
    // Replace with the real SUserNetworkCommand type if it already exists in the codebase.
    internal struct SUserNetworkCommand
    {
        internal int CommandType;
        internal int A;
        internal int B;
        internal int C;
        internal int D;

        internal void Clear()
        {
            this = default;
        }

        internal bool IsValid()
        {
            return CommandType != 0;
        }
    }

    // Assumed base class exists in the project.
    // Constructor signature is unknown in the snippet; adapt the base(...) call to the real one.
    internal sealed class CWorldDisplayElement : NC2InGameGuiBase.CBaseBitmapToolElement
    {
        private readonly bool _isInteractive; // original: this[0x50] = param_3 (enables mouse interactions)

        private NC2E2.C2DEngineDisplay? _engineDisplay;      // +0x48
        private SUserInteractionTargetData _searchObject;     // +0x58 (in RE it is much larger; in the project this struct is 0x44)

        // "Default interaction command" cache computed in l_UpdateObjectUnderMouse()
        private int _defaultInteractionCommandType;           // original: *(int*)(this + 0x288) via pSVar2
        private SUserNetworkCommand _defaultCommandPrimary;   // +0x290 (0x14)
        private SUserNetworkCommand _defaultCommandSecondary; // +0x2A4 (0x14)

        // Mouse selection rectangle state
        private bool _mouseSelectingAreaActive;               // this[0x2b8]
        private int _mouseSelectingStartX;                    // this + 700 (0x2BC)
        private int _mouseSelectingStartY;                    // this + 0x2C0
        private int _mouseSelectingCurrentX;                  // this + 0x2C4
        private int _mouseSelectingCurrentY;                  // this + 0x2C8

        // Draw frame mode
        private int _drawFrameMode;                           // *(int*)(this + 0x2cc)

        // Optional mouse click handler forwarding (element + message id)
        private NXBaseGui.CBaseElement? _leftClickHandler;     // *(CBaseElement**)(this+0x2d0)
        private uint _leftClickHandlerMessage;                // *(uint*)(this+0x2d8)
        private NXBaseGui.CBaseElement? _rightClickHandler;    // *(CBaseElement**)(this+0x2e0)
        private uint _rightClickHandlerMessage;               // *(uint*)(this+0x2e8)

        // Follow mode blob (0x18 bytes in RE). Stored as explicit fields.
        private bool _followModeActive;                       // this[0x2f0] != 0
        private bool _followModeHuman;                        // this[0x2f1] != 0  (heuristic mapping)
        private int _followHumanId;                           // *(int*)(this+0x2f4)
        private int _followHumanSerial;                       // *(int*)(this+0x2f8)
        private bool _followModeVehicleValid;                 // this[0x2fc] != 0
        private int _followVehicleId;                         // *(int*)(this+0x300)
        private int _followVehicleSerial;                     // *(int*)(this+0x304)

        // Darken bitmap feature
        private uint _darkenMode;                             // *(uint*)(this + 0x308)
        private CPalette? _darkenPalette; // *(CPalette**)(this+0x310)
        private ushort[]? _darkenRemap16;                     // *(void**)(this+0x318) as 0x20000 bytes => ushort[65536]

        // Mouse debounce timestamp
        private int _mouseDebounceMs;                         // *(int*)(this + 0x2ec)

        // --------------------------------------------------------------------
        // Constructor (port of CWorldDisplayElement::CWorldDisplayElement(rect, param_2, param_3))
        // --------------------------------------------------------------------
        internal CWorldDisplayElement(SRectangle rect, bool param2, bool param3) : base(in rect, 1u, 0x1A7u, GetDesktopBackBufferBitsPerPixel())
        {
            _engineDisplay = new NC2E2.C2DEngineDisplay(GetBackgroundBitmap(), param2, CGuiBaseDataManager.mStaticVars);

            _darkenMode = 0;
            _darkenPalette = null;
            _darkenRemap16 = null;

            _drawFrameMode = 0;

            NC2InGameGuiManager.CGuiManager? gui = NC2InGameGuiManager.CGuiManager.sTheObjectPtr;
            if (gui != null)
            {
                gui.EngineWorldDisplayGuiElement_Register(this);
                gui.FrameCall_Register(this);
            }
        }

        // --------------------------------------------------------------------
        // Destructor equivalents: explicit cleanup
        // --------------------------------------------------------------------
        internal void DisposeManaged()
        {
            DarkenBitmap_Exit();

            NC2InGameGuiManager.CGuiManager? gui = NC2InGameGuiManager.CGuiManager.sTheObjectPtr;
            if (gui != null)
            {
                gui.FrameCall_UnRegister(this);
                gui.EngineWorldDisplayGuiElement_UnRegister(this);
            }

            _engineDisplay?.Dispose();
            _engineDisplay = null;
        }

        // --------------------------------------------------------------------
        // l_DarkenBitmap_Exit()
        // --------------------------------------------------------------------
        internal void DarkenBitmap_Exit()
        {
            _darkenMode = 0;

            if (_darkenPalette is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _darkenPalette = null;
            _darkenRemap16 = null;
        }

        // --------------------------------------------------------------------
        // XGui_BE_Element_Draw(CBitmap&, SRectangle const&)
        // --------------------------------------------------------------------
        internal override void XGui_BE_Element_Draw(CBitmap target, in SRectangle clipRect)
        {
            _engineDisplay?.DE_UpdateDisplay();

            base.XGui_BE_Element_Draw(target, in clipRect);

            l_DarkenBitmap_Do(target);

            // RE: if (DAT_1003a6488 < 0.01) NXBasics::CBitmap::Tool_Darken(param_1);
            // In your C# API Tool_Darken requires a rect, so the clip rect is used.
            if (NC2GuiToolsBase.GuiGlobals.ScreenFadeFactor < 0.01f)
            {
                SRectangle r = clipRect;
                target.Tool_Darken(r);
            }

            // Frame mode
            if (_drawFrameMode != 0)
            {
                NC2GuiToolsBase.DrawTool.DrawFrame(0, target, in clipRect);
            }
        }

        // --------------------------------------------------------------------
        // l_ValidateObjectUnderMouse()
        // --------------------------------------------------------------------
        internal void l_ValidateObjectUnderMouse()
        {
            // Validation is optional. If no provider exists yet, keep current selection as-is.
            NC2Logic.IUserInteractionTargetValidation? validation = GetValidationProviderOrNull();
            if (validation == null)
            {
                return;
            }

            ulong invalidReset = _searchObject.Tool_Validate(validation);
            if (invalidReset != 0)
            {
                _engineDisplay?.ClearObjectsToMark();

                _defaultInteractionCommandType = 0;
                _defaultCommandPrimary.Clear();
                _defaultCommandSecondary.Clear();
            }
        }

        // Provide validation from the logic layer when available.
        // Return null until the real provider exists.
        private static IUserInteractionTargetValidation? GetValidationProviderOrNull()
        {
            return null;
        }

        // --------------------------------------------------------------------
        // l_DarkenBitmap_Do(CBitmap const&)
        // --------------------------------------------------------------------
        internal void l_DarkenBitmap_Do(CBitmap target)
        {
            if (_darkenMode == 0)
            {
                return;
            }

            // 32bpp path: map each pixel to palette true-color entry by max channel intensity.
            if (target.BitsPerPixel == BitmapBpp32)
            {
                if (_darkenPalette == null)
                {
                    return;
                }

                // Need raw pixels (same mechanism used by Tool_Darken).
                if (!target.TryGetPixelBuffer(out byte[] buffer, out int baseOffset, out int pitchPixels, out int bytesPerPixel))
                {
                    return;
                }

                if (buffer == null || bytesPerPixel != 4)
                {
                    return;
                }

                uint[] trueColorTable = _darkenPalette.GetTrueColorTablePtr();

                int width = target.Width;
                int height = target.Height;
                int pitch = pitchPixels > 0 ? pitchPixels : width;

                for (int y = 0; y < height; y++)
                {
                    int rowBaseBytes = checked(baseOffset + (y * pitch) * 4);

                    for (int x = 0; x < width; x++)
                    {
                        int off = checked(rowBaseBytes + (x * 4));

                        uint value = CBitmap.ReadUInt32LittleEndian(buffer, off);

                        uint b = value & 0xFFu;
                        uint g = (value >> 8) & 0xFFu;
                        uint r = (value >> 16) & 0xFFu;

                        uint max = r;
                        if (g > max) max = g;
                        if (b > max) max = b;

                        CBitmap.WriteUInt32LittleEndian(buffer, off, trueColorTable[(int)max]);
                    }
                }

                return;
            }

            // 16bpp path: remap each pixel through remap16[oldValue].
            if (target.BitsPerPixel == BitmapBpp16)
            {
                if (_darkenRemap16 == null)
                {
                    return;
                }

                if (!target.TryGetPixelBuffer(out byte[] buffer, out int baseOffset, out int pitchPixels, out int bytesPerPixel))
                {
                    return;
                }

                if (buffer == null || bytesPerPixel != 2)
                {
                    return;
                }

                ushort[] remap = _darkenRemap16;

                int width = target.Width;
                int height = target.Height;
                int pitch = pitchPixels > 0 ? pitchPixels : width;

                for (int y = 0; y < height; y++)
                {
                    int rowBaseBytes = checked(baseOffset + (y * pitch) * 2);

                    for (int x = 0; x < width; x++)
                    {
                        int off = checked(rowBaseBytes + (x * 2));
                        ushort src = CBitmap.ReadUInt16LittleEndian(buffer, off);
                        ushort dst = remap[src];
                        CBitmap.WriteUInt16LittleEndian(buffer, off, dst);
                    }
                }
            }
        }

        // --------------------------------------------------------------------
        // MouseSelectingArea_IsAreaValid() const
        // --------------------------------------------------------------------
        internal bool MouseSelectingArea_IsAreaValid()
        {
            if (!_mouseSelectingAreaActive)
            {
                return false;
            }

            uint dx = (uint)Math.Abs(_mouseSelectingStartX - _mouseSelectingCurrentX);
            if (dx <= 4u)
            {
                return false;
            }

            uint dy = (uint)Math.Abs(_mouseSelectingStartY - _mouseSelectingCurrentY);
            return dy > 4u;
        }

        // --------------------------------------------------------------------
        // l_UpdateCursorPosition(int, int)
        // --------------------------------------------------------------------
        internal void l_UpdateCursorPosition(int globalX, int globalY)
        {
            SPoint p = new(globalX, globalY);

            // Convert global -> local element coords
            NXBaseGui.CBaseElement.l_BE_GlobalToLocalCoordinates(this, ref p);

            // Local display -> global world pixel coords
            _engineDisplay?.DE_LocalDisplayPixelToGlobalWorldPixelCoordinates(ref p, true);

            if (!_mouseSelectingAreaActive)
            {
                if (_engineDisplay != null)
                {
                    _engineDisplay.Description.DED_MakeValidWorldPixelPosition(ref p);

                    NC2Logic.SMapMigPoint mig = default;
                    _engineDisplay.Description.DED_WorldPixelToMapMIGCoordinates(ref p, ref mig);

                    NC2InGameGuiInputManager.CInputManager.Cursor_SetPosition(NC2InGameGuiInputManager.CInputManager.sTheObjectPtr, ref mig);
                }
            }
            else
            {
                _mouseSelectingCurrentX = p.X;
                _mouseSelectingCurrentY = p.Y;
            }
        }

        // --------------------------------------------------------------------
        // FollowMode_End()
        // --------------------------------------------------------------------
        internal void FollowMode_End()
        {
            ClearFollowMode();
        }

        // --------------------------------------------------------------------
        // l_DoTimeSyncedScrolling(TWorldDisplayScrollTimeSyncedDirection)
        // --------------------------------------------------------------------
        internal void l_DoTimeSyncedScrolling(TWorldDisplayScrollTimeSyncedDirection direction)
        {
            int speed = NC2InGameGuiManager.CGuiManager.Options_ScrollSpeed_GetInPixelPerSecond(NC2InGameGuiManager.CGuiManager.sTheObjectPtr);

            int dx = 0;
            int dy = 0;

            switch (direction)
            {
                case TWorldDisplayScrollTimeSyncedDirection.Left:
                    dx = -speed;
                    dy = 0;
                    break;
                case TWorldDisplayScrollTimeSyncedDirection.Right:
                    dx = speed;
                    dy = 0;
                    break;
                case TWorldDisplayScrollTimeSyncedDirection.Up:
                    dx = 0;
                    dy = -speed;
                    break;
                case TWorldDisplayScrollTimeSyncedDirection.Down:
                    dx = 0;
                    dy = speed;
                    break;
                default:
                    dx = 0;
                    dy = 0;
                    break;
            }

            float zoom = NC2InGameGuiManager.CGuiManager.GetZoomFactorOrOne(NC2InGameGuiManager.CGuiManager.sTheObjectPtr);
            float invZoom = 1.0f / zoom;

            _engineDisplay?.DE_SetWantedPositionAddDeltaPixelPoint((int)(dx * invZoom), (int)(dy * invZoom));

            ClearFollowMode();
        }

        // --------------------------------------------------------------------
        // l_FollowMode_Update()
        // --------------------------------------------------------------------
        internal void l_FollowMode_Update()
        {
            if (!_followModeActive)
            {
                return;
            }

            // Validate tracked entity; if invalid, clear follow mode.
            if (_followModeHuman)
            {
                if (!NC2LogicApi.TryValidateHuman(_followHumanId, _followHumanSerial))
                {
                    ClearFollowMode();
                    return;
                }
            }
            else
            {
                if (!_followModeVehicleValid || !NC2LogicApi.TryValidateVehicle(_followVehicleId, _followVehicleSerial))
                {
                    ClearFollowMode();
                    return;
                }
            }

            if (_engineDisplay == null)
            {
                return;
            }

            int moveableId;
            if (_followModeHuman)
            {
                moveableId = NC2LogicApi.GetHumanMoveableId(_followHumanId);
            }
            else
            {
                moveableId = NC2LogicApi.GetVehicleMoveableId(_followVehicleId);
            }

            SPoint p = default;
            _engineDisplay.DE_MapMoveableToGlobalWorldPixelCoordinates(moveableId, ref p);

            // RE: p.Y -= 0x19
            p.Y -= 0x19;

            _engineDisplay.DE_SetWantedPositionToPixelPoint(ref p);
        }

        // --------------------------------------------------------------------
        // DrawFrame_SetMode(TBaseDrawModes)
        // --------------------------------------------------------------------
        internal void DrawFrame_SetMode(int mode)
        {
            _drawFrameMode = mode;
        }

        // --------------------------------------------------------------------
        // Misc_LeftMouseClick_HandlerSet(CBaseElement*, unsigned int)
        // --------------------------------------------------------------------
        internal void Misc_LeftMouseClick_HandlerSet(NXBaseGui.CBaseElement element, uint message)
        {
            _leftClickHandler = element;
            _leftClickHandlerMessage = message;
        }

        // --------------------------------------------------------------------
        // Misc_RightMouseClick_HandlerSet(CBaseElement*, unsigned int)
        // --------------------------------------------------------------------
        internal void Misc_RightMouseClick_HandlerSet(NXBaseGui.CBaseElement element, uint message)
        {
            _rightClickHandler = element;
            _rightClickHandlerMessage = message;
        }

        // --------------------------------------------------------------------
        // FollowMode_FollowHuman(int)
        // --------------------------------------------------------------------
        internal void FollowMode_FollowHuman(int humanId)
        {
            ClearFollowMode();

            _followModeActive = true;
            _followModeHuman = true;

            _followHumanId = humanId;
            _followHumanSerial = NC2LogicApi.GetHumanSerial(humanId);

            l_FollowMode_Update();
        }

        // --------------------------------------------------------------------
        // FollowMode_GetHumanId()
        // --------------------------------------------------------------------
        internal int FollowMode_GetHumanId()
        {
            if (!_followModeActive)
            {
                return -1;
            }

            l_FollowMode_Validate();

            if (_followModeActive && _followModeHuman)
            {
                return _followHumanId;
            }

            return -1;
        }

        // --------------------------------------------------------------------
        // l_FollowMode_Validate()
        // --------------------------------------------------------------------
        internal void l_FollowMode_Validate()
        {
            if (!_followModeActive)
            {
                return;
            }

            if (_followModeHuman)
            {
                if (!NC2LogicApi.TryValidateHuman(_followHumanId, _followHumanSerial))
                {
                    ClearFollowMode();
                }
                return;
            }

            if (!_followModeVehicleValid || !NC2LogicApi.TryValidateVehicle(_followVehicleId, _followVehicleSerial))
            {
                ClearFollowMode();
            }
        }

        // --------------------------------------------------------------------
        // FollowMode_FollowVehicle(int)
        // --------------------------------------------------------------------
        internal void FollowMode_FollowVehicle(int vehicleId)
        {
            ClearFollowMode();

            _followModeActive = true;
            _followModeHuman = false;
            _followModeVehicleValid = true;

            _followVehicleId = vehicleId;
            _followVehicleSerial = NC2LogicApi.GetVehicleSerial(vehicleId);

            l_FollowMode_Update();
        }

        // --------------------------------------------------------------------
        // FollowMode_GetVehicleId()
        // --------------------------------------------------------------------
        internal int FollowMode_GetVehicleId()
        {
            if (!_followModeActive)
            {
                return -1;
            }

            l_FollowMode_Validate();

            if (_followModeActive && !_followModeHuman && _followModeVehicleValid)
            {
                return _followVehicleId;
            }

            return -1;
        }

        // --------------------------------------------------------------------
        // DarkenBitmap_Init(unsigned int)
        // --------------------------------------------------------------------
        internal void DarkenBitmap_Init(uint mode)
        {
            DarkenBitmap_Exit();

            _darkenMode = mode;
            if (mode == 0)
            {
                return;
            }

            string palettePath = $"data\\gui\\palettes\\ingame_remap_{mode:00}.pcx";

            // Assumed to exist (matching the dump call).
            CPalette palette = NXBasicsApi.XB_PictureTool_LoadPaletteOutOfPicture(palettePath);
            _darkenPalette = palette;

            // Build 16-bit remap table only when the background is 16bpp.
            CBitmap background = GetBackgroundBitmap();
            if (background.BitsPerPixel != BitmapBpp16)
            {
                return;
            }

            CHighColorCreator creator = CXBSystemManager.sHighColorCreatorPtr;
            if (creator == null || !creator.IsEnabled)
            {
                return;
            }

            // Palette provides a high-color table (ushort[256]) used for mapping intensity -> 16bpp color.
            ushort[] highColorTable = palette.GetHighColorTablePtr();

            // RE: alloc 0x20000 bytes => 65536 ushorts.
            ushort[] remap = new ushort[65536];

            // This matches the RE loop:
            // intensityR = ((maskR & px) >> derivedR) << 3;
            // intensityG = ((maskG & px) >> derivedG) << 3;
            // intensityB = ((maskB & px) >> derivedB) << 3;
            // max = max(intensityR, intensityG, intensityB);
            // remap[px] = highColorTable[max];
            for (int i = 0; i < 65536; i++)
            {
                ushort px = (ushort)i;

                uint r = (uint)((creator._maskR & px) >> (creator._derivedR & 0x1F)) << 3;
                uint g = (uint)((creator._maskG & px) >> (creator._derivedG & 0x1F)) << 3;
                uint b = (uint)((creator._maskB & px) >> (creator._derivedB & 0x1F)) << 3;

                uint max = r;
                if (g > max) max = g;
                if (b > max) max = b;

                if (max > 255u) max = 255u;

                remap[i] = highColorTable[(int)max];
            }

            _darkenRemap16 = remap;
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------
        private void ClearFollowMode()
        {
            _followModeActive = false;
            _followModeHuman = false;
            _followHumanId = 0;
            _followHumanSerial = 0;

            _followModeVehicleValid = false;
            _followVehicleId = 0;
            _followVehicleSerial = 0;
        }

        private static byte GetDesktopBackBufferBitsPerPixel()
        {
            NXBaseGui.CDesktop? desktop = NXBaseGui.CDesktop.sTheObjectPtr;
            if (desktop == null)
            {
                return 0;
            }

            // BackBuffer throws if null; guard it explicitly.
            try
            {
                return desktop.BackBuffer.BitsPerPixel;
            }
            catch
            {
                return 0;
            }
        }
    }
}