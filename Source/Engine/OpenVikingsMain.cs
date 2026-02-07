using OpenVikings.Interfaces;

namespace OpenVikings.Engine
{
    internal class OpenVikingsMain : IOpenVikingsMain
    {
        private readonly ApplicationMessageProcessor _messageProcessor;
        private readonly IMasterControlProgram _masterControlProgram;
        private readonly OpenVikingsGfxSettings _gfx;

        internal OpenVikingsMain(ApplicationMessageProcessor messageProcessor, IMasterControlProgram masterControlProgram, OpenVikingsGfxSettings gfx)
        {
            _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
            _masterControlProgram = masterControlProgram ?? throw new ArgumentNullException(nameof(masterControlProgram));
            _gfx = gfx ?? throw new ArgumentNullException(nameof(gfx));
        }

        public void RunOnce()
        {
            // Legacy logic:
            // if (DoMessages() != 0) {
            //     if (System_Update(...) == 0) return;
            // }
            // ApplicationEnded();

            bool messagesOk = _messageProcessor.DoMessages();

            if (messagesOk)
            {
                bool continueRunning = _masterControlProgram.Update();

                // Continue running (legacy System_Update returned 0 for this case).
                if (continueRunning)
                {
                    return;
                }
            }

            // Signal OpenVikingsApp.MainThreadTick() to end the application.
            _gfx.CallbackTimeMs = 0x7777;
        }
    }
}
