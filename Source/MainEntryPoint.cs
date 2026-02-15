using OpenVikings.Dexter;
using OpenVikings.SDL2;
using Silk.NET.SDL;

namespace OpenVikings
{

    class Program
    {
        [STAThread]
        internal static int Main(string[] args)
        {
            DexterDebug.OpenDebugFile();
            DexterDebug.SetDebugMode(0);

            using SDL2.SdlContext sdl = SdlBootstrap.Create("Weltwunder", 800, 600);

            try
            {
                sdl.LogSetAllPriority(LogPriority.LogPriorityInfo);

                DexterGFXState gfxState = new(renderWidth: 800, renderHeight: 600, windowWidth: 800, windowHeight: 600);
                DexterGFXScreen gfxScreen = new(gfxState, renderBitDepth: 8, screenLocked: false);

                DexterKeyTables.CreateDefault(out uint[] osKeyTranslateA, out uint[] osKeyTranslateB, out uint[] osKeyTranslateC);

                OSEnvironment osEnvironment = sdl.Environment;

                DexterOS dexterOs = new(osKeyTranslateA, osKeyTranslateB, osKeyTranslateC, osEnvironment, gfxState, gfxScreen);
                dexterOs.SetCommandLineArgs(args.Length, args);

                DexterGFX dexterGfx = new(1024);

                SignalHooks.Install(sdl.Api);

                DexterApp app = new(
                    dexterOs,
                    dexterGfx,
                    osEnvironment,
                    mainCallback: static () => { },
                    installCallback: null,
                    exitCallback: null,
                    setupCallback: null,
                    getCallbackTimeMs: null,
                    onFpsSample: null,
                    osUpdateCallback: dexterOs.OSUpdate);

                bool ok = app.Init();
                if (ok)
                {
                    sdl.SetEventEnabled(0x700u, false);
                    sdl.SetEventEnabled(0x701u, false);
                    sdl.SetEventEnabled(0x702u, false);

                    app.DexterOSMainLoop();
                }

                return 0;
            }
            finally
            {
                DexterDebug.CloseDebugFile();
            }
        }
    }
}