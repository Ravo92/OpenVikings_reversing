namespace OpenVikings.NC2GuiToolsBase
{
    internal static class FxTool
    {
        internal static void Confirm()
        {
            if (NDSManager.CManager.sTheObjectPtr != null)
            {
                NDSManager.CManager.FireAndForgetLoadAndPlay(NDSManager.CManager.sTheObjectPtr, "data\\engine2d\\bin\\sounds\\gui\\click_confirm.wav", 100, unchecked((int)0xFFFFFFFF), 1, false, false, 100);
            }
        }

        internal static void Fail()
        {
            if (NDSManager.CManager.sTheObjectPtr != null)
            {
                NDSManager.CManager.FireAndForgetLoadAndPlay(NDSManager.CManager.sTheObjectPtr, "data\\engine2d\\bin\\sounds\\gui\\click_fail.wav", 100, unchecked((int)0xFFFFFFFF), 1, false, false, 100);
            }
        }

        internal static void Chat()
        {
            if (NDSManager.CManager.sTheObjectPtr != null)
            {
                NDSManager.CManager.FireAndForgetLoadAndPlay(NDSManager.CManager.sTheObjectPtr, "data\\engine2d\\bin\\sounds\\gui\\chat_incoming.wav", 100, unchecked((int)0xFFFFFFFF), 1, false, false, 100);
            }
        }
    }
}
