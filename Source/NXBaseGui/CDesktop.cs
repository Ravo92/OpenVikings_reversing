using OpenVikings.Interfaces;
using OpenVikings.NXBasics;
using OpenVikings.NXSysKeyManager;
using OpenVikings.NXSysMouseManager;
using static OpenVikings.StructsCollection;

namespace OpenVikings.NXBaseGui;

// Notes:
// - No pointers; all ownership is managed via IDisposable.
// - Methods keep their original presence and naming (incl. l_* helpers).
// - Offsets/vtable-calls from the pseudo code are mapped to explicit virtual methods on CBaseElement/CBaseWindow.
// - Where exact semantics are unclear, conservative defaults are used and marked with TODO comments.

internal sealed class CDesktop : IDisposable
{
    internal static CDesktop? sTheObjectPtr;

    private SRectangle _area;

    internal ref readonly SRectangle Area
    {
        get { return ref _area; }
    }

    private CBitmap? _desktopBitmap;

    private CBaseElement? _activeElement;              // +0x1f8
    private CBaseElement? _mouseOverElement;           // +0x200
    private CBaseElement? _keyboardFocusRoot;          // +0x208
    private CBaseElement? _primaryMessageHandler;      // +0x210
    private CBaseElement? _defaultMessageHandler;      // +0x218

    private readonly DesktopVars _vars;

    // Close list replacement for the old CListBase at +0x220.
    // In the original code it was processed LIFO (RemoveFromEnd).
    private readonly List<CBaseWindow> _toClose;

    private int _doWorkResult;                         // +0x24c
    private bool _desktopElementsDisabled;              // this[600]
    private bool _mouseLeftDown;                       // this[0x248]
    private bool _mouseWheelActive;                    // this[0x249]

    private IDesktopPostDrawHook? _postDrawHook;        // +0x250 (function/object pointer in original)

    // Return value of Desktop_DoWork (0x24c)
    private int _lastDoWorkResult;

    private static readonly SPoint _activateSentinelPoint = new(in _activateSentinelRect);

    private static readonly SRectangle _activateSentinelRect = new(-1, -1, 1, 1);

    internal CBitmap BackBuffer
    {
        get
        {
            if (_desktopBitmap == null)
            {
                throw new InvalidOperationException("Desktop back buffer is not available.");
            }

            return _desktopBitmap;
        }
    }

    internal CDesktop(uint width, uint height, byte colorDepth)
    {
        _vars = new DesktopVars();
        _toClose = [];

        _area = new SRectangle(0, 0, 0, 0);

        L_Desktop_InitDesktopVars();

        _area.SetVariables(0, 0, unchecked((int)width), unchecked((int)height));

        _desktopBitmap = new CBitmap(unchecked(width), unchecked(height), colorDepth);
        _desktopBitmap.Fill(0);

        sTheObjectPtr = this;
    }

    public void Dispose()
    {
        Window_CloseAll();
        Element_DeleteAll();
        _postDrawHook?.Dispose();
        _postDrawHook = null;
        _desktopBitmap?.Dispose();
        _desktopBitmap = null;

        sTheObjectPtr = null;

        L_Desktop_InitDesktopVars();

        _vars.Dispose();
    }

    // Exposes the old "base rectangle" behavior without inheritance.
    internal SRectangle GetArea()
    {
        return _area;
    }

    // NXBaseGui::CDesktop::L_Desktop_InitDesktopVars()
    internal void L_Desktop_InitDesktopVars()
    {
        // This replaces the old MemorySet(this, 0, 0x260) semantics.
        _activeElement = null;
        _mouseOverElement = null;
        _keyboardFocusRoot = null;
        _primaryMessageHandler = null;
        _defaultMessageHandler = null;

        _doWorkResult = 0;

        _desktopElementsDisabled = false;
        _mouseLeftDown = false;
        _mouseWheelActive = false;

        _toClose.Clear();

        _vars.ClearListsOnly();
    }

    // NXBaseGui::CDesktop::DefaultMessageHandler_Set(NXBaseGui::CBaseElement*)
    internal void DefaultMessageHandler_Set(CBaseElement element)
    {
        element.Flags |= BaseElementFlags.DefaultMessageHandler;
        _defaultMessageHandler = element;
    }

    // NXBaseGui::CDesktop::Element_DisableDesktopElements()
    internal void Element_DisableDesktopElements()
    {
        _desktopElementsDisabled = true;
    }

    // NXBaseGui::CDesktop::Element_EnableDesktopElements()
    internal void Element_EnableDesktopElements()
    {
        _desktopElementsDisabled = false;
    }

    // NXBaseGui::CDesktop::Desktop_ChangeResolutionAndColorDepth(unsigned int, unsigned int, unsigned char)
    internal void Desktop_ChangeResolutionAndColorDepth(uint width, uint height, byte colorDepth)
    {
        _desktopBitmap?.Dispose();
        _desktopBitmap = null;

        _area.SetVariables(0, 0, unchecked((int)width), unchecked((int)height));

        _desktopBitmap = new CBitmap(unchecked(width), unchecked(height), colorDepth);
        _desktopBitmap.Fill(0);
    }

    // NXBaseGui::CDesktop::Desktop_DoWork(bool, bool)
    internal int Desktop_DoWork(bool clearBackBuffer, bool inputOnly)
    {
        _lastDoWorkResult = 0;

        L_Message_HandleMouse(inputOnly);
        L_Message_HandleKeyBoard(inputOnly);

        L_Window_WorkOnToCloseList();

        if (clearBackBuffer && _desktopBitmap != null)
        {
            _desktopBitmap.Fill(0);
        }

        // Redraw lists in same order as pseudo code
        L_Desktop_RedrawElementList(_vars.BackgroundDraw, in _area);
        L_Desktop_RedrawElementList(_vars.WindowsDraw, in _area);
        L_Desktop_RedrawElementList(_vars.OverlayDraw, in _area);
        L_Desktop_RedrawElementList(_vars.OverlayWindowsDraw, in _area);

        if (_postDrawHook != null && _desktopBitmap != null)
        {
            _postDrawHook.PostDraw(_desktopBitmap, CMouseManager.sTheObjectPtr);
        }

        return _lastDoWorkResult;
    }

    // NXBaseGui::CDesktop::L_Message_HandleInput(bool)
    internal void L_Message_HandleInput(bool inputOnly)
    {
        L_Message_HandleMouse(inputOnly);
        L_Message_HandleKeyBoard(inputOnly);
    }

    // NXBaseGui::CDesktop::L_Window_WorkOnToCloseList()
    internal void L_Window_WorkOnToCloseList()
    {
        while (true)
        {
            CBaseWindow? window = _vars.WindowsToClose.L_Base_RemoveFromEnd();
            if (window == null)
            {
                break;
            }

            Window_Close(window);
        }
    }

    // NXBaseGui::CDesktop::L_Desktop_RedrawAll(bool)
    private void L_Desktop_RedrawAll(bool clearBackBuffer)
    {
        if (clearBackBuffer && _desktopBitmap != null)
        {
            _desktopBitmap.Fill(0);
        }

        L_Desktop_RedrawElementList(_vars.BackgroundDraw, in _area);
        L_Desktop_RedrawElementList(_vars.WindowsDraw, in _area);
        L_Desktop_RedrawElementList(_vars.OverlayDraw, in _area);
        L_Desktop_RedrawElementList(_vars.OverlayWindowsDraw, in _area);
    }

    // NXBaseGui::CDesktop::Window_CloseAll()
    internal void Window_CloseAll()
    {
        while (true)
        {
            CBaseWindow? window = _vars.OverlayWindowsAll.L_Base_GetStartElement();
            if (window == null)
            {
                break;
            }

            Window_Close(window);
        }

        while (true)
        {
            CBaseWindow? window = _vars.WindowsAll.L_Base_GetStartElement();
            if (window == null)
            {
                break;
            }

            Window_Close(window);
        }
    }

    // NXBaseGui::CDesktop::Element_DeleteAll()
    internal void Element_DeleteAll()
    {
        // Decompile order: overlay elements first, then background elements
        DeleteAllElementsFrom(_vars.OverlayLogical, _vars.OverlayDraw);
        DeleteAllElementsFrom(_vars.BackgroundLogical, _vars.BackgroundDraw);
    }

    private void DeleteAllElementsFrom(CListBase<CBaseElement> logical, CListBase<CBaseElement> draw)
    {
        while (true)
        {
            CBaseElement? element = logical.L_Base_GetStartElement();
            if (element == null)
            {
                break;
            }

            L_Element_DoNotUse(element);

            logical.L_Base_RemoveElement(element);
            draw.L_Base_RemoveElement(element);

            element.DisposeLikeOriginal();
        }
    }

    // NXBaseGui::CDesktop::Element_AddBackground(NXBaseGui::CBaseElement*, bool)
    internal void Element_AddBackground(CBaseElement element, bool addToEnd)
    {
        if ((element.Flags & BaseElementFlags.AddedToDesktop) != 0)
        {
            return;
        }

        element.Flags |= BaseElementFlags.AddedToDesktop;

        element.RectRef.PlaceInside(in _area);

        if (addToEnd)
        {
            _vars.BackgroundDraw.InsertAtStart(element);
            _vars.BackgroundLogical.InsertAtStart(element);
        }
        else
        {
            _vars.BackgroundDraw.InsertAtStart(element);
            _vars.BackgroundLogical.InsertAtEnd(element);
        }

        element.XGui_BE_Element_AddedToDesktop();
    }

    // NXBaseGui::CDesktop::Element_AddOverlay(NXBaseGui::CBaseElement*, bool)
    internal void Element_AddOverlay(CBaseElement element, bool addToEnd)
    {
        if ((element.Flags & BaseElementFlags.AddedToDesktop) != 0)
        {
            return;
        }

        element.Flags |= BaseElementFlags.AddedToDesktop;

        element.RectRef.PlaceInside(in _area);

        if (addToEnd)
        {
            _vars.OverlayDraw.InsertAtStart(element);
            _vars.OverlayLogical.InsertAtStart(element);
        }
        else
        {
            _vars.OverlayDraw.InsertAtStart(element);
            _vars.OverlayLogical.InsertAtEnd(element);
        }

        element.XGui_BE_Element_AddedToDesktop();
    }

    // NXBaseGui::CDesktop::Window_Add(NXBaseGui::CBaseWindow*)
    internal void Window_Add(CBaseWindow window)
    {
        if (window == null)
        {
            return;
        }

        if ((window.Flags & BaseElementFlags.AddedToDesktop) != 0)
        {
            return;
        }

        window.Flags |= BaseElementFlags.AddedToDesktop;

        window.RectRef.PlaceInside(in _area);

        // Decompile order: +0xB8, +0xE0, +0x108, +0x130
        _vars.WindowsAll.InsertAtEnd(window);
        _vars.WindowsFrontA.InsertAtStart(window);
        _vars.WindowsDraw.InsertAtEnd(window);
        _vars.WindowsFrontB.InsertAtStart(window);

        window.BW_Internal_CallForAllChildElements_ElementAddedToDesktop();
        window.XGui_BE_Element_AddedToDesktop();

        // Activate window (decompile does this if current active differs)
        if (!ReferenceEquals(_activeElement, window))
        {
            _activeElement?.XGui_BE_Element_DeActivate();
            _activeElement = window;

            // Decompile passes "(-1,-1)" via a sentinel. With your SPoint, use a stored sentinel.
            window.XGui_BE_Element_Activate(in _activateSentinelPoint);

            CBaseElement root = L_Element_GetRootElementPtr(window);

            if ((root.Flags & BaseElementFlags.KeyboardFocusRootCandidate) != 0)
            {
                _keyboardFocusRoot = root;
            }
            else
            {
                _keyboardFocusRoot = null;
            }
        }
    }

    // NXBaseGui::CDesktop::Window_AddOverlay(NXBaseGui::CBaseWindow*)
    internal void Window_AddOverlay(CBaseWindow window)
    {
        if (window == null)
        {
            return;
        }

        if ((window.Flags & BaseElementFlags.AddedToDesktop) != 0)
        {
            return;
        }

        // Decompile: *(uint *)(window + 0x1c) |= 0x800
        window.Flags |= BaseElementFlags.AddedToDesktop;

        // Decompile also sets window "A8" flag |= 2 here. If that field is not modeled yet, do not invent WindowLayer.
        // (Add it later as a byte in CBaseWindow if needed.)

        window.RectRef.PlaceInside(in _area);

        // Decompile order: +0x158, +0x180, +0x1A8, +0x1D0
        _vars.OverlayWindowsAll.InsertAtEnd(window);
        _vars.OverlayWindowsFrontA.InsertAtStart(window);
        _vars.OverlayWindowsDraw.InsertAtEnd(window);
        _vars.OverlayWindowsFrontB.InsertAtStart(window);

        window.BW_Internal_CallForAllChildElements_ElementAddedToDesktop();
        window.XGui_BE_Element_AddedToDesktop();

        // Activate window (same logic as Window_Add / Element_Activate)
        if (!ReferenceEquals(_activeElement, window))
        {
            _activeElement?.XGui_BE_Element_DeActivate();
            _activeElement = window;

            // Your SPoint has no (int,int) ctor. Use a prebuilt sentinel SPoint representing (-1,-1).
            window.XGui_BE_Element_Activate(in _activateSentinelPoint);

            CBaseElement root = L_Element_GetRootElementPtr(window);

            if ((root.Flags & BaseElementFlags.KeyboardFocusRootCandidate) != 0)
            {
                _keyboardFocusRoot = root;
            }
            else
            {
                _keyboardFocusRoot = null;
            }
        }
    }

    // NXBaseGui::CDesktop::Element_Activate(NXBaseGui::CBaseElement*)
    internal void Element_Activate(CBaseElement element)
    {
        if (element == null)
        {
            return;
        }

        if (ReferenceEquals(_activeElement, element))
        {
            return;
        }

        _activeElement?.XGui_BE_Element_DeActivate();

        _activeElement = element;

        // Decompile passes a sentinel "(-1,-1)" point. Your SPoint has no (int,int) ctor,
        // so a prebuilt sentinel SPoint must be used here.
        _activeElement.XGui_BE_Element_Activate(in _activateSentinelPoint);

        CBaseElement root = L_Element_GetRootElementPtr(_activeElement);

        if ((root.Flags & BaseElementFlags.KeyboardFocusRootCandidate) != 0)
        {
            _keyboardFocusRoot = root;
        }
        else
        {
            _keyboardFocusRoot = null;
        }
    }

    // Placeholder signatures to keep the API surface intact.
    // Implementations depend on the existing CBaseWindow/CBaseElement contracts.
    internal static void Window_Close(CBaseWindow window)
    {
        window.DisposeLikeOriginal();
    }

    // NXBaseGui::CDesktop::Desktop_FindWindow(NXBaseGui::CBaseWindow*)
    internal bool Desktop_FindWindow(CBaseWindow window)
    {
        if (window == null)
        {
            return false;
        }

        if (_vars.OverlayWindowsAll.Contains(window))
        {
            return true;
        }

        if (_vars.WindowsAll.Contains(window))
        {
            return true;
        }

        return false;
    }

    // NXBaseGui::CDesktop::Window_Hide(NXBaseGui::CBaseWindow*)
    internal uint Window_Hide(CBaseWindow window)
    {
        if (window == null)
        {
            return 0;
        }

        // Decompile: vtable +0x80 => "can be hidden?"
        if (!window.XGui_BE_Hide_CanBeHidden())
        {
            return 0;
        }

        // Decompile behavior:
        // If the currently active element is:
        // - a window: compare directly
        // - a child element: compare its ParentWindow against the window being hidden
        CBaseElement? activeElement = _activeElement;
        if (activeElement != null)
        {
            CBaseWindow? activeCompare;

            if (activeElement.XGui_BE_HasSubElements())
            {
                activeCompare = activeElement as CBaseWindow;
            }
            else
            {
                activeCompare = activeElement.ParentWindow;
            }

            if (activeCompare != null && ReferenceEquals(activeCompare, window))
            {
                activeElement.XGui_BE_Element_DeActivate();
                _activeElement = null;

                // Decompile also deactivates the window itself in this case
                window.XGui_BE_Element_DeActivate();
            }
        }

        // Decompile: BW_Internal_Hide
        window.BW_Internal_Hide();

        // Decompile returns *(uint*)(window+0x38)
        return window.UniqueId;
    }

    // NXBaseGui::CDesktop::Window_GetActive() const
    internal CBaseWindow? Window_GetActive()
    {
        CBaseElement? active = _activeElement;
        if (active == null)
        {
            return null;
        }

        // Decompile: if ((flags & 0x40) == 0) return *(+0x28) else return self
        if ((active.Flags & BaseElementFlags.HasSubElements) == 0)
        {
            return active.ParentElement as CBaseWindow;
        }

        return active as CBaseWindow;
    }

    // NXBaseGui::CDesktop::Window_UnHide(NXBaseGui::CBaseWindow*)
    internal static void Window_UnHide(CBaseWindow window)
    {
        if (window == null)
        {
            return;
        }

        window.BW_Internal_UnHide();
    }

    // NXBaseGui::CDesktop::Window_UnHide(unsigned int)
    internal void Window_UnHide(uint uniqueId)
    {
        foreach (CBaseWindow window in _vars.OverlayWindowsAll)
        {
            if (window.UniqueId == uniqueId)
            {
                window.BW_Internal_UnHide();
                return;
            }
        }

        foreach (CBaseWindow window in _vars.WindowsAll)
        {
            if (window.UniqueId == uniqueId)
            {
                window.BW_Internal_UnHide();
                return;
            }
        }
    }

    // NXBaseGui::CDesktop::l_Window_FindByUniqueId(unsigned int)
    internal CBaseWindow? L_Window_FindByUniqueId(uint uniqueId)
    {
        foreach (CBaseWindow window in _vars.OverlayWindowsAll)
        {
            if (window.UniqueId == uniqueId)
            {
                return window;
            }
        }

        foreach (CBaseWindow window in _vars.WindowsAll)
        {
            if (window.UniqueId == uniqueId)
            {
                return window;
            }
        }

        return null;
    }

    // NXBaseGui::CDesktop::Window_HideAll(NXBaseGui::CBaseWindow const*)
    internal void Window_HideAll(CBaseWindow? except)
    {
        HideAllFromList(_vars.OverlayWindowsAll, except);
        HideAllFromList(_vars.WindowsAll, except);
    }

    private void HideAllFromList(CListBase<CBaseWindow> list, CBaseWindow? except)
    {
        foreach (CBaseWindow window in list)
        {
            if (ReferenceEquals(window, except))
            {
                continue;
            }

            // Decompile vfunc +0x80: "can be hidden?"
            if (!window.XGui_BE_Hide_CanBeHidden())
            {
                continue;
            }

            // Decompile: if active window (or active child belongs to this window) -> deactivate
            CBaseWindow? active = Window_GetActive();
            if (active != null && ReferenceEquals(active, window))
            {
                _activeElement?.XGui_BE_Element_DeActivate();
                _activeElement = null;

                // Decompile calls DeActivate on the window too in that case
                window.XGui_BE_Element_DeActivate();
            }

            window.BW_Internal_Hide();
        }
    }

    // NXBaseGui::CDesktop::Window_UnHideAll()
    internal void Window_UnHideAll()
    {
        foreach (CBaseWindow window in _vars.OverlayWindowsAll)
        {
            window.BW_Internal_UnHide();
        }

        foreach (CBaseWindow window in _vars.WindowsAll)
        {
            window.BW_Internal_UnHide();
        }
    }

    // NXBaseGui::CDesktop::Element_Hide(NXBaseGui::CBaseElement*)
    internal uint Element_Hide(CBaseElement? element)
    {
        if (element == null)
        {
            return 0;
        }

        if (!element.XGui_BE_Hide_CanBeHidden())
        {
            return 0;
        }

        L_Element_DoNotUse(element);

        element.BE_Internal_Element_Hide();
        return element.UniqueId;
    }

    // NXBaseGui::CDesktop::l_Element_DoNotUse(NXBaseGui::CBaseElement*)
    internal void L_Element_DoNotUse(CBaseElement element)
    {
        if (ReferenceEquals(_activeElement, element))
        {
            element.XGui_BE_Element_DeActivate();
            _activeElement = null;
        }

        if (ReferenceEquals(_mouseOverElement, element))
        {
            element.BE_Internal_Element_MouseLeftArea();
            _mouseOverElement = null;
        }

        if (ReferenceEquals(_keyboardFocusRoot, element))
        {
            _keyboardFocusRoot = null;
        }

        if (ReferenceEquals(_primaryMessageHandler, element))
        {
            _primaryMessageHandler = null;
        }

        if (ReferenceEquals(_defaultMessageHandler, element))
        {
            _defaultMessageHandler = null;
        }
    }

    // NXBaseGui::CDesktop::Element_UnHide(NXBaseGui::CBaseElement*)
    internal static void Element_UnHide(CBaseElement element)
    {
        if (element == null)
        {
            return;
        }

        element.BE_Internal_Element_UnHide();
    }

    // NXBaseGui::CDesktop::Element_HideAll(NXBaseGui::CBaseElement const*)
    internal void Element_HideAll(CBaseElement? except)
    {
        HideAllFromList(_vars.BackgroundLogical, except);
        HideAllFromList(_vars.OverlayLogical, except);
    }

    private void HideAllFromList(CListBase<CBaseElement> list, CBaseElement? except)
    {
        foreach (CBaseElement element in list)
        {
            if (ReferenceEquals(element, except))
            {
                continue;
            }

            // Decompile vfunc +0x80: "can be hidden?"
            if (!element.XGui_BE_Hide_CanBeHidden())
            {
                continue;
            }

            L_Element_DoNotUse(element);
            element.BE_Internal_Element_Hide();
        }
    }

    // NXBaseGui::CDesktop::Element_UnHideAll()
    internal void Element_UnHideAll()
    {
        UnHideAllFromList(_vars.BackgroundLogical);
        UnHideAllFromList(_vars.OverlayLogical);
    }

    private static void UnHideAllFromList(CListBase<CBaseElement> list)
    {
        foreach (CBaseElement element in list)
        {
            element.BE_Internal_Element_UnHide();
        }
    }

    // NXBaseGui::CDesktop::Element_Disable(NXBaseGui::CBaseElement*)
    internal uint Element_Disable(CBaseElement element)
    {
        L_Element_DoNotUse(element);
        element.BE_Internal_Element_Disable();
        return element.UniqueId;
    }

    // NXBaseGui::CDesktop::Element_Enable(NXBaseGui::CBaseElement*)
    internal static void Element_Enable(CBaseElement element)
    {
        element.BE_Internal_Element_Enable();
    }

    // NXBaseGui::CDesktop::Element_Remove(NXBaseGui::CBaseElement*)
    internal void Element_Remove(CBaseElement element)
    {
        // Decompile: param_1[0x1d] &= 0xF7 (clear bit 0x08)
        element.ElementStateFlags = (byte)(element.ElementStateFlags & 0xF7);

        L_Element_DoNotUse(element);

        _vars.BackgroundDraw.L_Base_RemoveElement(element);
        _vars.BackgroundLogical.L_Base_RemoveElement(element);

        _vars.OverlayDraw.L_Base_RemoveElement(element);
        _vars.OverlayLogical.L_Base_RemoveElement(element);
    }

    // NXBaseGui::CDesktop::Element_Delete(NXBaseGui::CBaseElement*)
    internal void Element_Delete(CBaseElement element)
    {
        Element_Remove(element);
        element.DisposeLikeOriginal();
    }

    // NXBaseGui::CDesktop::L_Element_GetRootElementPtr(NXBaseGui::CBaseElement*)
    private static CBaseElement L_Element_GetRootElementPtr(CBaseElement element)
    {
        CBaseElement current = element;
        CBaseElement? parent = current.ParentElement;

        while (parent != null)
        {
            current = parent;
            parent = current.ParentElement;
        }

        return current;
    }

    // NXBaseGui::CDesktop::Element_LooseFocus(NXBaseGui::CBaseElement*)
    internal void Element_LooseFocus(CBaseElement element)
    {
        if (ReferenceEquals(_activeElement, element))
        {
            element.XGui_BE_Element_DeActivate();
            _activeElement = null;
        }

        CBaseElement? parentElement = element.ParentElement;
        if (parentElement is CBaseWindow parentWindow)
        {
            if (ReferenceEquals(parentWindow.ActiveElementRef, element))
            {
                element.XGui_BE_Element_DeActivate();
                parentWindow.ActiveElementRef = null;
            }
        }

        if (ReferenceEquals(_keyboardFocusRoot, element))
        {
            _keyboardFocusRoot = null;
        }
    }

    // NXBaseGui::CDesktop::Window_AddToCloseList(NXBaseGui::CBaseWindow*)
    internal void Window_AddToCloseList(CBaseWindow? window)
    {
        if (window == null)
        {
            return;
        }

        // Original code prevented duplicates.
        if (_toClose.Contains(window))
        {
            return;
        }

        _toClose.Add(window);
    }

    // NXBaseGui::CDesktop::Window_ToFront(NXBaseGui::CBaseWindow*)
    internal void Window_ToFront(CBaseWindow window)
    {
        bool isNormal = (window.LayerByte & 1) != 0;

        CListBase<CBaseWindow> allList;
        CListBase<CBaseWindow> frontA;
        CListBase<CBaseWindow> drawList;
        CListBase<CBaseWindow> frontB;

        if (isNormal)
        {
            allList = _vars.WindowsAll;
            frontA = _vars.WindowsFrontA;
            drawList = _vars.WindowsDraw;
            frontB = _vars.WindowsFrontB;
        }
        else
        {
            allList = _vars.OverlayWindowsAll;
            frontA = _vars.OverlayWindowsFrontA;
            drawList = _vars.OverlayWindowsDraw;
            frontB = _vars.OverlayWindowsFrontB;
        }

        allList.L_Base_RemoveElement(window);
        frontA.L_Base_RemoveElement(window);
        drawList.L_Base_RemoveElement(window);
        frontB.L_Base_RemoveElement(window);

        allList.InsertAtEnd(window);
        frontA.InsertAtStart(window);
        drawList.InsertAtEnd(window);
        frontB.InsertAtStart(window);
    }

    // NXBaseGui::CDesktop::Window_GetFrontMost() const
    internal CBaseWindow? Window_GetFrontMost()
    {
        return _vars.WindowsFrontA.L_Base_GetStartElement();
    }

    // NXBaseGui::CDesktop::PrimaryMessageHandler_Set(NXBaseGui::CBaseElement*)
    internal void PrimaryMessageHandler_Set(CBaseElement element)
    {
        _primaryMessageHandler = element;
    }

    // NXBaseGui::CDesktop::L_Desktop_Redraw(NXBasics::SRectangle&)
    internal void L_Desktop_Redraw(SRectangle rect)
    {
        L_Desktop_RedrawElementList(_vars.BackgroundDraw, rect);
        L_Desktop_RedrawElementList(_vars.WindowsDraw, rect);
        L_Desktop_RedrawElementList(_vars.OverlayDraw, rect);
        L_Desktop_RedrawElementList(_vars.OverlayWindowsDraw, rect);
    }

    // NXBaseGui::CDesktop::L_Desktop_RedrawElementList(...)
    internal void L_Desktop_RedrawElementList<T>(CListBase<T> list, in SRectangle clipRect)
        where T : CBaseElement
    {
        foreach (T element in list)
        {
            // decompile: if ((byteFlags1D & 2) != 0) continue;   (hidden/skip)
            if ((element.GetInternalByte1D() & 0x02) != 0)
            {
                continue;
            }

            SRectangle elementRect = element.GetRectangle();
            if (!clipRect.IsTouching(in elementRect))
            {
                continue;
            }

            // localClip = elementRect clipped to clipRect
            SRectangle localClip = elementRect;
            localClip.CutInside(in clipRect);

            // sub-bitmap for that region
            using CBitmap sub = new(_desktopBitmap!, in localClip);

            // convert clip into element-local coordinates
            localClip.MovePosition(-elementRect.X, -elementRect.Y);

            // draw element
            element.XGui_BE_Element_Draw(sub, in localClip);

            // decompile: if ((flags & 0x40) != 0) { draw children; post draw; }
            if (element is CBaseWindow window && window.XGui_BE_HasSubElements())
            {
                window.BW_Internal_DrawAllChildElements(sub, in localClip);
                window.XGui_BE_Window_PostDraw(sub, in localClip);
            }
        }
    }

    // NXBaseGui::CDesktop::L_Message_HandleMouse(bool)
    private void L_Message_HandleMouse(bool inputOnly)
    {
        if (CMouseManager.sTheObjectPtr == null)
        {
            return;
        }

        SMouseMessage msg = default;
        CMouseManager.sTheObjectPtr.GetNewMouseMessage(ref msg);

        if (inputOnly)
        {
            return;
        }

        SPoint position = default;
        position.X = msg.X;
        position.Y = msg.Y;

        uint flags = msg.Flags;

        bool leftDown = (flags & (uint)MouseMessageFlags.LeftDown) != 0;
        bool leftUp = (flags & (uint)MouseMessageFlags.LeftUp) != 0;
        bool rightDown = (flags & (uint)MouseMessageFlags.RightDown) != 0;
        bool rightUp = (flags & (uint)MouseMessageFlags.RightUp) != 0;
        bool middleDown = (flags & (uint)MouseMessageFlags.MiddleDown) != 0;
        bool middleUp = (flags & (uint)MouseMessageFlags.MiddleUp) != 0;

        bool moved = (flags & (uint)MouseMessageFlags.Move) != 0;
        bool wheel = (flags & (uint)MouseMessageFlags.Wheel) != 0;

        GuiMessageTypes filter = GuiMessageTypes.None;

        // --- Any button down -----------------------------------------------------
        if (leftDown || rightDown || middleDown)
        {
            if (leftDown)
            {
                _mouseLeftDown = true;
            }

            CBaseElement? hit = Element_FindOnPosition(position, false, filter);
            if (hit != null)
            {
                if (!ReferenceEquals(_activeElement, hit))
                {
                    _activeElement?.XGui_BE_Element_DeActivate();
                    _activeElement = hit;

                    SPoint dummy = default;
                    dummy.X = -1;
                    dummy.Y = -1;
                    hit.XGui_BE_Element_Activate(in dummy);

                    CBaseElement root = L_Element_GetRootElementPtr(hit);
                    _keyboardFocusRoot = (root.Flags & BaseElementFlags.KeyboardFocusRootCandidate) != 0 ? root : null;
                }

                if (leftDown && hit.BE_CanBePressed())
                {
                    SPoint local = position;
                    hit.L_BE_GlobalToLocalCoordinates(ref local);

                    if (hit.XGui_BE_Element_ButtonPressed(in local))
                    {
                        return;
                    }
                }
            }
            else
            {
                if (leftDown)
                {
                    L_Message_DoHandle(TXGuiMessageTypes.MouseLeftDown, (uint)position.X, (uint)position.Y, 0, 0);
                }

                if (rightDown)
                {
                    L_Message_DoHandle(TXGuiMessageTypes.MouseRightDown, (uint)position.X, (uint)position.Y, 0, 0);
                }

                if (middleDown)
                {
                    L_Message_DoHandle(TXGuiMessageTypes.MouseMiddleDown, (uint)position.X, (uint)position.Y, 0, 0);
                }
            }
        }

        // --- X up -------------------------------------------------------------
        if (leftUp)
        {
            _mouseLeftDown = false;

            bool handled = false;
            if (_activeElement != null)
            {
                handled = _activeElement.XGui_BE_Element_ButtonReleased();
            }

            if (!handled)
            {
                L_Message_DoHandle(TXGuiMessageTypes.MouseLeftUp, (uint)position.X, (uint)position.Y, 0, 0);
            }
        }

        // --- Right up ------------------------------------------------------------
        if (rightUp)
        {
            L_Message_DoHandle(TXGuiMessageTypes.MouseRightUp, (uint)position.X, (uint)position.Y, 0, 0);
        }

        // --- Middle up -----------------------------------------------------------
        if (middleUp)
        {
            L_Message_DoHandle(TXGuiMessageTypes.MouseMiddleUp, (uint)position.X, (uint)position.Y, 0, 0);
        }

        // --- Move / Wheel --------------------------------------------------------
        if (moved || wheel)
        {
            CBaseElement? underMouse = Element_FindRealOnPosition(position, true);

            if (underMouse == null)
            {
                _mouseOverElement?.BE_Internal_Element_MouseLeftArea();
                _mouseOverElement = null;
            }
            else
            {
                if (!ReferenceEquals(_mouseOverElement, underMouse))
                {
                    _mouseOverElement?.BE_Internal_Element_MouseLeftArea();
                    _mouseOverElement = underMouse;
                    _mouseOverElement.BE_Internal_Element_MouseEnteredArea();
                }
            }

            if (wheel && msg.WheelDelta != 0)
            {
                _mouseWheelActive = true;
                L_Message_DoHandle(TXGuiMessageTypes.MouseWheel, (uint)position.X, (uint)position.Y, (uint)msg.WheelDelta, 0);
            }

            if (moved)
            {
                L_Message_DoHandle(TXGuiMessageTypes.MouseMove, (uint)position.X, (uint)position.Y, 0, 0);
            }
        }

        // --- Drag (kein eigenes Flag im aktuellen CMouseManager) -----------------
        if (_mouseLeftDown && (msg.TotalDeltaX != 0 || msg.TotalDeltaY != 0))
        {
            CBaseElement? realActive = Element_GetRealActive();
            if (realActive != null)
            {
                realActive.RectRef.MovePosition(msg.TotalDeltaX, msg.TotalDeltaY);
                realActive.RectRef.PlaceInside(in _area);
            }

            L_Message_DoHandle(TXGuiMessageTypes.MouseDrag, (uint)position.X, (uint)position.Y, (uint)msg.TotalDeltaX, (uint)msg.TotalDeltaY);
        }
    }

    // NXBaseGui::CDesktop::l_Message_HandleKeyBoard(bool)
    private void L_Message_HandleKeyBoard(bool inputOnly)
    {
        if (inputOnly)
        {
            CKeyManager.sTheObjectPtr?.Init();
            return;
        }

        CKeyManager? keyMgr = CKeyManager.sTheObjectPtr;
        if (keyMgr == null)
        {
            return;
        }

        while (keyMgr.HasMessages)
        {
            byte state = keyMgr.GetKeyboardMessage(out SKeyMessage msg); // 1=down, 2=up
            if (state == 0)
            {
                return;
            }

            TXGuiMessageTypes type = state == 1 ? TXGuiMessageTypes.KeyDown : TXGuiMessageTypes.KeyUp;

            uint a = msg.Key0;
            uint b = msg.Key1AsByte;
            uint c = (ushort)msg.Extra;
            uint d = (uint)msg.Modifiers;

            // 1) primary handler nur wenn (byteFlag & 4) != 0 (decompile macht so einen Check)
            // TODO: echten Byte-Flag an CBaseElement anbinden; hier vorerst das vorhandene Flag verwendet.
            CBaseElement? primary = _primaryMessageHandler;
            if (primary != null && (primary.Flags & BaseElementFlags.KeyboardFocusRootCandidate) != 0)
            {
                if ((primary.MessageMask & type.Value) != 0)
                {
                    ulong handled = primary.XGui_BE_Message_Handle(type, a, b, c, d);
                    if (handled != 0)
                    {
                        continue;
                    }
                }
            }

            // 2) keyboard focus root (decompile ohne den 0x04 Check)
            CBaseElement? focusRoot = _keyboardFocusRoot;
            if (focusRoot != null)
            {
                if ((focusRoot.MessageMask & type.Value) != 0)
                {
                    ulong handled = focusRoot.XGui_BE_Message_Handle(type, a, b, c, d);
                    if (handled != 0)
                    {
                        continue;
                    }
                }
            }

            // 3) default handler nur wenn (byteFlag & 4) != 0
            // TODO: echten Byte-Flag an CBaseElement anbinden; hier vorerst das vorhandene Flag verwendet.
            CBaseElement? def = _defaultMessageHandler;
            if (def != null && (def.Flags & BaseElementFlags.KeyboardFocusRootCandidate) != 0)
            {
                if ((def.MessageMask & type.Value) != 0)
                {
                    def.XGui_BE_Message_Handle(type, a, b, c, d);
                }
            }
        }
    }

    // NXBaseGui::CDesktop::Element_FindOnPosition(NXBasics::SPoint&, bool, NXBaseGui::TXGuiMessageTypes)
    internal CBaseElement? Element_FindOnPosition(SPoint point, bool allowNonActivatable, GuiMessageTypes filter)
    {
        // Order in pseudo: overlay-window hit-test list, then (if desktop elements enabled) overlay elements, then windows, then background.
        CBaseElement? found = L_Element_FindOnPosition(_vars.OverlayWindowsFrontB, point, allowNonActivatable, filter);
        if (found != null)
        {
            return found;
        }

        if (!_desktopElementsDisabled)
        {
            found = L_Element_FindOnPosition(_vars.OverlayLogical, point, allowNonActivatable, filter);
            if (found != null)
            {
                return found;
            }
        }

        found = L_Element_FindOnPosition(_vars.OverlayWindowsFrontB, point, allowNonActivatable, filter);
        if (found != null)
        {
            return found;
        }

        if (!_desktopElementsDisabled)
        {
            found = L_Element_FindOnPosition(_vars.BackgroundLogical, point, allowNonActivatable, filter);
            return found;
        }

        return null;
    }

    // NXBaseGui::CDesktop::L_Message_DoHandle(NXBaseGui::TXGuiMessageTypes, unsigned int, unsigned int, unsigned int, unsigned int)
    private void L_Message_DoHandle(TXGuiMessageTypes messageType, uint a, uint b, uint c, uint d)
    {
        uint typeValue = messageType.Value;

        // 1) Primary handler first (if it wants this message)
        CBaseElement? primary = _primaryMessageHandler;
        if (primary != null)
        {
            if ((primary.MessageMask & typeValue) != 0)
            {
                ulong handled = primary.XGui_BE_Message_Handle(messageType, a, b, c, d);
                if (handled != 0)
                {
                    return;
                }
            }
        }

        // 2) Active element (if it wants this message)
        CBaseElement? active = _activeElement;
        if (active != null)
        {
            if ((active.MessageMask & typeValue) != 0)
            {
                ulong handled = active.XGui_BE_Message_Handle(messageType, a, b, c, d);
                if (handled != 0)
                {
                    return;
                }
            }
        }

        // 3) Keyboard focus root (if it wants this message)
        CBaseElement? focusRoot = _keyboardFocusRoot;
        if (focusRoot != null)
        {
            if ((focusRoot.MessageMask & typeValue) != 0)
            {
                ulong handled = focusRoot.XGui_BE_Message_Handle(messageType, a, b, c, d);
                if (handled != 0)
                {
                    return;
                }
            }
        }

        // 4) Default message handler last (if it wants this message)
        CBaseElement? def = _defaultMessageHandler;
        if (def != null)
        {
            if ((def.MessageMask & typeValue) != 0)
            {
                def.XGui_BE_Message_Handle(messageType, a, b, c, d);
                return;
            }
        }

        // TODO: Decompile shows additional “broadcast” behavior for some message types.
        // If those bits are identified on TXGuiMessageTypes (e.g. 0x1000 / 0x2000 style),
        // add a pass here that iterates the corresponding desktop/window lists and calls
        // XGui_BE_Message_Handle on each element/window that has (MessageMask & typeValue) != 0.
    }

    private static GuiMessageTypes MapTxToGui(TXGuiMessageTypes messageType)
    {
        // TODO: mapping verifizieren (TEMP: wenn TX intern dieselben Bits nutzt)
        if (messageType.Value == TXGuiMessageTypes.MouseLeftDown.Value) return GuiMessageTypes.MouseLeftDown;
        if (messageType.Value == TXGuiMessageTypes.MouseLeftUp.Value) return GuiMessageTypes.MouseLeftUp;
        if (messageType.Value == TXGuiMessageTypes.MouseRightDown.Value) return GuiMessageTypes.MouseRightDown;
        if (messageType.Value == TXGuiMessageTypes.MouseRightUp.Value) return GuiMessageTypes.MouseRightUp;
        if (messageType.Value == TXGuiMessageTypes.MouseMove.Value) return GuiMessageTypes.MouseMove;
        if (messageType.Value == TXGuiMessageTypes.MouseWheel.Value) return GuiMessageTypes.MouseWheel;
        if (messageType.Value == TXGuiMessageTypes.MouseWheelEnd.Value) return GuiMessageTypes.MouseWheelEnd;
        if (messageType.Value == TXGuiMessageTypes.MouseDrag.Value) return GuiMessageTypes.MouseDrag;

        if (messageType.Value == TXGuiMessageTypes.KeyDown.Value) return GuiMessageTypes.KeyDown;
        if (messageType.Value == TXGuiMessageTypes.KeyUp.Value) return GuiMessageTypes.KeyUp;

        // Unbekannt/ungefiltert
        return GuiMessageTypes.None;
    }

    // NXBaseGui::CDesktop::Element_GetRealActive()
    internal CBaseElement? Element_GetRealActive()
    {
        CBaseElement? active = _activeElement;
        if (active == null)
        {
            return null;
        }

        return active.XGui_BE_Element_GetRealActiveElementPtr();
    }

    // NXBaseGui::CDesktop::Element_FindRealOnPosition(NXBasics::SPoint&, bool)
    internal CBaseElement? Element_FindRealOnPosition(SPoint point, bool forcePick)
    {
        CBaseElement? hit = Element_FindOnPosition(point, allowNonActivatable: false, filter: GuiMessageTypes.None);
        if (hit == null)
        {
            return null;
        }

        // If the hit is a window, try to resolve a child element explicitly (decompile sometimes does that)
        if (hit is CBaseWindow window)
        {
            SPoint local = point;
            window.L_BE_GlobalToLocalCoordinates(ref local);

            CBaseElement? childHit = window.BW_Internal_FindElementOnPosition(in local, forcePick);
            if (childHit != null)
            {
                return childHit;
            }
        }

        return hit;
    }

    // NXBaseGui::CDesktop::L_Element_FindOnPosition(...)
    private static CBaseElement? L_Element_FindOnPosition<T>(CListBase<T> list, SPoint point, bool allowNonActivatable, GuiMessageTypes filter) where T : CBaseElement
    {
        foreach (T element in list)
        {
            // allowNonActivatable || BE_CanBeActivated()
            if (!allowNonActivatable && !element.BE_CanBeActivated())
            {
                continue;
            }

            // hidden check: your convention uses bit 0x02 in the +0x1D byte (GetInternalByte1D)
            if ((element.GetInternalByte1D() & 0x02) != 0)
            {
                continue;
            }

            // message filter check
            if (filter != GuiMessageTypes.None)
            {
                // Requires a MessageMask on CBaseElement (uint or GuiMessageTypes).
                // If MessageMask is uint, compare against (uint)filter.
                if ((element.MessageMask & (uint)filter) == 0)
                {
                    continue;
                }
            }

            // hit test in parent coords against element rect
            SRectangle rect = element.GetRectangle();
            if (point.X < rect.X || point.X >= rect.X + rect.Width || point.Y < rect.Y || point.Y >= rect.Y + rect.Height)
            {
                continue;
            }

            // convert point to element local coords (decompile does this often)
            SPoint local = point;
            element.L_BE_GlobalToLocalCoordinates(ref local);

            // ask element (and its sub-elements) who is really hit
            CBaseElement? hit = element.XGui_BE_Element_FindElementOnPosition(in local, forcePick: allowNonActivatable);
            if (hit != null)
            {
                return hit;
            }
        }

        return null;
    }

    // NXBaseGui::CDesktop::Element_GetUnderMousePtr()
    internal CBaseElement? Element_GetUnderMousePtr()
    {
        CMouseManager? mouseMgr = CMouseManager.sTheObjectPtr;
        if (mouseMgr == null)
        {
            return null;
        }

        SPoint point = mouseMgr.Position;

        CBaseElement? hit = Element_FindOnPosition(point, true, GuiMessageTypes.None);
        if (hit == null)
        {
            return null;
        }

        if (hit is CBaseWindow window)
        {
            SPoint local = point;
            local.X -= window.Rect.X;
            local.Y -= window.Rect.Y;

            CBaseElement? childHit = window.BW_Internal_FindElementOnPosition(in local, true);
            if (childHit != null)
            {
                return childHit;
            }
        }

        return hit;
    }

    // NXBaseGui::CDesktop::Message_HandleUser(...)
    internal void Message_HandleUser(uint p1, uint p2, uint p3, uint p4)
    {
        L_Message_DoHandle(TXGuiMessageTypes.User, p1, p2, p3, p4);
    }

    // NXBaseGui::CDesktop::Window_ChangeAreaRectangle(...)
    internal void Window_ChangeAreaRectangle(CBaseWindow window, SRectangle rect)
    {
        rect.PlaceInside(in _area);

        ref SRectangle dst = ref window.RectRef;
        dst = rect;

        // TODO: decompile copies cached fields (0x48/0x4C). If those are modeled later, update here.
    }

    // NXBaseGui::CDesktop::Message_SendToAllElements(...)
    internal CBaseElement? Message_SendToAllElements(uint p1, uint p2, uint p3, uint p4)
    {
        TXGuiMessageTypes type = TXGuiMessageTypes.BroadcastToElements; // TODO: verify value

        CBaseElement? primary = _primaryMessageHandler;
        if (primary != null && (primary.MessageMask & type.Value) != 0)
        {
            ulong handled = primary.XGui_BE_Message_Handle(type, p1, p2, p3, p4);
            if (handled != 0)
            {
                return primary;
            }
        }

        foreach (CBaseWindow window in _vars.OverlayWindowsFrontB)
        {
            CBaseElement? found = window.BW_Internal_SendCommandToAllElements(p1, p2, p3, p4);
            if (found != null)
            {
                return found;
            }

            if ((window.MessageMask & type.Value) != 0)
            {
                ulong handled = window.XGui_BE_Message_Handle(type, p1, p2, p3, p4);
                if (handled != 0)
                {
                    return window;
                }
            }
        }

        foreach (CBaseWindow window in _vars.WindowsFrontB)
        {
            CBaseElement? found = window.BW_Internal_SendCommandToAllElements(p1, p2, p3, p4);
            if (found != null)
            {
                return found;
            }

            if ((window.MessageMask & type.Value) != 0)
            {
                ulong handled = window.XGui_BE_Message_Handle(type, p1, p2, p3, p4);
                if (handled != 0)
                {
                    return window;
                }
            }
        }

        foreach (CBaseElement element in _vars.OverlayLogical)
        {
            if ((element.MessageMask & type.Value) != 0)
            {
                ulong handled = element.XGui_BE_Message_Handle(type, p1, p2, p3, p4);
                if (handled != 0)
                {
                    return element;
                }
            }
        }

        foreach (CBaseElement element in _vars.BackgroundLogical)
        {
            if ((element.MessageMask & type.Value) != 0)
            {
                ulong handled = element.XGui_BE_Message_Handle(type, p1, p2, p3, p4);
                if (handled != 0)
                {
                    return element;
                }
            }
        }

        CBaseElement? def = _defaultMessageHandler;
        if (def != null && (def.MessageMask & type.Value) != 0)
        {
            ulong handled = def.XGui_BE_Message_Handle(type, p1, p2, p3, p4);
            if (handled != 0)
            {
                return def;
            }
        }

        return null;
    }

    // NXBaseGui::CDesktop::Message_SendToAllWindows(...)
    internal CBaseWindow? Message_SendToAllWindows(uint p1, uint p2, uint p3, uint p4)
    {
        TXGuiMessageTypes type = TXGuiMessageTypes.BroadcastToWindows; // TODO: verify value

        foreach (CBaseWindow window in _vars.OverlayWindowsFrontB)
        {
            if ((window.MessageMask & type.Value) != 0)
            {
                ulong handled = window.XGui_BE_Message_Handle(type, p1, p2, p3, p4);
                if (handled != 0)
                {
                    return window;
                }
            }
        }

        foreach (CBaseWindow window in _vars.WindowsFrontB)
        {
            if ((window.MessageMask & type.Value) != 0)
            {
                ulong handled = window.XGui_BE_Message_Handle(type, p1, p2, p3, p4);
                if (handled != 0)
                {
                    return window;
                }
            }
        }

        return null;
    }

    internal void SetPostDrawHook(IDesktopPostDrawHook? hook)
    {
        _postDrawHook?.Dispose();
        _postDrawHook = hook;
    }
}