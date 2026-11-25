using System;
using System.Collections.Generic;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// 8-connected components on access with corner-cut ban (ADR-003 §4).
    /// </summary>
    public static class AccessConnectivity
    {
        public enum Mask : byte
        {
            Pedestrian = 0,
            Zombie = 1
        }

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

        static readonly int[] Di = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] Dj = { 0, 0, 1, -1, 1, -1, 1, -1 };

        public static Report Analyze(
            CityGrid grid,
            byte[] access,
            ushort[] flags,
            int islandMinCells,
            Mask mask)
        {
            if (access == null)
            {
                throw new ArgumentNullException(nameof(access));
            }

            if (access.Length < grid.CellCount)
            {
                throw new ArgumentException("access length < grid.CellCount");
            }

            if (islandMinCells < 1)
            {
                islandMinCells = 1;
            }

            var report = new Report();
            CountClasses(access, grid.CellCount, report);

            bool[] passable = BuildPassable(access, grid.CellCount, mask, excludeCrosswalk: false, flags);
            int[] labels = LabelComponents(grid, access, passable, out List<RawComponent> raw);
            AssignSortedComponents(grid, raw, labels, islandMinCells, report);

            if (mask == Mask.Pedestrian)
            {
                bool[] withoutCw = BuildPassable(access, grid.CellCount, mask, excludeCrosswalk: true, flags);
                int[] labelsWithout = LabelComponents(grid, access, withoutCw, out List<RawComponent> rawWithout);
                report.pedestrianComponentsWithoutCrosswalk = rawWithout.Count;
                report.pedestrianComponentsWithCrosswalk = report.componentCount;

                if (flags != null && flags.Length >= grid.CellCount)
                {
                    CountConnectingCrosswalks(grid, access, flags, labelsWithout, report);
                    CollectRoadRail(grid, flags, report);
                }
            }

            return report;
        }

        /// <summary>
        /// Can step from (i0,j0) to (i1,j1) under 8-connectivity with corner ban.
        /// Both cells must already be known passable by the caller for the destination.
        /// </summary>
        public static bool CanStep(byte[] access, int w, int h, int i0, int j0, int i1, int j1)
        {
            int di = i1 - i0;
            int dj = j1 - j0;
            int adi = Math.Abs(di);
            int adj = Math.Abs(dj);
            if (adi > 1 || adj > 1 || (adi == 0 && adj == 0))
            {
                return false;
            }

            if (adi + adj == 1)
            {
                return true;
            }

            // Diagonal: forbidden if both orthogonal neighbours are Obstacle.
            int oi = i0 + di;
            int oj = j0;
            int pi = i0;
            int pj = j0 + dj;
            bool oObs = IsObstacleCell(access, w, h, oi, oj);
            bool pObs = IsObstacleCell(access, w, h, pi, pj);
            return !(oObs && pObs);
        }

        static bool IsObstacleCell(byte[] access, int w, int h, int i, int j)
        {
            if (i < 0 || j < 0 || i >= w || j >= h)
            {
                return true;
            }

            return access[i + j * w] == (byte)CityAccess.Obstacle;
        }

        static void CountClasses(byte[] access, int n, Report report)
        {
            for (int i = 0; i < n; i++)
            {
                switch ((CityAccess)access[i])
                {
                    case CityAccess.Obstacle: report.obstacleCells++; break;
                    case CityAccess.Roadway: report.roadwayCells++; break;
                    default: report.pedestrianCells++; break;
                }
            }
        }

        static bool[] BuildPassable(
            byte[] access, int n, Mask mask, bool excludeCrosswalk, ushort[] flags)
        {
            var pass = new bool[n];
            for (int i = 0; i < n; i++)
            {
                CityAccess a = (CityAccess)access[i];
                bool ok = mask == Mask.Pedestrian
                    ? a == CityAccess.Pedestrian
                    : a == CityAccess.Pedestrian || a == CityAccess.Roadway;
                if (ok && excludeCrosswalk && flags != null && i < flags.Length &&
                    (flags[i] & (ushort)CellFlags.Crosswalk) != 0)
                {
                    ok = false;
                }

                pass[i] = ok;
            }

            return pass;
        }

        struct RawComponent
        {
            public int seedLabel;
            public int cellCount;
            public long sumI;
            public long sumJ;
        }

        static int[] LabelComponents(
            CityGrid grid, byte[] access, bool[] passable, out List<RawComponent> raw)
        {
            int w = grid.w;
            int h = grid.h;
            int n = grid.CellCount;
            var labels = new int[n];
            raw = new List<RawComponent>();
            int next = 1;
            var queue = new int[n];

            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    int idx = i + j * w;
                    if (!passable[idx] || labels[idx] != 0)
                    {
                        continue;
                    }

                    int label = next++;
                    int qh = 0, qt = 0;
                    queue[qt++] = idx;
                    labels[idx] = label;
                    int count = 0;
                    long sumI = 0, sumJ = 0;

                    while (qh < qt)
                    {
                        int cur = queue[qh++];
                        int ci = cur % w;
                        int cj = cur / w;
                        count++;
                        sumI += ci;
                        sumJ += cj;

                        for (int d = 0; d < 8; d++)
                        {
                            int ni = ci + Di[d];
                            int nj = cj + Dj[d];
                            if (ni < 0 || nj < 0 || ni >= w || nj >= h)
                            {
                                continue;
                            }

                            int nidx = ni + nj * w;
                            if (!passable[nidx] || labels[nidx] != 0)
                            {
                                continue;
                            }

                            if (!CanStep(access, w, h, ci, cj, ni, nj))
                            {
                                continue;
                            }

                            labels[nidx] = label;
                            queue[qt++] = nidx;
                        }
                    }

                    raw.Add(new RawComponent
                    {
                        seedLabel = label,
                        cellCount = count,
                        sumI = sumI,
                        sumJ = sumJ
                    });
                }
            }

            return labels;
        }

        static void AssignSortedComponents(
            CityGrid grid,
            List<RawComponent> raw,
            int[] labels,
            int islandMinCells,
            Report report)
        {
            raw.Sort((a, b) =>
            {
                int c = b.cellCount.CompareTo(a.cellCount);
                if (c != 0)
                {
                    return c;
                }

                return a.seedLabel.CompareTo(b.seedLabel);
            });

            report.componentCount = raw.Count;
            report.components = new ComponentInfo[raw.Count];
            var remap = new Dictionary<int, int>(raw.Count);
            int passable = 0;
            for (int i = 0; i < raw.Count; i++)
            {
                passable += raw[i].cellCount;
            }

            double cellArea = grid.cell * (double)grid.cell;
            var islands = new List<ComponentInfo>();

            for (int i = 0; i < raw.Count; i++)
            {
                int newId = i + 1;
                remap[raw[i].seedLabel] = newId;
                double cx = raw[i].cellCount > 0
                    ? grid.origin.x + (raw[i].sumI / (double)raw[i].cellCount + 0.5) * grid.cell
                    : 0;
                double cz = raw[i].cellCount > 0
                    ? grid.origin.z + (raw[i].sumJ / (double)raw[i].cellCount + 0.5) * grid.cell
                    : 0;
                var info = new ComponentInfo
                {
                    id = newId,
                    cellCount = raw[i].cellCount,
                    areaM2 = raw[i].cellCount * cellArea,
                    centerX = cx,
                    centerZ = cz,
                    shareOfPassable = passable > 0 ? raw[i].cellCount / (double)passable : 0
                };
                report.components[i] = info;
                if (info.cellCount < islandMinCells)
                {
                    islands.Add(info);
                }
            }

            islands.Sort((a, b) =>
            {
                int c = b.cellCount.CompareTo(a.cellCount);
                return c != 0 ? c : a.id.CompareTo(b.id);
            });
            if (islands.Count > 10)
            {
                islands.RemoveRange(10, islands.Count - 10);
            }

            report.islands = islands.ToArray();
            report.islandCount = report.components.Length > 0
                ? CountIslands(report.components, islandMinCells)
                : 0;

            report.cellComponentId = new int[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int old = labels[i];
                report.cellComponentId[i] = old == 0 ? 0 : remap[old];
            }
        }

        static int CountIslands(ComponentInfo[] components, int islandMinCells)
        {
            int n = 0;
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i].cellCount < islandMinCells)
                {
                    n++;
                }
            }

            return n;
        }

        static void CountConnectingCrosswalks(
            CityGrid grid, byte[] access, ushort[] flags, int[] labelsWithout, Report report)
        {
            int w = grid.w;
            int h = grid.h;
            int connecting = 0;
            int cwCount = 0;
            var seen = new HashSet<int>();

            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    int idx = i + j * w;
                    if ((flags[idx] & (ushort)CellFlags.Crosswalk) == 0)
                    {
                        continue;
                    }

                    cwCount++;
                    if (access[idx] != (byte)CityAccess.Pedestrian)
                    {
                        continue;
                    }

                    seen.Clear();
                    for (int d = 0; d < 8; d++)
                    {
                        int ni = i + Di[d];
                        int nj = j + Dj[d];
                        if (ni < 0 || nj < 0 || ni >= w || nj >= h)
                        {
                            continue;
                        }

                        if (!CanStep(access, w, h, i, j, ni, nj))
                        {
                            continue;
                        }

                        int nidx = ni + nj * w;
                        int lab = labelsWithout[nidx];
                        if (lab != 0)
                        {
                            seen.Add(lab);
                        }
                    }

                    if (seen.Count >= 2)
                    {
                        connecting++;
                    }
                }
            }

            report.crosswalkCellCount = cwCount;
            report.crosswalkCellsConnecting = connecting;
        }

        static void CollectRoadRail(CityGrid grid, ushort[] flags, Report report)
        {
            ushort both = (ushort)(CellFlags.Road | CellFlags.Rail);
            var centers = new List<Vec2d>();
            int w = grid.w;
            int h = grid.h;
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    int idx = i + j * w;
                    if ((flags[idx] & both) == both)
                    {
                        centers.Add(grid.CellCenter(i, j));
                    }
                }
            }

            report.roadRailCellCount = centers.Count;
            report.roadRailCenters = centers.ToArray();
        }

        /// <summary>Size bins of ADR-003 §4.1. Stub until [TASK-004/4C].</summary>
        public struct SizeHistogram
        {
            public int upTo10;
            public int from11To50;
            public int from51To199;
            public int atLeast200;
        }

        public enum CrossingEndStatus : byte
        {
            BothEndsSameComponent = 0,
            ConnectsDifferent = 1,
            OneEndWithoutSidewalk = 2,
            BothEndsWithoutSidewalk = 3
        }

        public struct CrossingEnd
        {
            public bool hasSidewalk;
            public int componentId;
            public double distanceM;
        }

        public struct CrossingRow
        {
            public long osmId;
            public int cellCount;
            public CrossingEnd end0;
            public CrossingEnd end1;
            public CrossingEndStatus status;
        }

        public struct CellCluster
        {
            public int cellCount;
            public double centerX;
            public double centerZ;
        }

        /// <summary>Stub for 4C-tests (red). Replaced in [TASK-004/4C].</summary>
        public sealed class StrictDiagnostics
        {
            public int componentsWithoutCrosswalk;
            public int componentsWithCrosswalk;
            public int crosswalkGroupCount;
            public int connectingGroupCount;
            public int unmarkedCellCount;
            public int unmarkedClusterCount;
            public CellCluster[] unmarkedLargest = Array.Empty<CellCluster>();
            public CrossingRow[] crossings = Array.Empty<CrossingRow>();
            public ComponentInfo[] strictWithout = Array.Empty<ComponentInfo>();
            public ComponentInfo[] strictWith = Array.Empty<ComponentInfo>();
            public SizeHistogram histogramStrict;
            public SizeHistogram histogramPedestrian;
        }

        public static SizeHistogram Histogram(int[] cellCounts)
        {
            return default;
        }

        public static string FormatEnd(CrossingEnd end)
        {
            return string.Empty;
        }

        public static StrictDiagnostics AnalyzeStrict(
            CityGrid grid,
            byte[] access,
            ushort[] flags,
            Walkway[] walkways,
            float searchRadiusM,
            float crosswalkWidthFallback)
        {
            return new StrictDiagnostics();
        }
    }
}
