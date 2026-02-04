using System.Runtime.InteropServices;
using System.Text;

namespace OpenVikings.SystemHandles
{
    internal static class IntroOutroHandler
    {
        private enum IntroState
        {
            None = 0,
            ShowingBitmap = 1,
            PlayingVideo = 2,
            Done = 3
        }

        private static int _state;     // IntroState as int
        private static int _hooked;    // 0/1

        private const string MciAlias = "introvid";

        [DllImport("user32.dll")]
        private static extern int ShowCursor(bool bShow);

        private static int _cursorHiddenByUs; // 0/1

        #region MCI (winmm.dll)

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern int mciSendStringW(string command, StringBuilder? returnValue, int returnLength, nint callback);

        private static void MciStopAndClose()
        {
            _ = mciSendStringW("stop " + MciAlias, null, 0, 0);
            _ = mciSendStringW("close " + MciAlias, null, 0, 0);
        }

        private static bool IsVideoPlaying()
        {
            StringBuilder sb = new StringBuilder(64);
            int res = mciSendStringW("status " + MciAlias + " mode", sb, sb.Capacity, 0);
            if (res != 0)
            {
                return false;
            }

            string mode = sb.ToString().Trim();
            return mode.Equals("playing", StringComparison.OrdinalIgnoreCase);
        }

        private static bool PlayMpegInWindow(nint hWnd, string filePath)
        {
            MciStopAndClose();

            int openResult = mciSendStringW("open \"" + filePath + "\" type mpegvideo alias " + MciAlias, null, 0, 0);
            if (openResult != 0)
            {
                return false;
            }

            _ = mciSendStringW("window " + MciAlias + " handle " + hWnd, null, 0, 0);


            if (!WindowHandler.TryGetClientSize(out int w, out int h))
            {
                w = 800;
                h = 600;
            }

            _ = mciSendStringW("put " + MciAlias + " destination at 0 0 " + w + " " + h, null, 0, 0);

            int playRes = mciSendStringW("play " + MciAlias, null, 0, 0);
            if (playRes != 0)
            {
                MciStopAndClose();
                return false;
            }

            HideCursor();
            return true;
        }

        #endregion

        #region Public API

        private static void HideCursor()
        {
            if (Interlocked.Exchange(ref _cursorHiddenByUs, 1) != 0)
            {
                return;
            }

            int result = ShowCursor(false);
            while (result >= 0)
            {
                result = ShowCursor(false);
            }
        }

        private static void ShowCursorAgain()
        {
            if (Interlocked.Exchange(ref _cursorHiddenByUs, 0) == 0)
            {
                return;
            }

            int result = ShowCursor(true);
            while (result < 0)
            {
                result = ShowCursor(true);
            }
        }

        internal static void ShowIntroBmpAndArmSkip()
        {
            string bmpPath = Path.Combine(PathHandler.GetDataXPath(), "Pictures", "pre00.bmp");
            if (File.Exists(bmpPath))
            {
                WindowHandler.SetBitmapFromFile(bmpPath, true);
                Interlocked.Exchange(ref _state, (int)IntroState.ShowingBitmap);
            }
            else
            {
                Interlocked.Exchange(ref _state, (int)IntroState.Done);
            }

            HookInput();
        }

        internal static void ClearAll()
        {
            WindowHandler.ClearBitmap();
            MciStopAndClose();
            ShowCursorAgain();

            Interlocked.Exchange(ref _state, (int)IntroState.None);
            UnhookInput();
        }

        #endregion

        #region Input hook + handler

        private static void HookInput()
        {
            if (Interlocked.Exchange(ref _hooked, 1) != 0)
            {
                return;
            }

            WindowHandler.AnyUserInput += OnAnyUserInput;
        }

        private static void UnhookInput()
        {
            if (Interlocked.Exchange(ref _hooked, 0) == 0)
            {
                return;
            }

            WindowHandler.AnyUserInput -= OnAnyUserInput;
        }

        private static void OnAnyUserInput()
        {
            int current = Interlocked.CompareExchange(ref _state, 0, 0);

            if (current == (int)IntroState.ShowingBitmap)
            {
                WindowHandler.ClearBitmap();

                string videoPath = Path.Combine(PathHandler.GetDataXPath(), "FMV", "GER", "intro.mpg");
                if (File.Exists(videoPath) && PlayMpegInWindow(WindowHandler.WindowHandle, videoPath))
                {
                    Interlocked.Exchange(ref _state, (int)IntroState.PlayingVideo);
                }
                else
                {
                    Interlocked.Exchange(ref _state, (int)IntroState.Done);
                    UnhookInput();
                }

                return;
            }

            if (current == (int)IntroState.PlayingVideo)
            {
                MciStopAndClose();
                ShowCursorAgain();

                Interlocked.Exchange(ref _state, (int)IntroState.Done);
                UnhookInput();
                return;
            }

            if (current == (int)IntroState.Done)
            {
                UnhookInput();
            }
        }

        #endregion
    }
}