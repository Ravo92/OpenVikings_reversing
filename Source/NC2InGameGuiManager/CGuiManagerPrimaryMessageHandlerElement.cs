using OpenVikings.NXBaseGui;
using OpenVikings.NXBasics;

namespace OpenVikings.NC2InGameGuiManager
{
    internal sealed class CGuiManagerPrimaryMessageHandlerElement : CBaseElement
    {
        private const int DefaultLayerOrType = 4;
        private const int DefaultElementId = 0x0A01;

        internal CGuiManagerPrimaryMessageHandlerElement() : base(new SRectangle(0, 0, 0, 0), DefaultLayerOrType, DefaultElementId)
        {
            // In native code, the vtable is set to CBaseToolTextElement.
            // In C#, the runtime type already provides that "vtable" behavior.
        }
    }
}