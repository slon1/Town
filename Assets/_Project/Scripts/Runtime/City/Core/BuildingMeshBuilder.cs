using System;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// Extruded building mesh (ADR-004 §4). Positions are tightly packed xyz meters.
    /// Colors are RGBA32 little-endian. Indices are triangle lists.
    /// </summary>
    public sealed class BuildingMeshData
    {
        public float[] positions = Array.Empty<float>();
        public int[] indices = Array.Empty<int>();
        public uint[] colors = Array.Empty<uint>();
        public int buildingCount;
        public int skippedBuildings;
        public int skippedRings;
        public int vertexCount;
        public int triangleCount;
    }

    /// <summary>Stub for the red 6D commit. Replaced by the real extrusion.</summary>
    public static class BuildingMeshBuilder
    {
        public static BuildingMeshData Build(CityModel model)
        {
            return new BuildingMeshData();
        }
    }
}
