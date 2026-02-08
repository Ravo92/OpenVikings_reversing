namespace OpenVikings.NC2InGameGuiManager
{
    internal sealed class CGuiManagerMousePointer
    {
        // Mirrors: 0x18 bytes of zeroed state after vtable
        // We model this as explicit fields instead of raw memory.

        internal int X { get; private set; }
        internal int Y { get; private set; }
        internal bool Visible { get; private set; }

        internal CGuiManagerMousePointer()
        {
            // Mirrors:
            //   *(vtable) = PTR_DrawMouse
            //   memset(this + 8, 0, 0x18)

            X = 0;
            Y = 0;
            Visible = false;
        }

        internal void SetPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        internal void Show()
        {
            Visible = true;
        }

        internal void Hide()
        {
            Visible = false;
        }

        internal void Draw()
        {
            if (!Visible)
            {
                return;
            }

            // Mirrors DrawMouse vtable target
            // Actual rendering will be implemented once the draw path is RE'd
        }
    }
}