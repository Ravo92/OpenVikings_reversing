using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenVikings.SystemHandles
{
    internal static partial class WindowHandler
    {
        private const int WS_OVERLAPPEDWINDOW = 13565952;
        private const int WS_VISIBLE = 268435456;
        private const int SW_SHOW = 5;

        private static Thread _windowThread;

        [DllImport("user32.dll", EntryPoint = "RegisterClassW", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern ushort RegisterClassW([In] ref WNDCLASS lpWndClass);

        [DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr CreateWindowExW(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [LibraryImport("user32.dll", EntryPoint = "DestroyWindow", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DestroyWindow(IntPtr hWnd);

        [LibraryImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [LibraryImport("user32.dll", EntryPoint = "DefWindowProcA", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        private static partial IntPtr DefWindowProcA(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleA", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        private static partial IntPtr GetModuleHandleA(string? lpModuleName);

        [LibraryImport("user32.dll", EntryPoint = "PostQuitMessage", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        private static partial void PostQuitMessage(int nExitCode);

        [LibraryImport("user32.dll", EntryPoint = "GetMessageA", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetMessageA(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [LibraryImport("user32.dll", EntryPoint = "TranslateMessage", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool TranslateMessage(ref MSG lpMsg);

        [LibraryImport("user32.dll", EntryPoint = "DispatchMessageA", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        private static partial IntPtr DispatchMessageA(ref MSG lpMsg);

        [DllImport("user32.dll", EntryPoint = "UnregisterClassW", SetLastError = true, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterClassW(string lpClassName, IntPtr hInstance);

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        private static WndProcDelegate _wndProcDelegate;

        [StructLayout(LayoutKind.Sequential)]
        private struct WNDCLASS
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

        private static IntPtr WindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
        {
            Debug.WriteLine($"WindowProc: uMsg={uMsg}, hWnd={hWnd}, wParam={wParam}, lParam={lParam}");

            switch (uMsg)
            {
                case 16: // WM_CLOSE
                    Debug.WriteLine("WindowProc: WM_CLOSE received");
                    DestroyWindow(hWnd);
                    PostQuitMessage(0);
                    return IntPtr.Zero;
            }

            return DefWindowProcA(hWnd, uMsg, wParam, lParam);
        }

        internal static void CreateFullScreenWindowAsync(string windowName)
        {
            Thread thread = new(() =>
            {
                CreateFullScreenWindow(windowName);
            })
            {
                IsBackground = true
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            _windowThread = thread;
        }

        internal static void CreateFullScreenWindow(string windowName)
        {
            Debug.WriteLine("Creating full screen window...");

            IntPtr hInstance = GetModuleHandleA(null);
            Debug.WriteLine($"GetModuleHandle: hInstance={hInstance}");

            bool unregistered = UnregisterClassW(ConstantsHandler.WINDOW_CLASS_NAME, hInstance);
            Debug.WriteLine($"UnregisterClassW: unregistered={unregistered}, last error={Marshal.GetLastWin32Error()}");

            _wndProcDelegate = new(WindowProc);

            WNDCLASS wndClass = new()
            {
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = hInstance,
                hIcon = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                hbrBackground = IntPtr.Zero,
                lpszMenuName = null,
                lpszClassName = ConstantsHandler.WINDOW_CLASS_NAME
            };

            Debug.WriteLine("Registering window class...");
            if (RegisterClassW(ref wndClass) == 0)
            {
                Debug.WriteLine($"Failed to register window class. Error code: {Marshal.GetLastWin32Error()}");
                return;
            }

            IntPtr hWnd = CreateWindowExW(0, wndClass.lpszClassName, windowName, WS_OVERLAPPEDWINDOW | WS_VISIBLE, 0, 0, 1920, 1080, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

            if (hWnd == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                Debug.WriteLine($"CreateWindowExW failed. Error code: {errorCode}");
            }

            Debug.WriteLine("Showing window...");
            bool showWindowResult = ShowWindow(hWnd, SW_SHOW);
            Debug.WriteLine($"ShowWindow: result={showWindowResult}, last error={Marshal.GetLastWin32Error()}");

            Debug.WriteLine("Entering message loop...");
            while (GetMessageA(out MSG msg, IntPtr.Zero, 0, 0))
            {
                Debug.WriteLine($"GetMessageA: msg.message={msg.message}, msg.hWnd={msg.hWnd}");
                TranslateMessage(ref msg);
                DispatchMessageA(ref msg);
            }

            Debug.WriteLine("Exiting message loop.");
        }
    }
}
