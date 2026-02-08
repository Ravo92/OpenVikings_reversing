using OpenVikings.NC2Logic.Enum;
using System.Runtime.InteropServices;

namespace OpenVikings.NC2Logic
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct SUserCommandsForVehicleHandler
    {
        internal uint Count;

        internal int Command0;
        internal int Command1;
        internal int Command2;
        internal int Command3;
        internal int Command4;
        internal int Command5;
        internal int Command6;
        internal int Command7;
        internal int Command8;
        internal int Command9;

        internal void Init()
        {
            // RE sets entire struct to zero.
            this = default;
        }

        internal void AddCommand(int commandType)
        {
            uint index = Count;
            if (index >= 10u)
            {
                return;
            }

            SetCommandAt((int)index, commandType);
            Count = index + 1u;
        }

        internal bool IsCommandAllowed(int commandType)
        {
            uint count = Count;
            if (count < 1u)
            {
                return false;
            }

            if (GetCommandAt(0) == commandType)
            {
                return true;
            }

            uint index = 1u;
            while (index < count)
            {
                if (GetCommandAt((int)index) == commandType)
                {
                    return true;
                }

                index = index + 1u;
            }

            return false;
        }

        internal void AddCommand(TUserVehicleCommandTypes commandType)
        {
            AddCommand((int)commandType);
        }

        internal bool IsCommandAllowed(TUserVehicleCommandTypes commandType)
        {
            return IsCommandAllowed((int)commandType);
        }

        private int GetCommandAt(int index)
        {
            switch (index)
            {
                case 0: return Command0;
                case 1: return Command1;
                case 2: return Command2;
                case 3: return Command3;
                case 4: return Command4;
                case 5: return Command5;
                case 6: return Command6;
                case 7: return Command7;
                case 8: return Command8;
                case 9: return Command9;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private void SetCommandAt(int index, int value)
        {
            switch (index)
            {
                case 0: Command0 = value; break;
                case 1: Command1 = value; break;
                case 2: Command2 = value; break;
                case 3: Command3 = value; break;
                case 4: Command4 = value; break;
                case 5: Command5 = value; break;
                case 6: Command6 = value; break;
                case 7: Command7 = value; break;
                case 8: Command8 = value; break;
                case 9: Command9 = value; break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
}