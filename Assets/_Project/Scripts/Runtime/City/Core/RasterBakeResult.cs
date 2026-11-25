using System;

namespace ZombieCity.City.Core
{
    [Serializable]
    public class BuildingRasterInfo
    {
        public ushort id;
        public long osmId;
        public float heightM;
        public string buildingType;
        public int minI;
        public int minJ;
        public int maxI;
        public int maxJ;
        public int cellCount;
    }

    [Serializable]
    public class RasterStats
    {
        public int[] cellsByBit = new int[16];
        public int buildingConflicts;
        public int buildingsWithZeroCells;
        public int cellsUnderBridges;
        public int objectsOutsideGrid;
        public int cellsRoadAndCrosswalk;
        public int waterClearedByLinear;
        public int buildingCount;
    }

    /// <summary>
    /// Output of <see cref="CityRasterizer.Bake"/> (ADR-002 §5–§6).
    /// </summary>
    public sealed class RasterBakeResult
    {
        public CityGrid grid;
        public ushort[] flags = Array.Empty<ushort>();
        public ushort[] buildingId = Array.Empty<ushort>();
        public BuildingRasterInfo[] buildings = Array.Empty<BuildingRasterInfo>();
        public RasterStats stats = new RasterStats();
        public ulong modelDataHash;
        public ulong configHash;
        public ulong dataHash;
    }
}
