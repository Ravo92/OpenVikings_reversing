using OpenVikings.NXBaseGui;

namespace OpenVikings.Engine
{
    internal static class InGameGuiBase
    {
        internal static void BaseToolDesktopAddBackgroundElement(CBaseElement element, InGameGuiInputManager inputManager)
        {
            CDesktop? desktop = CDesktop.sTheObjectPtr;
            if (desktop == null)
            {
                return;
            }

            desktop.Element_AddBackground(element, addToEnd: true);

            // Mirrors "*InputManager::sTheObjectPtr != 0" check
            if (inputManager != null && inputManager.IsInputBlockedOrActive)
            {
                desktop.Element_Hide(element);
            }
        }

        internal static void BaseToolDesktopAddWindow(CBaseWindow window, bool allowVisibleWhileInputBlocked, InGameGuiInputManager inputManager)
        {
            CDesktop? desktop = CDesktop.sTheObjectPtr;
            if (desktop == null)
            {
                return;
            }

            desktop.Window_Add(window);

            if (inputManager != null && inputManager.IsInputBlockedOrActive && !allowVisibleWhileInputBlocked)
            {
                desktop.Window_Hide(window);
            }
        }
    }
}