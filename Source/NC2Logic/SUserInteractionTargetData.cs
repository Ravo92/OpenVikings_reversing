using System.Runtime.InteropServices;

namespace OpenVikings.NC2Logic
{
    internal enum UserInteractionTargetType : int
    {
        None = 0,
        Human = 1,
        Animal = 2,
        Vehicle = 3,
        House = 4,
        Guide = 5
    }

    internal interface IUserInteractionTargetValidation
    {
        bool TryGetHumanSerial(int humanId, out int serial, out bool alive);
        bool TryGetAnimalSerial(int animalId, out int serial, out bool alive);

        bool TryGetVehicleSerial(int vehicleId, out int serial, out bool alive);
        bool TryGetHouseSerial(int houseId, out int serial, out bool alive);
        bool TryGetGuideSerial(int guideId, out int serial, out bool alive);
    }

    // 0x44 bytes, matches RE offsets exactly (17 * 4 bytes).
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct SUserInteractionTargetData
    {
        // +0x00
        internal int Type;

        // +0x04 (unknown/unused here, but must exist for layout)
        internal int Unknown04;

        // +0x08 / +0x0C
        internal int HumanId;
        internal int HumanSerial;

        // +0x10 / +0x14
        internal int AnimalId;
        internal int AnimalSerial;

        // +0x18 / +0x1C
        internal int VehicleId;
        internal int VehicleSerial;

        // +0x20 / +0x24 (unknown, but Tool_Init sets +0x20 = -1)
        internal int Unknown20;
        internal int Unknown24;

        // +0x28 / +0x2C
        internal int HouseId;
        internal int HouseSerial;

        // +0x30 / +0x34
        internal int GuideId;
        internal int GuideSerial;

        // +0x38 / +0x3C (unknown)
        internal int Unknown38;
        internal int Unknown3C;

        // +0x40 (Tool_Init sets = -1)
        internal int Unknown40;

        internal void Tool_Init()
        {
            // DexterMemory::MemorySet(this,'\0',0x44);
            // then explicit fields set as seen in RE.
            Type = 0;
            Unknown04 = 0;

            HumanId = -1;
            HumanSerial = 0;

            AnimalId = -1;
            AnimalSerial = 0;

            VehicleId = -1;
            VehicleSerial = 0;

            Unknown20 = -1;
            Unknown24 = 0;

            HouseId = -1;
            HouseSerial = 0;

            GuideId = -1;
            GuideSerial = 0;

            Unknown38 = 0;
            Unknown3C = 0;

            Unknown40 = -1;
        }

        internal ulong Tool_Validate(IUserInteractionTargetValidation validation)
        {
            // Returns 1 if invalid was detected and the struct was reset,
            // returns 0 otherwise. This matches the RE function semantics.

            int type = Type;

            if (type == (int)UserInteractionTargetType.Human)
            {
                if (HumanId == -1)
                {
                    return 0;
                }

                int serial;
                bool alive;
                if (!validation.TryGetHumanSerial(HumanId, out serial, out alive) || !alive || serial != HumanSerial)
                {
                    Tool_ResetToEmptyInvalid();
                    return 1;
                }
            }

            if (type == (int)UserInteractionTargetType.Animal)
            {
                if (AnimalId == -1)
                {
                    return 0;
                }

                int serial;
                bool alive;
                if (!validation.TryGetAnimalSerial(AnimalId, out serial, out alive) || !alive || serial != AnimalSerial)
                {
                    Tool_ResetToEmptyInvalid();
                    return 1;
                }
            }

            if (type == (int)UserInteractionTargetType.Guide)
            {
                // RE: only validate if GuideId != -1
                if (GuideId != -1)
                {
                    int serial;
                    bool alive;
                    if (!validation.TryGetGuideSerial(GuideId, out serial, out alive) || !alive || serial != GuideSerial)
                    {
                        Tool_ResetToEmptyInvalid();
                        return 1;
                    }
                }
            }
            else if (type == (int)UserInteractionTargetType.House)
            {
                // RE: only validate if HouseId != -1
                if (HouseId != -1)
                {
                    int serial;
                    bool alive;
                    if (!validation.TryGetHouseSerial(HouseId, out serial, out alive) || !alive || serial != HouseSerial)
                    {
                        Tool_ResetToEmptyInvalid();
                        return 1;
                    }
                }
            }
            else if (type == (int)UserInteractionTargetType.Vehicle)
            {
                // RE: only validate if VehicleId != -1
                if (VehicleId != -1)
                {
                    int serial;
                    bool alive;
                    if (!validation.TryGetVehicleSerial(VehicleId, out serial, out alive) || !alive || serial != VehicleSerial)
                    {
                        Tool_ResetToEmptyInvalid();
                        return 1;
                    }
                }
            }

            return 0;
        }

        internal bool Tool_IsHouse(int houseId, IUserInteractionTargetValidation validation)
        {
            if (Type == (int)UserInteractionTargetType.House && HouseId == houseId)
            {
                int serial;
                bool alive;

                if (validation.TryGetHouseSerial(houseId, out serial, out alive) && alive)
                {
                    return HouseSerial == serial;
                }
            }

            return false;
        }

        internal bool Tool_IsHuman(int humanId, IUserInteractionTargetValidation validation)
        {
            if (Type == (int)UserInteractionTargetType.Human && HumanId == humanId)
            {
                int serial;
                bool alive;

                if (validation.TryGetHumanSerial(humanId, out serial, out alive) && alive)
                {
                    return HumanSerial == serial;
                }
            }

            return false;
        }

        internal bool Tool_IsAnimal(int animalId, IUserInteractionTargetValidation validation)
        {
            if (Type == (int)UserInteractionTargetType.Animal && AnimalId == animalId)
            {
                int serial;
                bool alive;

                if (validation.TryGetAnimalSerial(animalId, out serial, out alive) && alive)
                {
                    return AnimalSerial == serial;
                }
            }

            return false;
        }

        internal bool Tool_IsVehicle(int vehicleId, IUserInteractionTargetValidation validation)
        {
            if (Type == (int)UserInteractionTargetType.Vehicle && VehicleId == vehicleId)
            {
                int serial;
                bool alive;

                if (validation.TryGetVehicleSerial(vehicleId, out serial, out alive) && alive)
                {
                    return VehicleSerial == serial;
                }
            }

            return false;
        }

        internal bool Tool_IsGuide(int guideId, IUserInteractionTargetValidation validation)
        {
            if (Type == (int)UserInteractionTargetType.Guide && GuideId == guideId)
            {
                int serial;
                bool alive;

                if (validation.TryGetGuideSerial(guideId, out serial, out alive) && alive)
                {
                    return GuideSerial == serial;
                }
            }

            return false;
        }

        private void Tool_ResetToEmptyInvalid()
        {
            // This matches the exact reset block in RE (memset + specific -1 fields).
            Tool_Init();
        }
    }
}