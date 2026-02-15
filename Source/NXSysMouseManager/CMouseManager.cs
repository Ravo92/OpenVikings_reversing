using OpenVikings.Dexter;
using OpenVikings.NXBasics;
using OpenVikings.NXSys;
using static OpenVikings.StructsCollection;

namespace OpenVikings.NXSysMouseManager
{
    // NXSysMouseManager::CMouseManager
    internal sealed class CMouseManager : IDisposable
    {
        // "sTheObjectPtr"
        internal static CMouseManager? sTheObjectPtr;
        private readonly DexterOS _dexterOs;

        // Offsets (based on decompile)
        // 0x00
        private int _x;

        // 0x04
        private int _y;

        // 0x08..0x0A
        private bool _leftDown;   // this[8]
        private bool _middleDown; // this[9]
        private bool _rightDown;  // this[10]

        // 0x0C
        private bool _hasMouseArea; // this[0x0C] != 0

        // 0x10..0x1F (SRectangle)
        private SRectangle _mouseArea; // this+0x10

        // 0x20
        private bool _hasNewState; // this[0x20]

        // 0x24
        private int _lastStateChangeMs; // *(int*)(this+0x24)

        // 0x28 / 0x2C / 0x30
        private int _accumDeltaX; // *(int*)(this+0x28)
        private int _accumDeltaY; // *(int*)(this+0x2C)
        private int _accumWheel;  // *(int*)(this+0x30)

        // 0x34
        private bool _positionChanged; // this[0x34]

        // 0x35..0x3B (edge flags)
        private bool _leftReleased;   // this[0x35]
        private bool _leftPressed;    // this[0x36]
        private bool _middleReleased; // this[0x37]
        private bool _middlePressed;  // this[0x38]
        private bool _rightReleased;  // this[0x39]
        private bool _rightPressed;   // this[0x3A]
        private bool _leftDoubleClick;// this[0x3B]

        // 0x3C
        private int _lastLeftClickMs; // *(int*)(this+0x3C)

        // 0x40 / 0x44
        private int _lastLeftClickX; // *(int*)(this+0x40)
        private int _lastLeftClickY; // *(int*)(this+0x44)

        // 0x50 (function pointer / callback returning char/bool)
        // Used to decide whether absolute position updates should be applied.
        private Func<bool>? _blockAbsolutePositionUpdateCallback;

        // 0x58
        private bool _hideSystemCursorWhenFullscreen; // this[0x58]

        private bool _disposed;

        // NXSysMouseManager::CMouseManager::CMouseManager()
        internal CMouseManager(DexterOS dexterOs)
        {
            _dexterOs = dexterOs;
            sTheObjectPtr = this;

            ResetInternalState();

            NXSysMisc.XWS_SystemMouseCursor_Reset();
        }

        internal SPoint Position
        {
            get
            {
                SPoint p = default;
                p.X = _x;
                p.Y = _y;
                return p;
            }
        }

        // NXSysMouseManager::CMouseManager::~CMouseManager()
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            NXSysMisc.XWS_SystemMouseCursor_SetNormal();
            sTheObjectPtr = null;

            _disposed = true;
        }

        private void ResetInternalState()
        {
            // Equivalent intent to DexterMemory::MemorySet(this, '\0', 0x60) for the known fields.
            _x = 0;
            _y = 0;

            _leftDown = false;
            _middleDown = false;
            _rightDown = false;

            _hasMouseArea = false;
            _mouseArea = default;

            _hasNewState = false;
            _lastStateChangeMs = 0;

            _accumDeltaX = 0;
            _accumDeltaY = 0;
            _accumWheel = 0;

            _positionChanged = false;

            _leftReleased = false;
            _leftPressed = false;
            _middleReleased = false;
            _middlePressed = false;
            _rightReleased = false;
            _rightPressed = false;
            _leftDoubleClick = false;

            _lastLeftClickMs = 0;
            _lastLeftClickX = 0;
            _lastLeftClickY = 0;

            _blockAbsolutePositionUpdateCallback = null;

            _hideSystemCursorWhenFullscreen = false;
        }

        // NXSysMouseManager::CMouseManager::ResetWindowsMouseState()
        internal static void ResetWindowsMouseState()
        {
            NXSysMisc.XWS_SystemMouseCursor_Reset();
        }

        // NXSysMouseManager::CMouseManager::UpdateMouseState()
        internal void UpdateMouseState()
        {
            int osX = _dexterOs.MouseX;
            int osY = _dexterOs.MouseY;

            int dx = osX - _x;
            int dy = osY - _y;

            L_SetState_Total(_leftDown, _middleDown, _rightDown, osX, osY, dx, dy, 0);
            L_SetState_Total(_cLeftButtonState, _cMiddleButtonState, _cRightButtonState, _x, _y, 0, 0, 0);
        }

        // NXSysMouseManager::CMouseManager::L_SetState_Position(int, int)
        private void L_SetState_Position(int x, int y)
        {
            L_SetState_Total(_leftDown, _middleDown, _rightDown, x, y, x - _x, y - _y, 0);
        }

        // NXSysMouseManager::CMouseManager::L_UpdateMouseButtonState()
        private void L_UpdateMouseButtonState()
        {
            L_SetState_Total(_cLeftButtonState, _cMiddleButtonState, _cRightButtonState, _x, _y, 0, 0, 0);
        }

        // NXSysMouseManager::CMouseManager::UpdateMouseWheelState(int)
        internal void UpdateMouseWheelState(int wheelDelta)
        {
            L_SetState_Total(_leftDown, _middleDown, _rightDown, _x, _y, 0, 0, wheelDelta);
            L_SetState_Total(_cLeftButtonState, _cMiddleButtonState, _cRightButtonState, _x, _y, 0, 0, 0);
        }

        // NXSysMouseManager::CMouseManager::L_SetState_Wheel(int)
        private void L_SetState_Wheel(int wheelDelta)
        {
            L_SetState_Total(_leftDown, _middleDown, _rightDown, _x, _y, 0, 0, wheelDelta);
        }

        // NXSysMouseManager::CMouseManager::UpdateMouseButtonState(bool, bool, bool)
        internal void UpdateMouseButtonState(bool leftDown, bool middleDown, bool rightDown)
        {
            L_SetState_Total(leftDown, middleDown, rightDown, _x, _y, 0, 0, 0);
        }

        // NXSysMouseManager::CMouseManager::L_SetState_Buttons(bool, bool, bool)
        private void L_SetState_Buttons(bool leftDown, bool middleDown, bool rightDown)
        {
            L_SetState_Total(leftDown, middleDown, rightDown, _x, _y, 0, 0, 0);
        }

        // NXSysMouseManager::CMouseManager::SetMouseArea(NXBasics::SRectangle*)
        internal void SetMouseArea(SRectangle? area)
        {
            if (!area.HasValue)
            {
                _hasMouseArea = false;
                return;
            }

            _hasMouseArea = true;
            _mouseArea = area.Value;

            SPoint p = default;
            p.X = _x;
            p.Y = _y;

            p.PlaceInside(in _mouseArea);

            _x = p.X;
            _y = p.Y;

            _positionChanged = true;
        }

        // NXSysMouseManager::CMouseManager::Callback_ApplicationActivated(bool)
        internal static void Callback_ApplicationActivated(bool activated)
        {
        }

        // NXSysMouseManager::CMouseManager::Callback_FullscreenToggled(bool)
        internal static void Callback_FullscreenToggled(bool fullscreen)
        {
        }

        // NXSysMouseManager::CMouseManager::L_SetState_Total(...)
        private void L_SetState_Total(bool leftDown, bool middleDown, bool rightDown, int x, int y, int deltaX, int deltaY, int wheelDelta)
        {
            _accumDeltaX += deltaX;
            _accumDeltaY += deltaY;
            _accumWheel += wheelDelta;

            bool leftJustPressedFromUp;

            if (_leftDown == leftDown)
            {
                leftJustPressedFromUp = false;
            }
            else
            {
                if (!leftDown)
                {
                    _leftReleased = true; // this[0x35] = 1
                }
                else
                {
                    _leftPressed = true; // this[0x36] = 1
                }

                leftJustPressedFromUp = leftDown && !_leftDown;
                _leftDown = leftDown;

                _hasNewState = true; // this[0x20] = 1
                _lastStateChangeMs = (int)NXSysTime.XWS_Time_GetMilliSeconds();
            }

            if (_middleDown != middleDown)
            {
                if (middleDown)
                {
                    _middlePressed = true; // this[0x38] = 1
                }
                else
                {
                    _middleReleased = true; // this[0x37] = 1
                }

                _middleDown = middleDown;
                _hasNewState = true;
                _lastStateChangeMs = (int)NXSysTime.XWS_Time_GetMilliSeconds();
            }

            if (_rightDown != rightDown)
            {
                if (rightDown)
                {
                    _rightPressed = true; // this[0x3A] = 1
                }
                else
                {
                    _rightReleased = true; // this[0x39] = 1
                }

                _rightDown = rightDown;
                _hasNewState = true;
                _lastStateChangeMs = (int)NXSysTime.XWS_Time_GetMilliSeconds();
            }

            bool allowAbsolutePositionUpdate = true;

            if (_blockAbsolutePositionUpdateCallback != null)
            {
                bool block = _blockAbsolutePositionUpdateCallback();
                if (block)
                {
                    allowAbsolutePositionUpdate = false;
                }
            }

            bool stateChangedByMoveOrDelta = false;

            if (allowAbsolutePositionUpdate)
            {
                if (!(_x == x && _y == y))
                {
                    _positionChanged = true;
                    _x = x;
                    _y = y;

                    if (_hasMouseArea)
                    {
                        SPoint p = default;
                        p.X = _x;
                        p.Y = _y;
                        p.PlaceInside(in _mouseArea);
                        _x = p.X;
                        _y = p.Y;
                    }

                    stateChangedByMoveOrDelta = true;
                }
            }
            else
            {
                if (!(deltaX == 0 && deltaY == 0))
                {
                    _positionChanged = true;
                    stateChangedByMoveOrDelta = true;
                }
            }

            if (stateChangedByMoveOrDelta)
            {
                _hasNewState = true;
                _lastStateChangeMs = (int)NXSysTime.XWS_Time_GetMilliSeconds();
            }

            if (_positionChanged && _lastLeftClickMs != 0)
            {
                uint dx = (uint)Math.Abs(_lastLeftClickX - _x);
                if (dx > 4)
                {
                    _lastLeftClickMs = 0;
                }
                else
                {
                    uint dy = (uint)Math.Abs(_lastLeftClickY - _y);
                    if (dy > 4)
                    {
                        _lastLeftClickMs = 0;
                    }
                }
            }

            if (leftJustPressedFromUp)
            {
                int nowMs = (int)NXSysTime.XWS_Time_GetMilliSeconds();
                if ((uint)(nowMs - _lastLeftClickMs) < 400u)
                {
                    _leftDoubleClick = true; // this[0x3B] = 1
                    _lastLeftClickMs = 0;

                    _hasNewState = true;
                    _lastStateChangeMs = (int)NXSysTime.XWS_Time_GetMilliSeconds();
                }
                else
                {
                    _lastLeftClickMs = nowMs;
                    _lastLeftClickX = _x;
                    _lastLeftClickY = _y;
                }
            }

            if (wheelDelta != 0)
            {
                _hasNewState = true;
                _lastStateChangeMs = (int)NXSysTime.XWS_Time_GetMilliSeconds();
            }
        }

        // NXSysMouseManager::CMouseManager::L_SetState_DeltaMovement(int, int)
        private void L_SetState_DeltaMovement(int deltaX, int deltaY)
        {
            L_SetState_Total(_leftDown, _middleDown, _rightDown, _x, _y, deltaX, deltaY, 0);
        }

        // NXSysMouseManager::CMouseManager::GetNewMouseMessage(NXSysMouseManager::SMouseMessage&)
        internal void GetNewMouseMessage(ref SMouseMessage message)
        {
            message = default;

            if (!_hasNewState)
            {
                return;
            }

            _hasNewState = false;

            message.X = _x;
            message.Y = _y;

            message.TotalDeltaX = _accumDeltaX;
            message.TotalDeltaY = _accumDeltaY;
            message.WheelDelta = _accumWheel;

            _accumDeltaX = 0;
            _accumDeltaY = 0;
            _accumWheel = 0;

            uint flags = 0;

            if (_leftReleased)
            {
                flags |= 0x0008u;
                _leftReleased = false;
            }

            if (_leftPressed)
            {
                flags |= 0x0004u;
                _leftPressed = false;
            }

            if (_middleReleased)
            {
                flags |= 0x0020u;
                _middleReleased = false;
            }

            if (_middlePressed)
            {
                flags |= 0x0010u;
                _middlePressed = false;
            }

            if (_rightReleased)
            {
                flags |= 0x0080u;
                _rightReleased = false;
            }

            if (_rightPressed)
            {
                flags |= 0x0040u;
                _rightPressed = false;
            }

            if (_leftDoubleClick)
            {
                flags |= 0x0100u;
                _leftDoubleClick = false;
            }

            if (_positionChanged)
            {
                flags |= 0x0001u;
                _positionChanged = false;
            }

            if (message.WheelDelta != 0)
            {
                flags |= 0x0002u;
                _positionChanged = false; // dump sets this[0x34] = 0 here
            }

            message.Flags = flags;
        }

        // NXSysMouseManager::CMouseManager::SetHideSystemMouseCursorWhenFullscreenState(bool)
        internal void SetHideSystemMouseCursorWhenFullscreenState(bool hide)
        {
            _hideSystemCursorWhenFullscreen = hide;
        }

        // ---- External button state sources (referenced by decompile as globals) ----
        // These are expected to exist in the existing port (no behavior invented here).
        private static bool _cLeftButtonState;
        private static bool _cMiddleButtonState;
        private static bool _cRightButtonState;

        internal static void SetButtonStates(bool left, bool middle, bool right)
        {
            _cLeftButtonState = left;
            _cMiddleButtonState = middle;
            _cRightButtonState = right;
        }

        internal void SetBlockAbsolutePositionUpdateCallback(Func<bool>? callback)
        {
            _blockAbsolutePositionUpdateCallback = callback;
        }
    }

    // NXSysMouseManager::SMouseMessage (layout inferred from the decompile usage)
    internal struct SMouseMessage
    {
        internal uint Flags;
        internal int X;
        internal int Y;
        internal int TotalDeltaX;
        internal int TotalDeltaY;
        internal int WheelDelta;

        internal MouseMessageFlags FlagsEnum
        {
            readonly get { return (MouseMessageFlags)Flags; }
            set { Flags = (uint)value; }
        }

        internal readonly SPoint Position
        {
            get
            {
                SPoint p = default;
                p.X = X;
                p.Y = Y;
                return p;
            }
        }
    }

}