using OpenVikings.Dexter;
using OpenVikings.NC2GuiToolsBase.enums;
using System.Globalization;

namespace OpenVikings.NC2GuiToolsBase
{
    internal static class StringTool
    {
        // Shared fallback buffer in the snippet (DAT_1003a4560).
        // In managed code: just build a string.
        internal static string GetStringPtr(TStringArrayIds arrayId, uint index)
        {
            CGuiBaseStringsManager? mgr = CGuiBaseStringsManager.sTheObjectPtr;
            if (mgr != null)
            {
                return mgr.GetStringPtr(arrayId, index);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}:{1}", (int)arrayId, index);
        }

        internal static string GetMainMenuStringPtr(uint index) => GetStringPtr(TStringArrayIds.MainMenu, index);
        internal static string GetGameGuiMainStringPtr(uint index) => GetStringPtr(TStringArrayIds.GameGuiMain, index);
        internal static string GetMiscMenuStringPtr(uint index) => GetStringPtr(TStringArrayIds.MiscMenu, index);
        internal static string GetGameGuiHumanListWindowStringPtr(uint index) => GetStringPtr(TStringArrayIds.GameGuiHumanListWindow, index);
        internal static string GetGameGuiMiscWindowStringPtr(uint index) => GetStringPtr(TStringArrayIds.GameGuiMiscWindow, index);
        internal static string GetGameGuiMiscLogicStringPtr(uint index) => GetStringPtr(TStringArrayIds.GameGuiMiscLogic, index);
        internal static string GetGameGuiMessagesStringPtr(uint index) => GetStringPtr(TStringArrayIds.GameGuiMessages, index);
        internal static string GetGameGuiHumanSelectedWindowStringPtr(uint index) => GetStringPtr(TStringArrayIds.GameGuiHumanSelectedWindow, index);
        internal static string GetGameGuiHouseSelectedWindowStringPtr(uint index) => GetStringPtr(TStringArrayIds.GameGuiHouseSelectedWindow, index);
        internal static string GetGameGuiVehicleSelectedWindowStringPtr(uint index) => GetStringPtr(TStringArrayIds.GameGuiVehicleSelectedWindow, index);
        internal static string GetUpdate103StringPtr(uint index) => GetStringPtr(TStringArrayIds.Update103, index);
        internal static string GetOdin001StringPtr(uint index) => GetStringPtr(TStringArrayIds.Odin001, index);
        internal static string GetWonders001StringPtr(uint index) => GetStringPtr(TStringArrayIds.Wonders001, index);

        internal static string GetLogicUserCommandsForHumanTypeNamePtr(NC2Logic.TUserHumanCommandTypes type)
        {
            uint stringId = MapHumanCommandToStringId(type);
            string s = GetStringPtr(TStringArrayIds.GameGuiMiscLogic, stringId);
            return s;
        }

        internal static string GetLogicUserCommandsForHouseTypeNamePtr(NC2Logic.TUserHouseCommandTypes type)
        {
            uint stringId = MapHouseCommandToStringId(type);
            string s = GetStringPtr(TStringArrayIds.GameGuiMiscLogic, stringId);
            return s;
        }

        internal static string GetLogicUserCommandsForVehicleTypeNamePtr(NC2Logic.TUserVehicleCommandTypes type)
        {
            uint stringId = MapVehicleCommandToStringId(type);
            string s = GetStringPtr(TStringArrayIds.GameGuiMiscLogic, stringId);
            return s;
        }

        internal static void GetLogicSpecialItemNamePtr(NC2Logic.TSpecialItemTypes type, int param2, out string name)
        {
            name = "<UNKNOWN>";

            if (type == NC2Logic.TSpecialItemTypes.None)
            {
                return;
            }

            if (type == NC2Logic.TSpecialItemTypes.House)
            {
                string fmt = GetStringPtr(TStringArrayIds.GameGuiMiscLogic, 0xB6);
                string houseName = NC2Logic.StringAccess.GetHouseNamePtr(param2, 1);
                name = DexterString.StringPrintInline(fmt, houseName);
                return;
            }

            if (type == NC2Logic.TSpecialItemTypes.House2)
            {
                string fmt = GetStringPtr(TStringArrayIds.GameGuiMiscLogic, 0xB7);
                string houseName = NC2Logic.StringAccess.GetHouseNamePtr(param2, 1);
                name = DexterString.StringPrintInline(fmt, houseName);
                return;
            }

            if (type == NC2Logic.TSpecialItemTypes.House3)
            {
                string fmt = GetStringPtr(TStringArrayIds.GameGuiMiscLogic, 0xB8);
                string houseName = NC2Logic.StringAccess.GetHouseNamePtr(param2, 1);
                name = DexterString.StringPrintInline(fmt, houseName);
                return;
            }

            if (type == NC2Logic.TSpecialItemTypes.Job)
            {
                string fmt = GetStringPtr(TStringArrayIds.GameGuiMiscLogic, 0xB9);
                string jobName = NC2Logic.StringAccess.GetJobNamePtr(param2, 1);
                name = DexterString.StringPrintInline(fmt, jobName);
                return;
            }

            if (type == NC2Logic.TSpecialItemTypes.Good)
            {
                string fmt = GetStringPtr(TStringArrayIds.GameGuiMiscLogic, 0xBA);
                string goodName = NC2Logic.StringAccess.GetGoodNamePtr(param2, 1);
                name = DexterString.StringPrintInline(fmt, goodName);
                return;
            }

            if (type == NC2Logic.TSpecialItemTypes.Simple1)
            {
                name = GetStringPtr(TStringArrayIds.GameGuiMiscLogic, 0xB4);
                return;
            }

            if (type == NC2Logic.TSpecialItemTypes.Simple2)
            {
                name = GetStringPtr(TStringArrayIds.GameGuiMiscLogic, 0xB5);
                return;
            }
        }

        internal static string GetLogicDiplomacyStateNamePtr(NC2Logic.TDiplomacyState state)
        {
            if (state == NC2Logic.TDiplomacyState.None)
            {
                return "<NONE>";
            }

            if (state == NC2Logic.TDiplomacyState.Self)
            {
                return "<SELF>";
            }

            if (state == NC2Logic.TDiplomacyState.Allied)
            {
                return GetStringPtr(TStringArrayIds.GameGuiMiscLogic, 200);
            }

            if (state == NC2Logic.TDiplomacyState.Neutral)
            {
                return GetStringPtr(TStringArrayIds.GameGuiMiscLogic, 0xC9);
            }

            if (state == NC2Logic.TDiplomacyState.Enemy)
            {
                return GetStringPtr(TStringArrayIds.GameGuiMiscLogic, 0xCA);
            }

            return "<UNKNOWN>";
        }

        internal static string GetLogicGoodEquipmentTypeNamePtr(NC2Logic.TGoodEquipmentType type)
        {
            uint id;
            if (type == NC2Logic.TGoodEquipmentType.Type0) id = 0xDC;
            else if (type == NC2Logic.TGoodEquipmentType.Type1) id = 0xDD;
            else if (type == NC2Logic.TGoodEquipmentType.Type2) id = 0xDE;
            else if (type == NC2Logic.TGoodEquipmentType.Type3) id = 0xDF;
            else if (type == NC2Logic.TGoodEquipmentType.Type4) id = 0xE0;
            else if (type == NC2Logic.TGoodEquipmentType.Type5) id = 0xE1;
            else if (type == NC2Logic.TGoodEquipmentType.Type6) id = 0xE2;
            else return "<UNKNOWN>";

            return GetStringPtr(TStringArrayIds.GameGuiMiscLogic, id);
        }

        private static uint MapHumanCommandToStringId(NC2Logic.TUserHumanCommandTypes type)
        {
            // Direct transcription of the big switch table: command -> string-id in array 5.
            // The snippet also kept uVar3 for reporting; in managed code we just use the id directly.
            int t = (int)type;

            if (t == 0) return 0;
            if (t == 2) return 1;
            if (t == 3) return 4;
            if (t == 4) return 6;
            if (t == 5) return 8;
            if (t == 6) return 10;
            if (t == 7) return 0x0E;
            if (t == 8) return 0x0F;
            if (t == 9) return 0x10;
            if (t == 10) return 0x11;
            if (t == 0x0B) return 0x12;
            if (t == 0x0C) return 0x1F;
            if (t == 0x0D) return 0x20;

            if (t == 0x10) return 0x17;
            if (t == 0x11) return 0x18;
            if (t == 0x12) return 0x19;
            if (t == 0x13) return 0x1A;
            if (t == 0x14) return 0x1B;
            if (t == 0x15) return 0x1C;
            if (t == 0x16) return 0x1D;
            if (t == 0x17) return 0x1E;

            if (t == 0x18) return 0x13;
            if (t == 0x19) return 0x14;
            if (t == 0x1A) return 0x15;
            if (t == 0x1B) return 0x16;

            if (t == 0x1C) return 0x21;
            if (t == 0x1D) return 0x22;
            if (t == 0x1E) return 0x23;
            if (t == 0x1F) return 0x24;
            if (t == 0x20) return 0x25;

            if (t == 0x23) return 0x26;
            if (t == 0x24) return 0x27;
            if (t == 0x25) return 0x28;
            if (t == 0x26) return 0x29;
            if (t == 0x27) return 0x2A;
            if (t == 0x28) return 0x2B;
            if (t == 0x29) return 0x2C;
            if (t == 0x2A) return 0x2D;
            if (t == 0x2B) return 0x2E;
            if (t == 0x2C) return 0x2F;
            if (t == 0x2D) return 0x30;

            if (t == 0x30) return 2;
            if (t == 0x31) return 3;
            if (t == 0x32) return 5;
            if (t == 0x33) return 7;
            if (t == 0x34) return 9;
            if (t == 0x35) return 0x0B;
            if (t == 0x36) return 0x0D;
            if (t == 0x37) return 0x0C;

            return 0x31; // default from snippet
        }

        private static uint MapHouseCommandToStringId(NC2Logic.TUserHouseCommandTypes type)
        {
            int t = (int)type;

            if (t == 0) return 100;
            if (t == 1) return 0x65;
            if (t == 2) return 0x66;
            if (t == 3) return 0x67;
            if (t == 4) return 0x68;
            if (t == 5) return 0x69;
            if (t == 6) return 0x6A;
            if (t == 7) return 0x6B;
            if (t == 8) return 0x6C;
            if (t == 9) return 0x6D;
            if (t == 10) return 0x6E;
            if (t == 0x0B) return 0x6F;
            if (t == 0x0C) return 0x70;
            if (t == 0x0D) return 0x71;
            if (t == 0x0E) return 0x72;
            if (t == 0x0F) return 0x73;

            return 0x74;
        }

        private static uint MapVehicleCommandToStringId(NC2Logic.TUserVehicleCommandTypes type)
        {
            int t = (int)type;

            if (t == 0) return 0x8C;
            if (t == 1) return 0x8D;
            if (t == 2) return 0x8E;
            if (t == 3) return 0x8F;
            if (t == 4) return 0x90;
            if (t == 5) return 0x91;
            if (t == 6) return 0x92;
            if (t == 7) return 0x93;
            if (t == 8) return 0x94;
            if (t == 9) return 0x95;
            if (t == 10) return 0x96;
            if (t == 0x0B) return 0x97;
            if (t == 0x0C) return 0x98;
            if (t == 0x0D) return 0x99;
            if (t == 0x0E) return 0x9A;

            return 0x9B;
        }
    }
}