using System;

namespace ZombieCity.City.Core
{
    /// <summary>Base ground layer (ADR-004 §6). Defined in Core so the composer stays free of UnityEngine.</summary>
    public enum CityLayer
    {
        Access = 0,
        Flags = 1,
        BuildingId = 2
    }

    /// <summary>Optional cell overlays. Bits are a set; draw priority is ADR-003 §5.1, not the bit order.</summary>
    [Flags]
    public enum CityOverlay
    {
        None = 0,
        RailOnRoad = 1 << 0,
        UnmarkedCrossing = 1 << 1,
        Crosswalk = 1 << 2,
        GatePassage = 1 << 3,
        Bridge = 1 << 4
    }
}
