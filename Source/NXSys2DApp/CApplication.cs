namespace OpenVikings.NXSys2DApp
{
    // NXSys2DApp::CApplication
    internal static class CApplication
    {
        private static bool _cLeftButtonState;
        private static bool _cRightButtonState;
        private static bool _cMiddleButtonState;

        // NXSys2DApp::CApplication::DoMessages()
        // In the original it always returned 1; quitting was handled by DexterMain() / DexterApp.
        internal static bool DoMessages(Dexter.DexterOS os)
        {
            // ----------------------------
            // Mouse queue consumption
            // ----------------------------
            while (true)
            {
                Dexter.DexterOS.MouseEvent mouseEvent = os.PeekQueuedMouse();
                if (mouseEvent.Type == 0)
                {
                    break;
                }

                mouseEvent = os.GetQueuedMouse();

                // Focus events are injected with Type = focusEventCode (0x04/0x08) and Button = 0.
                // Keep them out of button logic.
                if (mouseEvent.Type == 0x04 || mouseEvent.Type == 0x08)
                {
                    // Focus handling is likely done elsewhere (or via a global focus flag).
                    continue;
                }

                // Type: 1=down, 2=up (as enqueued by DexterOS.MousePressDown/Up).
                bool isDown = mouseEvent.Type == 1;
                bool isUp = mouseEvent.Type == 2;

                byte button = mouseEvent.Button;

                // Buttons 0..2 map to left/right/middle state tracking in original.
                if (button == 0)
                {
                    if (isDown) _cLeftButtonState = true;
                    else if (isUp) _cLeftButtonState = false;
                }
                else if (button == 1)
                {
                    if (isDown) _cRightButtonState = true;
                    else if (isUp) _cRightButtonState = false;
                }
                else if (button == 2)
                {
                    if (isDown) _cMiddleButtonState = true;
                    else if (isUp) _cMiddleButtonState = false;
                }
                else if (button == 3)
                {
                    // Wheel "down": in DexterOS.OSUpdate you generate press+release immediately.
                    // Original reacts on the "up" transition.
                    if (isUp && NXSysMouseManager.CMouseManager.sTheObjectPtr != null)
                    {
                        NXSysMouseManager.CMouseManager.sTheObjectPtr.UpdateMouseWheelState(-1);
                    }
                }
                else if (button == 4)
                {
                    // Wheel "up"
                    if (isUp && NXSysMouseManager.CMouseManager.sTheObjectPtr != null)
                    {
                        NXSysMouseManager.CMouseManager.sTheObjectPtr.UpdateMouseWheelState(1);
                    }
                }
                else if (button == 5)
                {
                    // Special GUI/display delta event (exists in the C++ original).
                    // Your DexterOS currently never enqueues button=5 events, but keep the hook for parity.
                    ApplyGuiDisplayDelta(mouseEvent.X, mouseEvent.Y);
                }

                NXSysMouseManager.CMouseManager.sTheObjectPtr?.UpdateMouseButtonState(_cLeftButtonState, _cMiddleButtonState, _cRightButtonState);
            }

            // ----------------------------
            // Text queue consumption
            // ----------------------------
            while (true)
            {
                byte ascii = os.PeekTextKey();
                if (ascii == 0)
                {
                    break;
                }

                ascii = os.GetTextKey();
                if (ascii == 0)
                {
                    break;
                }

                // Adapt this call to your actual PushKey signature.
                // Original: PushKey(sTheObjectPtr, '\0', bVar1, 1) for text input.
                NXSysKeyManager.CKeyManager.sTheObjectPtr?.PushKey(0, ascii, true, 1);
            }

            // ----------------------------
            // Key queue consumption
            // ----------------------------
            while (true)
            {
                Dexter.DexterOS.KeyEvent keyEvent = os.PeekQueuedKey();
                if (keyEvent.Type == 0)
                {
                    break;
                }

                keyEvent = os.GetQueuedKey();

                if (NXSysKeyManager.CKeyManager.sTheObjectPtr == null)
                {
                    continue;
                }

                // Type: 1=down, 2=up
                short downFlag = keyEvent.Type == 1 ? (short)1 : (short)0;

                // Adapt this call to your actual PushKey signature.
                NXSysKeyManager.CKeyManager.sTheObjectPtr.PushKey(keyEvent.Key, 0, false, downFlag);
            }

            // Final mouse update like original
            NXSysMouseManager.CMouseManager.sTheObjectPtr?.UpdateMouseState();

            return true;
        }

        private static void ApplyGuiDisplayDelta(short dx, short dy)
        {
            // Keep this a guarded hook. Wire it to your GUI manager/display when those types exist in C#.
            if (NC2InGameGuiManager.CGuiManager.sTheObjectPtr == null)
            {
                return;
            }

            if (!NC2InGameGuiManager.CGuiManager.sTheObjectPtr.IsActiveForDisplayDelta)
            {
                return;
            }

            NC2E2.C2DEngineDisplay? display = NC2InGameGuiManager.CGuiManager.sTheObjectPtr.TryGetDisplayForDelta();
            if (display == null)
            {
                return;
            }

            display.DE_SetWantedPositionAddDeltaPixelPoint(dx, dy);
        }
    }
}