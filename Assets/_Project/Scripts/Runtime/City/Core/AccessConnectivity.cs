using System;

namespace ZombieCity.City.Core
{
    /// <summary>Stub for 3C-tests (red). Replaced in [TASK-003/3C].</summary>
    public static class AccessConnectivity
    {
        public enum Mask : byte { Pedestrian = 0, Zombie = 1 }

        public struct ComponentInfo
        {
            public int id;
            public int cellCount;
            public double areaM2;
            public double centerX;
            public double centerZ;
            public double shareOfPassable;
        }

        public sealed class Report
        {
            public int componentCount;
            public ComponentInfo[] components = Array.Empty<ComponentInfo>();
            public int islandCount;
            public ComponentInfo[] islands = Array.Empty<ComponentInfo>();
            public int[] cellComponentId = Array.Empty<int>();
            public int pedestrianComponentsWithoutCrosswalk;
            public int pedestrianComponentsWithCrosswalk;
            public int crosswalkCellsConnecting;
            public int crosswalkCellCount;
            public int roadRailCellCount;
            public Vec2d[] roadRailCenters = Array.Empty<Vec2d>();
            public int obstacleCells;
            public int roadwayCells;
            public int pedestrianCells;
        }

        public static Report Analyze(CityGrid grid, byte[] access, ushort[] flags, int islandMinCells, Mask mask)
        {
            return new Report();
        }

        public static bool CanStep(byte[] access, int w, int h, int i0, int j0, int i1, int j1) => false;
    }
}
