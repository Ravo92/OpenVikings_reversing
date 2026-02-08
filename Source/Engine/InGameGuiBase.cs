using OpenVikings.NXBaseGui;

namespace OpenVikings.Engine
{
    internal static class InGameGuiBase
    {
        internal static void BaseToolDesktopAddBackgroundElement(CBaseElement element, InGameGuiInputManager inputManager)
        {
            CDesktop.Current.ElementAddBackground(element, insertAtEnd: true);

            // Mirrors "*InputManager::sTheObjectPtr != 0" check
            if (inputManager != null && inputManager.IsInputBlockedOrActive)
            {
                CDesktop.ElementHide(element);
            }
        }

        internal static void BaseToolDesktopAddWindow(CBaseWindow window, bool allowVisibleWhileInputBlocked, InGameGuiInputManager inputManager)
        {
            CDesktop.Current.WindowAdd(window);

            if (inputManager != null && inputManager.IsInputBlockedOrActive && !allowVisibleWhileInputBlocked)
            {
                CDesktop.Current.WindowHide(window);
            }
        }
    }
}