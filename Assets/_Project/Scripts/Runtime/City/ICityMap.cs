using ZombieCity.City.Core;

namespace ZombieCity.City
{
    /// <summary>
    /// Runtime access to baked city rasters (ADR-001 §6). Extenject registration — P4.
    /// </summary>
    public interface ICityMap
    {
        CityGrid Grid { get; }
        CellFlags GetFlags(int i, int j);
        ushort GetBuildingId(int i, int j);
        CityAccess GetAccess(int i, int j);
    }
}
