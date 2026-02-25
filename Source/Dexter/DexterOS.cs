namespace OpenVikings.Dexter
{
    // Managed port of the shown DexterOS pseudo code.
    // Focus: preserve control flow and queue semantics (ring buffers of size 0x32).
    internal sealed class DexterOS
    {
        // --------------------------------------------------------------------
        // Singleton bindings
        // --------------------------------------------------------------------

        private static DexterOS? _os;

        internal static DexterOS OS
        {
            get
            {
                if (_os == null)
                {
                    throw new InvalidOperationException("DexterOS.OS is not bound. Call DexterOS.Bind(...) during startup.");
                }

                return _os;
            }
        }

        internal static void Bind(DexterOS os)
        {
            ArgumentNullException.ThrowIfNull(os);
            _os = os;
        }

        private static OSGeneric? _generic;

        internal static OSGeneric Generic
        {
            get
            {
                if (_generic == null)
                {
                    throw new InvalidOperationException("DexterOS.Generic is not bound. Call DexterOS.BindGeneric(...) during startup.");
                }

                return _generic;
            }
        }

        internal static void BindGeneric(OSGeneric generic)
        {
            ArgumentNullException.ThrowIfNull(generic);
            _generic = generic;
        }

        private static DexterGFX? _gfx;

        internal static DexterGFX GFX
        {
            get
            {
                if (_gfx == null)
                {
                    throw new InvalidOperationException("DexterOS.GFX is not bound. Call DexterOS.BindGfx(...) during startup.");
                }

                return _gfx;
            }
        }

        internal static void BindGfx(DexterGFX gfx)
        {
            ArgumentNullException.ThrowIfNull(gfx);
            _gfx = gfx;
        }

        // --------------------------------------------------------------------
        // Constants / queue sizes
        // --------------------------------------------------------------------

        private const int KeyCount = 0x7E;   // 126
        private const int QueueSize = 0x32;  // 50

        // --------------------------------------------------------------------
        // Instance fields (C++ globals / statics mapped into a single instance)
        // --------------------------------------------------------------------

        // C++: keystate bzero 0x1f8 => 126 * 4 bytes
        private readonly int[] _keyState = new int[KeyCount];

        // Key queue ring buffer (write index = _keyQueueWrite, read index = _keyQueueRead)
        private readonly KeyQueueEntry[] _keyQueue = new KeyQueueEntry[QueueSize];
        private int _keyQueueWrite;
        private int _keyQueueRead;

        // Mouse queue ring buffer
        private readonly MouseQueueEntry[] _mouseQueue = new MouseQueueEntry[QueueSize];
        private int _mouseQueueWrite;
        private int _mouseQueueRead;

        // Text queue ring buffer
        private readonly TextQueueEntry[] _textQueue = new TextQueueEntry[QueueSize];
        private int _textQueueWrite;
        private int _textQueueRead;

        // C++: DAT_100476a94 etc. (only what is used in the shown code)
        private byte _mouseButtonCount = 1;

        private readonly byte[] _mouseButtonState = new byte[8];
        private readonly int[] _mouseButtonTime = new int[8];

        // Mouse coordinates and speed (C++ uses shorts)
        private short _mouseX;
        private short _mouseY;
        private short _mouseSpeedX;
        private short _mouseSpeedY;

        // C++: TimeCheckReset (used as a global); stored here and exposed statically.
        private int _timeCheckReset;

        // C++: AppName/AppTitle/AppPath (char buffers)
        private string _appName = string.Empty;
        private string _appTitle = string.Empty;
        private string _appPath = string.Empty;

        // Command line
        private string[] _commandLineStrings = [];
        private int _commandLineArgs;

        // Mutex state
        private byte _mutexReady;
        private int _mutexDepth;

        // Thread state (IDs 0..4 are used in the shown code; 0 is "main")
        private readonly ThreadSlot[] _threads = new ThreadSlot[5];

        // This instance wraps OS environment access
        private readonly OSEnvironment _environment;

        // SDL/Platform input wrapper used by OSUpdate (project-specific; provide implementation in SDL layer)
        private readonly IInputBackend _input;

        // --------------------------------------------------------------------
        // Construction
        // --------------------------------------------------------------------

        internal DexterOS(OSEnvironment environment, IInputBackend input)
        {
            ArgumentNullException.ThrowIfNull(environment);
            ArgumentNullException.ThrowIfNull(input);

            _environment = environment;
            _input = input;

            // DexterOS::DexterOS()
            ClearArray(_keyState);
            ClearArray(_keyQueue);
            ClearArray(_textQueue);
            ClearArray(_mouseQueue);

            _keyQueueWrite = 0;
            _keyQueueRead = 0;

            _textQueueWrite = 0;
            _textQueueRead = 0;

            _mouseQueueWrite = 0;
            _mouseQueueRead = 0;
        }

        // --------------------------------------------------------------------
        // Static properties (matching existing pattern in the project)
        // --------------------------------------------------------------------

        internal static int TimeCheckReset
        {
            get => OS._timeCheckReset;
            set => OS._timeCheckReset = value;
        }

        internal static string AppName
        {
            get => OS._appName;
            set => OS._appName = value ?? string.Empty;
        }

        internal static string AppTitle
        {
            get => OS._appTitle;
            set => OS._appTitle = value ?? string.Empty;
        }

        internal static string AppPath
        {
            get => OS._appPath;
            set => OS._appPath = value ?? string.Empty;
        }

        // --------------------------------------------------------------------
        // DexterOS::HasCommandLine(char const*)
        // --------------------------------------------------------------------

        internal static bool HasCommandLine(string arg)
        {
            DexterOS os = OS;

            if (os._commandLineArgs < 1)
            {
                return false;
            }

            // C++ starts from index 1
            int i = 1;
            while (i <= os._commandLineArgs && i < os._commandLineStrings.Length)
            {
                if (DexterString.StringCompare(os._commandLineStrings[i], arg, false))
                {
                    return true;
                }

                i++;
            }

            return false;
        }

        // --------------------------------------------------------------------
        // DexterOS::Time()
        // --------------------------------------------------------------------

        internal static int Time()
        {
            DexterOS os = OS;

            int now = NowMs();
            return now - os._timeCheckReset;
        }

        // --------------------------------------------------------------------
        // DexterOS::RelaxThread(int, char const*)
        // --------------------------------------------------------------------

        internal static void RelaxThread()
        {
            OSGeneric.RelaxThread();
        }

        // --------------------------------------------------------------------
        // DexterOS::UpdateMouse(short, short, unsigned char, bool)
        // param_3: 1 = relative, else absolute
        // param_4: if true and absolute -> average with previous (smoothing)
        // --------------------------------------------------------------------

        internal static void UpdateMouse(short x, short y, byte mode, bool smoothAverage)
        {
            DexterOS os = OS;

            int renderW = GFX.RenderWidth;
            int renderH = GFX.RenderHeight;

            int windowW = GFX.WindowWidth;
            int windowH = GFX.WindowHeight;

            if (windowW != renderW)
            {
                float scaleX = (float)windowW / (float)renderW;
                if (scaleX > 0.0f)
                {
                    x = (short)(int)(x / scaleX);
                }
            }

            short scaledY = y;
            if (windowH != renderH)
            {
                float scaleY = (float)windowH / (float)renderH;
                if (scaleY > 0.0f)
                {
                    scaledY = (short)(int)(y / scaleY);
                }
            }

            os._mouseSpeedY = scaledY;

            short newX;
            short newY;

            if (mode == 1)
            {
                newX = (short)(x + os._mouseX);
                os._mouseSpeedX = x;

                newY = (short)(os._mouseY + os._mouseSpeedY);
                os._mouseY = newY;
            }
            else
            {
                newX = x;
                newY = os._mouseSpeedY;

                if (smoothAverage)
                {
                    newX = (short)((x + os._mouseX) >> 1);
                    newY = (short)((os._mouseSpeedY + os._mouseY) >> 1);
                }

                os._mouseSpeedX = (short)(newX - os._mouseX);
                os._mouseSpeedY = (short)(newY - os._mouseY);

                os._mouseY = newY;
            }

            os._mouseX = newX;

            os._mouseX = (short)Limit(os._mouseX, 0, renderW - 1);
            os._mouseY = (short)Limit(os._mouseY, 0, renderH - 1);
        }

        // --------------------------------------------------------------------
        // DexterOS::SetAppTitle(char const*, char const*)
        // param_1 => title (max 0x32), param_2 => name (max 0xFA)
        // --------------------------------------------------------------------

        internal static void SetAppTitle(string title, string name)
        {
            if (DexterString.StringLength(name) < 0xFA)
            {
                OS._appName = name ?? string.Empty;
            }

            if (DexterString.StringLength(title) < 0x32)
            {
                OS._appTitle = title ?? string.Empty;
            }
        }

        // --------------------------------------------------------------------
        // DexterOS::KeyState(unsigned int)
        // --------------------------------------------------------------------

        internal static bool KeyState(uint key)
        {
            DexterOS os = OS;

            if (key < KeyCount)
            {
                return os._keyState[(int)key] != 0;
            }

            return false;
        }

        // --------------------------------------------------------------------
        // DexterOS::KeyPressDown(unsigned int)
        // --------------------------------------------------------------------

        internal static void KeyPressDown(uint osKey)
        {
            DexterOS os = OS;

            uint translated = TranslateOsKey(osKey);
            if (translated == 0x7F)
            {
                return;
            }

            ushort mods = BuildModifierMask();

            int keyIndex = (int)translated;
            if (os._keyState[keyIndex] == 0)
            {
                int t = NowMs();
                os._keyState[keyIndex] = t - os._timeCheckReset;
            }

            int slot = os._keyQueueWrite;
            if (os._keyQueue[slot].Type == 0)
            {
                os._keyQueue[slot] = new KeyQueueEntry
                {
                    Type = 1,
                    Key = (int)translated,
                    Duration = 0,
                    Mods = mods
                };

                os._keyQueueWrite = NextQueueIndex(os._keyQueueWrite);
            }
        }

        // --------------------------------------------------------------------
        // DexterOS::KeyPressUp(unsigned int)
        // --------------------------------------------------------------------

        internal static void KeyPressUp(uint osKey)
        {
            DexterOS os = OS;

            uint translated = TranslateOsKey(osKey);
            if (translated == 0x7F)
            {
                return;
            }

            ushort mods = BuildModifierMask();

            int keyIndex = (int)translated;
            int downStamp = os._keyState[keyIndex];
            if (downStamp == 0)
            {
                return;
            }

            int now = NowMs();
            int heldMs = now - (os._timeCheckReset + downStamp);

            os._keyState[keyIndex] = 0;

            int slot = os._keyQueueWrite;
            if (os._keyQueue[slot].Type == 0)
            {
                os._keyQueue[slot] = new KeyQueueEntry
                {
                    Type = 2,
                    Key = (int)translated,
                    Duration = heldMs,
                    Mods = mods
                };

                os._keyQueueWrite = NextQueueIndex(os._keyQueueWrite);
            }
        }

        // --------------------------------------------------------------------
        // DexterOS::MousePressDown(unsigned char)
        // --------------------------------------------------------------------

        internal static void MousePressDown(byte button)
        {
            DexterOS os = OS;

            ushort mods = BuildModifierMask();

            int b = button;
            if (b < 0 || b >= os._mouseButtonState.Length)
            {
                return;
            }

            if (os._mouseButtonState[b] != 1)
            {
                os._mouseButtonState[b] = 1;

                int t = NowMs();
                os._mouseButtonTime[b] = t - os._timeCheckReset;

                if (os._mouseButtonCount <= b)
                {
                    os._mouseButtonCount = (byte)(button + 1);
                }
            }

            int slot = os._mouseQueueWrite;
            if (os._mouseQueue[slot].Type == 0)
            {
                os._mouseQueue[slot] = new MouseQueueEntry
                {
                    Type = 1,
                    ButtonOrKind = b,
                    Duration = 0,
                    X = os._mouseX,
                    Y = os._mouseY,
                    Mods = mods
                };

                os._mouseQueueWrite = NextQueueIndex(os._mouseQueueWrite);
            }
        }

        // --------------------------------------------------------------------
        // DexterOS::MousePressUp(unsigned char)
        // --------------------------------------------------------------------

        internal static void MousePressUp(byte button)
        {
            DexterOS os = OS;

            ushort mods = BuildModifierMask();

            int b = button;
            if (b < 0 || b >= os._mouseButtonState.Length)
            {
                return;
            }

            if (os._mouseButtonState[b] == 0)
            {
                return;
            }

            os._mouseButtonState[b] = 0;

            int now = NowMs();
            int started = os._timeCheckReset + os._mouseButtonTime[b];
            int heldMs = now - started;

            int slot = os._mouseQueueWrite;
            if (os._mouseQueue[slot].Type == 0)
            {
                os._mouseQueue[slot] = new MouseQueueEntry
                {
                    Type = 2,
                    ButtonOrKind = b,
                    Duration = heldMs,
                    X = os._mouseX,
                    Y = os._mouseY,
                    Mods = mods
                };

                os._mouseQueueWrite = NextQueueIndex(os._mouseQueueWrite);
            }
        }

        // --------------------------------------------------------------------
        // DexterOS::SetFocusChange(unsigned char)
        // Encoded as a "mouse queue" entry with a special Type and zeroed payload.
        // --------------------------------------------------------------------

        internal static void SetFocusChange(byte kind)
        {
            if (kind == 0)
            {
                return;
            }

            DexterOS os = OS;

            int slot = os._mouseQueueWrite;
            if (os._mouseQueue[slot].Type != 0)
            {
                return;
            }

            os._mouseQueue[slot] = new MouseQueueEntry
            {
                Type = kind,
                ButtonOrKind = 0,
                Duration = 0,
                X = 0,
                Y = 0,
                Mods = 0
            };

            os._mouseQueueWrite = NextQueueIndex(os._mouseQueueWrite);
        }

        // --------------------------------------------------------------------
        // DexterOS::GetQueuedKey()
        // --------------------------------------------------------------------

        internal static bool GetQueuedKey(out KeyQueueEntry entry)
        {
            DexterOS os = OS;

            entry = os._keyQueue[os._keyQueueRead];
            if (entry.Type == 0)
            {
                return false;
            }

            os._keyQueue[os._keyQueueRead] = default;
            os._keyQueueRead = NextQueueIndex(os._keyQueueRead);
            return true;
        }

        // --------------------------------------------------------------------
        // DexterOS::GetQueuedMouse()
        // --------------------------------------------------------------------

        internal static bool GetQueuedMouse(out MouseQueueEntry entry)
        {
            DexterOS os = OS;

            entry = os._mouseQueue[os._mouseQueueRead];
            if (entry.Type == 0)
            {
                return false;
            }

            os._mouseQueue[os._mouseQueueRead] = default;
            os._mouseQueueRead = NextQueueIndex(os._mouseQueueRead);
            return true;
        }

        // --------------------------------------------------------------------
        // DexterOS::GetTextKey()
        // Returns 0..255; 0 means "none" in the original.
        // --------------------------------------------------------------------

        internal static byte GetTextKey()
        {
            DexterOS os = OS;

            TextQueueEntry entry = os._textQueue[os._textQueueRead];
            if (entry.Used == 0)
            {
                return 0;
            }

            byte value = entry.Value;

            os._textQueue[os._textQueueRead] = default;
            os._textQueueRead = NextQueueIndex(os._textQueueRead);

            return value;
        }

        // --------------------------------------------------------------------
        // DexterOS::AddTextKey(unsigned char)
        // --------------------------------------------------------------------

        internal static void AddTextKey(byte value)
        {
            DexterOS os = OS;

            int slot = os._textQueueWrite;
            if (os._textQueue[slot].Used != 0)
            {
                return;
            }

            os._textQueue[slot] = new TextQueueEntry
            {
                Used = 1,
                Value = value
            };

            os._textQueueWrite = NextQueueIndex(os._textQueueWrite);
        }

        // --------------------------------------------------------------------
        // DexterOS::Pause(unsigned int)
        // --------------------------------------------------------------------

        internal static void Pause(uint ms)
        {
            if (ms < 300000)
            {
                OS._environment.Pause(ms);
            }
        }

        // --------------------------------------------------------------------
        // DexterOS::MutexInit / MutexLock / MutexUnLock
        // --------------------------------------------------------------------

        internal static void MutexInit()
        {
            if (Generic.MutexInit())
            {
                OS._mutexReady = 1;
            }
        }

        internal static void MutexLock()
        {
            DexterOS os = OS;

            if (os._mutexReady != 0)
            {
                Generic.MutexLock();
                os._mutexDepth++;
            }
        }

        internal static void MutexUnLock()
        {
            DexterOS os = OS;

            if (os._mutexReady != 0)
            {
                Generic.MutexUnLock();
                os._mutexDepth--;
            }
        }

        // --------------------------------------------------------------------
        // DexterOS::FailRequester(char const*, ...)
        // --------------------------------------------------------------------

        internal static void FailRequester(string format, params object[] args)
        {
            string message = (args == null || args.Length == 0)
                ? (format ?? string.Empty)
                : string.Format(format ?? string.Empty, args);

            OSGeneric.FailRequester(message);
        }

        // --------------------------------------------------------------------
        // DexterOS::ThreadRegister / ThreadRelease / ThreadEnd / GetDexterThreadID
        // --------------------------------------------------------------------

        internal static void ThreadRegister(ushort id)
        {
            DexterOS os = OS;

            if (os._mutexReady != 0)
            {
                Generic.MutexLock();
                os._mutexDepth++;
            }

            bool threadId = GetOSThreadID();
            os._threads[id].OsThreadId = threadId;
            os._threads[id].Allocated = true;

            DexThread.ThreadCount++;

            os._threads[id].StartedSignal = true;

            OSGeneric.ThreadRegister(0x6820);

            if (os._mutexReady != 0)
            {
                Generic.MutexUnLock();
                os._mutexDepth--;
            }
        }

        internal static void ThreadRelease(ushort id)
        {
            DexterOS os = OS;

            if (os._mutexReady != 0)
            {
                Generic.MutexLock();
                os._mutexDepth++;
            }

            OSGeneric.ThreadRegister(0x6820);

            os._threads[id].StartedSignal = false;
            os._threads[id].Allocated = false;

            bool current = GetOSThreadID();
            if (DexThread.CooperativeThread == current)
            {
                DexThread.CooperativeThread = false;
            }

            DexThread.ThreadCount--;

            if (os._mutexReady != 0)
            {
                Generic.MutexUnLock();
                os._mutexDepth--;
            }
        }

        internal static void ThreadEnd(short requestedId)
        {
            DexterOS os = OS;

            short detected = (short)GetDexterThreadID();

            short chosen = requestedId;
            if (detected == requestedId || requestedId <= -1)
            {
                chosen = detected;
            }

            if (chosen > 0)
            {
                ushort u = (ushort)chosen;
                if (u < os._threads.Length && os._threads[u].StartedSignal)
                {
                    os._threads[u].Alive = false;

                    if (detected != requestedId && requestedId > -1)
                    {
                        os._environment.ThreadEnd(0x6820);
                    }

                    os._threads[u].StartedSignal = false;
                }
            }
        }

        internal static int GetDexterThreadID()
        {
            DexterOS os = OS;

            bool tid = GetOSThreadID();

            if (os._threads[0].Allocated && os._threads[0].OsThreadId == tid)
            {
                return 0;
            }

            int i = 1;
            while (i <= 4)
            {
                if (os._threads[i].Allocated && os._threads[i].OsThreadId == tid)
                {
                    return i;
                }

                i++;
            }

            return -1;
        }

        // --------------------------------------------------------------------
        // DexterOS::OSUpdate()
        // --------------------------------------------------------------------

        internal static bool OSUpdate()
        {
            DexterOS os = OS;

            if (!os._input.PollEvent(out InputEvent ev))
            {
                return false;
            }

            bool processed = false;

            switch (ev.Type)
            {
                case InputEventType.Quit:
                    DexterApp.ShutDown();
                    DexterApp.AppContinue = false;
                    DexterApp.DexterCleanUp();
                    Environment.Exit(-1);
                    break;

                case InputEventType.FocusGained:
                    SetFocusChange(4);
                    processed = true;
                    break;

                case InputEventType.FocusLost:
                    SetFocusChange(8);
                    processed = true;
                    break;

                case InputEventType.KeyDown:
                    KeyPressDown((uint)ev.KeyCode);

                    if (!GFX.ScreenLocked && ev.KeyCode == 0x0D)
                    {
                        if ((ev.KeyModFlags & 0x03) != 0)
                        {
                            GFX.SetScreenMode((ushort)GFX.RenderWidth, (ushort)GFX.RenderHeight, GFX.RenderBitDepth, 4);
                        }
                    }

                    if (ev.KeyCode == 0x7A)
                    {
                        if ((ev.KeyModFlags & 0xC0) != 0)
                        {
                            os._input.MinimizeWindow();
                        }
                    }

                    if (ev.KeyCode == 0x67)
                    {
                        if ((ev.KeyModFlags & 0xC0) != 0)
                        {
                            os._input.ToggleRelativeMouseMode();
                        }
                    }

                    if (ev.KeyCode == 0x71)
                    {
                        if ((ev.KeyModFlags & 0x0C) != 0)
                        {
                            DexterApp.ShutDown();
                            DexterApp.AppContinue = false;
                            DexterApp.DexterCleanUp();
                            Environment.Exit(0);
                        }
                    }

                    processed = true;
                    break;

                case InputEventType.KeyUp:
                    KeyPressUp((uint)ev.KeyCode);
                    processed = true;
                    break;

                case InputEventType.MouseMove:
                    UpdateMouse(ev.MouseX, ev.MouseY, 1, false);
                    processed = true;
                    break;

                case InputEventType.MouseButtonDown:
                    if (ev.MouseButton == 1) MousePressDown(0);
                    if (ev.MouseButton == 2) MousePressDown(2);
                    if (ev.MouseButton == 3) MousePressDown(1);
                    processed = true;
                    break;

                case InputEventType.MouseButtonUp:
                    if (ev.MouseButton == 1) MousePressUp(0);
                    if (ev.MouseButton == 2) MousePressUp(2);
                    if (ev.MouseButton == 3) MousePressUp(1);
                    processed = true;
                    break;

                case InputEventType.MouseWheel:
                    if (ev.WheelY < 0)
                    {
                        MousePressDown(3);
                        MousePressUp(3);
                    }
                    else
                    {
                        MousePressDown(4);
                        MousePressUp(4);
                    }
                    processed = true;
                    break;

                case InputEventType.TextInput:
                    if (!string.IsNullOrEmpty(ev.Text))
                    {
                        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(ev.Text);
                        for (int i = 0; i < bytes.Length; i++)
                        {
                            AddTextKey(bytes[i]);
                        }

                        processed = true;
                    }
                    break;
            }

            return processed;
        }

        // --------------------------------------------------------------------
        // DexterOS::OSInit()
        // --------------------------------------------------------------------

        internal static bool OSInit()
        {
            DexterOS os = OS;

            os._mouseButtonCount = 1;
            os._appPath = string.Empty;

            int rc = os._input.InitVideoAndEvents();
            if (rc < 0)
            {
                return false;
            }

            os._input.GetDesktopDisplayMode(out DisplayMode dm);
            _ = dm.Width;
            _ = dm.Height;

            os._input.SetSdlInitOk(true);

            return true;
        }

        // --------------------------------------------------------------------
        // DexterOS::GetOSThreadID()
        // --------------------------------------------------------------------

        internal static bool GetOSThreadID()
        {
            return false;
        }

        // --------------------------------------------------------------------
        // Internal helpers
        // --------------------------------------------------------------------

        private static int NowMs()
        {
            DexterOS os = OS;

            // Support both int/long implementations by using long locally.
            // If OSEnvironment.Time() is int in your project, change "long" to "int" here.
            long t = os._environment.Time();
            return checked((int)t);
        }

        private static int NextQueueIndex(int index)
        {
            int next = index + 1;
            if (next != QueueSize)
            {
                return next;
            }

            return 0;
        }

        private static int Limit(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static void ClearArray<T>(T[] array)
        {
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = default!;
            }
        }

        private static uint TranslateOsKey(uint osKey)
        {
            uint result = 0x7F;

            uint[] a = OSKeyTables.Primary;
            uint[] b = OSKeyTables.Alt1;
            uint[] c = OSKeyTables.Alt2;

            for (int i = 0; i < KeyCount; i++)
            {
                if (a[i] == osKey) result = (uint)i;
                if (b[i] == osKey) result = (uint)i;
                if (c[i] == osKey) result = (uint)i;
            }

            return result;
        }

        private static ushort BuildModifierMask()
        {
            DexterOS os = OS;

            bool basePressed = os._input.IsAnyBaseModifierDown();
            bool extendedPressed = os._input.IsAnyExtendedModifierDown();

            if (!extendedPressed)
            {
                return (ushort)(basePressed ? 1 : 0);
            }

            return (ushort)((basePressed ? 1 : 0) + 4);
        }

        // --------------------------------------------------------------------
        // Queue entry types
        // --------------------------------------------------------------------

        internal struct KeyQueueEntry
        {
            internal short Type;     // 0=empty, 1=down, 2=up
            internal int Key;        // translated key index (0..125)
            internal int Duration;   // ms held (only for Type=2)
            internal ushort Mods;    // modifier mask
        }

        internal struct MouseQueueEntry
        {
            internal short Type;         // 0=empty, 1=down, 2=up, 4/8 focus-change kinds, etc.
            internal int ButtonOrKind;   // button index for 1/2, unused for focus kinds
            internal int Duration;       // ms held
            internal short X;
            internal short Y;
            internal ushort Mods;
        }

        internal struct TextQueueEntry
        {
            internal byte Used;      // 0=empty, 1=used
            internal byte Value;     // 0..255
        }

        private struct ThreadSlot
        {
            internal bool Allocated;
            internal bool Alive;
            internal bool StartedSignal;

            internal long OsThreadHandle;
            internal bool OsThreadId;

            internal Action? EntryPoint;
        }
    }

    // ------------------------------------------------------------------------
    // Backend abstractions used by OSUpdate/OSInit (implemented in SDL layer)
    // ------------------------------------------------------------------------

    internal interface IInputBackend
    {
        int InitVideoAndEvents();
        void GetDesktopDisplayMode(out DisplayMode mode);
        bool PollEvent(out InputEvent ev);

        void MinimizeWindow();
        void ToggleRelativeMouseMode();

        void SetSdlInitOk(bool ok);

        bool IsAnyBaseModifierDown();
        bool IsAnyExtendedModifierDown();
    }

    internal readonly struct DisplayMode
    {
        internal readonly int Width;
        internal readonly int Height;

        internal DisplayMode(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    internal enum InputEventType
    {
        None = 0,
        Quit = 1,
        FocusGained = 2,
        FocusLost = 3,
        KeyDown = 4,
        KeyUp = 5,
        MouseMove = 6,
        MouseButtonDown = 7,
        MouseButtonUp = 8,
        MouseWheel = 9,
        TextInput = 10
    }

    internal readonly struct InputEvent
    {
        internal readonly InputEventType Type;

        internal readonly int KeyCode;
        internal readonly byte KeyModFlags;

        internal readonly short MouseX;
        internal readonly short MouseY;
        internal readonly byte MouseButton;

        internal readonly int WheelY;

        internal readonly string? Text;

        internal InputEvent(InputEventType type, int keyCode, byte keyModFlags, short mouseX, short mouseY, byte mouseButton, int wheelY, string? text)
        {
            Type = type;
            KeyCode = keyCode;
            KeyModFlags = keyModFlags;
            MouseX = mouseX;
            MouseY = mouseY;
            MouseButton = mouseButton;
            WheelY = wheelY;
            Text = text;
        }
    }

    // ------------------------------------------------------------------------
    // Key tables placeholder (fill with the real tables from the project)
    // ------------------------------------------------------------------------

    internal static class OSKeyTables
    {
        internal static readonly uint[] Primary = CreateIdentity();
        internal static readonly uint[] Alt1 = CreateIdentity();
        internal static readonly uint[] Alt2 = CreateIdentity();

        private static uint[] CreateIdentity()
        {
            uint[] t = new uint[0x7E];
            for (int i = 0; i < t.Length; i++)
            {
                t[i] = (uint)i;
            }

            return t;
        }
    }
}