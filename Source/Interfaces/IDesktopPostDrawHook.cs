using OpenVikings.NXBasics;
using OpenVikings.NXSysMouseManager;

namespace OpenVikings.Interfaces
{
    internal interface IDesktopPostDrawHook : IDisposable
    {
        void PostDraw(CBitmap backBuffer, CMouseManager? mouseManager);
    }
}