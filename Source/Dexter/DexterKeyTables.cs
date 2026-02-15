using Silk.NET.SDL;

namespace OpenVikings.Dexter
{
    internal static class DexterKeyTables
    {
        // 42 entries => 42 * 3 = 126 internal key slots (0..125) match MaxKeyCodes (0x7E).
        internal static void CreateDefault(out uint[] osKeyTranslateA, out uint[] osKeyTranslateB, out uint[] osKeyTranslateC)
        {
            uint[] a = new uint[42];
            uint[] b = new uint[42];
            uint[] c = new uint[42];

            // Default everything to 0 so it doesn't accidentally match valid SDL keycodes too often.
            // Fill only a few common ones as an initial mapping.
            SetColumnA(a, b, c, 0, KeyCode.KA);
            SetColumnA(a, b, c, 1, KeyCode.KB);
            SetColumnA(a, b, c, 2, KeyCode.KC);
            SetColumnA(a, b, c, 3, KeyCode.KD);
            SetColumnA(a, b, c, 4, KeyCode.KE);
            SetColumnA(a, b, c, 5, KeyCode.KF);
            SetColumnA(a, b, c, 6, KeyCode.KG);
            SetColumnA(a, b, c, 7, KeyCode.KH);
            SetColumnA(a, b, c, 8, KeyCode.KI);
            SetColumnA(a, b, c, 9, KeyCode.KJ);
            SetColumnA(a, b, c, 10, KeyCode.KK);
            SetColumnA(a, b, c, 11, KeyCode.KL);
            SetColumnA(a, b, c, 12, KeyCode.KM);
            SetColumnA(a, b, c, 13, KeyCode.KN);
            SetColumnA(a, b, c, 14, KeyCode.KO);
            SetColumnA(a, b, c, 15, KeyCode.KP);
            SetColumnA(a, b, c, 16, KeyCode.KQ);
            SetColumnA(a, b, c, 17, KeyCode.KR);
            SetColumnA(a, b, c, 18, KeyCode.KS);
            SetColumnA(a, b, c, 19, KeyCode.KT);
            SetColumnA(a, b, c, 20, KeyCode.KU);
            SetColumnA(a, b, c, 21, KeyCode.KV);
            SetColumnA(a, b, c, 22, KeyCode.KW);
            SetColumnA(a, b, c, 23, KeyCode.KX);
            SetColumnA(a, b, c, 24, KeyCode.KY);
            SetColumnA(a, b, c, 25, KeyCode.KZ);

            // Space / Enter / Escape as examples.
            SetColumnA(a, b, c, 26, KeyCode.KSpace);
            SetColumnA(a, b, c, 27, KeyCode.KReturn);
            SetColumnA(a, b, c, 28, KeyCode.KEscape);

            osKeyTranslateA = a;
            osKeyTranslateB = b;
            osKeyTranslateC = c;
        }

        // Helper to fill one column in all three translation tables.
        // The same SDL keycode is written into all 3 tables (A/B/C) by default.
        private static void SetColumnA(uint[] a, uint[] b, uint[] c, int index, KeyCode key)
        {
            if (index < 0 || index >= a.Length)
            {
                return;
            }

            uint value = (uint)key;

            a[index] = value;
            b[index] = value;
            c[index] = value;
        }
    }
}