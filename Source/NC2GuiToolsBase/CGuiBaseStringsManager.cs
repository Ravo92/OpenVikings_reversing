using OpenVikings.NC2GuiToolsBase.enums;
using OpenVikings.NC2Logic;
using OpenVikings.NXBasics;
using System.Globalization;

namespace OpenVikings.NC2GuiToolsBase
{
    internal sealed class CGuiBaseStringsManager : IDisposable
    {
        internal static CGuiBaseStringsManager? sTheObjectPtr;

        // Original: mStaticVars was 0xD0 bytes = 13 pointers -> 13 string arrays.
        internal readonly CStringArray?[] mStaticVars;

        // Original: CGuiBaseStringsManager()::ls_Array referenced by index.
        // Provide these keys/paths in the same order as the original binary used.
        internal static readonly string[] ls_Array =
        {
            "mainmenu",
            "gameguimain",
            "miscmenu",
            "gameguihumanlistwindow",
            "gameguimiscwindow",
            "gameguimisclogic",
            "gameguimessages",
            "gameguihumanselectedwindow",
            "gameguihouseselectedwindow",
            "gameguivehicleselectedwindow",
            "update103",
            "odin001",
            "wonders001"
        };

        internal CGuiBaseStringsManager()
        {
            sTheObjectPtr = this;
            mStaticVars = new CStringArray?[13];

            for (int i = 0; i < mStaticVars.Length; i += 1)
            {
                bool _ = LanguageTool.GetFilename(ls_Array[i], out string filename, true);
                CStringArray? arr = StringIO.CreateOutOfIniFile(filename);
                mStaticVars[i] = arr;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < mStaticVars.Length; i += 1)
            {
                CStringArray? arr = mStaticVars[i];
                arr?.Dispose();

                mStaticVars[i] = null;
            }

            if (sTheObjectPtr == this)
            {
                sTheObjectPtr = null;
            }
        }

        internal string GetStringPtr(TStringArrayIds arrayId, uint index)
        {
            uint id = (uint)arrayId;

            if (id < (uint)mStaticVars.Length)
            {
                CStringArray? arr = mStaticVars[id];
                if (arr != null)
                {
                    string? s = arr.GetString(index);
                    if (s != null)
                    {
                        return s;
                    }
                }
            }

            return string.Format(CultureInfo.InvariantCulture, "<{0}:{1}:NOT DEFINED!>", id, index);
        }
    }
}