namespace OpenVikings.Dexter
{
    // Managed port of DexterApp.cxx (current version).
    // Goal: preserve original control flow, callbacks, random table initialization, and shutdown ordering.
    internal sealed class DexterApp : IDisposable
    {
        internal delegate void VoidCallback();
        internal delegate bool BoolCallback();

        // --------------------------------------------------------------------
        // Subsystem bindings (DexterGFX is instance-based in this port)
        // --------------------------------------------------------------------

        private static DexterGFX? _gfx;

        internal static DexterGFX GFX
        {
            get
            {
                if (_gfx == null)
                {
                    throw new InvalidOperationException("DexterApp.GFX is not bound. Bind a DexterGFX instance before calling DexterApp.Init/MainThread.");
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
        // Global-ish state (original uses globals; keep static for fidelity)
        // --------------------------------------------------------------------

        internal static bool DexterActive;
        internal static bool AppContinue;

        // Original external hooks (function pointers)
        internal static VoidCallback? DexterMainCallBack;
        internal static BoolCallback? DexterAppInstallCallBack;
        internal static BoolCallback? DexterAppExitCallBack;
        internal static BoolCallback? DexterAppSetupCallBack;

        // Original "globals" referenced from other modules
        internal static float[] Random = new float[0x61]; // 97 floats

        // These overlap warnings in decompile; treat as the RANMAR constants/state.
        // cd = 7654321 / 2^24, cm = 16777213 / 2^24, c initial = 362436 / 2^24
        internal static double RandomC = 362436.0 / 16777216.0;
        internal static readonly double RandomCd = 7654321.0 / 16777216.0;
        internal static readonly double RandomCm = 16777213.0 / 16777216.0;

        internal static int RandomI97 = 0x60; // 96 (0-based)
        internal static int RandomJ97 = 0x20; // 32 (0-based)  (decompile shows 0x20)

        // --------------------------------------------------------------------
        // Zones (decompile shows Zone allocation as MaxZones * 0x14 bytes, and zeroing entries)
        // The concrete zone semantics likely live elsewhere; this matches memory footprint init.
        // --------------------------------------------------------------------

        internal static short MaxZones;
        internal static DexZone[]? Zone;

        // --------------------------------------------------------------------
        // Constructor (DexterApp::DexterApp)
        // --------------------------------------------------------------------

        internal DexterApp()
        {
            // In C++ this calls base/embedded constructors:
            // DexterOS::DexterOS, DexterFile::DexterFile, DexterGFX::DexterGFX.
            // In managed code those are static subsystems; nothing to instantiate here.

            BuildRandomTable();

            // Reset "AppName/AppTitle/AppPath" like original constructor does.
            DexterOS.AppName = null;
            DexterOS.AppTitle = null;
            DexterOS.AppPath = null;
        }

        // --------------------------------------------------------------------
        // Dispose / Shutdown mapping (DexterApp::~DexterApp + ShutDown + DexterCleanUp)
        // --------------------------------------------------------------------

        public void Dispose()
        {
            // Map destructor behavior: do full cleanup only if active.
            if (DexterActive)
            {
                // Matches ~DexterApp() order in cxx.
                DexterOS.ThreadEnd(1);
                DexterOS.ThreadEnd(2);
                DexterOS.ThreadEnd(3);
                DexterOS.ThreadEnd(4);

                GFX.GFXShutDown();
                DexterAudio.SoundShutDown();

                DexterActive = false;

                DexterAppExitCallBack?.Invoke();

                if (Zone != null)
                {
                    Zone = null;
                    MaxZones = 0;
                }

                DexterFile.FileSystemShutDown();
                DexterMemory.MemorySystemShutDown();
                DexterMemory.FreeHeap();

                DexterOS.ThreadRelease(0);
                OSGeneric.CleanUp();

                DexterActive = false;
            }
        }

        // DexterApp::ShutDown()
        internal static void ShutDown()
        {
            if (!DexterActive)
            {
                return;
            }

            DexterOS.ThreadEnd(1);
            DexterOS.ThreadEnd(2);
            DexterOS.ThreadEnd(3);
            DexterOS.ThreadEnd(4);

            GFX.GFXShutDown();
            DexterAudio.SoundShutDown();

            DexterActive = false;
        }

        // DexterApp::DexterCleanUp()
        internal static void DexterCleanUp()
        {
            DexterAppExitCallBack?.Invoke();

            if (Zone != null)
            {
                Zone = null;
                MaxZones = 0;
            }

            DexterFile.FileSystemShutDown();
            DexterMemory.MemorySystemShutDown();
            DexterMemory.FreeHeap();

            DexterOS.ThreadRelease(0);
            OSGeneric.CleanUp();
        }

        // --------------------------------------------------------------------
        // Init (DexterApp::Init)
        // --------------------------------------------------------------------

        internal static bool Init()
        {
            // Init()::count++ exists in decompile but not used for logic; omit.

            DexterOS.AppName = "DexterApp";
            DexterOS.AppTitle = "Dexter Application";

            bool ok = DexterOS.OSInit();
            if (!ok)
            {
                return false;
            }

            // These two flags are globals in the binary; keep the side effects minimal here.
            // DAT_100477638 = 1; DAT_10047763a = 1;  (unknown purpose)

            DexterOS.ThreadRegister(0);
            DexterOS.TimeCheckReset = unchecked((int)OSEnvironment.Time());

            DexterAppConfig();

            if (DexterAppInstallCallBack != null)
            {
                bool installOk = DexterAppInstallCallBack();
                if (!installOk)
                {
                    return false;
                }
            }

            DexterActive = true;

            DexterMemory.AllocHeap();

            if (DexterAudio.UseSoundStatus)
            {
                DexterAudio.SoundInit();
            }

            GFX.SetRenderDepth(16, 0x10);
            ok = GFX.GFXInit();
            if (!ok)
            {
                // Failure path mirrors cxx: shut down active threads/gfx/audio, exit callback, free zones, filesystem/memory, cleanup.
                ShutDown();
                DexterCleanUp();
                return false;
            }

            // Allocate Zone array if MaxZones > 0 (and clear)
            if (MaxZones > 0)
            {
                int count = MaxZones;
                if (count > 0)
                {
                    Zone = new DexZone[count];
                    for (int i = 0; i < Zone.Length; i++)
                    {
                        Zone[i] = default;
                    }
                }
            }

            ok = DexterAppSetup();
            if (!ok)
            {
                // Failure path matches cxx.
                ShutDown();
                DexterCleanUp();
                return false;
            }

            return true;
        }

        // --------------------------------------------------------------------
        // DexterOSMain (DexterApp::DexterOSMain)
        // --------------------------------------------------------------------

        internal static void DexterOSMain()
        {
            while (AppContinue)
            {
                bool hadEvent = DexterOS.OSUpdate();
                if (!hadEvent)
                {
                    DexterAudio.SoundUpdate();
                    MainThread();
                }
            }

            // Post-loop cleanup matches cxx.
            if (DexterActive)
            {
                DexterOS.ThreadEnd(1);
                DexterOS.ThreadEnd(2);
                DexterOS.ThreadEnd(3);
                DexterOS.ThreadEnd(4);
                GFX.GFXShutDown();
                DexterAudio.SoundShutDown();
                DexterActive = false;
            }

            DexterAppExitCallBack?.Invoke();

            if (Zone != null)
            {
                Zone = null;
                MaxZones = 0;
            }

            DexterFile.FileSystemShutDown();
            DexterMemory.MemorySystemShutDown();
            DexterMemory.FreeHeap();
            DexterOS.ThreadRelease(0);
            OSGeneric.CleanUp();
        }

        // --------------------------------------------------------------------
        // DistanceExact / Distance
        // --------------------------------------------------------------------

        internal static long DistanceExact(int x1, int y1, int x2, int y2)
        {
            int ax = x1;
            int bx = x2;
            if (ax < bx)
            {
                (bx, ax) = (ax, bx);
            }

            int ay = y1;
            int by = y2;
            if (ay < by)
            {
                (by, ay) = (ay, by);
            }

            uint dx = unchecked((uint)(ax - bx));
            uint dy = unchecked((uint)(ay - by));

            double dist = OSGeneric.SystemSquareRoot(dx * dx + dy * dy);
            return (long)dist;
        }

        internal static int Distance(int x1, int y1, int x2, int y2)
        {
            int ax = x1;
            int bx = x2;
            if (ax < bx)
            {
                (bx, ax) = (ax, bx);
            }

            int ay = y1;
            int by = y2;
            if (ay < by)
            {
                (by, ay) = (ay, by);
            }

            uint dx = unchecked((uint)(ax - bx));
            uint dy = unchecked((uint)(ay - by));

            uint max = dx;
            uint min = dy;
            if (max < min)
            {
                max = dy;
                min = dx;
            }

            uint minScaled = (min >> 1) + min;
            return unchecked((int)((minScaled >> 2) + (max - ((max >> 7) + (max >> 5))) + (minScaled >> 6)));
        }

        // --------------------------------------------------------------------
        // MainThread / ApplicationEnded
        // --------------------------------------------------------------------

        internal static void MainThread()
        {
            if (!AppContinue)
            {
                return;
            }

            int t0 = unchecked((int)OSEnvironment.Time());
            int reset0 = DexterOS.TimeCheckReset;

            OSGeneric.RelaxThread();

            DexterMainCallBack?.Invoke();

            // Sentinels from decompile
            int cb = unchecked((int)GFX.CallBackTime);
            if (cb == 0x7777)
            {
                ApplicationEnded();
                return;
            }

            if (cb == 0x6666)
            {
                DexterDebug.UpdateFPS(0);
                return;
            }

            int t1 = unchecked((int)OSEnvironment.Time());

            // iVar3 = (t1 + (reset0 - t0)) - TimeCheckReset;
            int frameMs = unchecked((t1 + (reset0 - t0)) - DexterOS.TimeCheckReset);

            DexterDebug.UpdateFPS(frameMs);

            if (frameMs < cb)
            {
                do
                {
                    OSGeneric.RelaxThread();
                    t1 = unchecked((int)OSEnvironment.Time());
                    frameMs = unchecked((t1 + (reset0 - t0)) - DexterOS.TimeCheckReset);
                } while (frameMs < cb);
            }
        }

        internal static void ApplicationEnded()
        {
            AppContinue = false;
        }

        // --------------------------------------------------------------------
        // DexterAppConfig / Install / Shutdown (free functions in cxx)
        // --------------------------------------------------------------------

        internal static bool DefaultDexterAppInstall()
        {
            DexterFile.XMLInit();
            DexterFile.XMLSetString("ScreenMode", "FullScreen");
            DexterFile.XMLSetValue("ScreenWidth", GFX.WindowWidth);
            DexterFile.XMLSetValue("ScreenHeight", GFX.WindowHeight);

            DexterFile.XMLLoad("config.xml", (byte)' ');

            if (GFX.WindowWidth < 0x400 || GFX.WindowHeight < 0x300)
            {
                DexterFile.XMLSetValue("ScreenWidth", 800);
                DexterFile.XMLSetValue("ScreenHeight", 600);
            }
            else
            {
                if (DexterOS.HasCommandLine("--lowres") || DexterOS.HasCommandLine("-l"))
                {
                    DexterFile.XMLSetValue("ScreenWidth", 800);
                    DexterFile.XMLSetValue("ScreenHeight", 600);
                }
            }

            if (GFX.WindowWidth < 800 || GFX.WindowHeight < 600)
            {
                DexterFile.XMLSetValue("ScreenWidth", 0x280);
                DexterFile.XMLSetValue("ScreenHeight", 0x1e0);
            }

            if (DexterOS.HasCommandLine("--nosound") || DexterOS.HasCommandLine("-s"))
            {
                DexterAudio.UseSoundStatus = false;
            }

            if (DexterOS.HasCommandLine("--windowed") || DexterOS.HasCommandLine("-w"))
            {
                DexterFile.XMLSetString("ScreenMode", "Windowed");
            }

            if (DexterOS.HasCommandLine("--fullscreen") || DexterOS.HasCommandLine("-f"))
            {
                DexterFile.XMLSetString("ScreenMode", "Fullscreen");
            }

            int preferWidth = (int)DexterFile.XMLGetValue("ScreenWidth");
            int preferHeight = (int)DexterFile.XMLGetValue("ScreenHeight");

            bool windowed = DexterFile.XMLCompareString("ScreenMode", "Windowed");
            bool preferFullscreen = !windowed;

            DexterFile.MakeDir("singleplayer", (byte)' ');
            DexterFile.MakeDir("singleplayer/savegames", (byte)' ');

            GFX.SetScreenMode((ushort)preferWidth, (ushort)preferHeight, 0x10, (byte)(0x02 - (preferFullscreen ? 0 : 1)));
            return true;
        }

        internal static bool DefaultDexterAppShutdown()
        {
            DexterFile.XMLSave("config.xml", (byte)'!');
            return true;
        }

        internal static void DexterAppConfig()
        {
            string appDataPath = GetApplicationDataPath();
            DexterFile.SetStoragePath(appDataPath);

            GFX.SetAppRefresh(0x28);
            GFX.HideMousePointer();

            bool english = IsEnglish();

            string titleA = english ? "Cultures: 8th Wonder of the World" : "Cultures: Das Achte Weltwunder";
            string titleB = english ? "Cultures: 8th Wonder of the World" : "Cultures: Das Achte Weltwunder";

            DexterOS.SetAppTitle(titleB, titleA);

            DexterDebug.SetDebugMode(0);
            DexterMemory.SetMaxMallocs(10000);
            DexterAudio.SetSoundMode(0x02, 0x10, 0x5622);
            DexterFile.SetFileBufferSize(10000);

            // Match cxx: set default install/exit functions here.
            SetInstallFunction(DefaultDexterAppInstall);
            SetExitFunction(DefaultDexterAppShutdown);
        }

        internal static void SetInstallFunction(BoolCallback callback)
        {
            DexterAppInstallCallBack = callback;
        }

        internal static void SetExitFunction(BoolCallback callback)
        {
            DexterAppExitCallBack = callback;
        }

        // --------------------------------------------------------------------
        // Helpers that were external in the cxx (stubs; wire to your OS layer as needed)
        // --------------------------------------------------------------------

        private static string GetApplicationDataPath()
        {
            // cxx: _get_application_data_path()
            // Prefer the per-user appdata directory; fallback to current directory.
            try
            {
                string? p = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!string.IsNullOrEmpty(p))
                {
                    return p;
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                return Environment.CurrentDirectory;
            }
            catch
            {
                return ".";
            }
        }

        private static bool IsEnglish()
        {
            // cxx: IsEnglish()
            // Keep it simple; wire to real language selection if available.
            return false;
        }

        private static bool DexterAppSetup()
        {
            // cxx: DexterAppSetup() is called unconditionally and must return bool.
            // If a callback is provided, use it; otherwise succeed.
            if (DexterAppSetupCallBack != null)
            {
                return DexterAppSetupCallBack();
            }

            return true;
        }

        // --------------------------------------------------------------------
        // Random table init (DexterApp::DexterApp)
        // --------------------------------------------------------------------

        private static void BuildRandomTable()
        {
            int iVar9 = 2;
            int iVar3 = 3;
            int iVar5 = 0x0c;
            int iVar6 = 0x3a;

            for (int idx = 0; idx < 0x61; idx++)
            {
                float fVar11 = 0.0f;
                int iVar7 = 0x18;
                float fVar12 = 0.5f;

                int iVar10 = iVar9;
                int iVar2 = iVar3;

                while (iVar7 != 0)
                {
                    iVar3 = iVar5;
                    iVar9 = iVar2;

                    iVar5 = (((iVar10 * iVar9) % 0xb3) * iVar3) % 0xb3;
                    iVar6 = (iVar6 * 0x35 + 1) % 0xa9;

                    short uVar1 = unchecked((short)(iVar5 * iVar6));
                    short uVar4 = unchecked((short)(uVar1 + 0x3f));
                    if (uVar1 >= 0)
                    {
                        uVar4 = uVar1;
                    }

                    short masked = unchecked((short)(uVar4 & unchecked((short)0xFFC0)));
                    short diff = unchecked((short)(uVar1 - masked));

                    if (diff > 0x1f)
                    {
                        fVar11 += fVar12;
                    }

                    fVar12 *= 0.5f;

                    iVar7--;
                    iVar10 = iVar9;
                    iVar2 = iVar3;
                }

                Random[idx] = fVar11;
            }

            // The constructor sets these globals too (overlap warnings in decompile).
            RandomC = 362436.0 / 16777216.0;
            RandomI97 = 0x60;
            RandomJ97 = 0x20;
        }

        // --------------------------------------------------------------------
        // Zone storage layout (0x14 bytes in cxx)
        // --------------------------------------------------------------------

        internal struct DexZone
        {
            // Matches 0x14 bytes: 8 + 8 + 1 + padding
            internal ulong A;
            internal ulong B;
            internal byte Active;
        }
    }
}