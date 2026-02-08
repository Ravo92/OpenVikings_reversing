using OpenVikings.NXBasics;

namespace OpenVikings.NXBaseGui
{
    internal class CBaseWindow : CBaseElement
    {
        internal byte WindowFlagsA8 { get; set; }
        internal CBaseWindow ChainNext { get; set; }
        internal uint HideResultCode { get; set; }

        protected CBaseWindow(SRectangle rect, int layerOrType, int elementId) : base(rect, layerOrType, elementId)
        {
        }

        protected internal virtual bool CanBeHiddenOrIsVisible()
        {
            return true;
        }

        protected internal virtual void CallForAllChildElements_ElementAddedToDesktop()
        {
        }

        protected internal virtual void ConfigureAfterAddedToDesktop(ref long token)
        {
        }

        protected internal virtual void InternalHide()
        {
        }
    }
}