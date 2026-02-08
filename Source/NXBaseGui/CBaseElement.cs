using OpenVikings.NXBasics;

namespace OpenVikings.NXBaseGui
{
    internal class CBaseElement
    {
        internal SRectangle Rect { get; private set; }
        internal int LayerOrType { get; private set; }
        internal int ElementId { get; private set; }

        internal uint Flags { get; private set; }
        internal byte StateFlags { get; set; }

        internal bool Hidden { get; private set; }

        protected CBaseElement(SRectangle rect, int layerOrType, int elementId)
        {
            Rect = rect;
            LayerOrType = layerOrType;
            ElementId = elementId;

            Flags = 0;
            Hidden = false;
        }

        internal void SetRect(SRectangle rect)
        {
            Rect = rect;
        }

        internal void Hide()
        {
            Hidden = true;
        }

        internal bool HasFlag(uint flag)
        {
            return (Flags & flag) != 0;
        }

        internal void SetFlag(uint flag)
        {
            Flags |= flag;
        }

        internal void ClearFlag(uint flag)
        {
            Flags &= ~flag;
        }

        protected internal virtual void OnAddedToDesktop()
        {
        }

        // Mirrors the virtual call at vtable + 0x08 in CDesktop::Element_Delete.
        protected internal virtual void OnDeletedFromDesktop()
        {
        }

        // Mirrors the virtual call at vtable + 0x30 in CDesktop::Element_Remove when desktop slot 0x1F8 equals this element.
        protected internal virtual void OnDesktopSlot1F8Cleared()
        {
        }

        // Mirrors CBaseElement::BE_Internal_Element_MouseLeftArea(this).
        protected internal virtual void OnMouseLeftAreaInternal()
        {
        }
    }
}