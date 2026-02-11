using Silk.NET.SDL;
using System.Globalization;

namespace OpenVikings.Dexter
{
    internal sealed class DexterOS
    {
        private const int MaxQueueEntries = 0x32; // 50
        private const int MaxKeyCodes = 0x7e;     // 126
        private const int InvalidKey = 0x7f;

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
        private readonly OSEnvironment _osEnvironment;

        private int _commandLineArgs;
        private string[]? _commandLineStrings;

        private string _appName;
        private string _appTitle;
        private string _appPath;

        private bool _currentKeyPadEntryActive;

        private bool _qualifierGroupA_0;
        private bool _qualifierGroupA_1;
        private bool _qualifierGroupA_2;

        private bool _qualifierGroupB_0;
        private bool _qualifierGroupB_1;
        private bool _qualifierGroupB_2;

        private readonly ThreadSlot[] _threadSlots;
        private readonly object _threadMutex;
        private bool _threadMutexInitialized;
        private int _threadMutexDepth;

        private ushort _newThreadId;

        private readonly uint[] _osKeyTranslateA;
        private readonly uint[] _osKeyTranslateB;
        private readonly uint[] _osKeyTranslateC;

        private readonly DexterGFXState _gfxState;
        private readonly DexterGFXScreen _gfxScreen;

        private readonly Sdl _sdl;
        private bool _sdlInitialized;

        internal DexterOS(uint[] osKeyTranslateA, uint[] osKeyTranslateB, uint[] osKeyTranslateC, OSEnvironment osEnvironment, DexterGFXState gfxState, DexterGFXScreen gfxScreen)
        {
            _osEnvironment = osEnvironment;
            _gfxState = gfxState;
            _gfxScreen = gfxScreen;

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

            _sdl = Sdl.GetApi();
            _sdlInitialized = false;

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
                if (DexterString.StringCompare(_commandLineStrings[index], value, (byte)'\0'))
                {
                    return true;
                }
            }

            return false;
        }

        internal int Time()
        {
            return (int)_osEnvironment.Time() - _timeCheckReset;
        }

        internal void RelaxThread(int reason, string? tag)
        {
            _ = reason;
            _ = tag;
            OSGeneric.RelaxThread();
        }

        internal void UpdateMouse(short x, short y, byte mode, bool averageWithCurrent)
        {
            int scaledX = _gfxState.WindowToRenderX(x);
            int scaledY = _gfxState.WindowToRenderY(y);

            if (mode == 1)
            {
                short newX = (short)(scaledX + _mouseX);
                _mouseSpeedX = (short)scaledX;

                _mouseSpeedY = (short)scaledY;
                _mouseY = (short)(_mouseY + _mouseSpeedY);
                _mouseX = newX;
            }
            else
            {
                short newX = (short)scaledX;
                short newY = (short)scaledY;

                if (averageWithCurrent)
                {
                    newX = (short)((newX + _mouseX) >> 1);
                    newY = (short)((newY + _mouseY) >> 1);
                }

                _mouseSpeedX = (short)(newX - _mouseX);
                _mouseSpeedY = (short)(newY - _mouseY);
                _mouseX = newX;
                _mouseY = newY;
            }

            _mouseX = (short)Limit(_mouseX, 0, _gfxState.RenderWidth - 1);
            _mouseY = (short)Limit(_mouseY, 0, _gfxState.RenderHeight - 1);
        }

        internal void SetAppTitle(string title, string name)
        {
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

            if (_keyState[key] == 0)
            {
                int now = (int)_osEnvironment.Time();
                _keyState[key] = now - _timeCheckReset;
            }

            EnqueueKeyEvent(1, (byte)key, 0, qualifiers);
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

            int now = (int)_osEnvironment.Time();
            int duration = now - (_timeCheckReset + downTime);

            _keyState[key] = 0;

            EnqueueKeyEvent(2, (byte)key, duration, qualifiers);
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
                    int now = (int)_osEnvironment.Time();
                    _mouseButtonTime[button] = now - _timeCheckReset;

                    if (_mouseButtonCount <= button)
                    {
                        _mouseButtonCount = button + 1;
                    }
                }
            }

            EnqueueMouseEvent(1, button, 0, x, y, qualifiers);
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

                    int now = (int)_osEnvironment.Time();
                    int downTime = _mouseButtonTime[button];
                    int duration = now - (_timeCheckReset + downTime);

                    EnqueueMouseEvent(2, button, duration, x, y, qualifiers);
                }
            }
        }

        internal void SetFocusChange(byte focusEventCode)
        {
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
            EnqueueKeyEvent(1, (byte)key, 0, 0);
            EnqueueKeyEvent(2, (byte)key, 1, 0);
        }

        internal void AddQueuedMouse(byte button, short x, short y)
        {
            EnqueueMouseEvent(1, button, 0, x, y, 0);
            EnqueueMouseEvent(2, button, 1, x, y, 0);
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

        internal void AddTextKey(byte value)
        {
            if (_textQueue[_textQueueWrite].InUse)
            {
                return;
            }

            _textQueue[_textQueueWrite] = new TextEvent(true, value);
            _textQueueWrite = (_textQueueWrite + 1) % MaxQueueEntries;
        }

        internal uint SecTime()
        {
            int now = (int)_osEnvironment.Time();
            return (uint)((now - _timeCheckReset) / 1000);
        }

        internal void Pause(uint ms)
        {
            if (ms < 300000)
            {
                _osEnvironment.Pause(ms);
            }
        }

        internal void MutexInit()
        {
            // In modern C#, there is nothing to "init" for Monitor locking.
            // This flag is kept for compatibility with original flow.
            _threadMutexInitialized = true;
        }

        internal void MutexLock()
        {
            if (!_threadMutexInitialized)
            {
                return;
            }

            Monitor.Enter(_threadMutex);
            _threadMutexDepth++;
        }

        internal void MutexUnLock()
        {
            if (!_threadMutexInitialized)
            {
                return;
            }

            _threadMutexDepth--;
            Monitor.Exit(_threadMutex);
        }

        internal void SetWindowTitle(string title)
        {
            _appTitle = title;
            _osEnvironment.SetWindowTitle(title);
        }

        internal void FailRequester(string format, params object[] args)
        {
            string message = string.Format(CultureInfo.InvariantCulture, format, args);
            OSGeneric.FailRequester(message);
        }

        internal bool ThreadStart(ushort dexThreadId, Action entryPoint, string? name)
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
                MutexInit();
            }

            _threadSlots[slotIndex] = new ThreadSlot(true, true, false, -1, entryPoint, null);

            // Compatibility with the original flow (some paths use this before the thread reads its state).
            _newThreadId = dexThreadId;

            System.Threading.Thread thread = new(ThreadHeader)
            {
                IsBackground = true
            };

            _threadSlots[slotIndex] = _threadSlots[slotIndex].WithThread(thread);

            thread.Start(dexThreadId);

            // Busy-wait like original (but yield to avoid burning CPU).
            while (!_threadSlots[slotIndex].Registered)
            {
                OSGeneric.RelaxThread();
            }

            return true;
        }

        private void ThreadHeader(object? state)
        {
            ushort dexThreadId = state is ushort v ? v : _newThreadId;

            if (dexThreadId < 1 || dexThreadId > 4)
            {
                return;
            }

            int slotIndex = dexThreadId;

            // Register thread under the local mutex (not OSGeneric.*).
            if (_threadMutexInitialized)
            {
                MutexLock();
            }

            long osThreadId = GetOSThreadID();
            ThreadRegisterInternal(dexThreadId, osThreadId);

            if (_threadMutexInitialized)
            {
                MutexUnLock();
            }

            // SignalDexter() is unnecessary in this design (ThreadStart waits on Registered).
            // If a future coordination primitive is needed, use a ManualResetEventSlim in the slot.

            while (_threadSlots[slotIndex].Enabled)
            {
                OSGeneric.RelaxThread();
                _threadSlots[slotIndex].EntryPoint?.Invoke();
            }

            DexterMemory.ClearMallocCache();
            ThreadRelease(dexThreadId);
        }

        internal long GetOSThreadID()
        {
            return Environment.CurrentManagedThreadId;
        }

        internal void OpenURL(string url)
        {
            _gfxScreen.SetScreenMode(_gfxScreen.RenderWidth, _gfxScreen.RenderHeight, _gfxScreen.RenderBitDepth, true);
            OSEnvironment.SystemOpenUrl(url);
        }

        internal bool OSInit()
        {
            _mouseButtonCount = 1;
            _appPath = string.Empty;

            // 0x30 == SDL_INIT_VIDEO | SDL_INIT_EVENTS in the original code path
            int initResult = _sdl.Init(0x30);
            if (initResult < 0)
            {
                return false;
            }

            DisplayMode mode = default;
            unsafe
            {
                int dmResult = _sdl.GetDesktopDisplayMode(0, &mode);
                if (dmResult < 0)
                {
                    return false;
                }
            }

            int w = mode.W;
            int h = mode.H;

            if ((w > 0x780 || h > 0x4b0) && (w * 2 < 0xCCCCCCD) && (h * 2 < 0xCCCCCCD))
            {
                w >>= 1;
                h >>= 1;
            }

            int windowW = w & 0xFFFC;
            int windowH = h;

            _gfxScreen.SetWindowSize(windowW, windowH);

            return true;
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

            DexterThread.ThreadCount++;
        }

        internal void ThreadRelease(ushort dexThreadId)
        {
            if (dexThreadId < 1 || dexThreadId > 4)
            {
                return;
            }

            if (_threadMutexInitialized)
            {
                MutexLock();
            }

            int slotIndex = dexThreadId;
            _threadSlots[slotIndex] = new ThreadSlot(false, false, false, -1, null, null);

            long currentOsThreadId = GetOSThreadID();
            if (DexterThread.CooperativeThread == currentOsThreadId)
            {
                DexterThread.CooperativeThread = -1;
            }

            DexterThread.ThreadCount--;

            if (_threadMutexInitialized)
            {
                MutexUnLock();
            }
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
            internal ThreadSlot(bool inUse, bool enabled, bool registered, long osThreadId, Action? entryPoint, System.Threading.Thread? thread)
            {
                InUse = inUse;
                Enabled = enabled;
                Registered = registered;
                OsThreadId = osThreadId;
                EntryPoint = entryPoint;
                Thread = thread;
            }

            internal bool InUse { get; }
            internal bool Enabled { get; }
            internal bool Registered { get; }
            internal long OsThreadId { get; }
            internal Action? EntryPoint { get; }
            internal System.Threading.Thread? Thread { get; }

            internal ThreadSlot WithEnabled(bool enabled)
            {
                return new ThreadSlot(InUse, enabled, Registered, OsThreadId, EntryPoint, Thread);
            }

            internal ThreadSlot WithRegistered(bool registered)
            {
                return new ThreadSlot(InUse, Enabled, registered, OsThreadId, EntryPoint, Thread);
            }

            internal ThreadSlot WithInUse(bool inUse)
            {
                return new ThreadSlot(inUse, Enabled, Registered, OsThreadId, EntryPoint, Thread);
            }

            internal ThreadSlot WithThread(System.Threading.Thread thread)
            {
                return new ThreadSlot(InUse, Enabled, Registered, OsThreadId, EntryPoint, thread);
            }

            internal ThreadSlot WithOsThreadId(long osThreadId)
            {
                return new ThreadSlot(InUse, Enabled, Registered, osThreadId, EntryPoint, Thread);
            }

            internal ThreadSlot WithRegisteredAndEnabled(bool registered, bool enabled)
            {
                return new ThreadSlot(InUse, enabled, registered, OsThreadId, EntryPoint, Thread);
            }
        }
    }
}