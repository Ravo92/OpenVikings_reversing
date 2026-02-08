using OpenVikings.NXBasics;

namespace OpenVikings.NXBaseGui
{
    internal sealed class CDesktop
    {
        private const uint AddedToDesktopFlag = 0x800;

        internal static CDesktop? Current { get; private set; }

        internal bool CloseListEnabled { get; set; }

        internal int X { get; private set; }
        internal int Y { get; private set; }
        internal int Width { get; private set; }
        internal int Height { get; private set; }

        internal CBitmap? BackBuffer { get; private set; }
        internal InitDesktopVars Vars { get; }

        // Mirrors CDesktop internal element slots used by Element_Remove / Window_Add / Window_Hide.
        private CBaseElement? _slot1F8;
        private CBaseElement? _slot200;
        private CBaseElement? _slot208;
        private CBaseElement? _slot210;
        private CBaseElement? _slot218;

        private CBaseElement? _primaryMessageHandler;
        private object? _mousePointer;

        internal CDesktop(int width, int height, byte colorDepthBits)
        {
            Vars = new InitDesktopVars();
            ResetFields();

            X = 0;
            Y = 0;
            Width = width;
            Height = height;

            CBitmap bitmap = new((uint)width, (uint)height, colorDepthBits);
            BackBuffer = bitmap;

            if (Current != null)
            {
                throw new InvalidOperationException("CDesktop was created more than once.");
            }

            Current = this;
        }

        internal void WindowAddToCloseList(CBaseWindow window)
        {
            // Mirrors NXBaseGui::CDesktop::Window_AddToCloseList

            if (window == null)
            {
                return;
            }

            if (CloseListEnabled)
            {
                if (Vars.ListM.Contains(window))
                {
                    return;
                }
            }

            Vars.ListM.InsertAtEnd(window);
        }

        internal void WindowCloseAll()
        {
            // Mirrors NXBaseGui::CDesktop::Window_CloseAll

            while (true)
            {
                CBaseElement element = Vars.ListI.GetStartOrDefault();
                if (element is not CBaseWindow window)
                {
                    break;
                }

                WindowClose(window);
            }

            while (true)
            {
                CBaseElement element = Vars.ListE.GetStartOrDefault();
                if (element is not CBaseWindow window)
                {
                    break;
                }

                WindowClose(window);
            }
        }

        internal CBaseWindow? WindowGetFrontMost()
        {
            // Mirrors NXBaseGui::CDesktop::Window_GetFrontMost -> GetStartElement(this+0xE0)
            CBaseElement element = Vars.ListF.GetStartOrDefault();
            return element as CBaseWindow;
        }

        internal void WindowAdd(CBaseWindow window)
        {
            // Mirrors NXBaseGui::CDesktop::Window_Add

            if (window == null)
            {
                return;
            }

            if (window.HasFlag(AddedToDesktopFlag))
            {
                return;
            }

            window.SetFlag(AddedToDesktopFlag);

            // window[0xA8] |= 1
            window.WindowFlagsA8 = (byte)(window.WindowFlagsA8 | 0x01);

            // PlaceInside(windowRect, desktopRect) - RE semantics via mutating SRectangle::PlaceInside
            SRectangle desktopRect = new SRectangle(X, Y, Width, Height);

            SRectangle windowRect = window.Rect;
            windowRect.PlaceInside(desktopRect);
            window.SetRect(windowRect);

            // Insert into Window Group A lists:
            Vars.ListE.InsertAtEnd(window);
            Vars.ListF.InsertAtStart(window);
            Vars.ListG.InsertAtEnd(window);
            Vars.ListH.InsertAtStart(window);

            // CBaseWindow::BW_Internal_CallForAllChildElements_ElementAddedToDesktop(window)
            window.CallForAllChildElements_ElementAddedToDesktop();

            // vtable + 0x68
            window.OnAddedToDesktop();

            // Active slot handling (this + 0x1F8)
            CBaseWindow? currentActive = _slot1F8 as CBaseWindow;
            if (!ReferenceEquals(currentActive, window))
            {
                // vtable + 0x30 on previous active window
                currentActive?.OnDesktopSlot1F8Cleared();

                _slot1F8 = window;

                // local_20 = -1; vtable + 0x28 (window, &local_20)
                long token = -1;
                window.ConfigureAfterAddedToDesktop(ref token);

                // Traverse chain tail (matches the RE pattern you described)
                CBaseWindow tail = window;
                while (tail.ChainNext != null)
                {
                    tail = tail.ChainNext;
                }

                // if ((tail.Flags & 4) != 0) slot208 = tail else slot208 = null
                if ((tail.Flags & 0x4u) != 0)
                {
                    _slot208 = tail;
                }
                else
                {
                    _slot208 = null;
                }
            }
        }

        internal uint WindowHide(CBaseWindow window)
        {
            // Mirrors NXBaseGui::CDesktop::Window_Hide

            uint result = 0;

            if (window == null)
            {
                return result;
            }

            bool canHide = window.CanBeHiddenOrIsVisible();
            if (!canHide)
            {
                return result;
            }

            if (_slot1F8 is CBaseWindow active)
            {
                CBaseWindow? compare = active;
                if ((active.Flags & 0x40u) == 0)
                {
                    compare = active.ChainNext;
                }

                if (compare != null && ReferenceEquals(compare, window))
                {
                    active.OnDesktopSlot1F8Cleared();
                    _slot1F8 = null;
                    window.OnDesktopSlot1F8Cleared();
                }
            }

            // CBaseWindow::BW_Internal_Hide(window)
            window.InternalHide();

            // return *(uint *)(window + 0x38)
            result = window.HideResultCode;
            return result;
        }

        internal void WindowClose(CBaseWindow window)
        {
            // Mirrors NXBaseGui::CDesktop::Window_Close

            if (window == null)
            {
                return;
            }

            bool closeListEnabled = CloseListEnabled;

            if (closeListEnabled)
            {
                if (Vars.ListM.Contains(window))
                {
                    return;
                }
            }

            CBaseWindow? active = _slot1F8 as CBaseWindow;

            if (ReferenceEquals(active, window))
            {
                active?.OnDesktopSlot1F8Cleared();
                _slot1F8 = null;
            }

            if (closeListEnabled)
            {
                if (Vars.ListM.Contains(window))
                {
                    Vars.ListM.Remove(window);
                }
            }

            bool inGroupA = (window.WindowFlagsA8 & 0x01) != 0;

            if (!inGroupA)
            {
                Vars.ListK.Remove(window);
                Vars.ListL.Remove(window);
                Vars.ListJ.Remove(window);
                Vars.ListI.Remove(window);
            }
            else
            {
                Vars.ListG.Remove(window);
                Vars.ListH.Remove(window);
                Vars.ListF.Remove(window);
                Vars.ListE.Remove(window);
            }

            window.OnDeletedFromDesktop();

            // Active reselection logic (as you described)
            if (ReferenceEquals(active, window))
            {
                CBaseWindow? newActive = null;

                CBaseElement frontB = Vars.ListJ.GetStartOrDefault();
                if (frontB is CBaseWindow frontBWindow)
                {
                    newActive = frontBWindow;
                }
                else
                {
                    CBaseElement frontA = Vars.ListF.GetStartOrDefault();
                    if (frontA is CBaseWindow frontAWindow)
                    {
                        newActive = frontAWindow;
                    }
                }

                CBaseWindow? currentSlotActive = _slot1F8 as CBaseWindow;

                if (newActive != null && !ReferenceEquals(currentSlotActive, newActive))
                {
                    currentSlotActive?.OnDesktopSlot1F8Cleared();

                    _slot1F8 = newActive;

                    long token = -1;
                    newActive.ConfigureAfterAddedToDesktop(ref token);

                    CBaseWindow tail = newActive;
                    while (tail.ChainNext != null)
                    {
                        tail = tail.ChainNext;
                    }

                    if ((tail.Flags & 0x4u) != 0)
                    {
                        _slot208 = tail;
                    }
                    else
                    {
                        _slot208 = null;
                    }
                }
            }
        }

        internal void WindowToFront(CBaseWindow window)
        {
            // Mirrors NXBaseGui::CDesktop::Window_ToFront

            if (window == null)
            {
                return;
            }

            bool inGroupA = (window.WindowFlagsA8 & 0x01) != 0;

            if (!inGroupA)
            {
                Vars.ListI.Remove(window);
                Vars.ListJ.Remove(window);
                Vars.ListK.Remove(window);
                Vars.ListL.Remove(window);

                Vars.ListI.InsertAtEnd(window);
                Vars.ListJ.InsertAtStart(window);
                Vars.ListK.InsertAtEnd(window);
                Vars.ListL.InsertAtStart(window);
                return;
            }

            Vars.ListE.Remove(window);
            Vars.ListF.Remove(window);
            Vars.ListG.Remove(window);
            Vars.ListH.Remove(window);

            Vars.ListE.InsertAtEnd(window);
            Vars.ListF.InsertAtStart(window);
            Vars.ListG.InsertAtEnd(window);
            Vars.ListH.InsertAtStart(window);
        }

        internal void ElementAddBackground(CBaseElement element, bool insertAtEnd)
        {
            // Mirrors NXBaseGui::CDesktop::Element_AddBackground

            if (element == null)
            {
                return;
            }

            if (element.HasFlag(AddedToDesktopFlag))
            {
                return;
            }

            element.SetFlag(AddedToDesktopFlag);

            // PlaceInside(elementRect, desktopRect) - RE semantics via mutating SRectangle::PlaceInside
            SRectangle desktopRect = new SRectangle(X, Y, Width, Height);

            SRectangle elementRect = element.Rect;
            elementRect.PlaceInside(desktopRect);
            element.SetRect(elementRect);

            if (insertAtEnd)
            {
                Vars.ListA.InsertAtEnd(element);
                Vars.ListB.InsertAtStart(element);
            }
            else
            {
                Vars.ListA.InsertAtStart(element);
                Vars.ListB.InsertAtEnd(element);
            }

            element.OnAddedToDesktop();
        }

        internal static void ElementHide(CBaseElement element)
        {
            // Mirrors NXBaseGui::CDesktop::Element_Hide (conceptually)

            if (element == null)
            {
                return;
            }

            element.Hide();
        }

        internal void ElementRemove(CBaseElement element)
        {
            // Mirrors NXBaseGui::CDesktop::Element_Remove

            if (element == null)
            {
                return;
            }

            element.StateFlags = (byte)(element.StateFlags & ~0x08);

            if (ReferenceEquals(_slot1F8, element))
            {
                element.OnDesktopSlot1F8Cleared();
                _slot1F8 = null;
            }

            if (ReferenceEquals(_slot200, element))
            {
                element.OnMouseLeftAreaInternal();
                _slot200 = null;
            }

            if (ReferenceEquals(_slot208, element))
            {
                _slot208 = null;
            }

            if (ReferenceEquals(_slot210, element))
            {
                _slot210 = null;
            }

            if (ReferenceEquals(_slot218, element))
            {
                _slot218 = null;
            }

            Vars.ListA.Remove(element);
            Vars.ListB.Remove(element);
            Vars.ListC.Remove(element);
            Vars.ListD.Remove(element);
        }

        internal void ElementDelete(CBaseElement element)
        {
            // Mirrors NXBaseGui::CDesktop::Element_Delete

            ElementRemove(element);
            element?.OnDeletedFromDesktop();
        }

        internal uint DesktopDoWork(bool clearBackBuffer, bool processInputMessages)
        {
            // Mirrors NXBaseGui::CDesktop::Desktop_DoWork

            uint returnCode = 0;

            // TODO: Input handling controlled by processInputMessages.

            WindowWorkOnToCloseList();

            // TODO: Redraw traversal in RE order when your draw pipeline exists.

            // TODO: Draw mouse pointer if _mousePointer is modeled.

            return returnCode;
        }

        internal void WindowWorkOnToCloseList()
        {
            // Mirrors NXBaseGui::CDesktop::l_Window_WorkOnToCloseList

            while (true)
            {
                CBaseElement element = Vars.ListM.RemoveFromEndOrDefault();
                if (element is not CBaseWindow window)
                {
                    break;
                }

                WindowClose(window);
            }
        }

        internal void SetPrimaryMessageHandler(CBaseElement handler)
        {
            // Mirrors NXBaseGui::CDesktop::PrimaryMessageHandler_Set
            _primaryMessageHandler = handler;
        }

        internal void AttachMousePointer(object mousePointer)
        {
            // Mirrors *(... + 0x250) = mousePointer
            _mousePointer = mousePointer;
        }

        private void ResetFields()
        {
            BackBuffer = null;

            _slot1F8 = null;
            _slot200 = null;
            _slot208 = null;
            _slot210 = null;
            _slot218 = null;

            _primaryMessageHandler = null;
            _mousePointer = null;
        }
    }
}