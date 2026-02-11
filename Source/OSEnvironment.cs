using System.Diagnostics;

namespace OpenVikings
{
    internal interface ISdlApi
    {
        void SetWindowTitle(nint windowHandle, string title);
        uint GetTicks();
        void Delay(uint milliseconds);
    }

    internal sealed class OSEnvironment
    {
        private readonly ISdlApi _sdl;
        private readonly nint _sdlWindow;

        // Equivalent to DexterOS::AppPath in the original code.
        // Should be an absolute base path (with trailing separator optional).
        internal static string AppPath { get; set; } = string.Empty;

        internal OSEnvironment(ISdlApi sdl, nint sdlWindow)
        {
            _sdl = sdl;
            _sdlWindow = sdlWindow;
        }

        // OSEnvironment::SetWindowTitle(char const*)
        internal void SetWindowTitle(string title)
        {
            // The decompiled pseudo-code missed the title parameter in the SDL call.
            // The intended call is SDL_SetWindowTitle(window, title).
            _sdl.SetWindowTitle(_sdlWindow, title);
        }

        // OSEnvironment::Time()
        internal uint Time()
        {
            // SDL_GetTicks returns milliseconds since SDL initialization.
            return _sdl.GetTicks();
        }

        // OSEnvironment::Pause(unsigned int)
        internal void Pause(uint milliseconds)
        {
            _sdl.Delay(milliseconds);
        }

        // OSEnvironment::MakeDir(char const*)
        internal static bool MakeDir(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // OSEnvironment::ThreadStart(unsigned short)
        internal long ThreadStart(ushort threadId)
        {
            // Decompiled returns 0xFFFFFFFFFFFFFFFF (-1) unconditionally -> "not implemented / failed".
            // Preserved as-is until real threading behavior is known.
            _ = threadId;
            return -1;
        }

        // OSEnvironment::ThreadEnd(unsigned short)
        internal void ThreadEnd(ushort threadId)
        {
            // Decompiled is a no-op.
            _ = threadId;
        }

        // OSEnvironment::ModifyPath(char*)
        internal static string ModifyPath(string path)
        {
            // Decompiled:
            // if ((*param_1 & 0xfeU) == 0x2e) return;  // first char '.' OR '/' (0x2e '.'; 0x2f '/' maps to 0x2e with &0xfe)
            // StringInsert(param_1, &AppPath); // insert AppPath at the beginning

            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            char first = path[0];
            if (first == '.' || first == '/')
            {
                return path;
            }

            if (string.IsNullOrEmpty(AppPath))
            {
                return path;
            }

            // "Insert AppPath at the beginning" semantics (not Path.Combine).
            // If AppPath should behave like a directory prefix, it can be normalized elsewhere.
            return string.Concat(AppPath, path);
        }

        // OSEnvironment::SystemOpenURL(char*)
        internal static bool SystemOpenUrl(string url)
        {
            // Decompiled returns 1 unconditionally -> "success".
            // In managed code, attempt to open the URL and report success/failure.
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = url,
                    UseShellExecute = true
                };

                Process? process = Process.Start(startInfo);
                process?.Dispose();

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}