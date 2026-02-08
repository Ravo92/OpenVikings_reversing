namespace OpenVikings.NC2GuiToolsBase
{
    internal static class StringTool
    {
        internal static int GetLogicUserCommandsForVehicleTypeNameStringId(int commandType)
        {
            return commandType switch
            {
                0 => 0x8C,
                1 => 0x8D,
                2 => 0x8E,
                3 => 0x8F,
                4 => 0x90,
                5 => 0x91,
                6 => 0x92,
                7 => 0x93,
                8 => 0x94,
                9 => 0x95,
                10 => 0x96,
                11 => 0x97,
                12 => 0x98,
                13 => 0x99,
                14 => 0x9A,
                _ => 0x9B,
            };
        }
    }
}