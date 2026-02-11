using System.Globalization;

namespace OpenVikings
{
    internal static class OSGeneric
    {
        internal static long TimeMilliseconds()
        {
            // Monotonic enough for game timing; wraps after ~292 million years using TickCount64.
            return Environment.TickCount64;
        }

        internal static void Pause(uint milliseconds)
        {
            if (milliseconds == 0)
            {
                Thread.Yield();
                return;
            }

            if (milliseconds > int.MaxValue)
            {
                milliseconds = int.MaxValue;
            }

            Thread.Sleep((int)milliseconds);
        }

        internal static void RelaxThread()
        {
            // Similar to "yield" behavior; avoids burning a core in busy-wait loops.
            if (!Thread.Yield())
            {
                Thread.Sleep(0);
            }
        }

        internal static double SystemSquareRoot(double value)
        {
            return Math.Sqrt(value);
        }

        internal static void FailRequester(string format, params object[] args)
        {
            string message = string.Format(CultureInfo.InvariantCulture, format, args);
            throw new InvalidOperationException(message);
        }

        internal static bool SystemCreateDebugConsole()
        {
            // On Windows a real console attach could be implemented.
            // For cross-platform SDL2 projects, logging to stderr is usually enough.
            return true;
        }

        internal static void SystemDebugConsoleOutput(string message)
        {
            if (message == null)
            {
                return;
            }

            Console.Error.WriteLine(message);
        }

        internal static bool SystemOpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            try
            {
                // UseShellExecute is required on modern .NET to open URLs cross-platform.
                System.Diagnostics.ProcessStartInfo psi = new()
                {
                    FileName = url,
                    UseShellExecute = true
                };

                System.Diagnostics.Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool SystemFileDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
