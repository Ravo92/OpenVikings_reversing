using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenVikings.SystemHandles
{
    internal static partial class WindowHandler
    {
        private const int WS_OVERLAPPEDWINDOW = 0x00CF0000;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_POPUP = unchecked((int)0x80000000);

        private const int CW_USEDEFAULT = unchecked((int)0x80000000);

        private static readonly IntPtr BLACK_BRUSH = (IntPtr)4;

        private const int SW_SHOW = 5;

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        private static nint clientCursorHandle = 0;

        private static Thread? windowThread;
        private static IntPtr windowHandle = IntPtr.Zero;
        private static WndProcDelegate? wndProcDelegate;

        internal static IntPtr WindowHandle => windowHandle;

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        #region Structs and DLLs

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSW
        {
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hWnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", EntryPoint = "RegisterClassW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClassW([In] ref WNDCLASSW lpWndClass);

        [DllImport("user32.dll", EntryPoint = "UnregisterClassW", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterClassW(string lpClassName, IntPtr hInstance);

        [DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowExW(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", EntryPoint = "UpdateWindow", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "DestroyWindow", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "DefWindowProcW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)]
        private static extern int GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll", EntryPoint = "TranslateMessage", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll", EntryPoint = "DispatchMessageW", SetLastError = true)]
        private static extern IntPtr DispatchMessageW(ref MSG lpMsg);

        [DllImport("user32.dll", EntryPoint = "PostQuitMessage", SetLastError = true)]
        private static extern void PostQuitMessage(int nExitCode);

        [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandleW(string? lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AdjustWindowRectEx(ref RECT lpRect, uint dwStyle, bool bMenu, uint dwExStyle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern nint SetCursor(nint hCursor);

        #endregion

        private static IntPtr WindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
        {
            switch (uMsg)
            {
                case 0x0020: // WM_SETCURSOR
                    {
                        // lParam low-word: HitTest (HTCLIENT = 1)
                        int hitTest = unchecked((short)((long)lParam & 0xFFFF));
                        if (hitTest == 1 && clientCursorHandle != 0) // HTCLIENT
                        {
                            SetCursor(clientCursorHandle);
                            return (IntPtr)1;
                        }

                        break;
                    }

                case 0x0010: // WM_CLOSE
                    _ = DestroyWindow(hWnd);
                    return IntPtr.Zero;

                case 0x0002: // WM_DESTROY
                    PostQuitMessage(0);
                    return IntPtr.Zero;
            }

            return DefWindowProcW(hWnd, uMsg, wParam, lParam);
        }

        internal static void SetClientCursor(nint hCursor)
        {
            clientCursorHandle = hCursor;
        }

        /// <summary>
        /// Creates a windowed window whose client area has the exact specified width and height (e.g. 1280x720).
        /// </summary>
        internal static Task<IntPtr> CreateWindowedWindowAsync(string windowName, int clientWidth, int clientHeight, bool centerOnScreen)
        {
            TaskCompletionSource<IntPtr> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            Thread thread = new(() =>
            {
                try
                {
                    IntPtr hWnd = CreateWindowedWindowOnThisThread(windowName, clientWidth, clientHeight, centerOnScreen);
                    windowHandle = hWnd;

                    tcs.TrySetResult(hWnd);

                    RunMessageLoopOnThisThread();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            })
            {
                IsBackground = false
            };

#pragma warning disable CA1416
            thread.SetApartmentState(ApartmentState.STA);
#pragma warning restore CA1416
            thread.Start();

            windowThread = thread;
            return tcs.Task;
        }

        internal static Task<IntPtr> CreateFullScreenWindowAsync(string windowName)
        {
            return CreateWindowedWindowAsync(windowName, 1280, 720, true);
        }

        private static IntPtr CreateWindowedWindowOnThisThread(string windowName, int clientWidth, int clientHeight, bool centerOnScreen)
        {
            Debug.WriteLine("Creating windowed window...");

            IntPtr hInstance = GetModuleHandleW(null);
            Debug.WriteLine($"GetModuleHandleW: hInstance={hInstance}");

            _ = UnregisterClassW(ConstantsHandler.WINDOW_CLASS_NAME, hInstance);

            wndProcDelegate = new WndProcDelegate(WindowProc);

            WNDCLASSW wc = new()
            {
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProcDelegate),
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = hInstance,
                hIcon = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                hbrBackground = BLACK_BRUSH,
                lpszMenuName = null,
                lpszClassName = ConstantsHandler.WINDOW_CLASS_NAME
            };

            ushort atom = RegisterClassW(ref wc);
            if (atom == 0)
            {
                int err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"RegisterClassW failed. GetLastError={err}");
            }

            WindowSize windowSize = ComputeOuterSizeForClient(clientWidth, clientHeight, WS_POPUP | WS_VISIBLE, 0);

            int x = CW_USEDEFAULT;
            int y = CW_USEDEFAULT;

            if (centerOnScreen)
            {
                int screenW = GetSystemMetrics(SM_CXSCREEN);
                int screenH = GetSystemMetrics(SM_CYSCREEN);

                x = Math.Max(0, (screenW - windowSize.Width) / 2);
                y = Math.Max(0, (screenH - windowSize.Height) / 2);
            }

            IntPtr hWnd = CreateWindowExW(
                0,
                wc.lpszClassName,
                windowName,
                WS_POPUP | WS_VISIBLE,
                x, y, windowSize.Width, windowSize.Height,
                IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

            if (hWnd == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"CreateWindowExW failed. GetLastError={err}");
            }

            _ = ShowWindow(hWnd, SW_SHOW);
            _ = UpdateWindow(hWnd);

            ApplyClientSize(hWnd, clientWidth, clientHeight, centerOnScreen);

            Debug.WriteLine($"Window created: hWnd={hWnd}");
            return hWnd;
        }

        private static void RunMessageLoopOnThisThread()
        {
            Debug.WriteLine("Entering message loop...");

            while (true)
            {
                int ret = GetMessageW(out MSG msg, IntPtr.Zero, 0, 0);

                if (ret == 0)
                {
                    Debug.WriteLine("WM_QUIT received, leaving loop.");
                    break;
                }

                if (ret == -1)
                {
                    int err = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"GetMessageW failed. GetLastError={err}");
                    break;
                }

                _ = TranslateMessage(ref msg);
                _ = DispatchMessageW(ref msg);
            }
        }

        private static void ApplyClientSize(IntPtr hWnd, int clientWidth, int clientHeight, bool centerOnScreen)
        {
            if (hWnd == IntPtr.Zero)
            {
                return;
            }

            if (clientWidth <= 0 || clientHeight <= 0)
            {
                return;
            }

            uint style = unchecked((uint)(long)GetWindowLongPtrW(hWnd, GWL_STYLE));
            uint exStyle = unchecked((uint)(long)GetWindowLongPtrW(hWnd, GWL_EXSTYLE));

            RECT rect = new()
            {
                Left = 0,
                Top = 0,
                Right = clientWidth,
                Bottom = clientHeight
            };

            if (!AdjustWindowRectEx(ref rect, style, false, exStyle))
            {
                return;
            }

            int windowWidth = rect.Right - rect.Left;
            int windowHeight = rect.Bottom - rect.Top;

            int x = 0;
            int y = 0;

            if (centerOnScreen)
            {
                int screenW = GetSystemMetrics(SM_CXSCREEN);
                int screenH = GetSystemMetrics(SM_CYSCREEN);

                x = Math.Max(0, (screenW - windowWidth) / 2);
                y = Math.Max(0, (screenH - windowHeight) / 2);
            }

            _ = SetWindowPos(hWnd, IntPtr.Zero, x, y, windowWidth, windowHeight, SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        private static WindowSize ComputeOuterSizeForClient(int clientWidth, int clientHeight, int style, int exStyle)
        {
            RECT rect = new()
            {
                Left = 0,
                Top = 0,
                Right = clientWidth,
                Bottom = clientHeight
            };

            uint uStyle = unchecked((uint)style);
            uint uExStyle = unchecked((uint)exStyle);

            bool ok = AdjustWindowRectEx(ref rect, uStyle, false, uExStyle);
            if (!ok)
            {
                return new WindowSize(clientWidth, clientHeight);
            }

            int w = rect.Right - rect.Left;
            int h = rect.Bottom - rect.Top;
            return new WindowSize(w, h);
        }

        private readonly struct WindowSize
        {
            internal int Width { get; }
            internal int Height { get; }

            internal WindowSize(int width, int height)
            {
                Width = width;
                Height = height;
            }
        }
    }
}