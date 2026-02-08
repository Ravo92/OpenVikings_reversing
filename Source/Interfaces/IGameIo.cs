namespace OpenVikings.Interfaces
{
    internal interface IGameIo
    {
        void CleanmapLoad(string mapPath, bool clearWorld, bool resetState, bool keepPlayer);
    }
}
