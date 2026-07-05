using OpenVikings.NC2GuiToolsBase;
using OpenVikings.NC2GuiToolsBase.enums;
using OpenVikings.NXBaseGui;
using OpenVikings.NXBasics;

namespace OpenVikings.NC2InGameGuiBase
{
    internal sealed class CBaseToolGfxButton : CBaseElement
    {
        // this+0x40
        private uint _bobId;

        // this+0x48
        private CPalette? _palette;

        // this[0x50]
        private bool _centerGraphics;

        // this+0x54
        private uint _messageParam0;

        // this+0x58
        private CBaseElement? _messageTarget;

        // this+0x60 (char*)
        private string? _toolTip;

        // this[0x68]
        private bool _toolTipAtOnce;

        // this+0x6C / this+0x70
        private uint _messageParam1;
        private uint _messageParam2;

        // byte flags in the decompile (not part of CBaseElement)
        // this[0x51] == "draw frame/fill" switch
        private bool _drawFrameAndFill;

        // this[0x52] affects frame style (3 - (this[0x52]==0))
        private bool _styleFlag52;

        // this[0x74] is checked in several places to bypass shading / pressed action
        private bool _flag74;

        // this[0x75] disables bob hit test (then always hit)
        private bool _disableBobHitTest;

        internal CBaseToolGfxButton(in SRectangle rect, uint bobId, uint messageParam0, CBaseElement? messageTarget) : base(in rect, 2, new TXGuiMessageTypes(0x140))
        {
            L_Construct(bobId, null, messageParam0, messageTarget);
        }

        internal CBaseToolGfxButton(in SRectangle rect, uint bobId, string? toolTip, uint messageParam0, CBaseElement? messageTarget) : base(in rect, 2, new TXGuiMessageTypes(0x140))
        {
            L_Construct(bobId, toolTip, messageParam0, messageTarget);
        }

        private void L_Construct(uint bobId, string? toolTip, uint messageParam0, CBaseElement? messageTarget)
        {
            _bobId = bobId;
            _palette = null;

            _centerGraphics = true;

            _messageParam0 = messageParam0;
            _messageTarget = messageTarget;

            _toolTip = toolTip;

            _toolTipAtOnce = false;
            _messageParam1 = 0;
            _messageParam2 = 0;

            _drawFrameAndFill = false;
            _styleFlag52 = false;
            _flag74 = false;
            _disableBobHitTest = false;
        }

        internal void SetGraphicsId(uint bobId)
        {
            _bobId = bobId;
        }

        internal void SetPalettePtr(CPalette? palette)
        {
            _palette = palette;
        }

        internal void SetCenterGraphicsFlag(bool centerGraphics)
        {
            _centerGraphics = centerGraphics;
        }

        internal void SetToolTipString(string? toolTip)
        {
            _toolTip = toolTip;
        }

        internal override void DisposeLikeOriginal()
        {
            _toolTip = null;
            base.DisposeLikeOriginal();
        }

        internal override void XGui_BE_Element_Draw(CBitmap target, in SRectangle clipRect)
        {
            if (_drawFrameAndFill)
            {
                TBaseDrawModes mode;

                if (((ElementStateFlags & 0x01) == 0) || _flag74)
                {
                    mode = _styleFlag52 ? TBaseDrawModes.Mode3 : TBaseDrawModes.Mode2;
                }
                else
                {
                    mode = TBaseDrawModes.Mode4;
                }

                DrawTool.DrawTool_FillArea(this, target, clipRect, mode);
                DrawTool.DrawTool_DrawFrame(this, target, clipRect, mode);
            }

            CPalette paletteToUse = _palette ?? Globals.DefaultPalette;

            int bobType = Globals.BobManager.GetBobType(_bobId);

            int x = _centerGraphics ? (Rect.Width / 2) : 0;
            int y = _centerGraphics ? (Rect.Height / 2) : 0;

            bool mouseInside = (Flags & BaseElementFlags.MouseInside) != 0;

            if (bobType == 4)
            {
                int shade = (!mouseInside || _flag74) ? 0x80 : 0xC0;
                Globals.BobManager.PrintBob_UsingShadedAlpha(_bobId, target, x, y, paletteToUse, shade);
                return;
            }

            if (!mouseInside || _flag74)
            {
                Globals.BobManager.PrintBob(_bobId, target, x, y, paletteToUse);
                return;
            }

            Globals.BobManager.PrintBob_Shade_MMX(_bobId, target, x, y, paletteToUse, 0);
        }

        internal override ulong XGui_BE_Message_Handle(TXGuiMessageTypes messageType, uint a, uint b, uint c, uint d)
        {
            // decompile: if (type == 0x40) call vfunc +0x90
            if (messageType.Value == 0x40)
            {
                OnMessage40();
                return 0;
            }

            // decompile: if (type == 0x800 && a == 0x7ef) write out params to pointers and return 1
            // pointer-free: only return success = 1
            if (messageType.Value == 0x800 && a == 0x7EF)
            {
                return 1;
            }

            // decompile: forward 0x100 to parent element pointer at +0x30
            if (messageType.Value == 0x100 && ParentElement != null)
            {
                return ParentElement.XGui_BE_Message_Handle(messageType, a, b, c, d);
            }

            return 0;
        }

        protected override bool XGui_BE_Element_HitTest(in SPoint point)
        {
            if (_drawFrameAndFill || _disableBobHitTest)
            {
                return true;
            }

            SPoint localPoint = point;

            if (_centerGraphics)
            {
                localPoint.X -= Rect.Width / 2;
                localPoint.Y -= Rect.Height / 2;
            }

            return Globals.BobManager.IsBobHit(_bobId, in localPoint);
        }

        internal override void XGui_BE_Element_DoPressedAction()
        {
            // decompile:
            // if (target != 0 && this[0x74]==0) target->Message(0x800, param0, pointer2int(this), param1, param2); FXTool_Confirm()
            if (_messageTarget == null || _flag74)
            {
                return;
            }

            _messageTarget.XGui_BE_Message_Handle(new TXGuiMessageTypes(0x800), _messageParam0, UniqueId, _messageParam1, _messageParam2);
            FxTool.Confirm();
        }

        internal bool XGui_BE_ToolTip_Available(out string? toolTip)
        {
            toolTip = _toolTip;
            return _toolTip != null;
        }

        internal override ulong XGui_BE_ToolTip_AtOnce()
        {
            return _toolTipAtOnce ? 1UL : 0UL;
        }

        // Hook for the decompiled vfunc(+0x90) call on message 0x40.
        private static void OnMessage40()
        {
        }
    }
}