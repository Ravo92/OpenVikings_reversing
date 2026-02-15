namespace OpenVikings.Dexter
{
    // Managed port of DexterApp.cxx.
    // Focus: preserve original control flow and RNG/Zone behavior, while avoiding pointers and unmanaged memory.
    internal sealed class DexterApp
    {
        internal delegate bool BoolCallback();
        internal delegate void VoidCallback();
        internal delegate int IntCallback();

        private readonly DexterOS _os;
        private readonly OSEnvironment _osEnvironment;

        // External hooks (original: DexterMain, DexterAppInstall, DexterAppExit, DexterAppSetup, OSUpdate).
        private readonly VoidCallback _mainCallback;
        private readonly BoolCallback? _installCallback;
        private readonly BoolCallback? _exitCallback;
        private readonly BoolCallback? _setupCallback;

        private readonly IntCallback? _getCallbackTimeMs;
        private readonly VoidCallback? _onFpsSample;
        private readonly BoolCallback? _osUpdateCallback;

        private bool _dexterActive;
        internal bool AppContinue { get; private set; }

        private uint _fullScreenKeyVal;
        private uint _normalWindowKeyVal;
        private uint _doubleWindowKeyVal;

        // ----------------------------
        // RANMAR RNG (Marsaglia & Zaman)
        // ----------------------------

        private readonly float[] _randomU; // 97
        private int _i97;                  // 0..96
        private int _j97;                  // 0..96

        // c, cd, cm as doubles (original uses 64-bit constants).
        private double _c;
        private readonly double _cd;
        private readonly double _cm;

        private readonly RandomState[] _randomStack;
        private int _randomStackPos;

        // ----------------------------
        // Zones
        // ----------------------------

        private ZoneEntry[]? _zones;
        private short _maxZones;

        internal DexterApp(DexterOS os, OSEnvironment osEnvironment, VoidCallback mainCallback, BoolCallback? installCallback, BoolCallback? exitCallback, BoolCallback? setupCallback, IntCallback? getCallbackTimeMs, VoidCallback? onFpsSample, BoolCallback? osUpdateCallback)
        {
            ArgumentNullException.ThrowIfNull(os);
            ArgumentNullException.ThrowIfNull(osEnvironment);
            ArgumentNullException.ThrowIfNull(mainCallback);

            _os = os;
            _osEnvironment = osEnvironment;

            _mainCallback = mainCallback;
            _installCallback = installCallback;
            _exitCallback = exitCallback;
            _setupCallback = setupCallback;

            _getCallbackTimeMs = getCallbackTimeMs;
            _onFpsSample = onFpsSample;
            _osUpdateCallback = osUpdateCallback;

            _dexterActive = false;
            AppContinue = false;

            _fullScreenKeyVal = 0;
            _normalWindowKeyVal = 0;
            _doubleWindowKeyVal = 0;

            _randomU = new float[0x61];
            _randomStack = new RandomState[4];
            _randomStackPos = 0;

            // RANMAR defaults (match classic published constants)
            _cd = 7654321.0 / 16777216.0;
            _cm = 16777213.0 / 16777216.0;

            // Constructor in the original initializes a deterministic starting state.
            Randomize(0);
        }

        // DexterApp::Init()
        internal bool Init()
        {
            // App metadata (original used DexterString::StringCopy + static globals).
            _os.SetAppTitle("Dexter Application", "DexterApp");

            bool ok = _os.OSInit();
            if (!ok)
            {
                return false;
            }

            AppContinue = true;

            // Original: ThreadRegister(0) + TimeCheckReset = OSEnvironment::Time().
            // This managed port relies on OSEnvironment/SDL tick base being set up by OSInit().

            if (_installCallback != null)
            {
                if (!_installCallback())
                {
                    return false;
                }
            }

            _dexterActive = true;

            // Original: DexterMemory::AllocHeap() + audio init + gfx init.
            DexterMemory.AllocHeap();

            // Zones are allocated only once the desired MaxZones is known.
            // Preserve behavior: allocate at Init() time if MaxZones was already set.
            if (_maxZones > 0 && _zones == null)
            {
                _zones = new ZoneEntry[_maxZones];
            }

            if (_setupCallback != null)
            {
                if (!_setupCallback())
                {
                    ShutDown();
                    DexterCleanUp();
                    return false;
                }
            }

            return true;
        }

        // DexterApp::ShutDown()
        internal void ShutDown()
        {
            if (!_dexterActive)
            {
                return;
            }

            // Original ended threads 1..4 and shut down gfx + audio.
            // In managed code, threads are cooperative and released by DexterOS.
            _os.ThreadRelease(1);
            _os.ThreadRelease(2);
            _os.ThreadRelease(3);
            _os.ThreadRelease(4);

            _dexterActive = false;
        }

        // DexterApp::DexterCleanUp()
        internal void DexterCleanUp()
        {
            _exitCallback?.Invoke();

            _zones = null;
            _maxZones = 0;

            DexterMemory.MemorySystemShutDown();
            DexterMemory.FreeHeap();

            _os.ThreadRelease(0);
        }

        // DexterApp::DexterOSMain()
        // In the original, this loop called DexterOS::OSUpdate() (SDL pump) and only ran MainThread when no events were pending.
        // This port keeps that behavior via an injected callback to avoid hard-coding an SDL implementation here.
        internal void DexterOSMainLoop()
        {
            while (AppContinue)
            {
                bool processedEvent = _osUpdateCallback != null && _osUpdateCallback();
                if (!processedEvent)
                {
                    MainThreadTick();
                }
            }

            ShutDown();
            DexterCleanUp();
        }

        // DexterApp::MainThread()
        internal bool MainThreadTick()
        {
            if (!AppContinue)
            {
                return false;
            }

            int beginTicks = unchecked((int)_osEnvironment.Time());

            OSGeneric.RelaxThread();
            _mainCallback();

            int callbackTimeMs = _getCallbackTimeMs != null ? _getCallbackTimeMs() : 0;

            // Original had sentinels: 0x7777 => ended, 0x6666 => skip fps update.
            if (callbackTimeMs == 0x7777)
            {
                ApplicationEnded();
                return false;
            }

            if (callbackTimeMs == 0x6666)
            {
                _onFpsSample?.Invoke();
                return AppContinue;
            }

            int endTicks = unchecked((int)_osEnvironment.Time());
            int frameTime = unchecked(endTicks - beginTicks);

            // Hook for FPS/debug sampling.
            _onFpsSample?.Invoke();

            if (callbackTimeMs > 0 && frameTime < callbackTimeMs)
            {
                do
                {
                    OSGeneric.RelaxThread();
                    endTicks = unchecked((int)_osEnvironment.Time());
                    frameTime = unchecked(endTicks - beginTicks);
                } while (frameTime < callbackTimeMs);
            }

            return AppContinue;
        }

        // DexterApp::ApplicationEnded()
        internal void ApplicationEnded()
        {
            AppContinue = false;
        }

        // DexterApp::MemoryUsageReport() (empty in original)
        internal void MemoryUsageReport()
        {
        }

        // DexterApp::Abort() (empty in original)
        internal void Abort()
        {
        }

        internal void SetFullScreenKey(uint value)
        {
            _fullScreenKeyVal = value;
        }

        internal void SetNormalWindowKey(uint value)
        {
            _normalWindowKeyVal = value;
        }

        internal void SetDoubleWindowKey(uint value)
        {
            _doubleWindowKeyVal = value;
        }

        internal uint FullScreenKey()
        {
            return _fullScreenKeyVal;
        }

        internal uint NormalWindowKey()
        {
            return _normalWindowKeyVal;
        }

        internal uint DoubleWindowKey()
        {
            return _doubleWindowKeyVal;
        }

        // ------------------------------------------------------------
        // RNG
        // ------------------------------------------------------------

        internal int Rand()
        {
            float u = NextUnitFloat();
            float value = u * 32767.0f;
            return (int)value;
        }

        internal int RRand(int minInclusive, int maxInclusive)
        {
            if (minInclusive > maxInclusive)
            {
                (maxInclusive, minInclusive) = (minInclusive, maxInclusive);
            }

            float u = NextUnitFloat();

            // (max - min + 1) is inclusive range.
            float span = (maxInclusive - minInclusive) + 1.0f;
            float value = minInclusive + (u * span);

            int result = (int)value;
            if (value > maxInclusive)
            {
                result = maxInclusive;
            }

            return result;
        }

        internal uint RandomValue(uint maxValue)
        {
            if (maxValue == 0)
            {
                return 0;
            }

            float u = NextUnitFloat();
            uint scaled = (uint)(u * maxValue);

            // Match original's edge handling: never return maxValue.
            if (scaled == maxValue)
            {
                return maxValue - 1;
            }

            return scaled;
        }

        internal void Randomize(int seed)
        {
            // The original clamps the seed into a 0..31999 range with wrap behavior.
            int normalized = seed;
            if (normalized < 0)
            {
                normalized = 0;
            }

            if (normalized > 31999)
            {
                normalized %= 32000;
            }

            // The original derives 4 internal seeds (i,j,k,l) from the normalized value.
            // This mirrors the classic RANMAR initialization using (ij, kl).
            int ij = normalized;
            int kl = 32000 - ij;

            int i = ((ij / 177) % 177) + 2;
            int j = (ij % 177) + 2;

            int k = (kl / 169) + 1;
            int l = (kl % 169);

            for (int idx = 0; idx < 0x61; idx++)
            {
                float s = 0.0f;
                float t = 0.5f;

                for (int n = 0; n < 24; n++)
                {
                    int m = (((i * j) % 179) * k) % 179;
                    i = j;
                    j = k;
                    k = m;

                    l = ((l * 53) + 1) % 169;

                    int prod = (short)m * (short)l;
                    int prodAdj = prod >= 0 ? prod : (prod + 63);

                    if (((short)(prod - (prodAdj & unchecked((short)0xFFC0)))) > 31)
                    {
                        s += t;
                    }

                    t *= 0.5f;
                }

                _randomU[idx] = s;
            }

            // Indices correspond to i97=97, j97=33 in 1-based notation.
            _i97 = 0x60;
            _j97 = 0x20;

            _c = 362436.0 / 16777216.0;
        }

        internal void RandomPush()
        {
            if (_randomStackPos >= _randomStack.Length)
            {
                return;
            }

            RandomState state = new(_randomU, _i97, _j97, _c);
            _randomStack[_randomStackPos] = state;
            _randomStackPos++;
        }

        internal void RandomPop()
        {
            if (_randomStackPos <= 0)
            {
                return;
            }

            _randomStackPos--;
            RandomState state = _randomStack[_randomStackPos];

            Array.Copy(state.U, _randomU, _randomU.Length);
            _i97 = state.I97;
            _j97 = state.J97;
            _c = state.C;
        }

        private float NextUnitFloat()
        {
            float uni = _randomU[_i97] - _randomU[_j97];
            if (uni < 0.0f)
            {
                uni += 1.0f;
            }

            _randomU[_i97] = uni;

            _i97--;
            if (_i97 < 0)
            {
                _i97 = 0x60;
            }

            _j97--;
            if (_j97 < 0)
            {
                _j97 = 0x60;
            }

            _c -= _cd;
            if (_c < 0.0)
            {
                _c += _cm;
            }

            float result = uni - (float)_c;
            if (result < 0.0f)
            {
                result += 1.0f;
            }

            return result;
        }

        // ------------------------------------------------------------
        // Math helpers
        // ------------------------------------------------------------

        internal double SquareRoot(double value)
        {
            return OSGeneric.SystemSquareRoot(value);
        }

        internal long DistanceExact(int x1, int y1, int x2, int y2)
        {
            int dx = x2 - x1;
            int dy = y2 - y1;

            double dist = OSGeneric.SystemSquareRoot((double)(dx * dx + dy * dy));
            return (long)dist;
        }

        // Original uses an approximation (fast distance).
        internal int Distance(int x1, int y1, int x2, int y2)
        {
            uint dx = (uint)Math.Abs(x2 - x1);
            uint dy = (uint)Math.Abs(y2 - y1);

            uint max = dx;
            uint min = dy;
            if (max < min)
            {
                max = dy;
                min = dx;
            }

            uint minScaled = (min >> 1) + min;

            // Same bit-shift approximation as the decompiled code.
            return (int)((minScaled >> 2) + (max - ((max >> 7) + (max >> 5))) + (minScaled >> 6));
        }

        // ------------------------------------------------------------
        // Zones
        // ------------------------------------------------------------

        internal bool ValidZoneId(short zoneId)
        {
            if (zoneId < 0)
            {
                return false;
            }

            if (zoneId >= _maxZones)
            {
                return false;
            }

            return _zones != null;
        }

        internal void SetMaxZones(short maxZones)
        {
            // Preserve original: once allocated, it cannot be changed.
            if (_zones != null)
            {
                return;
            }

            _maxZones = maxZones;
            if (_maxZones > 0)
            {
                _zones = new ZoneEntry[_maxZones];
            }
        }

        internal void ZoneFree(short zoneId)
        {
            if (!ValidZoneId(zoneId))
            {
                return;
            }

            _zones![zoneId] = _zones[zoneId].WithActive(false);
        }

        internal void AddZone(short zoneId, short x, short y, ushort width, ushort height)
        {
            if (!ValidZoneId(zoneId))
            {
                return;
            }

            int left = x;
            int top = y;
            int right = x + width;
            int bottom = y + height;

            _zones![zoneId] = new ZoneEntry(left, top, right, bottom, true);
        }

        internal bool ZoneHit(short zoneId, ushort x, ushort y)
        {
            if (!ValidZoneId(zoneId))
            {
                return false;
            }

            ZoneEntry zone = _zones![zoneId];
            if (!zone.Active)
            {
                return false;
            }

            int px = x;
            int py = y;

            return zone.Left <= px && px <= zone.Right && zone.Top <= py && py <= zone.Bottom;
        }

        internal void InitZones()
        {
            if (_zones == null || _maxZones < 1)
            {
                return;
            }

            for (int i = 0; i < _zones.Length; i++)
            {
                _zones[i] = _zones[i].WithActive(false);
            }
        }

        // mode: when 1, return the last matching zone; otherwise return the first match.
        internal int ZoneFind(ushort x, ushort y, byte mode)
        {
            if (_zones == null || _maxZones < 1)
            {
                return -1;
            }

            int found = -1;
            int px = x;
            int py = y;

            for (int i = 0; i < _zones.Length; i++)
            {
                ZoneEntry zone = _zones[i];
                if (!zone.Active)
                {
                    continue;
                }

                if (zone.Left <= px && px <= zone.Right && zone.Top <= py && py <= zone.Bottom)
                {
                    found = i;
                    if (mode != 1)
                    {
                        break;
                    }
                }
            }

            return found;
        }

        internal int MouseZone(byte mode)
        {
            int px = _os.MouseX;
            int py = _os.MouseY;

            return ZoneFind((ushort)px, (ushort)py, mode);
        }

        private readonly struct RandomState
        {
            internal RandomState(float[] u, int i97, int j97, double c)
            {
                U = new float[u.Length];
                Array.Copy(u, U, u.Length);
                I97 = i97;
                J97 = j97;
                C = c;
            }

            internal float[] U { get; }
            internal int I97 { get; }
            internal int J97 { get; }
            internal double C { get; }
        }

        private readonly struct ZoneEntry
        {
            internal ZoneEntry(int left, int top, int right, int bottom, bool active)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
                Active = active;
            }

            internal int Left { get; }
            internal int Top { get; }
            internal int Right { get; }
            internal int Bottom { get; }
            internal bool Active { get; }

            internal ZoneEntry WithActive(bool active)
            {
                return new ZoneEntry(Left, Top, Right, Bottom, active);
            }
        }
    }
}