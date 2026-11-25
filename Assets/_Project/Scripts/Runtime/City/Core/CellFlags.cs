using System;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// Per-cell bit flags (ADR-001 §5.1 / ADR-002).
    /// </summary>
    [Flags]
    public enum CellFlags : ushort
    {
        None = 0,
        Road = 1 << 0,
        Sidewalk = 1 << 1,
        Crosswalk = 1 << 2,
        Building = 1 << 3,
        Water = 1 << 4,
        Barrier = 1 << 5,
        Rail = 1 << 6,
        Green = 1 << 7,
        Bridge = 1 << 8,
        SidewalkGenerated = 1 << 9,
        BuildingPassage = 1 << 10,
        Cycleway = 1 << 11,
        Steps = 1 << 12,
        Gate = 1 << 13,
        Shared = 1 << 14
    }
}
