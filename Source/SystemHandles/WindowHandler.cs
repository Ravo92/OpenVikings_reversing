using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenVikings.SystemHandles
{
    internal static partial class WindowHandler
    {
        #region Win32 constants

        private const int WS_VISIBLE = 0x10000000;
        private const int WS_POPUP = unchecked((int)0x80000000);

        private const int CW_USEDEFAULT = unchecked((int)0x80000000);

        private const int SW_SHOW = 5;

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        private const int WM_PAINT = 0x000F;
        private const int WM_ERASEBKGND = 0x0014;

        private const uint SRCCOPY = 0x00CC0020;

        private const uint IMAGE_BITMAP = 0;
        private const uint LR_LOADFROMFILE = 0x0010;
        private const uint LR_CREATEDIBSECTION = 0x2000;

        private const uint WM_SETCURSOR = 0x0020;
        private const uint WM_CLOSE = 0x0010;
        private const uint WM_DESTROY = 0x0002;

        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_XBUTTONDOWN = 0x020B;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private static readonly IntPtr BLACK_BRUSH = (IntPtr)4;

        internal static event Action? AnyUserInput;

        #endregion

        #region State

        private static nint clientCursorHandle = 0;

        private static Thread? windowThread;
        private static IntPtr windowHandle = IntPtr.Zero;
        private static WndProcDelegate? wndProcDelegate;

        internal static IntPtr WindowHandle => windowHandle;

        private static readonly Lock _bitmapLock = new();
        private static IntPtr _hBitmap = IntPtr.Zero;
        private static bool _stretchToClient = true;

        #endregion

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        #region Structs

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

        [StructLayout(LayoutKind.Sequential)]
        private struct PAINTSTRUCT
        {
            public IntPtr hdc;
            public bool fErase;
            public RECT rcPaint;
            public bool fRestore;
            public bool fIncUpdate;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] rgbReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        #endregion

        #region user32 / gdi32 imports

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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadImageW(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
            IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool StretchBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
            IntPtr hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc, uint rop);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int GetObjectW(IntPtr h, int c, out BITMAP pv);

        #endregion

        #region Public API

        internal static void SetClientCursor(nint hCursor)
        {
            clientCursorHandle = hCursor;
        }

        internal static void SetBitmapFromFile(string path, bool stretchToClient)
        {
            IntPtr hBmp = LoadImageW(IntPtr.Zero, path, IMAGE_BITMAP, 0, 0, LR_LOADFROMFILE | LR_CREATEDIBSECTION);
            if (hBmp == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException("LoadImageW failed. GetLastError=" + err);
            }

            lock (_bitmapLock)
            {
                DeleteCurrentBitmap_NoLock();
                _hBitmap = hBmp;
                _stretchToClient = stretchToClient;
            }

            RequestRepaint();
        }

        internal static void ClearBitmap()
        {
            lock (_bitmapLock)
            {
                DeleteCurrentBitmap_NoLock();
            }

            RequestRepaint();
        }

        internal static Task<IntPtr> CreateWindowedWindowAsync(string windowName, int clientWidth, int clientHeight, bool centerOnScreen)
        {
            TaskCompletionSource<IntPtr> tcs = new TaskCompletionSource<IntPtr>(TaskCreationOptions.RunContinuationsAsynchronously);

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

        #endregion

        #region Window procedure

        private static IntPtr WindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
        {
            if (uMsg == WM_ERASEBKGND)
            {
                return (IntPtr)1;
            }

            if (uMsg == WM_PAINT)
            {
                return OnPaint(hWnd);
            }

            // Any click / key press => trigger event
            if (uMsg == WM_LBUTTONDOWN ||
                uMsg == WM_RBUTTONDOWN ||
                uMsg == WM_MBUTTONDOWN ||
                uMsg == WM_XBUTTONDOWN ||
                uMsg == WM_KEYDOWN ||
                uMsg == WM_SYSKEYDOWN)
            {
                RaiseAnyUserInput();
            }

            if (uMsg == WM_SETCURSOR)
            {
                int hitTest = unchecked((short)((long)lParam & 0xFFFF));
                if (hitTest == 1 && clientCursorHandle != 0) // HTCLIENT
                {
                    SetCursor(clientCursorHandle);
                    return (IntPtr)1;
                }
            }

            if (uMsg == WM_CLOSE)
            {
                _ = DestroyWindow(hWnd);
                return IntPtr.Zero;
            }

            if (uMsg == WM_DESTROY)
            {
                PostQuitMessage(0);
                return IntPtr.Zero;
            }

            return DefWindowProcW(hWnd, uMsg, wParam, lParam);
        }


        #endregion

        #region Paint

        private static IntPtr OnPaint(IntPtr hWnd)
        {
            IntPtr hdc = BeginPaint(hWnd, out PAINTSTRUCT ps);

            try
            {
                IntPtr hBmp;
                bool stretch;

                lock (_bitmapLock)
                {
                    hBmp = _hBitmap;
                    stretch = _stretchToClient;
                }

                if (hBmp == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                if (!TryGetBitmapInfo(hBmp, out BITMAP bmp))
                {
                    return IntPtr.Zero;
                }

                if (!GetClientRect(hWnd, out RECT clientRect))
                {
                    return IntPtr.Zero;
                }

                int dstW = clientRect.Right - clientRect.Left;
                int dstH = clientRect.Bottom - clientRect.Top;

                DrawBitmapToHdc(hdc, hBmp, bmp.bmWidth, bmp.bmHeight, dstW, dstH, stretch);
            }
            finally
            {
                _ = EndPaint(hWnd, ref ps);
            }

            return IntPtr.Zero;
        }

        private static bool TryGetBitmapInfo(IntPtr hBmp, out BITMAP bmp)
        {
            int size = Marshal.SizeOf<BITMAP>();
            int got = GetObjectW(hBmp, size, out bmp);
            return got != 0;
        }

        private static void DrawBitmapToHdc(IntPtr targetHdc, IntPtr hBmp, int srcW, int srcH, int dstW, int dstH, bool stretch)
        {
            IntPtr memDc = CreateCompatibleDC(targetHdc);
            if (memDc == IntPtr.Zero)
            {
                return;
            }

            try
            {
                IntPtr old = SelectObject(memDc, hBmp);

                try
                {
                    if (stretch)
                    {
                        _ = StretchBlt(targetHdc, 0, 0, dstW, dstH, memDc, 0, 0, srcW, srcH, SRCCOPY);
                    }
                    else
                    {
                        int w = Math.Min(dstW, srcW);
                        int h = Math.Min(dstH, srcH);
                        _ = BitBlt(targetHdc, 0, 0, w, h, memDc, 0, 0, SRCCOPY);
                    }
                }
                finally
                {
                    _ = SelectObject(memDc, old);
                }
            }
            finally
            {
                _ = DeleteDC(memDc);
            }
        }

        private static void DeleteCurrentBitmap_NoLock()
        {
            if (_hBitmap != IntPtr.Zero)
            {
                _ = DeleteObject(_hBitmap);
                _hBitmap = IntPtr.Zero;
            }
        }

        private static void RequestRepaint()
        {
            if (windowHandle != IntPtr.Zero)
            {
                _ = InvalidateRect(windowHandle, IntPtr.Zero, false);
            }
        }

        #endregion

        #region Create window / message loop

        private static IntPtr CreateWindowedWindowOnThisThread(string windowName, int clientWidth, int clientHeight, bool centerOnScreen)
        {
            Debug.WriteLine("Creating windowed window...");

            IntPtr hInstance = GetModuleHandleW(null);

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
                throw new InvalidOperationException("RegisterClassW failed. GetLastError=" + err);
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
                throw new InvalidOperationException("CreateWindowExW failed. GetLastError=" + err);
            }

            _ = ShowWindow(hWnd, SW_SHOW);
            _ = UpdateWindow(hWnd);

            ApplyClientSize(hWnd, clientWidth, clientHeight, centerOnScreen);

            Debug.WriteLine("Window created: hWnd=" + hWnd);
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
                    break;
                }

                if (ret == -1)
                {
                    int err = Marshal.GetLastWin32Error();
                    Debug.WriteLine("GetMessageW failed. GetLastError=" + err);
                    break;
                }

                _ = TranslateMessage(ref msg);
                _ = DispatchMessageW(ref msg);
            }
        }

        private static void ApplyClientSize(IntPtr hWnd, int clientWidth, int clientHeight, bool centerOnScreen)
        {
            if (hWnd == IntPtr.Zero || clientWidth <= 0 || clientHeight <= 0)
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

        internal static bool TryGetClientSize(out int width, out int height)
        {
            width = 0;
            height = 0;

            if (windowHandle == IntPtr.Zero)
            {
                return false;
            }

            if (!GetClientRect(windowHandle, out RECT rect))
            {
                return false;
            }

            width = rect.Right - rect.Left;
            height = rect.Bottom - rect.Top;

            return width > 0 && height > 0;
        }

        private static void RaiseAnyUserInput()
        {
            AnyUserInput?.Invoke();
        }

        #endregion
    }
}