using OpenVikings.Interfaces;

namespace OpenVikings.Engine
{
    // dexterApp() in the original code. This is the main application class that runs the main loop logic.
    internal sealed class OpenVikingsApp(ITimeSource timeSource, IThreadRelaxer relaxer, IOpenVikingsMain OpenVikingsMain, IOpenVikingsDebug debug, IApplicationLifecycle lifecycle, OpenVikingsGfxSettings gfx, OpenVikingsOsState os)
    {
        private readonly ITimeSource _timeSource = timeSource ?? throw new ArgumentNullException(nameof(timeSource));
        private readonly IThreadRelaxer _relaxer = relaxer ?? throw new ArgumentNullException(nameof(relaxer));
        private readonly IOpenVikingsMain _OpenVikingsMain = OpenVikingsMain ?? throw new ArgumentNullException(nameof(OpenVikingsMain));
        private readonly IOpenVikingsDebug _debug = debug ?? throw new ArgumentNullException(nameof(debug));
        private readonly IApplicationLifecycle _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        private readonly OpenVikingsGfxSettings _gfx = gfx ?? throw new ArgumentNullException(nameof(gfx));
        private readonly OpenVikingsOsState _os = os ?? throw new ArgumentNullException(nameof(os));

        /// <summary>
        /// Controls whether the main tick should run (equivalent to AppContinue != '\0').
        /// </summary>
        internal bool AppContinue { get; set; }

        /// <summary>
        /// Executes a single "tick" of the original MainThread logic.
        /// Returns false if the application should stop; otherwise true.
        /// </summary>
        internal bool MainThreadTick()
        {
            if (!AppContinue)
            {
                return true;
            }

            // Capture timing values BEFORE running the main logic.
            uint startTime = _timeSource.GetMilliseconds();
            uint resetBefore = _os.TimeCheckResetMs;

            // Yield/sleep briefly (matches the original RelaxThread call).
            _relaxer.Relax();

            // Run one iteration of the engine/main logic.
            _OpenVikingsMain.RunOnce();

            // Read callback after running main logic, because it may be updated there.
            uint callback = _gfx.CallbackTimeMs;

            // Special callback: end application.
            if (callback == 0x7777)
            {
                _lifecycle.ApplicationEnded();
                return false;
            }

            // Special callback: reset FPS and return.
            if (callback == 0x6666)
            {
                _debug.UpdateFps(0);
                return true;
            }

            // Capture timing values AFTER running the main logic.
            uint nowTime = _timeSource.GetMilliseconds();
            uint resetAfter = _os.TimeCheckResetMs;

            // Original timing formula:
            // frameTime = (nowTime + (resetBefore - startTime)) - resetAfter
            uint frameTime = (nowTime + (resetBefore - startTime)) - resetAfter;

            _debug.UpdateFps(frameTime);

            // If we are faster than the callback target, wait until we reach it.
            if (frameTime < callback)
            {
                WaitUntilCallbackTime(startTime, resetBefore, callback);
            }

            return true;
        }

        private void WaitUntilCallbackTime(uint startTime, uint resetBefore, uint callbackTimeMs)
        {
            while (true)
            {
                _relaxer.Relax();

                uint nowTime = _timeSource.GetMilliseconds();
                uint resetAfter = _os.TimeCheckResetMs;

                uint elapsed = (nowTime + (resetBefore - startTime)) - resetAfter;

                if (elapsed >= callbackTimeMs)
                {
                    break;
                }
            }
        }
    }
}
