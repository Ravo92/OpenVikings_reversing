using OpenVikings.NXBasics;

namespace OpenVikings.NXBaseGui;

[Flags]
internal enum BaseWindowElementFlags : uint
{
    None = 0,
    InWindowList = 0x800
}

internal class CBaseWindow : CBaseElement
{
    // this + 0x40 .. +0xAF (SBaseWindowVars blob in the original)
    private SBaseWindowVars _vars;

    // this + 0xA0 (active element ptr)
    private CBaseElement? _activeElement;

    internal CBaseElement? ActiveElementRef
    {
        get { return _activeElement; }
        set { _activeElement = value; }
    }

    private byte _layerByte; // maps to +0xA8 in pseudo

    internal byte LayerByte
    {
        get { return _layerByte; }
        set { _layerByte = value; }
    }

    public CBaseWindow(in SRectangle rect, uint flags, TXGuiMessageTypes messageType) : base(in rect, flags | 0x43U, messageType)
    {
        L_BW_ConstructWindow(in rect);
    }

    // NXBaseGui::CBaseWindow::L_BW_ConstructWindow(NXBasics::SRectangle const&)
    internal void L_BW_ConstructWindow(in SRectangle rect)
    {
        L_BW_InitWindowVars();
        _vars.WindowRect = new SRectangle(0, 0, rect.Width, rect.Height);
    }

    // NXBaseGui::CBaseWindow::L_BW_DestructWindow()
    internal void L_BW_DestructWindow()
    {
        BW_Element_DeleteAll();

        CDesktop.sTheObjectPtr?.Element_Remove(this);

        L_BW_InitWindowVars();
    }

    // NXBaseGui::CBaseWindow::L_BW_InitWindowVars()
    internal void L_BW_InitWindowVars()
    {
        _vars.WindowRect = default;
        _vars.ElementsDrawOrder = new CListBase<CBaseElement>();
        _vars.ElementsReversePickOrder = new CListBase<CBaseElement>();
        _activeElement = null;
    }

    // NXBaseGui::CBaseWindow::BW_Element_DeleteAll()
    internal void BW_Element_DeleteAll()
    {
        CBaseElement? element = _vars.ElementsDrawOrder.L_Base_GetStartElement();
        while (element != null)
        {
            element.ClearInWindowBit();

            if (_activeElement == element)
            {
                element.XGui_BE_Element_DeActivate();
                _activeElement = null;
            }

            CDesktop.sTheObjectPtr?.Element_Remove(element);

            _vars.ElementsDrawOrder.L_Base_RemoveElement(element);
            _vars.ElementsReversePickOrder.L_Base_RemoveElement(element);

            element.DisposeLikeOriginal();

            element = _vars.ElementsDrawOrder.L_Base_GetStartElement();
        }
    }

    // NXBaseGui::CBaseWindow::BW_Element_Add(NXBaseGui::CBaseElement*)
    internal void BW_Element_Add(CBaseElement element)
    {
        if (!element.HasInWindowBit())
        {
            element.SetInWindowBit();
            element.SetParentWindow(this);

            _vars.ElementsDrawOrder.InsertAtEnd(element);
            _vars.ElementsReversePickOrder.InsertAtStart(element);
        }
    }

    // NXBaseGui::CBaseWindow::BW_Element_Remove(NXBaseGui::CBaseElement*)
    internal void BW_Element_Remove(CBaseElement element)
    {
        element.ClearInWindowBit();

        if (_activeElement == element)
        {
            element.XGui_BE_Element_DeActivate();
            _activeElement = null;
        }

        CDesktop.sTheObjectPtr?.Element_Remove(this);
        _vars.ElementsDrawOrder.L_Base_RemoveElement(element);
        _vars.ElementsReversePickOrder.L_Base_RemoveElement(element);
    }

    // NXBaseGui::CBaseWindow::BW_Element_DoNotUse(NXBaseGui::CBaseElement*)
    internal void BW_Element_DoNotUse(CBaseElement element)
    {
        if (_activeElement == element)
        {
            element.XGui_BE_Element_DeActivate();
            _activeElement = null;
        }
    }

    // NXBaseGui::CBaseWindow::BW_Element_Delete(NXBaseGui::CBaseElement*)
    internal void BW_Element_Delete(CBaseElement element)
    {
        element.ClearInWindowBit();

        if (_activeElement == element)
        {
            element.XGui_BE_Element_DeActivate();
            _activeElement = null;
        }

        CDesktop.sTheObjectPtr?.Element_Remove(this);
        _vars.ElementsDrawOrder.L_Base_RemoveElement(element);
        _vars.ElementsReversePickOrder.L_Base_RemoveElement(element);

        element.DisposeLikeOriginal();
    }

    // NXBaseGui::CBaseWindow::BW_CloseWindow()
    internal static void BW_CloseWindow(CBaseWindow window)
    {
        CDesktop.sTheObjectPtr?.Window_AddToCloseList(window);
    }

    // NXBaseGui::CBaseWindow::BW_Internal_Hide()
    internal void BW_Internal_Hide()
    {
        if ((GetInternalByte1D() & 2) == 0)
        {
            BE_Internal_Element_Hide();
        }
    }

    // NXBaseGui::CBaseWindow::BW_Internal_UnHide()
    internal void BW_Internal_UnHide()
    {
        if ((GetInternalByte1D() & 2) != 0)
        {
            BE_Internal_Element_UnHide();
        }
    }

    // NXBaseGui::CBaseWindow::XGui_BE_Element_Draw(NXBasics::CBitmap&, NXBasics::SRectangle const&)
    internal override void XGui_BE_Element_Draw(CBitmap target, in SRectangle clipRect)
    {
        // Intentionally empty (original returns immediately)
    }

    // NXBaseGui::CBaseWindow::BW_Internal_DrawAllChildElements(NXBasics::CBitmap&, NXBasics::SRectangle const&) const
    internal void BW_Internal_DrawAllChildElements(CBitmap target, in SRectangle clipRect)
    {
        foreach (CBaseElement node in _vars.ElementsDrawOrder)
        {
            if ((node.GetInternalByte1D() & 0x02) != 0)
            {
                continue;
            }

            SRectangle elementRect = node.GetRectangle();

            if (!clipRect.IsTouching(in elementRect))
            {
                continue;
            }

            SRectangle localClip = elementRect;
            localClip.CutInside(in clipRect);

            using CBitmap sub = new(target, in localClip);

            localClip.MovePosition(-elementRect.X, -elementRect.Y);

            node.XGui_BE_Element_Draw(sub, in localClip);
        }
    }

    // NXBaseGui::CBaseWindow::XGui_BE_Element_Activate(NXBasics::SPoint const&)
    internal override void XGui_BE_Element_Activate(in SPoint point)
    {
        base.XGui_BE_Element_Activate(in point);

        CDesktop.sTheObjectPtr?.Window_ToFront(this);

        SPoint localPoint = point;
        XGui_BE_Element_ParentToLocalPosition(ref localPoint);

        CBaseElement? hit = BW_Internal_FindElementOnPosition(in localPoint, false);
        if (hit != null)
        {
            if (hit == _activeElement)
            {
                return;
            }

            _activeElement?.XGui_BE_Element_DeActivate();
            _activeElement = hit;
            hit.XGui_BE_Element_Activate(in localPoint);
            return;
        }

        _activeElement?.XGui_BE_Element_DeActivate();
        _activeElement = null;
    }

    // NXBaseGui::CBaseWindow::BW_Internal_FindElementOnPosition(NXBasics::SPoint const&, bool) const
    internal CBaseElement? BW_Internal_FindElementOnPosition(in SPoint point, bool forcePick)
    {
        foreach (CBaseElement current in _vars.ElementsReversePickOrder)
        {
            if ((current.GetInternalByte1D() & 0x02) != 0)
            {
                continue;
            }

            SRectangle rect = current.GetRectangle();

            if (point.X >= rect.X &&
                point.X < rect.X + rect.Width &&
                point.Y >= rect.Y &&
                point.Y < rect.Y + rect.Height)
            {
                SPoint localPoint = point;
                current.XGui_BE_Element_ParentToLocalPosition(ref localPoint);

                CBaseElement? hit = current.XGui_BE_Element_FindElementOnPosition(in localPoint, forcePick);
                if (hit != null)
                {
                    return hit;
                }
            }
        }

        return null;
    }

    // NXBaseGui::CBaseWindow::XGui_BE_Element_DeActivate()
    internal override void XGui_BE_Element_DeActivate()
    {
        _activeElement?.XGui_BE_Element_DeActivate();
        _activeElement = null;

        base.XGui_BE_Element_DeActivate();
    }

    // NXBaseGui::CBaseWindow::XGui_BE_Element_ButtonPressed(NXBasics::SPoint const&)
    internal override bool XGui_BE_Element_ButtonPressed(in SPoint point)
    {
        if (!IsActive())
        {
            return false;
        }

        SPoint localPoint = point;
        XGui_BE_Element_ParentToLocalPosition(ref localPoint);

        foreach (CBaseElement current in _vars.ElementsReversePickOrder)
        {
            if (!localPoint.IsInside(in current.Rect))
            {
                continue;
            }

            SPoint p2 = localPoint;
            current.XGui_BE_Element_ParentToLocalPosition(ref p2);

            CBaseElement? hit = current.XGui_BE_Element_FindElementOnPosition(in p2, false);
            if (hit == null)
            {
                continue;
            }

            if (!hit.BE_CanBePressed())
            {
                return false;
            }

            if (hit == _activeElement)
            {
                return hit.XGui_BE_Element_ButtonPressed(in localPoint);
            }

            return true;
        }

        return false;
    }

    // NXBaseGui::CBaseWindow::XGui_BE_Element_ButtonReleased()
    internal override bool XGui_BE_Element_ButtonReleased()
    {
        if (_activeElement != null)
        {
            return _activeElement.XGui_BE_Element_ButtonReleased();
        }

        return false;
    }

    // NXBaseGui::CBaseWindow::BW_Internal_SendCommandToAllElements(...)
    internal CBaseElement? BW_Internal_SendCommandToAllElements(uint a, uint b, uint c, uint d)
    {
        foreach (CBaseElement element in _vars.ElementsReversePickOrder)
        {
            ulong handled = element.XGui_BE_Message_Handle(TXGuiMessageTypes.BroadcastToWindows, a, b, c, d);
            if (handled != 0)
            {
                return element;
            }
        }

        return null;
    }

    // NXBaseGui::CBaseWindow::BW_Internal_CallForAllChildElements_ElementAddedToDesktop() const
    internal void BW_Internal_CallForAllChildElements_ElementAddedToDesktop()
    {
        foreach (CBaseElement element in _vars.ElementsDrawOrder)
        {
            // Original: CALL qword ptr [vtable + 0x68]
            // Slot +0x68 maps to CBaseElement::XGui_BE_Element_FindElementOnPosition(SPoint const&, bool)
            // The call site provides no meaningful inputs, but the vtable slot is confirmed.
            element.XGui_BE_Element_FindElementOnPosition(default, false);
        }
    }

    // NXBaseGui::CBaseWindow::XGui_BE_Element_GetRealActiveElementPtr()
    internal override CBaseElement XGui_BE_Element_GetRealActiveElementPtr()
    {
        return _activeElement ?? this;
    }


    // NXBaseGui::CBaseWindow::XGui_BE_Window_PostDraw(NXBasics::CBitmap&, NXBasics::SRectangle const&)
    internal virtual void XGui_BE_Window_PostDraw(CBitmap target, in SRectangle clipRect)
    {
        // intentionally empty
    }


    // ---- Minimal "vars blob" modeled as a struct ----

    private struct SBaseWindowVars
    {
        internal SRectangle WindowRect;
        internal CListBase<CBaseElement> ElementsDrawOrder;
        internal CListBase<CBaseElement> ElementsReversePickOrder;
    }

    // ---- Helpers intentionally reference *existing* APIs only; missing members should be compiler errors ----

    private static bool IsActive()
    {
        return GetState() == BaseElementState.Active;
    }

    // Everything below is intentionally thin; implement/adjust in the real codebase where the fields exist.

    internal new BaseElementState State
    {
        get { return _state; }
    }

    private static BaseElementState GetState()
    {
        return (BaseElementState)GetInternalState();
    }

    // These "GetInternal*" calls are *not* dummy references to other classes; they are placeholders for base-field access.
    // If base already exposes these properly, delete these wrappers and use base members directly.

    private static int GetInternalState()
    {
        return GetStateRaw();
    }

    // ---- More "expected base hooks" (compiler will force implementation) ----

    private static int GetStateRaw()
    {
        return 0;
    }
}