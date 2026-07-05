using OpenVikings.NXBasics;

namespace OpenVikings.NXBaseGui;

[Flags]
internal enum BaseElementFlags : uint
{
    None = 0,

    // Observed in decompile / required by Desktop logic
    KeyboardFocusRootCandidate = 0x00000004,
    DefaultMessageHandler = 0x00000080,
    AddedToDesktop = 0x00000800,

    HasSubElements = 0x00000040,
    CloseWindowOnRelease = 0x00000008,

    MouseInside = 0x00000100,
    Hidden = 0x00000200,
    Disabled = 0x00000400,

    // Masks observed in your earlier mapping
    CanBeActivatedMask = 0x00000401,
    CanBePressedMask = 0x00000402
}

internal enum BaseElementState : int
{
    Inactive = 0,
    Active = 1,
    Pressed = 2
}

internal class CBaseElement
{
    private static uint sUniqueIdCounter;

    // this + 0x08.. (rectangle)
    private SRectangle _rect;

    // this + 0x18 (state)
    internal BaseElementState _state;

    // this + 0x1C (flags)
    private BaseElementFlags _flags;

    // this + 0x1D (byte flags; decompile uses bit 0x02 for hide)
    private byte _elementStateFlags;

    // this + 0x20 (message mask/type)
    private TXGuiMessageTypes _messageType;

    // this + 0x28 in decompile: parent element pointer (root chain)
    private CBaseElement? _parentElement;

    // Convenience only: owner window (used for CloseWindowOnRelease and some desktop logic)
    private CBaseWindow? _parentWindow;

    // this + 0x38 (unique id)
    private uint _uniqueId;

    internal ref SRectangle RectRef
    {
        get { return ref _rect; }
    }

    internal ref readonly SRectangle Rect
    {
        get { return ref _rect; }
    }

    internal BaseElementFlags Flags
    {
        get { return _flags; }
        set { _flags = value; }
    }

    internal byte ElementStateFlags
    {
        get { return _elementStateFlags; }
        set { _elementStateFlags = value; }
    }

    internal BaseElementState State
    {
        get { return _state; }
    }

    internal uint UniqueId
    {
        get { return _uniqueId; }
    }

    internal CBaseElement? ParentElement
    {
        get { return _parentElement; }
    }

    internal CBaseWindow? ParentWindow
    {
        get { return _parentWindow; }
    }

    internal uint MessageMask
    {
        get { return _messageType.Value; }
    }

    public CBaseElement(in SRectangle rect, uint flags, TXGuiMessageTypes messageType)
    {
        L_BE_ConstructElement(in rect, flags, messageType);
    }

    // NXBaseGui::CBaseElement::L_BE_ConstructElement(NXBasics::SRectangle const&, unsigned int, NXBaseGui::TXGuiMessageTypes)
    internal void L_BE_ConstructElement(in SRectangle rect, uint flags, TXGuiMessageTypes messageType)
    {
        L_BE_InitElementVars();

        _rect = rect;
        _flags = (BaseElementFlags)ComputeCtorFlags(flags);
        _messageType = messageType;

        _uniqueId = sUniqueIdCounter;
        sUniqueIdCounter++;
    }

    // NXBaseGui::CBaseElement::~CBaseElement()
    internal void L_BE_DestructElement()
    {
        L_BE_InitElementVars();
    }

    // vtable+8 equivalent
    internal virtual void DisposeLikeOriginal()
    {
        L_BE_DestructElement();
    }

    // NXBaseGui::CBaseElement::L_BE_InitElementVars()
    internal void L_BE_InitElementVars()
    {
        _rect = default;
        _state = BaseElementState.Inactive;
        _flags = BaseElementFlags.None;
        _elementStateFlags = 0;
        _messageType = default;

        _parentElement = null;
        _parentWindow = null;

        _uniqueId = 0;
    }

    internal void SetParentElement(CBaseElement? parentElement)
    {
        _parentElement = parentElement;
    }

    internal void SetParentWindow(CBaseWindow? parentWindow)
    {
        _parentWindow = parentWindow;
    }

    // NXBaseGui::CBaseElement::XGui_BE_Element_Draw(NXBasics::CBitmap&, NXBasics::SRectangle const&)
    internal virtual void XGui_BE_Element_Draw(CBitmap target, in SRectangle clipRect)
    {
        // Intentionally empty
    }

    // NXBaseGui::CBaseElement::XGui_BE_Element_Activate(NXBasics::SPoint const&)
    internal virtual void XGui_BE_Element_Activate(in SPoint point)
    {
        if (_state != BaseElementState.Active)
        {
            _state = BaseElementState.Active;
        }
    }

    // NXBaseGui::CBaseElement::XGui_BE_Element_DeActivate()
    internal virtual void XGui_BE_Element_DeActivate()
    {
        if (_state != BaseElementState.Inactive)
        {
            _state = BaseElementState.Inactive;
        }
    }

    // NXBaseGui::CBaseElement::XGui_BE_Element_ButtonPressed(NXBasics::SPoint const&)
    internal virtual bool XGui_BE_Element_ButtonPressed(in SPoint point)
    {
        if (_state != BaseElementState.Pressed)
        {
            _state = BaseElementState.Pressed;
        }

        return true;
    }

    // NXBaseGui::CBaseElement::XGui_BE_Element_ButtonReleased()
    internal virtual bool XGui_BE_Element_ButtonReleased()
    {
        if (_state == BaseElementState.Pressed)
        {
            if (((uint)_flags & (uint)BaseElementFlags.CloseWindowOnRelease) != 0 && _parentWindow != null)
            {
                CBaseWindow.BW_CloseWindow(_parentWindow);
            }

            _state = BaseElementState.Active;
            XGui_BE_Element_DoPressedAction();
            return true;
        }

        if (_state == BaseElementState.Active)
        {
            _state = BaseElementState.Inactive;
        }

        return false;
    }

    // NXBaseGui::CBaseElement::BE_Internal_Element_MouseEnteredArea()
    internal void BE_Internal_Element_MouseEnteredArea()
    {
        uint prev = (uint)_flags;
        _flags = (BaseElementFlags)(prev | (uint)BaseElementFlags.MouseInside);
    }

    // byte at offset 0x1D, used as bitfield in the original
    internal byte GetInternalByte1D()
    {
        return _elementStateFlags;
    }

    // "InWindowBit" is bit 0x08 (matches decompile: & 0xF7 clears it)
    internal bool HasInWindowBit()
    {
        return (_elementStateFlags & 0x08) != 0;
    }

    internal void SetInWindowBit()
    {
        _elementStateFlags = (byte)(_elementStateFlags | 0x08);
    }

    // NXBaseGui::CBaseElement::ClearInWindowBit()
    internal void ClearInWindowBit()
    {
        _elementStateFlags = (byte)(_elementStateFlags & 0xF7);
    }

    internal SRectangle GetRectangle()
    {
        return _rect;
    }

    // NXBaseGui::CBaseElement::BE_CanBePressed() const
    internal bool BE_CanBePressed()
    {
        return ((uint)_flags & (uint)BaseElementFlags.CanBePressedMask) == 2;
    }

    // NXBaseGui::CBaseElement::BE_CanBeActivated() const
    internal bool BE_CanBeActivated()
    {
        return ((uint)_flags & (uint)BaseElementFlags.CanBeActivatedMask) == 1;
    }

    // NXBaseGui::CBaseElement::BE_Internal_Element_MouseLeftArea()
    internal void BE_Internal_Element_MouseLeftArea()
    {
        _elementStateFlags = (byte)(_elementStateFlags & 0xFE);

        if (_state == BaseElementState.Pressed)
        {
            _state = BaseElementState.Active;
        }
    }

    // NXBaseGui::CBaseElement::XGui_BE_Message_Handle(...)
    internal virtual ulong XGui_BE_Message_Handle(TXGuiMessageTypes messageType, uint a, uint b, uint c, uint d)
    {
        return 0;
    }

    // NXBaseGui::CBaseElement::XGui_BE_Element_ParentToLocalPosition(NXBasics::SPoint&) const
    internal void XGui_BE_Element_ParentToLocalPosition(ref SPoint point)
    {
        point.X -= _rect.X;
        point.Y -= _rect.Y;
    }

    // NXBaseGui::CBaseElement::L_BE_GlobalToLocalCoordinates(NXBasics::SPoint&)
    internal void L_BE_GlobalToLocalCoordinates(ref SPoint point)
    {
        int globalX = point.X;
        int globalY = point.Y;

        CBaseElement? parentElement = _parentElement;
        if (parentElement != null)
        {
            SRectangle parentRect = parentElement._rect;
            globalX -= parentRect.X;
            globalY -= parentRect.Y;
        }
        else if (_parentWindow != null)
        {
            // Fallback: only if no parent element exists
            SRectangle parentRect = _parentWindow.Rect;
            globalX -= parentRect.X;
            globalY -= parentRect.Y;
        }

        point.X = globalX - _rect.X;
        point.Y = globalY - _rect.Y;
    }

    // NXBaseGui::CBaseElement::BE_Internal_Element_Hide()
    internal void BE_Internal_Element_Hide()
    {
        if (((uint)_flags & (uint)BaseElementFlags.Hidden) == 0)
        {
            CDesktop.sTheObjectPtr?.L_Element_DoNotUse(this);

            _flags = (BaseElementFlags)((uint)_flags | (uint)BaseElementFlags.Hidden);

            // Decompile uses (byte +0x1D) bit 0x02 as "hidden" marker in many checks.
            _elementStateFlags = (byte)(_elementStateFlags | 0x02);
        }
    }

    // NXBaseGui::CBaseElement::BE_Internal_Element_UnHide()
    internal void BE_Internal_Element_UnHide()
    {
        if (((uint)_flags & (uint)BaseElementFlags.Hidden) != 0)
        {
            _flags = (BaseElementFlags)((uint)_flags & 0xfffffdffU);
            _elementStateFlags = (byte)(_elementStateFlags & 0xFD);
        }
    }

    // NXBaseGui::CBaseElement::BE_Internal_Element_Disable()
    internal void BE_Internal_Element_Disable()
    {
        if (((uint)_flags & (uint)BaseElementFlags.Disabled) == 0)
        {
            CDesktop.sTheObjectPtr?.L_Element_DoNotUse(this);
            _flags = (BaseElementFlags)((uint)_flags | (uint)BaseElementFlags.Disabled);
        }
    }

    // NXBaseGui::CBaseElement::BE_Internal_Element_Enable()
    internal void BE_Internal_Element_Enable()
    {
        if (((uint)_flags & (uint)BaseElementFlags.Disabled) != 0)
        {
            _flags = (BaseElementFlags)((uint)_flags & 0xfffffbffU);
        }
    }

    // NXBaseGui::CBaseElement::XGui_BE_Element_FindElementOnPosition(NXBasics::SPoint const&, bool)
    internal virtual CBaseElement? XGui_BE_Element_FindElementOnPosition(in SPoint point, bool forcePick)
    {
        if ((((uint)_flags & (uint)BaseElementFlags.CanBeActivatedMask) == 1 || forcePick) && (((uint)_flags & (uint)BaseElementFlags.Hidden) == 0))
        {
            if (XGui_BE_Element_HitTest(in point))
            {
                return this;
            }
        }

        return null;
    }

    // NXBaseGui::CBaseElement::XGui_BE_HasSubElements() const
    internal bool XGui_BE_HasSubElements()
    {
        return (((uint)_flags & (uint)BaseElementFlags.HasSubElements) >> 6) != 0;
    }

    // NXBaseGui::CBaseElement::XGui_BE_Hide_CanBeHidden()
    internal virtual bool XGui_BE_Hide_CanBeHidden()
    {
        return true;
    }

    // NXBaseGui::CBaseElement::XGui_BE_Element_GetRealActiveElementPtr()
    internal virtual CBaseElement XGui_BE_Element_GetRealActiveElementPtr()
    {
        return this;
    }

    // NXBaseGui::CBaseElement::XGui_BE_ToolTip_AtOnce()
    internal virtual ulong XGui_BE_ToolTip_AtOnce()
    {
        return 0;
    }

    // NXBaseGui::CBaseElement::XGui_BE_Element_DoPressedAction()
    internal virtual void XGui_BE_Element_DoPressedAction()
    {
        // Intentionally empty
    }

    // vtable +0x68 in decompile: called when element is added to desktop
    internal virtual void XGui_BE_Element_AddedToDesktop()
    {
        // Intentionally empty
    }

    protected virtual bool XGui_BE_Element_HitTest(in SPoint point)
    {
        int left = _rect.X;
        int top = _rect.Y;

        return point.X >= left && point.X < left + _rect.Width && point.Y >= top && point.Y < top + _rect.Height;
    }

    private static uint ComputeCtorFlags(uint flags)
    {
        // Decompile: 0x21 - ((flags & 2) == 0) | flags
        uint baseValue = (flags & 2U) == 0 ? 0x20U : 0x21U;
        return baseValue | flags;
    }
}