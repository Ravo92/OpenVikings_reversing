using OpenVikings;

internal sealed class DexterOS
{
    private const int MaxQueueEntries = 0x32; // 50
    private const int MaxKeyCodes = 0x7e;     // 126
    private const int InvalidKey = 0x7f;

    // Key states store the timestamp (ms since reset) of the initial press; 0 = up.
    private readonly int[] _keyState;

    private readonly KeyEvent[] _keyQueue;
    private int _keyQueueWrite;
    private int _keyQueueRead;

    private readonly MouseEvent[] _mouseQueue;
    private int _mouseQueueWrite;
    private int _mouseQueueRead;

    private readonly TextEvent[] _textQueue;
    private int _textQueueWrite;
    private int _textQueueRead;

    private readonly byte[] _mouseButtonState; // indices 0..4
    private readonly int[] _mouseButtonTime;   // press start times (ms since reset)
    private int _mouseButtonCount;

    private short _mouseX;
    private short _mouseY;
    private short _mouseSpeedX;
    private short _mouseSpeedY;

    private int _timeCheckReset;

    // Command line
    private int _commandLineArgs;        // "argc - 1"
    private string[]? _commandLineStrings;

    // App strings (size-limited like original)
    private string _appName;
    private string _appTitle;
    private string _appPath;

    // Keypad entry state from the snippet ("_0_1_ = 1")
    private bool _currentKeyPadEntryActive;

    // Qualifier sources (modelled from DAT_1004775e0 etc.)
    // Group A (maps to bit0)
    private bool _qualifierGroupA_0;
    private bool _qualifierGroupA_1;
    private bool _qualifierGroupA_2;

    // Group B (maps to bit2 / +4)
    private bool _qualifierGroupB_0;
    private bool _qualifierGroupB_1;
    private bool _qualifierGroupB_2;

    // Threading slots: 0..4 (0 main + 1..4)
    private readonly ThreadSlot[] _threadSlots;
    private readonly object _threadMutex;
    private bool _threadMutexInitialized;
    private int _threadMutexDepth;

    // This exists in the decompile; in C# it is only used for compatibility with the original flow.
    private ushort _newThreadId;

    // Key translation tables: 42 entries * 3 codes = 126 possible OS codes checked by the original loop.
    // These are assumed to already be filled elsewhere in the project as required.
    private readonly uint[] _osKeyTranslateA;
    private readonly uint[] _osKeyTranslateB;
    private readonly uint[] _osKeyTranslateC;

    internal DexterOS(uint[] osKeyTranslateA, uint[] osKeyTranslateB, uint[] osKeyTranslateC)
    {
        // Arrays are explicitly sized like the original memory blocks.
        _keyState = new int[MaxKeyCodes];

        _keyQueue = new KeyEvent[MaxQueueEntries];
        _mouseQueue = new MouseEvent[MaxQueueEntries];
        _textQueue = new TextEvent[MaxQueueEntries];

        _mouseButtonState = new byte[5];
        _mouseButtonTime = new int[5];

        _mouseButtonCount = 0;

        _timeCheckReset = 0;

        _commandLineArgs = 0;
        _commandLineStrings = null;

        _appName = string.Empty;
        _appTitle = string.Empty;
        _appPath = string.Empty;

        _currentKeyPadEntryActive = true;

        _threadSlots = new ThreadSlot[5];
        _threadMutex = new object();
        _threadMutexInitialized = false;
        _threadMutexDepth = 0;
        _newThreadId = 0;

        _osKeyTranslateA = osKeyTranslateA;
        _osKeyTranslateB = osKeyTranslateB;
        _osKeyTranslateC = osKeyTranslateC;

        InitKeyStates();
        InitKeyQueue();
        InitTextQueue();
        InitMouseQueue();
    }

    internal void InitKeyStates()
    {
        Array.Clear(_keyState, 0, _keyState.Length);
    }

    internal void InitKeyQueue()
    {
        Array.Clear(_keyQueue, 0, _keyQueue.Length);
        _keyQueueWrite = 0;
        _keyQueueRead = 0;
    }

    internal void InitTextQueue()
    {
        Array.Clear(_textQueue, 0, _textQueue.Length);
        _textQueueWrite = 0;
        _textQueueRead = 0;
    }

    internal void InitMouseQueue()
    {
        Array.Clear(_mouseQueue, 0, _mouseQueue.Length);
        _mouseQueueWrite = 0;
        _mouseQueueRead = 0;
    }

    internal void SetCommandLineArgs(int argc, string[] argv)
    {
        // Original: CommandLineArgs = argc - 1; CommandLineStrings = argv;
        _commandLineArgs = argc - 1;
        _commandLineStrings = argv;
    }

    internal bool HasCommandLine(string value)
    {
        if (_commandLineStrings == null || _commandLineArgs < 1)
        {
            return false;
        }

        int lastIndex = _commandLineArgs;
        for (int index = 1; index <= lastIndex && index < _commandLineStrings.Length; index++)
        {
            if (DexterString.StringCompare(_commandLineStrings[index], value, '\0'))
            {
                return true;
            }
        }

        return false;
    }

    internal int Time()
    {
        int now = OSEnvironment.Time();
        return now - _timeCheckReset;
    }

    internal void RelaxThread(int reason, string? tag)
    {
        _ = reason;
        _ = tag;
        OSGeneric.RelaxThread();
    }

    internal void UpdateMouse(short x, short y, byte mode, bool averageWithCurrent)
    {
        // Rescale X if window != render
        if ((int)DexterGFX.WindowWidth != (int)DexterGFX.RenderWidth)
        {
            float ratioX = (float)(int)DexterGFX.WindowWidth / (float)(int)DexterGFX.RenderWidth;
            if (ratioX > 0.0f)
            {
                x = (short)(int)((float)x / ratioX);
            }
        }

        short scaledY = y;
        if ((int)DexterGFX.WindowHeight != (int)DexterGFX.RenderHeight)
        {
            float ratioY = (float)(int)DexterGFX.WindowHeight / (float)(int)DexterGFX.RenderHeight;
            if (ratioY > 0.0f)
            {
                scaledY = (short)(int)((float)y / ratioY);
            }
        }

        if (mode == 1)
        {
            // Relative movement
            short newX = (short)(x + _mouseX);
            _mouseSpeedX = x;

            _mouseSpeedY = scaledY;
            _mouseY = (short)(_mouseY + _mouseSpeedY);
            _mouseX = newX;
        }
        else
        {
            // Absolute (optionally averaged)
            short newX = x;
            short newY = scaledY;

            if (averageWithCurrent)
            {
                newX = (short)(((int)x + (int)_mouseX) >> 1);
                newY = (short)(((int)scaledY + (int)_mouseY) >> 1);
            }

            _mouseSpeedX = (short)(newX - _mouseX);
            _mouseSpeedY = (short)(newY - _mouseY);
            _mouseX = newX;
            _mouseY = newY;
        }

        _mouseX = (short)Limit((int)_mouseX, 0, (int)(short)(DexterGFX.RenderWidth - 1));
        _mouseY = (short)Limit((int)_mouseY, 0, (int)(short)(DexterGFX.RenderHeight - 1));
    }

    internal void SetAppTitle(string title, string name)
    {
        // Original limits: name < 0xFA (250), title < 0x32 (50)
        if (DexterString.StringLength(name) < 0xFA)
        {
            _appName = name;
        }

        if (DexterString.StringLength(title) < 0x32)
        {
            _appTitle = title;
        }
    }

    internal bool KeyState(uint key)
    {
        if (key < MaxKeyCodes)
        {
            return _keyState[key] != 0;
        }

        return false;
    }

    internal int KeyTranslate(uint osKeyCode)
    {
        int translated = InvalidKey;

        int entries = _osKeyTranslateA.Length;
        if (_osKeyTranslateB.Length < entries)
        {
            entries = _osKeyTranslateB.Length;
        }
        if (_osKeyTranslateC.Length < entries)
        {
            entries = _osKeyTranslateC.Length;
        }

        for (int i = 0; i < entries; i++)
        {
            int baseIndex = i * 3;

            if (_osKeyTranslateA[i] == osKeyCode)
            {
                translated = baseIndex;
            }

            int candidate = translated;
            if (_osKeyTranslateB[i] == osKeyCode)
            {
                candidate = baseIndex + 1;
            }

            if (_osKeyTranslateC[i] == osKeyCode)
            {
                candidate = baseIndex + 2;
            }

            translated = candidate;
        }

        return translated;
    }

    internal void KeyPressDown(uint osKeyCode)
    {
        int key = KeyTranslate(osKeyCode);
        if (key == InvalidKey || key < 0 || key >= MaxKeyCodes)
        {
            return;
        }

        ushort qualifiers = GetQualifiersInternal();

        // If the key was previously up, store the press time (ms since reset).
        if (_keyState[key] == 0)
        {
            int now = OSEnvironment.Time();
            _keyState[key] = now - _timeCheckReset;
        }

        EnqueueKeyEvent((byte)1, (byte)key, 0, qualifiers);
    }

    internal void KeyPressUp(uint osKeyCode)
    {
        int key = KeyTranslate(osKeyCode);
        if (key == InvalidKey || key < 0 || key >= MaxKeyCodes)
        {
            return;
        }

        ushort qualifiers = GetQualifiersInternal();

        int downTime = _keyState[key];
        if (downTime == 0)
        {
            return;
        }

        int now = OSEnvironment.Time();
        int duration = now - (_timeCheckReset + downTime);

        _keyState[key] = 0;

        EnqueueKeyEvent((byte)2, (byte)key, duration, qualifiers);
    }

    internal byte GetQualifiers()
    {
        ushort q = GetQualifiersInternal();
        return (byte)(q & 0xFF);
    }

    internal void MousePressDown(byte button)
    {
        short x = _mouseX;
        short y = _mouseY;

        ushort qualifiers = GetQualifiersInternal();

        if (button < 5)
        {
            if (_mouseButtonState[button] != 1)
            {
                _mouseButtonState[button] = 1;
                int now = OSEnvironment.Time();
                _mouseButtonTime[button] = now - _timeCheckReset;

                if (_mouseButtonCount <= button)
                {
                    _mouseButtonCount = button + 1;
                }
            }
        }

        EnqueueMouseEvent((byte)1, button, 0, x, y, qualifiers);
    }

    internal void MousePressUp(byte button)
    {
        short x = _mouseX;
        short y = _mouseY;

        ushort qualifiers = GetQualifiersInternal();

        if (button < 5)
        {
            if (_mouseButtonState[button] != 0)
            {
                _mouseButtonState[button] = 0;

                int now = OSEnvironment.Time();
                int downTime = _mouseButtonTime[button];
                int duration = now - (_timeCheckReset + downTime);

                EnqueueMouseEvent((byte)2, button, duration, x, y, qualifiers);
            }
        }
    }

    internal void SetFocusChange(byte focusEventCode)
    {
        // Original writes an entry where Type = focusEventCode and clears rest.
        if (focusEventCode == 0)
        {
            return;
        }

        if (_mouseQueue[_mouseQueueWrite].Type != 0)
        {
            return;
        }

        _mouseQueue[_mouseQueueWrite] = new MouseEvent((byte)focusEventCode, 0, 0, 0, 0, 0);
        _mouseQueueWrite = (_mouseQueueWrite + 1) % MaxQueueEntries;
    }

    internal bool MouseState(byte button, bool consume)
    {
        if (button >= 5)
        {
            return false;
        }

        bool isDown = _mouseButtonState[button] != 0;
        if (isDown && consume)
        {
            _mouseButtonState[button] = 0;
            _mouseButtonTime[button] = 0;
            return true;
        }

        return isDown;
    }

    internal KeyEvent GetQueuedKey()
    {
        KeyEvent ev = _keyQueue[_keyQueueRead];

        if (ev.Type != 0)
        {
            _keyQueue[_keyQueueRead] = default;
            _keyQueueRead = (_keyQueueRead + 1) % MaxQueueEntries;
        }

        return ev;
    }

    internal KeyEvent PeekQueuedKey()
    {
        return _keyQueue[_keyQueueRead];
    }

    internal void AddQueuedKey(uint key)
    {
        // Original pushes a "down" (type 1, duration 0) and then an "up" (type 2, duration 1).
        EnqueueKeyEvent((byte)1, (byte)key, 0, 0);
        EnqueueKeyEvent((byte)2, (byte)key, 1, 0);
    }

    internal void AddQueuedMouse(byte button, short x, short y)
    {
        // Original pushes down (duration 0) then up (duration 1) with coordinates.
        EnqueueMouseEvent((byte)1, button, 0, x, y, 0);
        EnqueueMouseEvent((byte)2, button, 1, x, y, 0);
    }

    internal MouseEvent GetQueuedMouse()
    {
        MouseEvent ev = _mouseQueue[_mouseQueueRead];

        if (ev.Type != 0)
        {
            _mouseQueue[_mouseQueueRead] = default;
            _mouseQueueRead = (_mouseQueueRead + 1) % MaxQueueEntries;
        }

        return ev;
    }

    internal MouseEvent PeekQueuedMouse()
    {
        return _mouseQueue[_mouseQueueRead];
    }

    internal byte GetTextKey()
    {
        TextEvent ev = _textQueue[_textQueueRead];

        if (ev.InUse)
        {
            _textQueue[_textQueueRead] = default;
            _textQueueRead = (_textQueueRead + 1) % MaxQueueEntries;
            return ev.Value;
        }

        return 0;
    }

    internal byte PeekTextKey()
    {
        TextEvent ev = _textQueue[_textQueueRead];
        return ev.InUse ? ev.Value : (byte)0;
    }

    internal void AddTextKey(byte value)
    {
        if (_textQueue[_textQueueWrite].InUse)
        {
            return;
        }

        _textQueue[_textQueueWrite] = new TextEvent(true, value);
        _textQueueWrite = (_textQueueWrite + 1) % MaxQueueEntries;
    }

    internal bool TextInput(char[] buffer, short maxLen)
    {
        // Returns: true when still running, false when done (Enter) OR no input? The original returns (bVar6 == 0)
        // Here: false when Enter pressed; true when no more queued input or still editing.
        while (true)
        {
            byte b = GetTextKey();
            if (b == 0)
            {
                return true;
            }

            int len = DexterString.StringLength(buffer);

            if (b >= 0x20)
            {
                if (len < maxLen)
                {
                    buffer[len] = (char)b;
                    if (len + 1 < buffer.Length)
                    {
                        buffer[len + 1] = '\0';
                    }
                }
            }
            else
            {
                // Backspace
                if (b == 8)
                {
                    if (len > 0)
                    {
                        buffer[len - 1] = '\0';
                    }
                }
                else if (b == 0x0D)
                {
                    // Enter ends input
                    return false;
                }
            }
        }
    }

    internal uint SecTime()
    {
        int now = OSEnvironment.Time();
        return (uint)((now - _timeCheckReset) / 1000);
    }

    internal void Pause(uint ms)
    {
        if (ms < 300000)
        {
            OSEnvironment.Pause(ms);
        }
    }

    internal void MutexInit()
    {
        bool ok = OSGeneric.MutexInit();
        if (ok)
        {
            _threadMutexInitialized = true;
        }
    }

    internal void MutexLock()
    {
        if (!_threadMutexInitialized)
        {
            return;
        }

        System.Threading.Monitor.Enter(_threadMutex);
        _threadMutexDepth++;
    }

    internal void MutexUnLock()
    {
        if (!_threadMutexInitialized)
        {
            return;
        }

        _threadMutexDepth--;
        System.Threading.Monitor.Exit(_threadMutex);
    }

    internal void SetWindowTitle(string title)
    {
        _appTitle = title;
        OSEnvironment.SetWindowTitle(title);
    }

    internal int MouseButtons()
    {
        return _mouseButtonCount;
    }

    internal void FailRequester(string format, params object[] args)
    {
        string message = string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args);
        OSGeneric.FailRequester(message);
    }

    internal bool ThreadStart(ushort dexThreadId, System.Action entryPoint, string? name)
    {
        _ = name;

        if (dexThreadId < 1 || dexThreadId > 4)
        {
            return false;
        }

        int slotIndex = dexThreadId;

        if (_threadSlots[slotIndex].InUse)
        {
            return false;
        }

        if (!_threadMutexInitialized)
        {
            bool ok = OSGeneric.MutexInit();
            if (ok)
            {
                _threadMutexInitialized = true;
            }
        }

        _threadSlots[slotIndex] = new ThreadSlot(true, true, false, entryPoint, null);

        // Keep a compatibility field; the original uses it before the OS creates the thread.
        _newThreadId = dexThreadId;

        System.Threading.Thread thread = new System.Threading.Thread(ThreadHeader);
        _threadSlots[slotIndex] = _threadSlots[slotIndex].WithThread(thread);

        thread.IsBackground = true;
        thread.Start(dexThreadId);

        // Wait until the thread registers itself (mirrors the busy-wait loop in the decompile).
        while (!_threadSlots[slotIndex].Registered)
        {
            OSGeneric.RelaxThread();
        }

        return true;
    }

    internal void ThreadHeader(object? state)
    {
        ushort dexThreadId = state is ushort v ? v : _newThreadId;
        int slotIndex = dexThreadId;

        if (_threadMutexInitialized)
        {
            OSGeneric.MutexLock();
            _threadMutexDepth++;
        }

        long osThreadId = GetOSThreadID();

        ThreadRegisterInternal(dexThreadId, osThreadId);

        if (_threadMutexInitialized)
        {
            OSGeneric.MutexUnLock();
            _threadMutexDepth--;
        }

        OSGeneric.SignalDexter();

        // Cooperative loop: call the delegate repeatedly while Enabled.
        while (_threadSlots[slotIndex].Enabled)
        {
            OSGeneric.RelaxThread();
            System.Action? action = _threadSlots[slotIndex].EntryPoint;
            if (action != null)
            {
                action();
            }
        }

        DexterMemory.ClearMallocCache();
        ThreadRelease(dexThreadId);
    }

    internal void ThreadRegister(ushort dexThreadId)
    {
        if (dexThreadId > 4)
        {
            return;
        }

        if (_threadMutexInitialized)
        {
            OSGeneric.MutexLock();
            _threadMutexDepth++;
        }

        long osThreadId = GetOSThreadID();
        ThreadRegisterInternal(dexThreadId, osThreadId);

        if (_threadMutexInitialized)
        {
            OSGeneric.MutexUnLock();
            _threadMutexDepth--;
        }
    }

    internal void ThreadRelease(ushort dexThreadId)
    {
        if (dexThreadId > 4)
        {
            return;
        }

        if (_threadMutexInitialized)
        {
            OSGeneric.MutexLock();
            _threadMutexDepth++;
        }

        OSGeneric.ThreadRelease(0x6820);

        int slotIndex = dexThreadId;
        _threadSlots[slotIndex] = new ThreadSlot(false, false, false, null, null);

        long currentOsThreadId = GetOSThreadID();
        if (DexThread.CooperativeThread == currentOsThreadId)
        {
            DexThread.CooperativeThread = -1;
        }

        DexThread.ThreadCount--;

        if (_threadMutexInitialized)
        {
            OSGeneric.MutexUnLock();
            _threadMutexDepth--;
        }
    }

    internal void ThreadEnd(short requestedDexThreadId)
    {
        ushort currentDexThreadId = (ushort)GetDexterThreadID();

        ushort effective = (ushort)requestedDexThreadId;
        if (currentDexThreadId == requestedDexThreadId || requestedDexThreadId < 0)
        {
            effective = currentDexThreadId;
        }

        if (effective > 0 && effective <= 4)
        {
            int slotIndex = effective;
            if (_threadSlots[slotIndex].Registered)
            {
                _threadSlots[slotIndex] = _threadSlots[slotIndex].WithEnabled(false);

                if (currentDexThreadId != requestedDexThreadId && requestedDexThreadId >= 0)
                {
                    OSEnvironment.ThreadEnd(0x6820);
                }

                _threadSlots[slotIndex] = _threadSlots[slotIndex].WithRegistered(false);
            }
        }
    }

    internal int GetDexterThreadID()
    {
        long osThreadId = GetOSThreadID();

        for (int i = 0; i < _threadSlots.Length; i++)
        {
            if (_threadSlots[i].InUse && _threadSlots[i].OsThreadId == osThreadId)
            {
                return i;
            }
        }

        return -1;
    }

    internal void OpenURL(string url)
    {
        _ = url;
        DexterGFX.SetScreenMode(DexterGFX.RenderWidth, DexterGFX.RenderHeight, DexterGFX.RenderBitDepth, true);
        OSEnvironment.SystemOpenURL();
    }

    internal bool OSInit()
    {
        _mouseButtonCount = 1;
        _appPath = string.Empty;

        bool ok = SDL.Init(0x30);
        if (!ok)
        {
            return false;
        }

        SDL.DisplayMode mode = SDL.GetDesktopDisplayMode(0);

        int w = mode.Width;
        int h = mode.Height;

        // Mirrors the original "half if too large and still above thresholds" logic.
        if ((w > 0x780 || h > 0x4b0) && (w * 2 < 0xCCCCCCD) && (h * 2 < 0xCCCCCCD))
        {
            w = w / 2;
            h = h / 2;
        }

        DexterGFX.ScreenWidth = (ushort)(w & 0xFFFC);
        DexterGFX.ScreenHeight = (ushort)h;

        SDL.InitOk = true;
        DexterApp.AppActive = true;

        return true;
    }

    internal long GetOSThreadID()
    {
        return System.Threading.Thread.CurrentThread.ManagedThreadId;
    }

    internal bool OSUpdate()
    {
        if (!SDL.PollEvent(out SDL.Event ev))
        {
            return false;
        }

        // Based on the switch ranges in the decompile; exact numeric values are kept as-is.
        // Expected: SDL_QUIT=0x100, focus gained/lost 0x103/0x105, keydown 0x300, keyup 0x301,
        // mouse motion 0x400, mouse down 0x401, mouse up 0x402, wheel 0x403, text input 0x303.
        uint type = ev.Type;

        if (type < 0x301)
        {
            if (type < 0x105)
            {
                if (type == 0x100)
                {
                    DexterApp.ShutDown();
                    DexterApp.AppContinue = false;
                    DexterApp.DexterCleanUp();
                    SDL.ExitProcess(-1);
                }
                else if (type == 0x103)
                {
                    SetFocusChange(0x04);
                }

                return false;
            }

            if (type == 0x105)
            {
                SetFocusChange(0x08);
                return true;
            }

            if (type == 0x300)
            {
                uint key = ev.Key.KeySym;
                KeyPressDown(key);

                if (!DexterGFX.ScreenLocked && key == 0x0D)
                {
                    if ((ev.Key.Modifiers & 3) != 0)
                    {
                        DexterGFX.SetScreenMode(DexterGFX.RenderWidth, DexterGFX.RenderHeight, DexterGFX.RenderBitDepth, false);
                    }
                }

                if (key == 0x7A)
                {
                    if ((ev.Key.ScancodeFlags & 0xC0) != 0)
                    {
                        SDL.MinimizeWindow();
                    }
                }

                if (key == 0x67)
                {
                    if ((ev.Key.ScancodeFlags & 0xC0) != 0)
                    {
                        bool relative = SDL.GetRelativeMouseMode();
                        SDL.SetRelativeMouseMode(!relative);
                    }
                }

                if (key == 0x71 && (ev.Key.Modifiers & 0x0C) != 0)
                {
                    DexterApp.ShutDown();
                    DexterApp.AppContinue = false;
                    DexterApp.DexterCleanUp();
                    SDL.ExitProcess(0);
                }

                return true;
            }

            if (type == 0x301)
            {
                KeyPressUp(ev.Key.KeySym);
                return true;
            }

            return false;
        }

        switch (type)
        {
            case 0x400:
                UpdateMouse(ev.Motion.XRel, ev.Motion.YRel, 1, false);
                return true;

            case 0x401:
                if (ev.Button.Button == 1)
                {
                    MousePressDown(0);
                }
                else if (ev.Button.Button == 2)
                {
                    MousePressDown(2);
                }
                else if (ev.Button.Button == 3)
                {
                    MousePressDown(1);
                }
                return true;

            case 0x402:
                if (ev.Button.Button == 1)
                {
                    MousePressUp(0);
                }
                else if (ev.Button.Button == 2)
                {
                    MousePressUp(2);
                }
                else if (ev.Button.Button == 3)
                {
                    MousePressUp(1);
                }
                return true;

            case 0x403:
                // Wheel: emulate press+release of button 3/4 depending on direction
                if (ev.Wheel.Y < 0)
                {
                    MousePressDown(3);
                    MousePressUp(3);
                }
                else
                {
                    MousePressDown(4);
                    MousePressUp(4);
                }
                return true;

            default:
                if (type == 0x303)
                {
                    // Text input: first char in Text, subsequent in Tail until '\0'
                    byte first = ev.Text.FirstByte;
                    if (first != 0)
                    {
                        AddTextKey(first);

                        byte[] tail = ev.Text.TailBytes;
                        for (int i = 0; i < tail.Length; i++)
                        {
                            if (tail[i] == 0)
                            {
                                break;
                            }
                            AddTextKey(tail[i]);
                        }
                    }
                    return true;
                }
                return false;
        }
    }

    private void EnqueueKeyEvent(byte type, byte key, int duration, ushort qualifiers)
    {
        if (_keyQueue[_keyQueueWrite].Type != 0)
        {
            return;
        }

        _keyQueue[_keyQueueWrite] = new KeyEvent(type, key, duration, qualifiers);
        _keyQueueWrite = (_keyQueueWrite + 1) % MaxQueueEntries;
    }

    private void EnqueueMouseEvent(byte type, byte button, int duration, short x, short y, ushort qualifiers)
    {
        if (_mouseQueue[_mouseQueueWrite].Type != 0)
        {
            return;
        }

        _mouseQueue[_mouseQueueWrite] = new MouseEvent(type, button, duration, x, y, qualifiers);
        _mouseQueueWrite = (_mouseQueueWrite + 1) % MaxQueueEntries;
    }

    private ushort GetQualifiersInternal()
    {
        bool groupA = (_qualifierGroupA_0 || _qualifierGroupA_1) || _qualifierGroupA_2;
        bool groupB = (_qualifierGroupB_0 || _qualifierGroupB_1) || _qualifierGroupB_2;

        // Original: return (groupA ? 1 : 0) + (groupB ? 4 : 0), but if groupB is false -> only groupA.
        ushort value = (ushort)(groupA ? 1 : 0);
        if (groupB)
        {
            value = (ushort)(value + 4);
        }

        return value;
    }

    private void ThreadRegisterInternal(ushort dexThreadId, long osThreadId)
    {
        int slotIndex = dexThreadId;

        _threadSlots[slotIndex] = _threadSlots[slotIndex]
            .WithOsThreadId(osThreadId)
            .WithInUse(true)
            .WithRegistered(true)
            .WithEnabled(true);

        DexThread.ThreadCount++;
        OSGeneric.ThreadRegister(0x6820);
    }

    private static int Limit(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    internal readonly struct KeyEvent
    {
        internal KeyEvent(byte type, byte key, int duration, ushort qualifiers)
        {
            Type = type;
            Key = key;
            Duration = duration;
            Qualifiers = qualifiers;
        }

        internal byte Type { get; }
        internal byte Key { get; }
        internal ushort Qualifiers { get; }
        internal int Duration { get; }
    }

    internal readonly struct MouseEvent
    {
        internal MouseEvent(byte type, byte button, int duration, short x, short y, ushort qualifiers)
        {
            Type = type;
            Button = button;
            Duration = duration;
            X = x;
            Y = y;
            Qualifiers = qualifiers;
        }

        internal byte Type { get; }
        internal byte Button { get; }
        internal ushort Qualifiers { get; }
        internal int Duration { get; }
        internal short X { get; }
        internal short Y { get; }
    }

    internal readonly struct TextEvent
    {
        internal TextEvent(bool inUse, byte value)
        {
            InUse = inUse;
            Value = value;
        }

        internal bool InUse { get; }
        internal byte Value { get; }
    }

    private readonly struct ThreadSlot
    {
        internal ThreadSlot(bool inUse, bool enabled, bool registered, System.Action? entryPoint, System.Threading.Thread? thread)
        {
            InUse = inUse;
            Enabled = enabled;
            Registered = registered;
            EntryPoint = entryPoint;
            Thread = thread;
            OsThreadId = -1;
        }

        internal bool InUse { get; }
        internal bool Enabled { get; }
        internal bool Registered { get; }
        internal long OsThreadId { get; }
        internal Action? EntryPoint { get; }
        internal Thread? Thread { get; }

        internal ThreadSlot WithEnabled(bool enabled)
        {
            ThreadSlot slot = this;
            return new ThreadSlot(slot.InUse, enabled, slot.Registered, slot.EntryPoint, slot.Thread).WithOsThreadId(slot.OsThreadId);
        }

        internal ThreadSlot WithRegistered(bool registered)
        {
            ThreadSlot slot = this;
            return new ThreadSlot(slot.InUse, slot.Enabled, registered, slot.EntryPoint, slot.Thread).WithOsThreadId(slot.OsThreadId);
        }

        internal ThreadSlot WithInUse(bool inUse)
        {
            ThreadSlot slot = this;
            return new ThreadSlot(inUse, slot.Enabled, slot.Registered, slot.EntryPoint, slot.Thread).WithOsThreadId(slot.OsThreadId);
        }

        internal ThreadSlot WithThread(System.Threading.Thread thread)
        {
            ThreadSlot slot = this;
            return new ThreadSlot(slot.InUse, slot.Enabled, slot.Registered, slot.EntryPoint, thread).WithOsThreadId(slot.OsThreadId);
        }

        internal ThreadSlot WithOsThreadId(long osThreadId)
        {
            // Record-style copy that preserves all other fields.
            return new ThreadSlot(InUse, Enabled, Registered, EntryPoint, Thread) { _osThreadId = osThreadId };
        }

        // Backing hack to keep struct readonly externally while allowing a single internal set through WithOsThreadId.
        private readonly long _osThreadId;
        internal long OsThreadId => _osThreadId;
    }
}

internal sealed class MessageBuf
{
    private const int MaxQueueEntries = 0x32; // 50

    private readonly DexEvent[] _events;
    private int _write;
    private int _read;

    internal MessageBuf()
    {
        _events = new DexEvent[MaxQueueEntries];
        _write = 0;
        _read = 0;
    }

    internal void AddMessage(DexEvent ev)
    {
        if (ev.Type == 0)
        {
            return;
        }

        if (_events[_write].Type != 0)
        {
            return;
        }

        _events[_write] = ev;
        _write = (_write + 1) % MaxQueueEntries;
    }

    internal DexEvent PeekMessage()
    {
        return _events[_read];
    }

    internal DexEvent GetMessage()
    {
        DexEvent ev = _events[_read];
        if (ev.Type != 0)
        {
            _events[_read] = default;
            _read = (_read + 1) % MaxQueueEntries;
        }
        return ev;
    }

    internal readonly struct DexEvent
    {
        internal DexEvent(short type, int a, int b, short c, short d, short e, short f)
        {
            Type = type;
            A = a;
            B = b;
            C = c;
            D = d;
            E = e;
            F = f;
        }

        internal short Type { get; }
        internal int A { get; }
        internal int B { get; }
        internal short C { get; }
        internal short D { get; }
        internal short E { get; }
        internal short F { get; }
    }
}