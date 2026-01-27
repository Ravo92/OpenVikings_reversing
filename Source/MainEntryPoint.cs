using OpenVikings.SystemHandles;
using System.Runtime.InteropServices;
using System.Text;
using static OpenVikings.SystemHandles.MutexHandler;

class Program
{

    #region Structs

    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFO
    {
        public uint cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public uint dwX;
        public uint dwY;
        public uint dwXSize;
        public uint dwYSize;
        public uint dwXCountChars;
        public uint dwYCountChars;
        public uint dwFillAttribute;
        public uint dwFlags;
        public ushort wShowWindow;
        public ushort cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CPINFO
    {
        public uint MaxCharSize;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] DefaultChar;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public byte[] LeadByte;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct EXCEPTION_REGISTRATION_RECORD
    {
        public nint Next;
        public nint Handler;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NT_TIB
    {
        public nint ExceptionList;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct TEB
    {
        public NT_TIB NtTib;
    }

    #endregion

    #region DLLImports

    [DllImport("kernel32.dll")]
    private static extern uint GetVersion();

    [DllImport("kernel32.dll")]
    private static extern nint GetCommandLineA();

    [DllImport("kernel32.dll")]
    private static extern nint GetModuleHandleA(nint lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern void GetStartupInfoA(out STARTUPINFO lpStartupInfo);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetEnvironmentStringsW();

    [DllImport("kernel32.dll")]
    private static extern nint GetEnvironmentStrings();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int WideCharToMultiByte(
        uint CodePage,
        uint dwFlags,
        nint lpWideCharStr,
        int cchWideChar,
        StringBuilder lpMultiByteStr,
        int cbMultiByte,
        nint lpDefaultChar,
        nint lpUsedDefaultChar);

    [DllImport("kernel32.dll")]
    private static extern bool FreeEnvironmentStringsW(nint lpszEnvironmentBlock);

    [DllImport("kernel32.dll")]
    private static extern bool FreeEnvironmentStringsA(nint lpszEnvironmentBlock);

    [DllImport("kernel32.dll")]
    private static extern nint HeapCreate(uint flOptions, uint dwInitialSize, uint dwMaximumSize);

    [DllImport("kernel32.dll")]
    private static extern bool HeapDestroy(nint hHeap);

    [DllImport("kernel32.dll")]
    private static extern void ExitProcess(uint uExitCode);

    [DllImport("kernel32.dll")]
    private static extern uint TlsAlloc();

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TlsSetValue(uint dwTlsIndex, nint lpTlsValue);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll")]
    private static extern nint GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    private static extern int GetFileType(nint hFile);

    [DllImport("kernel32.dll")]
    private static extern uint SetHandleCount(uint uNumber);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCPInfo(uint CodePage, out CPINFO lpCPInfo);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern uint GetModuleFileNameA(nint hModule, nint lpFilename, uint nSize);

    [DllImport("user32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern nint LoadCursorFromFileA(string lpFileName);

    #endregion

    #region Globals

    private static nint data_56b5e8;
    private static int data_56b5ec;
    private static nint data_56b600;
    private static uint data_56b700;
    private static uint data_50c6e4;
    private static nint data_569994 = IntPtr.Zero;
    private static nint data_569a20 = IntPtr.Zero;
    private static int data_56b704 = 0;
    private static nint data_569cc0 = IntPtr.Zero;
    private static uint data_56b3c0;
    private static int data_56b5e4;
    private static byte[] data_56b4e0 = new byte[256];
    private static int data_56b3d0;
    private static int data_56b3dc;
    private static int data_569cd0;
    private static int data_56b3d4;
    private static nint data_569bbc = Marshal.AllocHGlobal(260);
    private static nint data_56b714 = IntPtr.Zero;
    private static readonly uint data_56999c = 1;
    private static nint data_50c230 = IntPtr.Zero;
    private static int data_4fe05c = 0;
    private static int data_4fe000 = 0;
    private static readonly nint data_56b4e1;
    private static nint data_50f624;
    private static nint data_50f620;
    private static nint data_5697ec;
    private static nint data_50f6a4;
    private static nint data_554d74;
    private static nint data_50f698;
    private static nint data_4fe164;

    private static OpenVikings.DLLCalls.Gedx8musicdrv.IGedx8MusicDriver dmDriver;

    #endregion


    private delegate void FunctionDelegate();

    // sub_401000()
    [STAThread]
    static void Main(string[] args)
    {
        ArgumentHandler argumentHandler = new();
        argumentHandler.HandleArguments(args);

        if (TryCreateMutex("weltwunder") != MutexCreationResult.Success)
        {
            Environment.Exit(0);
            return;
        }

        InitGameHandler.InitGame();

        nint arg1 = sub_4e5ddd(0x168);
        nint arg4 = sub_4e5ddd(0x18);

        sub_4e5df0(arg1, 0, 0x168);
        Marshal.WriteInt32((IntPtr)arg1, 3);
        Marshal.WriteInt32((IntPtr)(arg1 + 4), 0);

        nint eax = sub_4e5ddd(1);
        if (eax != IntPtr.Zero)
        {
            sub_4069f3(eax);
        }

        PathHandler.GetFolderPath("logs");

        SaveFolderHandler saveFolderHandler = new();
        saveFolderHandler.SetOptionsGameSettingsINIFile("game.ini");

        sub_4e1463(data_5697ec, arg4);
        sub_4e1ca0(data_5697ec);

        // ------------------------------------------------------------
        // Datafile detection
        // ------------------------------------------------------------
        byte var_5 = 0;
        int edi = 9;

        do
        {
            nint var_6c = (nint)Marshal.AllocHGlobal(0x100);
            try
            {
                sub_4e5d8b(var_6c, $"use_data_file_{edi}");

                nint eax_2 = sub_4e1658(var_6c);
                if (eax_2 != IntPtr.Zero && sub_4e5d10(eax_2) > 0 && sub_40659c(eax_2, 0) != 0)
                {
                    nint eax_5 = sub_4e5ddd(0x14);
                    uint eax_6 = eax_5 == IntPtr.Zero ? 0u : sub_40665d(eax_5);

                    sub_4064e4(eax_6);
                    var_5 = 1;
                }
            }
            finally
            {
                Marshal.FreeHGlobal((IntPtr)var_6c);
            }

            edi--;
        }
        while (edi >= 0);

        // ------------------------------------------------------------
        // Fallback: load datax\\libs\\dataXXXX.lib only if var_5 == 0
        // ------------------------------------------------------------
        if (var_5 == 0)
        {
            for (int i = 10; i >= 1; i--)
            {
                nint var_6c = (nint)Marshal.AllocHGlobal(0x100);
                try
                {
                    sub_4e5d8b(var_6c, $"datax\\libs\\data{i:D4}.lib");

                    if (sub_40659c(var_6c, 0) != 0)
                    {
                        nint eax_8 = sub_4e5ddd(0x14);
                        uint eax_9 = eax_8 == IntPtr.Zero ? 0u : sub_40665d(eax_8);

                        sub_4064e4(eax_9);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal((IntPtr)var_6c);
                }
            }
        }

        sub_4063f9("$gameroot$", data_4fe164);

        ApplyCursorIfResolved("datax\\mouse\\mousenormal.cur", arg1, 0x150);
        ApplyCursorIfResolved("datax\\mouse\\mousepressed.cur", arg1, 0x154);
        ApplyCursorIfResolved("datax\\mouse\\mouseright.cur", arg1, 0x158);

        sub_405ac8(sub_4e163b("set_language"));

        if (sub_4791a6(data_554d74) != 0)
        {
            Marshal.WriteInt32((IntPtr)(arg1 + 0x0c), unchecked((int)sub_478d16(data_554d74)));
            Marshal.WriteInt32((IntPtr)(arg1 + 0x10), unchecked((int)sub_478d27(data_554d74)));
            Marshal.WriteInt32((IntPtr)(arg1 + 0x14), unchecked((int)sub_478d5e(data_554d74)));

            nint eax_36 = data_554d74;
            SendMessageA(sub_478ced(eax_36), 0x80, 0, LoadIconA((uint)Marshal.ReadInt32((IntPtr)eax_36), 0x65));
            SendMessageA(sub_478ced(eax_36), 0x80, 1, LoadIconA((uint)Marshal.ReadInt32((IntPtr)eax_36), 0x66));

            IntPtr titlePtr = IntPtr.Zero;
            try
            {
                titlePtr = Marshal.StringToHGlobalAnsi("Weltwunder");
                SendMessageA(sub_478ced(data_554d74), 0x0c, 0, (nint)titlePtr);
            }
            finally
            {
                if (titlePtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(titlePtr);
                }
            }

            sub_4016ee(arg1);

            if (Marshal.ReadByte((IntPtr)(arg1 + 0x14c)) != 0)
            {
                sub_478b53(Marshal.ReadIntPtr((IntPtr)(arg1 + 0x150)));
            }

            sub_401750(arg1);

            nint eax_42 = sub_4e5ddd(0x4300);
            if (eax_42 != IntPtr.Zero)
            {
                sub_40471f(eax_42, sub_478ced(data_554d74), 1);
            }

            sub_4e163b("fx_quality");
            sub_404aac(data_50f6a4, sub_4e163b("fx_volume"));

            if (sub_4e163b("music_mode") == 2)
            {
                sub_4e167d(data_5697ec, "music_mode", 1, 1, IntPtr.Zero);
            }

            nint eax_49 = sub_4e5ddd(0x224);
            if (eax_49 != IntPtr.Zero)
            {
                sub_403524(eax_49);
            }

            string dmPath = Path.Combine(Environment.CurrentDirectory, "datax", "dm2") + "\\";

            dmDriver = OpenVikings.DLLCalls.Gedx8musicdrv.Gedx8musicdrvFacade.GetInterface2_Managed();
            dmDriver.SetBasePath(dmPath);
            dmDriver.Initialize();

            int mode = 0; // TODO: map from settings if needed
            int sampleRate;
            int bytesOrBits;

            if (mode == 1)
            {
                sampleRate = 22050;
                bytesOrBits = 16;
            }
            else if (mode == 2)
            {
                sampleRate = 11025;
                bytesOrBits = 8;
            }
            else
            {
                sampleRate = 44100;
                bytesOrBits = 64;
            }

            int instance = dmDriver.CreateInstance();
            _ = dmDriver.InitSynthesizer(instance, new OpenVikings.DLLCalls.Gedx8musicdrv.Gedx8InitParams(sampleRate, bytesOrBits));

            int dmVolume = sub_4e163b("dm_volume");

            // TODO: dmDriver.SetVolume(dmVolume);
            sub_403932(data_50f698, dmVolume);

            if (sub_4e163b("music_mode") == 3)
            {
                sub_4e167d(data_5697ec, "music_mode", 1, 1, IntPtr.Zero);
            }

            Marshal.WriteInt32((IntPtr)arg1, 2);
        }
    }

    private static void ApplyCursorIfResolved(string cursorRelPath, nint arg1, int cursorOffset)
    {
        nint tmp = (nint)Marshal.AllocHGlobal(0x104);
        try
        {
            if (FUN_00406542(cursorRelPath, tmp))
            {
                string resolved = Marshal.PtrToStringAnsi((IntPtr)tmp) ?? string.Empty;
                nint cursorHandle = LoadCursorFromFileA(resolved);

                Marshal.WriteIntPtr((IntPtr)(arg1 + cursorOffset), (IntPtr)cursorHandle);
            }
        }
        finally
        {
            Marshal.FreeHGlobal((IntPtr)tmp);
        }
    }

    private static void sub_4e5df0(nint ptr, byte value, int size)
    {
        for (int i = 0; i < size; i++)
        {
            Marshal.WriteByte((IntPtr)ptr, i, value);
        }
    }

    private static int sub_4ec5de(int arg1)
    {
        uint flOptions = (uint)(arg1 == 0 ? 0 : 1); // Annahme: flOptions basiert auf dem Argument, wenn arg1 0 ist, dann 0, sonst 1
        nint eax = HeapCreate(flOptions, 0x1000, 0);
        data_56b5e8 = eax;

        if (eax != IntPtr.Zero)
        {
            int eax_1 = sub_4ec496();
            data_56b5ec = eax_1;
            nint eax_2 = IntPtr.Zero;

            if (eax_1 != 3)
            {
                if (eax_1 != 2)
                {
                    return 1;
                }
                eax_2 = sub_4edfa1();
            }
            else
            {
                eax_2 = sub_4ed45a(0x3f8);
            }

            if (eax_2 != IntPtr.Zero)
            {
                return 1;
            }

            HeapDestroy(data_56b5e8);
        }

        return 0;
    }

    private static int sub_4ec496()
    {
        // Implementiere die Logik von sub_4ec496
        return 0;
    }

    private static nint sub_4edfa1()
    {
        // Implementiere die Logik von sub_4edfa1
        return IntPtr.Zero;
    }

    private static nint sub_4ed45a(int size)
    {
        // Implementiere die Logik von sub_4ed45a
        return IntPtr.Zero;
    }

    private static void sub_4e71cb(uint arg1)
    {
        if (data_56999c == 1)
        {
            sub_4ec80c();
        }

        sub_4ec845(arg1);
        ExitProcess(0xff);
    }

    private static void sub_4ec80c()
    {
        // Implementiere die Logik von sub_4ec80c
        Console.WriteLine("sub_4ec80c aufgerufen.");
    }

    private static void sub_4ec845(uint arg1)
    {
        // Implementiere die Logik von sub_4ec845
        Console.WriteLine($"sub_4ec845 aufgerufen mit Argument: {arg1}");
    }

    private static int sub_4eb167()
    {
        sub_4ebdc2();
        uint eax = TlsAlloc();
        data_50c6e4 = eax;

        if (eax != 0xffffffff)
        {
            nint lpTlsValue = sub_4ef3ac(1, 0x74);

            if (lpTlsValue != IntPtr.Zero && TlsSetValue(data_50c6e4, lpTlsValue))
            {
                sub_4eb1bb(lpTlsValue);
                uint eax_2 = GetCurrentThreadId();
                Marshal.WriteInt32(lpTlsValue, 4, unchecked((int)0xffffffff));
                Marshal.WriteInt32(lpTlsValue, 0, (int)eax_2);
                return 1;
            }
        }

        return 0;
    }

    private static void sub_4ebdc2()
    {
        // Implementiere die Logik von sub_4ebdc2
        Console.WriteLine("sub_4ebdc2 aufgerufen.");
    }

    private static nint sub_4ef3ac(int arg1, int arg2)
    {
        // Implementiere die Logik von sub_4ef3ac
        // Hier könnte beispielsweise Speicher allokiert werden
        nint allocatedMemory = Marshal.AllocHGlobal(arg2);
        return allocatedMemory;
    }

    private static void sub_4eb1bb(nint lpTlsValue)
    {
        // Implementiere die Logik von sub_4eb1bb
        Console.WriteLine("sub_4eb1bb aufgerufen.");
    }

    private static uint sub_4ea927()
    {
        nint esi = sub_4e85fc(0x480);

        if (esi == IntPtr.Zero)
        {
            sub_4e71a6(0x1b);
        }

        data_56b600 = esi;
        data_56b700 = 0x20;

        nint i = esi + 0x480;
        while (esi.ToInt64() < i.ToInt64())
        {
            Marshal.WriteByte(esi, 4, 0);
            Marshal.WriteInt32(esi, unchecked((int)0xffffffff));
            Marshal.WriteInt32(esi, 8, 0);
            Marshal.WriteByte(esi, 5, 0xa);
            esi += 0x24;
        }

        STARTUPINFO lpStartupInfo;
        GetStartupInfoA(out lpStartupInfo);

        // Die weiteren Schritte sind abhängig von den spezifischen Details von lpStartupInfo
        // In C# müssen diese Variablen und die Logik, die auf sie zugreift, entsprechend implementiert werden
        // Zum Beispiel:
        short var_1a = 0; // Beispielwert, muss aus STARTUPINFO oder einem anderen Kontext ermittelt werden
        nint var_18 = IntPtr.Zero; // Beispielwert, muss ebenfalls ermittelt werden

        if (var_1a != 0 && var_18 != IntPtr.Zero)
        {
            int i_1 = Marshal.ReadInt32(var_18);
            nint ebx_1 = var_18 + 4;
            nint var_8_1 = ebx_1 + i_1;

            if (i_1 >= 0x800)
            {
                i_1 = 0x800;
            }

            if (data_56b700 < i_1)
            {
                IntPtr[] data_56b604 = new IntPtr[32]; // Beispielhafte Initialisierung, abhängig von der Verwendung
                int index = 0;

                do
                {
                    nint eax_4 = sub_4e85fc(0x480);

                    if (eax_4 == IntPtr.Zero)
                    {
                        i_1 = (int)data_56b700;
                        break;
                    }

                    data_56b700 += 0x20;
                    data_56b604[index++] = eax_4;

                    nint j = eax_4 + 0x480;
                    while (eax_4.ToInt64() < j.ToInt64())
                    {
                        Marshal.WriteByte(eax_4, 4, 0);
                        Marshal.WriteInt32(eax_4, unchecked((int)0xffffffff));
                        Marshal.WriteInt32(eax_4, 8, 0);
                        Marshal.WriteByte(eax_4, 5, 0xa);
                        eax_4 += 0x24;
                    }
                } while (data_56b700 < i_1);
            }

            int esi_2 = 0;
            if (i_1 > 0)
            {
                do
                {
                    nint hFile_1 = Marshal.ReadIntPtr(var_8_1);

                    if (hFile_1 != new IntPtr(unchecked((int)0xffffffff)))
                    {
                        byte eax_5 = Marshal.ReadByte(ebx_1);

                        if ((eax_5 & 1) != 0)
                        {
                            int eax_6 = (eax_5 & 8) == 0 ? GetFileType(hFile_1) : 0;
                            if ((eax_5 & 8) != 0 || eax_6 != 0)
                            {
                                int offset = (esi_2 >> 5) * 0x24 + (esi_2 & 0x1f) * 0x24;
                                nint eax_10 = data_56b600 + offset;
                                Marshal.WriteIntPtr(eax_10, hFile_1);
                                Marshal.WriteByte(eax_10, 4, eax_5);
                            }
                        }
                    }

                    var_8_1 += 4;
                    esi_2++;
                    ebx_1 += 1;
                } while (esi_2 < i_1);
            }
        }

        for (int i_2 = 0; i_2 < 3; i_2++)
        {
            int offset = i_2 * 9 * 4;
            nint esi_3 = data_56b600 + offset;

            if (Marshal.ReadIntPtr(esi_3) != new IntPtr(unchecked((int)0xffffffff)))
            {
                Marshal.WriteByte(esi_3, 4, (byte)(Marshal.ReadByte(esi_3, 4) | 0x80));
            }
            else
            {
                Marshal.WriteByte(esi_3, 4, 0x81);
                int nStdHandle = (i_2 == 0) ? -10 : (i_2 == 1) ? -11 : -12;

                nint hFile = GetStdHandle(nStdHandle);
                if (hFile == new IntPtr(unchecked((int)0xffffffff)))
                {
                    Marshal.WriteByte(esi_3, 4, (byte)(Marshal.ReadByte(esi_3, 4) | 0x40));
                }
                else
                {
                    int eax_16 = GetFileType(hFile);
                    if (eax_16 == 0)
                    {
                        Marshal.WriteByte(esi_3, 4, (byte)(Marshal.ReadByte(esi_3, 4) | 0x40));
                    }
                    else
                    {
                        Marshal.WriteIntPtr(esi_3, hFile);
                        if (eax_16 == 2)
                        {
                            Marshal.WriteByte(esi_3, 4, (byte)(Marshal.ReadByte(esi_3, 4) | 0x40));
                        }
                        else if (eax_16 == 3)
                        {
                            Marshal.WriteByte(esi_3, 4, (byte)(Marshal.ReadByte(esi_3, 4) | 8));
                        }
                    }
                }
            }
        }

        return SetHandleCount(data_56b700);
    }

    private static Func<int, int> data_50c240 = sub_4ea147;
    private static int sub_4e71a6(uint arg1)
    {
        if (data_56999c == 1)
        {
            sub_4ec80c();
        }

        sub_4ec845(arg1);
        return data_50c240(0xff);
    }

    private static int sub_4ea147(int arg)
    {
        // Implementiere die Logik für sub_4ea147
        Console.WriteLine($"sub_4ea147 aufgerufen mit Argument: {arg}");
        return arg; // Beispielrückgabewert
    }

    private static nint sub_4ec337()
    {
        nint esi = IntPtr.Zero;
        nint lpMultiByteStr_2 = IntPtr.Zero;

        if (data_569cc0 == IntPtr.Zero)
        {
            esi = GetEnvironmentStringsW();

            if (esi == IntPtr.Zero)
            {
                nint penv = GetEnvironmentStrings();

                if (penv == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                data_569cc0 = new IntPtr(2);
                lpMultiByteStr_2 = ProcessAnsiEnvironmentStrings(penv);
                FreeEnvironmentStringsA(penv);
                return lpMultiByteStr_2;
            }

            data_569cc0 = new IntPtr(1);
        }
        else if (data_569cc0 == new IntPtr(2))
        {
            nint penv = GetEnvironmentStrings();
            if (penv != IntPtr.Zero)
            {
                lpMultiByteStr_2 = ProcessAnsiEnvironmentStrings(penv);
                FreeEnvironmentStringsA(penv);
            }
            return lpMultiByteStr_2;
        }

        if (esi == IntPtr.Zero)
        {
            esi = GetEnvironmentStringsW();
            if (esi == IntPtr.Zero)
                return IntPtr.Zero;
        }

        lpMultiByteStr_2 = ProcessUnicodeEnvironmentStrings(esi);
        FreeEnvironmentStringsW(esi);

        return lpMultiByteStr_2;
    }

    private static nint ProcessAnsiEnvironmentStrings(nint penv)
    {
        nint penv_1 = penv;

        while (Marshal.ReadByte(penv_1) != 0)
        {
            penv_1 += 1;

            if (Marshal.ReadByte(penv_1) == 0)
            {
                penv_1 += 1;
                if (Marshal.ReadByte(penv_1) == 0)
                    break;
            }
        }

        nint esi_1 = sub_4e85fc((int)(penv_1.ToInt64() - penv.ToInt64()) + 1);

        if (esi_1 != IntPtr.Zero)
        {
            sub_4e6170(esi_1, penv, (int)(penv_1.ToInt64() - penv.ToInt64()) + 1);
        }
        else
        {
            esi_1 = IntPtr.Zero;
        }

        return esi_1;
    }

    private static nint ProcessUnicodeEnvironmentStrings(nint esi)
    {
        nint eax_4 = esi;

        while (Marshal.ReadInt16(eax_4) != 0)
        {
            eax_4 += 2; // Increment by 2 bytes for the next WCHAR

            if (Marshal.ReadInt16(eax_4) == 0)
            {
                eax_4 += 2; // Increment by 2 bytes for the next WCHAR
                if (Marshal.ReadInt16(eax_4) == 0)
                    break;
            }
        }

        int cbMultiByte = WideCharToMultiByte(0, 0, esi, (int)((eax_4.ToInt64() - esi.ToInt64()) / 2 + 1), null, 0, IntPtr.Zero, IntPtr.Zero);

        if (cbMultiByte == 0)
        {
            return IntPtr.Zero;
        }

        nint lpMultiByteStr = sub_4e85fc(cbMultiByte);

        if (lpMultiByteStr != IntPtr.Zero)
        {
            StringBuilder sb = new StringBuilder(cbMultiByte);
            if (WideCharToMultiByte(0, 0, esi, (int)((eax_4.ToInt64() - esi.ToInt64()) / 2 + 1), sb, cbMultiByte, IntPtr.Zero, IntPtr.Zero) == 0)
            {
                sub_4e8513(lpMultiByteStr);
                lpMultiByteStr = IntPtr.Zero;
            }
        }

        return lpMultiByteStr;
    }

    private static nint sub_4e85fc(int size)
    {
        return Marshal.AllocHGlobal(size);
    }

    private static nint sub_4e8513(nint arg1)
    {
        int var_8 = -1;
        int var_c = 0x4fb9a8;
        int var_10 = 0x4ec734;

        TEB teb = new TEB();
        nint exceptionListPtr = teb.NtTib.ExceptionList;
        nint exceptionListPtr_1 = exceptionListPtr;
        teb.NtTib.ExceptionList = exceptionListPtr_1;

        if (arg1 != IntPtr.Zero)
        {
            int eax = Marshal.ReadInt32(data_56b5ec);

            if (eax != 3)
            {
                if (eax == 2)
                {
                    sub_4ebdeb(9);
                    int var_8_3 = 1;
                    nint var_2c;
                    int var_24;
                    nint eax_2 = sub_4ee1fd(arg1, out var_2c, out var_24);

                    if (eax_2 != IntPtr.Zero)
                    {
                        sub_4ee254(var_2c, var_24, eax_2);
                    }

                    var_8 = -1;
                    exceptionListPtr = sub_4e85d5();

                    if (eax_2 == IntPtr.Zero)
                    {
                        exceptionListPtr = HeapFree(data_56b5e8, 0, arg1);
                    }
                }
                else
                {
                    exceptionListPtr = HeapFree(data_56b5e8, 0, arg1);
                }
            }
            else
            {
                sub_4ebdeb(9);
                var_8 = 0;
                nint eax_1 = sub_4ed4a2(arg1);

                if (eax_1 != IntPtr.Zero)
                {
                    sub_4ed4cd(eax_1, arg1);
                }

                var_8 = -1;
                exceptionListPtr = sub_4e857d();

                if (eax_1 == IntPtr.Zero)
                {
                    exceptionListPtr = HeapFree(data_56b5e8, 0, arg1);
                }
            }
        }

        teb.NtTib.ExceptionList = exceptionListPtr_1;
        return exceptionListPtr;
    }

    private static void sub_4ebdeb(int arg)
    {
        Console.WriteLine($"sub_4ebdeb aufgerufen mit Argument: {arg}");
    }

    private static nint sub_4ee1fd(nint arg1, out nint var_2c, out int var_24)
    {
        Console.WriteLine("sub_4ee1fd aufgerufen");
        var_2c = IntPtr.Zero;
        var_24 = 0;
        return IntPtr.Zero;
    }

    private static void sub_4ee254(nint var_2c, int var_24, nint eax_2)
    {
        Console.WriteLine("sub_4ee254 aufgerufen");
    }
    private static nint sub_4e85d5()
    {
        Console.WriteLine("sub_4e85d5 aufgerufen");
        return IntPtr.Zero;
    }

    private static nint HeapFree(nint hHeap, uint dwFlags, nint lpMem)
    {
        Console.WriteLine("HeapFree aufgerufen");
        Marshal.FreeHGlobal(lpMem);
        return IntPtr.Zero;
    }

    private static nint sub_4ed4a2(nint arg1)
    {
        Console.WriteLine("sub_4ed4a2 aufgerufen");
        return IntPtr.Zero;
    }

    private static void sub_4ed4cd(nint eax_1, nint arg1)
    {
        Console.WriteLine("sub_4ed4cd aufgerufen");
    }

    private static nint sub_4e857d()
    {
        Console.WriteLine("sub_4e857d aufgerufen");
        return IntPtr.Zero;
    }

    private static void sub_4e6170(nint destination, nint source, int length)
    {
        byte[] buffer = new byte[length];
        Marshal.Copy(source, buffer, 0, length);
        Marshal.Copy(buffer, 0, destination, length);
    }

    private static nint sub_4ec183(string arg1, nint arg2, nint arg3, ref int arg4, ref int arg5)
    {
        int localArg5 = arg5;
        int localArg4 = arg4;

        localArg5 = 0;
        nint esi = arg3;
        nint edi = arg2;
        localArg4 = 1;
        nint eax_1 = Marshal.StringToHGlobalAnsi(arg1);

        if (edi != IntPtr.Zero)
        {
            Marshal.WriteIntPtr(edi, esi);
            edi += IntPtr.Size;
            arg2 = edi;
        }

        while (true)
        {
            if (Marshal.ReadByte(eax_1) != 0x22)
            {
                while (true)
                {
                    localArg5 += 1;

                    if (esi != IntPtr.Zero)
                    {
                        byte edx = Marshal.ReadByte(eax_1);
                        Marshal.WriteByte(esi, edx);
                        esi += 1;
                    }

                    byte edx_2 = Marshal.ReadByte(eax_1);
                    eax_1 += 1;

                    if ((Marshal.ReadByte(data_56b4e1 + edx_2) & 4) != 0)
                    {
                        localArg5 += 1;

                        if (esi != IntPtr.Zero)
                        {
                            byte ebx_1 = Marshal.ReadByte(eax_1);
                            Marshal.WriteByte(esi, ebx_1);
                            esi += 1;
                        }

                        eax_1 += 1;
                    }

                    if (edx_2 == 0x20 || edx_2 == 0)
                    {
                        if (edx_2 != 0)
                        {
                            if (esi != IntPtr.Zero)
                                Marshal.WriteByte(esi - 1, 0);

                            break;
                        }
                    }
                    else
                    {
                        if (edx_2 == 9)
                            continue;

                        eax_1 -= 1;
                        break;
                    }
                }
            }
            else
            {
                eax_1 += 1;

                while (true)
                {
                    byte edx = Marshal.ReadByte(eax_1);
                    eax_1 += 1;

                    if (edx == 0x22 || edx == 0)
                        break;

                    if ((Marshal.ReadByte(data_56b4e1 + edx) & 4) != 0)
                    {
                        localArg5 += 1;

                        if (esi != IntPtr.Zero)
                        {
                            edx = Marshal.ReadByte(eax_1);
                            Marshal.WriteByte(esi, edx);
                            esi += 1;
                            eax_1 += 1;
                        }
                    }

                    localArg5 += 1;

                    if (esi != IntPtr.Zero)
                    {
                        Marshal.WriteByte(esi, edx);
                        esi += 1;
                    }
                }

                localArg5 += 1;

                if (esi != IntPtr.Zero)
                {
                    Marshal.WriteByte(esi, 0);
                    esi += 1;
                }

                if (Marshal.ReadByte(eax_1) == 0x22)
                    eax_1 += 1;
            }

            arg5 = 0;

            while (Marshal.ReadByte(eax_1) != 0)
            {
                while (true)
                {
                    byte edx = Marshal.ReadByte(eax_1);

                    if (edx != 0x20 && edx != 9)
                        break;

                    eax_1 += 1;
                }

                if (Marshal.ReadByte(eax_1) == 0)
                    break;

                if (edi != IntPtr.Zero)
                {
                    Marshal.WriteIntPtr(edi, esi);
                    edi += IntPtr.Size;
                    arg2 = edi;
                }

                localArg4 += 1;

                while (true)
                {
                    int i_2 = 0;
                    byte edx = 0;

                    while (Marshal.ReadByte(eax_1) == 0x5c)
                    {
                        eax_1 += 1;
                        i_2 += 1;
                    }

                    if (Marshal.ReadByte(eax_1) == 0x22)
                    {
                        if ((i_2 & 1) == 0)
                        {
                            if (arg5 == 0 || Marshal.ReadByte(eax_1 + 1) != 0x22)
                            {
                                arg1 = null;
                            }
                            else
                            {
                                eax_1 += 1;
                            }

                            edi = arg2;
                            arg5 = 0;
                        }

                        i_2 /= 2;
                    }

                    if (i_2 != 0)
                    {
                        for (int i = 0; i < i_2; i++)
                        {
                            if (esi != IntPtr.Zero)
                            {
                                Marshal.WriteByte(esi, 0x5c);
                                esi += 1;
                            }

                            localArg5 += 1;
                        }
                    }

                    edx = Marshal.ReadByte(eax_1);

                    if (edx == 0)
                        break;

                    if (arg5 == 0 && (edx == 0x20 || edx == 9))
                        break;

                    if (arg1 != null)
                    {
                        if (esi != IntPtr.Zero)
                        {
                            if ((Marshal.ReadByte(data_56b4e1 + edx) & 4) != 0)
                            {
                                Marshal.WriteByte(esi, edx);
                                esi += 1;
                                eax_1 += 1;
                                localArg5 += 1;
                            }

                            edx = Marshal.ReadByte(eax_1);
                            Marshal.WriteByte(esi, edx);
                            esi += 1;
                        }
                        else if ((Marshal.ReadByte(data_56b4e1 + edx) & 4) != 0)
                        {
                            eax_1 += 1;
                            localArg5 += 1;
                        }

                        localArg5 += 1;
                    }

                    eax_1 += 1;
                }

                if (esi != IntPtr.Zero)
                {
                    Marshal.WriteByte(esi, 0);
                    esi += 1;
                }

                localArg5 += 1;
            }

            if (edi != IntPtr.Zero)
                Marshal.WriteIntPtr(edi, IntPtr.Zero);

            arg4 = localArg4 + 1;
            arg5 = localArg5;
            return new IntPtr(arg4);
        }
    }

    private static int sub_4ecbbc(uint arg1)
    {
        sub_4ebdeb(0x19);
        uint CodePage = sub_4ecd69(arg1);
        int result;

        if (CodePage != data_56b3c0)
        {
            if (CodePage == 0)
            {
                sub_4ecde6();
            }
            else
            {
                bool found = false;
                int index = 0;

                // Assume data_50cb58 as an array of uint32_t representing CodePage entries
                uint[] data_50cb58 = new uint[12];  // Beispielinitialisierung, anpassen nach Bedarf

                for (index = 0; index < data_50cb58.Length; index++)
                {
                    if (data_50cb58[index] == CodePage)
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    // Example logic, this must be tailored based on actual data
                    int esi_2 = index * 0x30;
                    Array.Clear(data_56b4e0, 0, data_56b4e0.Length);

                    byte[] ebx_1 = new byte[8];  // Beispielhafte Initialisierung, anpassen nach Bedarf

                    for (int i = 0; i < 4; i++)
                    {
                        if (ebx_1[0] != 0)
                        {
                            for (int j = 1; j < ebx_1.Length; j += 2)
                            {
                                if (ebx_1[j] == 0)
                                    break;

                                uint eax_2 = ebx_1[j - 1];
                                uint edi_5 = ebx_1[j];

                                if (eax_2 <= edi_5)
                                {
                                    for (uint k = eax_2; k <= edi_5; k++)
                                    {
                                        data_56b4e0[k] |= ebx_1[i];
                                    }
                                }
                            }
                        }
                    }

                    data_56b3dc = 1;
                    data_56b3c0 = CodePage;
                    data_56b3d0 = index;  // Anpassung je nach Datenstruktur
                    data_56b5e4 = sub_4ecdb3(CodePage);
                }
                else
                {
                    CPINFO lpCPInfo;
                    if (!GetCPInfo(CodePage, out lpCPInfo))
                    {
                        if (data_569cd0 != 0)
                        {
                            sub_4ecde6();
                        }

                        result = -1;
                        sub_4ebe4c(0x19);
                        return result;
                    }

                    data_56b5e4 = 0;
                    Array.Clear(data_56b4e0, 0, data_56b4e0.Length);
                    data_56b3c0 = CodePage;

                    if (lpCPInfo.MaxCharSize <= 1)
                    {
                        data_56b3dc = 0;
                    }
                    else
                    {
                        for (int i = 0; i < lpCPInfo.LeadByte.Length; i += 2)
                        {
                            if (lpCPInfo.LeadByte[i] == 0)
                                break;

                            for (uint j = lpCPInfo.LeadByte[i]; j <= lpCPInfo.LeadByte[i + 1]; j++)
                            {
                                data_56b4e0[j] |= 4;
                            }
                        }

                        for (int i = 1; i < 0xff; i++)
                        {
                            data_56b4e0[i] |= 8;
                        }

                        data_56b5e4 = sub_4ecdb3(CodePage);
                        data_56b3dc = 1;
                    }

                    data_56b3d0 = 0;
                    data_56b3d4 = 0;
                }
            }

            sub_4ece0f();
        }

        result = 0;
        sub_4ebe4c(0x19);
        return result;
    }

    private static void FUN_004e5c20_StrCpy(StringBuilder dest, string src)
    {
        ArgumentNullException.ThrowIfNull(dest);

        dest.Clear();
        if (!string.IsNullOrEmpty(src))
        {
            dest.Append(src);
        }
    }

    private static int sub_4e5d10(nint arg1)
    {
        nint ecx = arg1;

        // Loop 1: Align ecx to a 4-byte boundary
        while ((ecx.ToInt64() & 3) != 0)
        {
            byte eax = Marshal.ReadByte(ecx);
            ecx += 1;

            if (eax == 0)
            {
                return (int)(ecx.ToInt64() - arg1.ToInt64()) - 1;
            }
        }

        // Loop 2: Check 4 bytes at a time
        while (true)
        {
            int eax_2 = Marshal.ReadInt32(ecx);
            ecx += 4;

            if ((((eax_2 ^ 0xffffffff) ^ (0x7efefeff + eax_2)) & 0x81010100) != 0)
            {
                int eax_5 = Marshal.ReadInt32(ecx - 4);

                if ((eax_5 & 0xFF) == 0)
                {
                    return (int)(ecx.ToInt64() - arg1.ToInt64()) - 4;
                }

                if ((eax_5 & 0xFF00) == 0)
                {
                    return (int)(ecx.ToInt64() - arg1.ToInt64()) - 3;
                }

                if ((eax_5 & 0xFF0000) == 0)
                {
                    return (int)(ecx.ToInt64() - arg1.ToInt64()) - 2;
                }

                if ((eax_5 & 0xFF000000) == 0)
                {
                    return (int)(ecx.ToInt64() - arg1.ToInt64()) - 1;
                }
            }
        }
    }

    private static int sub_4ea109()
    {
        nint eax_1 = data_50c230;

        if (eax_1 != IntPtr.Zero)
        {
            // Ruft die Funktion auf, auf die data_50c230 zeigt
            FunctionDelegate function = (FunctionDelegate)Marshal.GetDelegateForFunctionPointer(eax_1, typeof(FunctionDelegate));
            function();
        }

        // Ruft sub_4ea20f auf mit entsprechenden Argumenten
        sub_4ea20f(data_4fe05c, 0x4fe06c);

        // Hier könnte ein anderer Rückgabewert bestimmt werden, zum Beispiel:
        // Wenn sub_4ea20f nichts zurückgibt, könnte der Rückgabewert `0` oder ein anderer Wert sein.
        sub_4ea20f(data_4fe000, 0x4fe058);

        // Da sub_4ea20f void ist, geben wir hier z.B. einen Standardwert zurück.
        return 0;  // oder ein anderer Wert, der in diesem Kontext sinnvoll ist
    }

    private static void sub_4ea20f(nint arg1, int arg2)
    {
        // Schleife über die Speicheradressen von arg1 bis arg2
        while (arg1.ToInt32() < arg2)
        {
            // Liest die Adresse der Funktion
            nint eax = Marshal.ReadIntPtr(arg1);

            if (eax != IntPtr.Zero)
            {
                // Konvertiere den Funktionszeiger in einen Delegate und rufe die Funktion auf
                FunctionDelegate function = (FunctionDelegate)Marshal.GetDelegateForFunctionPointer(eax, typeof(FunctionDelegate));
                function();
            }

            arg1 = IntPtr.Add(arg1, IntPtr.Size);
        }
    }
    private static void sub_4ea136(int value) { }
    private static int sub_4ebe61(int value1, int[] value2) { return 0; }
    private static uint sub_4ecd69(uint arg)
    {
        Console.WriteLine($"sub_4ecd69 aufgerufen mit Argument: {arg}");
        return arg;  // Beispielrückgabe
    }
    private static void sub_4ecde6()
    {
        Console.WriteLine("sub_4ecde6 aufgerufen");
    }
    private static int sub_4ecdb3(uint CodePage)
    {
        Console.WriteLine($"sub_4ecdb3 aufgerufen mit CodePage: {CodePage}");
        return 0;  // Beispielrückgabe
    }
    private static void sub_4ece0f()
    {
        Console.WriteLine("sub_4ece0f aufgerufen");
    }
    private static void sub_4ebe4c(int arg)
    {
        Console.WriteLine($"sub_4ebe4c aufgerufen mit Argument: {arg}");
    }
    private static void sub_4069f3(nint arg) { }
    private static void sub_4e1551(string arg) { }
    private static void sub_4e1ca0(nint arg) { }
    private static void sub_4e5d8b(nint arg1, string format) { }
    private static nint sub_4e1658(nint arg) { return IntPtr.Zero; }
    private static int sub_40659c(nint arg1, int arg2) { return 0; }
    private static uint sub_40665d(nint arg) { return 0; }
    private static void sub_4064e4(uint arg) { }
    private static void sub_479076(nint arg) { }
    private static int sub_4e163b(string arg) { return 0; }
    private static void sub_4016ea() { }
    private static void sub_4790cd(nint arg1, nint arg2) { }
    private static void sub_4063f9(string arg1, nint arg2) { }

    private static bool FUN_00406542_TryResolvePath(string pathAnsi, out string resolvedPath)
    {
        if (string.IsNullOrWhiteSpace(pathAnsi))
        {
            resolvedPath = string.Empty;
            return false;
        }

        resolvedPath = Path.GetFullPath(pathAnsi);
        return File.Exists(resolvedPath);
    }

    private static void FUN_00406542_WriteAnsiZ(nint dest, string text, int maxBytesIncludingNull)
    {
        if (maxBytesIncludingNull <= 1)
        {
            return;
        }

        byte[] bytes = Encoding.ASCII.GetBytes(text ?? string.Empty);

        int count = bytes.Length;
        if (count > maxBytesIncludingNull - 1)
        {
            count = maxBytesIncludingNull - 1;
        }

        Marshal.Copy(bytes, 0, (IntPtr)dest, count);
        Marshal.WriteByte((IntPtr)(dest + count), 0);
    }

    private static bool FUN_00406542(string pathAnsi, nint param_2)
    {

        if (!FUN_00406542_TryResolvePath(pathAnsi, out string resolved))
        {
            return false;
        }

        FUN_00406542_WriteAnsiZ(param_2, resolved, 260);
        return true;
    }


    private static void sub_405ac8(int arg) { }
    private static int sub_4791a6(nint arg) { return 0; }
    private static uint sub_478d16(nint arg) { return 0; }
    private static uint sub_478d27(nint arg) { return 0; }
    private static uint sub_478d5e(nint arg) { return 0; }
    private static nint sub_478ced(nint arg) { return IntPtr.Zero; }
    private static nint LoadIconA(uint hInstance, int lpIconName) { return IntPtr.Zero; }
    private static nint SendMessageA(nint hWnd, uint Msg, int wParam, nint lParam) { return IntPtr.Zero; }
    private static void sub_4016ee(nint arg) { }
    private static void sub_478b53(nint arg) { }
    private static void sub_401750(nint arg) { }
    private static void sub_40471f(nint arg1, nint arg2, int arg3) { }
    private static void sub_404aac(nint arg1, int arg2) { }
    private static void sub_403524(nint arg) { }
    private static void GetCurrentDirectoryA(int nBufferLength, nint lpBuffer) { }

    private static void FUN_004e5c30_StrCat(StringBuilder dest, string src)
    {
        ArgumentNullException.ThrowIfNull(dest);

        if (!string.IsNullOrEmpty(src))
        {
            dest.Append(src);
        }
    }

    private static nint FindAnsiStringEnd(nint str)
    {
        nint p = str;

        // Align p
        while ((p.ToInt64() & 3) != 0)
        {
            if (Marshal.ReadByte(p) == 0)
            {
                return p;
            }
            p += 1;
        }

        while (true)
        {
            uint word = unchecked((uint)Marshal.ReadInt32(p));

            if ((((word ^ 0xFFFFFFFFu) ^ (word + 0x7EFEFEFFu)) & 0x81010100u) != 0u)
            {
                if ((word & 0x000000FFu) == 0u) return p;
                if ((word & 0x0000FF00u) == 0u) return p + 1;
                if ((word & 0x00FF0000u) == 0u) return p + 2;
                if ((word & 0xFF000000u) == 0u) return p + 3;
            }

            p += 4;
        }
    }
    private static void sub_403932(nint arg1, int arg2) { }
    private static void sub_4e167d(nint arg1, string arg2, int arg3, int arg4, nint arg5) { }
    private static nint sub_402761() { return IntPtr.Zero; }
    private static void sub_403e45(nint arg) { }
    private static void sub_403e6f(nint arg1, nint arg2) { }
    private static void sub_4e1463(nint arg1, nint arg2) { }

    private static nint sub_4e5ddd(int size)
    {
        return Marshal.AllocHGlobal(size);
    }

    private static bool sub_40234f()
    {
        // Implementierung der Funktion
        return true;
    }

    private static int sub_4799d8(nint data)
    {
        // Implementierung der Funktion
        return 1;
    }

    private static int sub_40241e(nint data)
    {
        // Implementierung der Funktion
        return 0;
    }

    private static void sub_4023ae(nint data)
    {
        // Implementierung der Funktion
    }

    private static void sub_4015fb(nint data)
    {
        // Implementierung der Funktion
    }

    private static void sub_4e5e48(nint data)
    {
        Marshal.FreeHGlobal(data);
    }
}