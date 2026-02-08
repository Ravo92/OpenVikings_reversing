using OpenVikings.NC2Logic;

namespace OpenVikings.Engine
{
    internal sealed class LogicTickDispatcher
    {
        private readonly CCallbackManager _callbackManager;

        // Mirrors param_3 in ls_LogicCallback: a global tick counter
        private uint _tickCount;

        internal LogicTickDispatcher(CCallbackManager callbackManager)
        {
            _callbackManager = callbackManager;
            _tickCount = 0;
        }

        internal uint TickCount
        {
            get { return _tickCount; }
        }

        internal void StepOnce()
        {
            // Increment first or after depends on the original engine.
            // RE callbacks treat param_3 as a counter that grows monotonically.
            _tickCount++;

            ExecuteLogicCallbacks(_tickCount);
        }

        private void ExecuteLogicCallbacks(uint tickCount)
        {
            // Mirrors engine logic tick dispatch: type=1, tickCount in parameter "b"
            _callbackManager.InvokeCallbacks(callbackType: 1, a: 0, b: tickCount, c: 0, d: 0);
        }
    }
}
