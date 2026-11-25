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
            Zombie = 1,
            Strict = 2,
            StrictWithCrosswalk = 3
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
                bool road = flags != null && i < flags.Length && (flags[i] & (ushort)CellFlags.Road) != 0;
                bool cross = flags != null && i < flags.Length && (flags[i] & (ushort)CellFlags.Crosswalk) != 0;
                bool ok;
                switch (mask)
                {
                    case Mask.Zombie:
                        ok = a == CityAccess.Pedestrian || a == CityAccess.Roadway;
                        break;
                    case Mask.Strict:
                        ok = a == CityAccess.Pedestrian && !road;
                        break;
                    case Mask.StrictWithCrosswalk:
                        ok = a == CityAccess.Pedestrian && (!road || cross);
                        break;
                    default:
                        ok = a == CityAccess.Pedestrian;
                        break;
                }

                if (ok && excludeCrosswalk && cross)
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
            var h = new SizeHistogram();
            if (cellCounts == null)
            {
                return h;
            }

            for (int i = 0; i < cellCounts.Length; i++)
            {
                int c = cellCounts[i];
                if (c <= 10)
                {
                    h.upTo10++;
                }
                else if (c <= 50)
                {
                    h.from11To50++;
                }
                else if (c <= 199)
                {
                    h.from51To199++;
                }
                else
                {
                    h.atLeast200++;
                }
            }

            return h;
        }

        public static string FormatEnd(CrossingEnd end)
        {
            return end.hasSidewalk
                ? end.componentId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "нет тротуара рядом";
        }

        /// <summary>
        /// Strict pedestrian diagnostics (ADR-003 §4.1).
        /// Crossing ends are the two vertices of each <see cref="WalkwayKind.Crossing"/> line.
        /// </summary>
        public static StrictDiagnostics AnalyzeStrict(
            CityGrid grid,
            byte[] access,
            ushort[] flags,
            Walkway[] walkways,
            float searchRadiusM,
            float crosswalkWidthFallback,
            int islandMinCells = 200)
        {
            if (searchRadiusM < 0f)
            {
                searchRadiusM = 0f;
            }

            Report strict = Analyze(grid, access, flags, islandMinCells, Mask.Strict);
            Report strictWith = Analyze(grid, access, flags, islandMinCells, Mask.StrictWithCrosswalk);
            Report pedestrian = Analyze(grid, access, flags, islandMinCells, Mask.Pedestrian);

            var diag = new StrictDiagnostics
            {
                componentsWithoutCrosswalk = strict.componentCount,
                componentsWithCrosswalk = strictWith.componentCount,
                strictWithout = strict.components ?? Array.Empty<ComponentInfo>(),
                strictWith = strictWith.components ?? Array.Empty<ComponentInfo>(),
                histogramStrict = Histogram(ComponentSizes(strict.components)),
                histogramPedestrian = Histogram(ComponentSizes(pedestrian.components))
            };

            if (flags == null || flags.Length < grid.CellCount)
            {
                diag.crossings = Array.Empty<CrossingRow>();
                diag.unmarkedLargest = Array.Empty<CellCluster>();
                return diag;
            }

            CountCrosswalkGroups(grid, access, flags, strict.cellComponentId, diag);
            CollectUnmarked(grid, access, flags, diag);
            diag.crossings = ProbeCrossings(
                grid, flags, strict.cellComponentId, walkways, searchRadiusM, crosswalkWidthFallback);
            return diag;
        }

        static int[] ComponentSizes(ComponentInfo[] components)
        {
            if (components == null || components.Length == 0)
            {
                return Array.Empty<int>();
            }

            var sizes = new int[components.Length];
            for (int i = 0; i < components.Length; i++)
            {
                sizes[i] = components[i].cellCount;
            }

            return sizes;
        }

        static void CountCrosswalkGroups(
            CityGrid grid, byte[] access, ushort[] flags, int[] strictLabels, StrictDiagnostics diag)
        {
            int w = grid.w;
            int h = grid.h;
            int n = grid.CellCount;
            var visited = new bool[n];
            int groups = 0;
            int connecting = 0;
            var queue = new int[n];
            var ids = new List<int>();

            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    int seed = i + j * w;
                    if (visited[seed] || (flags[seed] & (ushort)CellFlags.Crosswalk) == 0)
                    {
                        continue;
                    }

                    groups++;
                    ids.Clear();
                    int qh = 0, qt = 0;
                    queue[qt++] = seed;
                    visited[seed] = true;
                    while (qh < qt)
                    {
                        int cur = queue[qh++];
                        int ci = cur % w;
                        int cj = cur / w;
                        for (int d = 0; d < 8; d++)
                        {
                            int ni = ci + Di[d];
                            int nj = cj + Dj[d];
                            if (ni < 0 || nj < 0 || ni >= w || nj >= h)
                            {
                                continue;
                            }

                            int nidx = ni + nj * w;
                            if (!CanStep(access, w, h, ci, cj, ni, nj))
                            {
                                continue;
                            }

                            if ((flags[nidx] & (ushort)CellFlags.Crosswalk) != 0)
                            {
                                if (!visited[nidx])
                                {
                                    visited[nidx] = true;
                                    queue[qt++] = nidx;
                                }
                            }
                            else if (strictLabels != null && nidx < strictLabels.Length && strictLabels[nidx] != 0)
                            {
                                InsertSortedUnique(ids, strictLabels[nidx]);
                            }
                        }
                    }

                    if (ids.Count >= 2)
                    {
                        connecting++;
                    }
                }
            }

            diag.crosswalkGroupCount = groups;
            diag.connectingGroupCount = connecting;
        }

        static void InsertSortedUnique(List<int> ids, int id)
        {
            int lo = 0;
            int hi = ids.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (ids[mid] < id)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            if (lo < ids.Count && ids[lo] == id)
            {
                return;
            }

            ids.Insert(lo, id);
        }

        static void CollectUnmarked(CityGrid grid, byte[] access, ushort[] flags, StrictDiagnostics diag)
        {
            int w = grid.w;
            int h = grid.h;
            int n = grid.CellCount;
            ushort sideBits = (ushort)(CellFlags.Sidewalk | CellFlags.SidewalkGenerated);
            var visited = new bool[n];
            var clusters = new List<CellCluster>();
            var queue = new int[n];
            int cells = 0;

            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    int seed = i + j * w;
                    if (visited[seed] || !IsUnmarked(flags[seed], sideBits))
                    {
                        continue;
                    }

                    int qh = 0, qt = 0;
                    queue[qt++] = seed;
                    visited[seed] = true;
                    int count = 0;
                    long sumI = 0, sumJ = 0;
                    while (qh < qt)
                    {
                        int cur = queue[qh++];
                        int ci = cur % w;
                        int cj = cur / w;
                        count++;
                        cells++;
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
                            if (visited[nidx] || !IsUnmarked(flags[nidx], sideBits))
                            {
                                continue;
                            }

                            if (!CanStep(access, w, h, ci, cj, ni, nj))
                            {
                                continue;
                            }

                            visited[nidx] = true;
                            queue[qt++] = nidx;
                        }
                    }

                    double cx = grid.origin.x + (sumI / (double)count + 0.5) * grid.cell;
                    double cz = grid.origin.z + (sumJ / (double)count + 0.5) * grid.cell;
                    clusters.Add(new CellCluster { cellCount = count, centerX = cx, centerZ = cz });
                }
            }

            clusters.Sort(CompareClusters);
            int clusterCount = clusters.Count;
            if (clusters.Count > 10)
            {
                clusters.RemoveRange(10, clusters.Count - 10);
            }

            diag.unmarkedCellCount = cells;
            diag.unmarkedClusterCount = clusterCount;
            diag.unmarkedLargest = clusters.ToArray();
        }

        static int CompareClusters(CellCluster a, CellCluster b)
        {
            int c = b.cellCount.CompareTo(a.cellCount);
            if (c != 0)
            {
                return c;
            }

            int x = a.centerX.CompareTo(b.centerX);
            return x != 0 ? x : a.centerZ.CompareTo(b.centerZ);
        }

        static bool IsUnmarked(ushort flags, ushort sideBits)
        {
            bool road = (flags & (ushort)CellFlags.Road) != 0;
            bool side = (flags & sideBits) != 0;
            bool cross = (flags & (ushort)CellFlags.Crosswalk) != 0;
            return road && side && !cross;
        }

        static CrossingRow[] ProbeCrossings(
            CityGrid grid,
            ushort[] flags,
            int[] strictLabels,
            Walkway[] walkways,
            float searchRadiusM,
            float crosswalkWidthFallback)
        {
            if (walkways == null || walkways.Length == 0)
            {
                return Array.Empty<CrossingRow>();
            }

            var order = new List<int>();
            for (int i = 0; i < walkways.Length; i++)
            {
                Walkway w = walkways[i];
                if (w == null || w.kind != WalkwayKind.Crossing || w.isTunnel || w.points == null || w.points.Length < 2)
                {
                    continue;
                }

                order.Add(i);
            }

            order.Sort((a, b) =>
            {
                int c = walkways[a].osmId.CompareTo(walkways[b].osmId);
                return c != 0 ? c : a.CompareTo(b);
            });

            var rows = new CrossingRow[order.Count];
            var seen = new bool[grid.CellCount];
            for (int r = 0; r < order.Count; r++)
            {
                Walkway w = walkways[order[r]];
                Array.Clear(seen, 0, seen.Length);
                int cells = 0;
                float width = w.widthM > 0f ? w.widthM : crosswalkWidthFallback;
                RasterPrimitives.ForEachPolylineCapsuleA(grid, w.points, width, (i, j) =>
                {
                    int idx = grid.Index(i, j);
                    if (seen[idx])
                    {
                        return;
                    }

                    seen[idx] = true;
                    if ((flags[idx] & (ushort)CellFlags.Crosswalk) != 0)
                    {
                        cells++;
                    }
                });

                CrossingEnd e0 = ProbeEnd(grid, strictLabels, w.points[0], searchRadiusM);
                CrossingEnd e1 = ProbeEnd(grid, strictLabels, w.points[w.points.Length - 1], searchRadiusM);
                rows[r] = new CrossingRow
                {
                    osmId = w.osmId,
                    cellCount = cells,
                    end0 = e0,
                    end1 = e1,
                    status = StatusOf(e0, e1)
                };
            }

            return rows;
        }

        static CrossingEndStatus StatusOf(CrossingEnd a, CrossingEnd b)
        {
            if (!a.hasSidewalk && !b.hasSidewalk)
            {
                return CrossingEndStatus.BothEndsWithoutSidewalk;
            }

            if (!a.hasSidewalk || !b.hasSidewalk)
            {
                return CrossingEndStatus.OneEndWithoutSidewalk;
            }

            return a.componentId == b.componentId
                ? CrossingEndStatus.BothEndsSameComponent
                : CrossingEndStatus.ConnectsDifferent;
        }

        static CrossingEnd ProbeEnd(CityGrid grid, int[] strictLabels, Vec2d point, float radius)
        {
            var end = new CrossingEnd();
            if (strictLabels == null || radius < 0f)
            {
                return end;
            }

            grid.WorldToCell(new Vec2d(point.x - radius, point.z - radius), out int i0, out int j0);
            grid.WorldToCell(new Vec2d(point.x + radius, point.z + radius), out int i1, out int j1);
            if (i0 > i1)
            {
                int t = i0;
                i0 = i1;
                i1 = t;
            }

            if (j0 > j1)
            {
                int t = j0;
                j0 = j1;
                j1 = t;
            }

            i0 = Math.Max(0, i0);
            j0 = Math.Max(0, j0);
            i1 = Math.Min(grid.w - 1, i1);
            j1 = Math.Min(grid.h - 1, j1);

            double best = double.MaxValue;
            int bestI = int.MaxValue;
            int bestJ = int.MaxValue;
            int bestId = 0;
            bool found = false;
            double limit = radius + 1e-9;
            for (int j = j0; j <= j1; j++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    int idx = i + j * grid.w;
                    if (idx < 0 || idx >= strictLabels.Length || strictLabels[idx] == 0)
                    {
                        continue;
                    }

                    Vec2d c = grid.CellCenter(i, j);
                    double dx = point.x - c.x;
                    double dz = point.z - c.z;
                    double dist = Math.Sqrt(dx * dx + dz * dz);
                    if (dist > limit)
                    {
                        continue;
                    }

                    bool better = !found || dist < best - 1e-9 ||
                                  (Math.Abs(dist - best) <= 1e-9 && (j < bestJ || (j == bestJ && i < bestI)));
                    if (!better)
                    {
                        continue;
                    }

                    found = true;
                    best = dist;
                    bestI = i;
                    bestJ = j;
                    bestId = strictLabels[idx];
                }
            }

            if (!found)
            {
                return end;
            }

            end.hasSidewalk = true;
            end.componentId = bestId;
            end.distanceM = best;
            return end;
        }
    }
}
