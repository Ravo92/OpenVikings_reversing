using OpenVikings.Interfaces;

namespace OpenVikings.Engine
{
    internal class OpenVikingsMain : IOpenVikingsMain
    {
        private readonly ApplicationMessageProcessor _messageProcessor;
        private readonly IMasterControlProgram _masterControlProgram;
        private readonly OpenVikingsGFXSettings _gfx;
        private readonly LogicTickDispatcher _logicTickDispatcher;

        internal OpenVikingsMain(ApplicationMessageProcessor messageProcessor, IMasterControlProgram masterControlProgram, OpenVikingsGFXSettings gfx, LogicTickDispatcher logicTickDispatcher)
        {
            _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
            _masterControlProgram = masterControlProgram ?? throw new ArgumentNullException(nameof(masterControlProgram));
            _gfx = gfx ?? throw new ArgumentNullException(nameof(gfx));
            _logicTickDispatcher = logicTickDispatcher;
        }

        public void RunOnce()
        {
            // Mirrors: DoMessages()
            bool messagesOk = _messageProcessor.DoMessages();

            if (messagesOk)
            {
                // ------------------------------------------------------------
                // LOGIC TICK (engine type = 1)
                // ------------------------------------------------------------
                _logicTickDispatcher.StepOnce();

                // ------------------------------------------------------------
                // SYSTEM / STATE UPDATE
                // ------------------------------------------------------------
                bool continueRunning = _masterControlProgram.Update();

                if (continueRunning)
                {
                    return;
                }
            }

            // Mirrors legacy exit path
            _gfx.CallbackTimeMs = 0x7777;
        }
    }
}
