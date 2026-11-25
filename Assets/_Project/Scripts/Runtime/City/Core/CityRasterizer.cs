using System;
using System.Collections.Generic;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// CityModel → flags + buildingId (ADR-002 §4–§6).
    /// </summary>
    public static class CityRasterizer
    {
        const ushort BuildingClearMask = (ushort)(
            CellFlags.Road | CellFlags.Sidewalk | CellFlags.Cycleway | CellFlags.Steps |
            CellFlags.Crosswalk | CellFlags.Shared | CellFlags.Green);

        const ushort BridgeClearMask = (ushort)(
            CellFlags.Water | CellFlags.Building | CellFlags.Road | CellFlags.Sidewalk |
            CellFlags.Cycleway | CellFlags.Steps | CellFlags.Crosswalk | CellFlags.Shared |
            CellFlags.Green | CellFlags.Rail | CellFlags.Barrier);

        public static RasterBakeResult Bake(CityModel model, CityBakeConfig config)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (config == null)
            {
                config = new CityBakeConfig();
            }

            CityGrid grid = CityGrid.FromModel(model, config.cellSize);
            int n = grid.CellCount;
            var flags = new ushort[n];
            var buildingId = new ushort[n];
            var stats = new RasterStats();

            // --- Step 1: Green ---
            ApplyAreas(model.areas, AreaKind.Green, grid, flags, (ushort)CellFlags.Green, null, buildingId, stats, clearWater: false);

            // --- Step 2: Water areas + waterways ---
            ApplyAreas(model.areas, AreaKind.Water, grid, flags, (ushort)CellFlags.Water, null, buildingId, stats, clearWater: false);
            ApplyWaterways(model.waterways, config, grid, flags, stats);

            // --- Step 3: Roads (surface) ---
            ApplyRoadsSurface(model.roads, grid, flags, stats);

            // --- Step 4: Walkways + PedestrianArea ---
            ApplyWalkwaysSurface(model.walkways, config, grid, flags, stats);
            ApplyAreas(model.areas, AreaKind.PedestrianArea, grid, flags, (ushort)CellFlags.Sidewalk, null, buildingId, stats, clearWater: true);

            // --- Step 5: Railway ---
            ApplyRailwaysSurface(model.railways, config, grid, flags, stats);

            // --- Step 6: Buildings ---
            BuildingRasterInfo[] buildings = ApplyBuildings(model.areas, grid, flags, buildingId, stats);

            // --- Step 7: Building passages ---
            ApplyBuildingPassages(model.roads, model.walkways, grid, flags, buildingId, stats);

            // --- Step 8: Barriers + Gates ---
            ApplyBarriers(model.barriers, config, grid, flags, stats);
            ApplyGates(model.pointFeatures, config, grid, flags);

            // --- Step 9: Bridges ---
            ApplyBridges(model.roads, model.walkways, model.railways, config, grid, flags, buildingId, stats);

            // Count tunnels / crossing nodes as "not drawn" — objectsOutside only for geometry miss.
            CountTunnelOutsideStats(model, stats);

            FinalizeStats(flags, stats);
            ulong dataHash = ComputeDataHash(grid, flags, buildingId, buildings);

            return new RasterBakeResult
            {
                grid = grid,
                flags = flags,
                buildingId = buildingId,
                buildings = buildings,
                stats = stats,
                modelDataHash = model.meta != null ? model.meta.dataHash : 0UL,
                configHash = config.ComputeSettingsHash(),
                dataHash = dataHash
            };
        }

        static void ApplyAreas(
            Area[] areas,
            AreaKind kind,
            CityGrid grid,
            ushort[] flags,
            ushort bit,
            ushort? clearMask,
            ushort[] buildingId,
            RasterStats stats,
            bool clearWater)
        {
            if (areas == null)
            {
                return;
            }

            Area[] sorted = SortAreasByOsmId(areas, kind);
            for (int a = 0; a < sorted.Length; a++)
            {
                Area area = sorted[a];
                Vec2d[][] rings = AreaToRings(area);
                int stamped = 0;
                RasterPrimitives.ForEachPolygonCellA(grid, rings, (i, j) =>
                {
                    int idx = grid.Index(i, j);
                    if (clearWater)
                    {
                        ClearWaterIfSet(flags, idx, stats);
                    }

                    if (clearMask.HasValue)
                    {
                        flags[idx] = (ushort)(flags[idx] & ~clearMask.Value);
                    }

                    flags[idx] |= bit;
                    stamped++;
                });
                if (stamped == 0)
                {
                    stats.objectsOutsideGrid++;
                }
            }
        }

        static void ApplyWaterways(Waterway[] waterways, CityBakeConfig config, CityGrid grid, ushort[] flags, RasterStats stats)
        {
            if (waterways == null)
            {
                return;
            }

            Waterway[] sorted = (Waterway[])waterways.Clone();
            Array.Sort(sorted, (a, b) => a.osmId.CompareTo(b.osmId));
            for (int i = 0; i < sorted.Length; i++)
            {
                Waterway w = sorted[i];
                if (w == null || w.isTunnel || w.points == null || w.points.Length < 2)
                {
                    continue;
                }

                int stamped = 0;
                RasterPrimitives.ForEachPolylineCapsuleA(grid, w.points, config.waterwayWidth, (ci, cj) =>
                {
                    flags[grid.Index(ci, cj)] |= (ushort)CellFlags.Water;
                    stamped++;
                });
                if (stamped == 0)
                {
                    stats.objectsOutsideGrid++;
                }
            }
        }

        static void ApplyRoadsSurface(Road[] roads, CityGrid grid, ushort[] flags, RasterStats stats)
        {
            if (roads == null)
            {
                return;
            }

            Road[] sorted = (Road[])roads.Clone();
            Array.Sort(sorted, (a, b) => a.osmId.CompareTo(b.osmId));
            for (int i = 0; i < sorted.Length; i++)
            {
                Road r = sorted[i];
                if (r == null || r.isTunnel || r.isBridge || r.isBuildingPassage || r.points == null || r.points.Length < 2)
                {
                    continue;
                }

                ushort bits = (ushort)CellFlags.Road;
                if (r.isShared)
                {
                    bits |= (ushort)CellFlags.Shared;
                }

                int stamped = StampLinearClearWaterA(grid, flags, r.points, r.widthM, bits, stats);
                if (stamped == 0)
                {
                    stats.objectsOutsideGrid++;
                }
            }
        }

        static void ApplyWalkwaysSurface(
            Walkway[] walkways, CityBakeConfig config, CityGrid grid, ushort[] flags, RasterStats stats)
        {
            if (walkways == null)
            {
                return;
            }

            Walkway[] sorted = (Walkway[])walkways.Clone();
            Array.Sort(sorted, (a, b) => a.osmId.CompareTo(b.osmId));
            for (int i = 0; i < sorted.Length; i++)
            {
                Walkway w = sorted[i];
                if (w == null || w.isTunnel || w.isBridge || w.isBuildingPassage || w.points == null || w.points.Length < 2)
                {
                    continue;
                }

                ushort bit = WalkwayBit(w.kind);
                float width = w.widthM > 0 ? w.widthM : config.crosswalkWidthFallback;
                if (w.kind == WalkwayKind.Crossing && w.widthM <= 0)
                {
                    width = config.crosswalkWidthFallback;
                }

                int stamped = StampLinearClearWaterA(grid, flags, w.points, width, bit, stats);
                if (stamped == 0)
                {
                    stats.objectsOutsideGrid++;
                }
            }
        }

        static void ApplyRailwaysSurface(
            Railway[] railways, CityBakeConfig config, CityGrid grid, ushort[] flags, RasterStats stats)
        {
            if (railways == null)
            {
                return;
            }

            Railway[] sorted = (Railway[])railways.Clone();
            Array.Sort(sorted, (a, b) => a.osmId.CompareTo(b.osmId));
            for (int i = 0; i < sorted.Length; i++)
            {
                Railway r = sorted[i];
                if (r == null || r.isTunnel || r.isBridge || r.points == null || r.points.Length < 2)
                {
                    continue;
                }

                int stamped = StampLinearClearWaterA(
                    grid, flags, r.points, config.railwayWidth, (ushort)CellFlags.Rail, stats);
                if (stamped == 0)
                {
                    stats.objectsOutsideGrid++;
                }
            }
        }

        static BuildingRasterInfo[] ApplyBuildings(
            Area[] areas, CityGrid grid, ushort[] flags, ushort[] buildingId, RasterStats stats)
        {
            if (areas == null)
            {
                return Array.Empty<BuildingRasterInfo>();
            }

            var list = new List<Area>();
            for (int i = 0; i < areas.Length; i++)
            {
                if (areas[i] != null && areas[i].kind == AreaKind.Building)
                {
                    list.Add(areas[i]);
                }
            }

            list.Sort((a, b) => a.osmId.CompareTo(b.osmId));
            if (list.Count > 65535)
            {
                throw new InvalidOperationException("Building count exceeds 65535");
            }

            var infos = new BuildingRasterInfo[list.Count];
            stats.buildingCount = list.Count;

            for (int b = 0; b < list.Count; b++)
            {
                Area area = list[b];
                ushort id = (ushort)(b + 1);
                var info = new BuildingRasterInfo
                {
                    id = id,
                    osmId = area.osmId,
                    heightM = area.heightM,
                    buildingType = area.buildingType,
                    minI = int.MaxValue,
                    minJ = int.MaxValue,
                    maxI = int.MinValue,
                    maxJ = int.MinValue,
                    cellCount = 0
                };

                Vec2d[][] rings = AreaToRings(area);
                RasterPrimitives.ForEachPolygonCellA(grid, rings, (i, j) =>
                {
                    int idx = grid.Index(i, j);
                    ushort f = flags[idx];
                    if ((f & (ushort)CellFlags.Bridge) != 0)
                    {
                        return;
                    }

                    if (buildingId[idx] != 0)
                    {
                        stats.buildingConflicts++;
                    }

                    flags[idx] = (ushort)((f & ~BuildingClearMask) | (ushort)CellFlags.Building);
                    buildingId[idx] = id;
                    info.cellCount++;
                    if (i < info.minI)
                    {
                        info.minI = i;
                    }

                    if (j < info.minJ)
                    {
                        info.minJ = j;
                    }

                    if (i > info.maxI)
                    {
                        info.maxI = i;
                    }

                    if (j > info.maxJ)
                    {
                        info.maxJ = j;
                    }
                });

                if (info.cellCount == 0)
                {
                    info.minI = info.minJ = info.maxI = info.maxJ = 0;
                    stats.buildingsWithZeroCells++;
                    stats.objectsOutsideGrid++;
                }

                infos[b] = info;
            }

            return infos;
        }

        static void ApplyBuildingPassages(
            Road[] roads, Walkway[] walkways, CityGrid grid, ushort[] flags, ushort[] buildingId, RasterStats stats)
        {
            var roadList = new List<Road>();
            if (roads != null)
            {
                for (int i = 0; i < roads.Length; i++)
                {
                    if (roads[i] != null && roads[i].isBuildingPassage && !roads[i].isTunnel)
                    {
                        roadList.Add(roads[i]);
                    }
                }
            }

            roadList.Sort((a, b) => a.osmId.CompareTo(b.osmId));
            for (int i = 0; i < roadList.Count; i++)
            {
                Road r = roadList[i];
                if (r.points == null || r.points.Length < 2)
                {
                    continue;
                }

                int stamped = 0;
                RasterPrimitives.ForEachPolylineCapsuleA(grid, r.points, r.widthM, (ci, cj) =>
                {
                    int idx = grid.Index(ci, cj);
                    flags[idx] = (ushort)((flags[idx] & ~(ushort)CellFlags.Building) | (ushort)CellFlags.BuildingPassage | (ushort)CellFlags.Road);
                    buildingId[idx] = 0;
                    stamped++;
                });
                if (stamped == 0)
                {
                    stats.objectsOutsideGrid++;
                }
            }

            var walkList = new List<Walkway>();
            if (walkways != null)
            {
                for (int i = 0; i < walkways.Length; i++)
                {
                    if (walkways[i] != null && walkways[i].isBuildingPassage && !walkways[i].isTunnel)
                    {
                        walkList.Add(walkways[i]);
                    }
                }
            }

            walkList.Sort((a, b) => a.osmId.CompareTo(b.osmId));
            for (int i = 0; i < walkList.Count; i++)
            {
                Walkway w = walkList[i];
                if (w.points == null || w.points.Length < 2)
                {
                    continue;
                }

                ushort classBit = WalkwayBit(w.kind);
                if (classBit == (ushort)CellFlags.Crosswalk)
                {
                    classBit = (ushort)CellFlags.Sidewalk;
                }

                int stamped = 0;
                RasterPrimitives.ForEachPolylineCapsuleA(grid, w.points, w.widthM, (ci, cj) =>
                {
                    int idx = grid.Index(ci, cj);
                    flags[idx] = (ushort)((flags[idx] & ~(ushort)CellFlags.Building) | (ushort)CellFlags.BuildingPassage | classBit);
                    buildingId[idx] = 0;
                    stamped++;
                });
                if (stamped == 0)
                {
                    stats.objectsOutsideGrid++;
                }
            }
        }

        static void ApplyBarriers(
            Barrier[] barriers, CityBakeConfig config, CityGrid grid, ushort[] flags, RasterStats stats)
        {
            if (barriers == null)
            {
                return;
            }

            Barrier[] sorted = (Barrier[])barriers.Clone();
            Array.Sort(sorted, (a, b) => a.osmId.CompareTo(b.osmId));
            for (int i = 0; i < sorted.Length; i++)
            {
                Barrier b = sorted[i];
                if (b == null || b.points == null || b.points.Length < 2)
                {
                    continue;
                }

                int stamped = 0;
                RasterPrimitives.ForEachPolylineCapsuleB(grid, b.points, config.barrierWidth, (ci, cj) =>
                {
                    flags[grid.Index(ci, cj)] |= (ushort)CellFlags.Barrier;
                    stamped++;
                });
                if (stamped == 0)
                {
                    stats.objectsOutsideGrid++;
                }
            }
        }

        static void ApplyGates(PointFeature[] points, CityBakeConfig config, CityGrid grid, ushort[] flags)
        {
            if (points == null)
            {
                return;
            }

            PointFeature[] sorted = (PointFeature[])points.Clone();
            Array.Sort(sorted, (a, b) => a.osmId.CompareTo(b.osmId));
            double r = config.gateRadius;
            double r2 = r * r;
            for (int p = 0; p < sorted.Length; p++)
            {
                PointFeature f = sorted[p];
                if (f == null || f.kind != PointKind.Gate)
                {
                    continue;
                }

                GetCellRangeAround(grid, f.position, r, out int i0, out int j0, out int i1, out int j1);
                for (int j = j0; j <= j1; j++)
                {
                    for (int i = i0; i <= i1; i++)
                    {
                        Vec2d c = grid.CellCenter(i, j);
                        double dx = c.x - f.position.x;
                        double dz = c.z - f.position.z;
                        if (dx * dx + dz * dz <= r2 + 1e-9)
                        {
                            int idx = grid.Index(i, j);
                            flags[idx] = (ushort)((flags[idx] & ~(ushort)CellFlags.Barrier) | (ushort)CellFlags.Gate);
                        }
                    }
                }
            }
        }

        static void ApplyBridges(
            Road[] roads,
            Walkway[] walkways,
            Railway[] railways,
            CityBakeConfig config,
            CityGrid grid,
            ushort[] flags,
            ushort[] buildingId,
            RasterStats stats)
        {
            var items = new List<(int layer, long osmId, int kind, Vec2d[] pts, float width, ushort classBit)>();
            // kind: 0 road, 1 walkway, 2 railway — for stable tie-break with osmId already
            if (roads != null)
            {
                for (int i = 0; i < roads.Length; i++)
                {
                    Road r = roads[i];
                    if (r != null && r.isBridge && !r.isTunnel && r.points != null && r.points.Length >= 2)
                    {
                        ushort bits = (ushort)CellFlags.Road;
                        if (r.isShared)
                        {
                            bits |= (ushort)CellFlags.Shared;
                        }

                        items.Add((r.layer, r.osmId, 0, r.points, r.widthM, bits));
                    }
                }
            }

            if (walkways != null)
            {
                for (int i = 0; i < walkways.Length; i++)
                {
                    Walkway w = walkways[i];
                    if (w != null && w.isBridge && !w.isTunnel && w.points != null && w.points.Length >= 2)
                    {
                        items.Add((w.layer, w.osmId, 1, w.points, w.widthM, WalkwayBit(w.kind)));
                    }
                }
            }

            if (railways != null)
            {
                for (int i = 0; i < railways.Length; i++)
                {
                    Railway r = railways[i];
                    if (r != null && r.isBridge && !r.isTunnel && r.points != null && r.points.Length >= 2)
                    {
                        items.Add((r.layer, r.osmId, 2, r.points, config.railwayWidth, (ushort)CellFlags.Rail));
                    }
                }
            }

            items.Sort((a, b) =>
            {
                int c = a.layer.CompareTo(b.layer);
                if (c != 0)
                {
                    return c;
                }

                c = a.osmId.CompareTo(b.osmId);
                return c != 0 ? c : a.kind.CompareTo(b.kind);
            });

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                int stamped = 0;
                RasterPrimitives.ForEachPolylineCapsuleA(grid, it.pts, it.width, (ci, cj) =>
                {
                    int idx = grid.Index(ci, cj);
                    flags[idx] = (ushort)((flags[idx] & ~BridgeClearMask) | it.classBit | (ushort)CellFlags.Bridge);
                    buildingId[idx] = 0;
                    stamped++;
                });
                if (stamped == 0)
                {
                    stats.objectsOutsideGrid++;
                }
            }
        }

        static int StampLinearClearWaterA(
            CityGrid grid, ushort[] flags, Vec2d[] points, float width, ushort bits, RasterStats stats)
        {
            int stamped = 0;
            RasterPrimitives.ForEachPolylineCapsuleA(grid, points, width, (i, j) =>
            {
                int idx = grid.Index(i, j);
                ClearWaterIfSet(flags, idx, stats);
                flags[idx] |= bits;
                stamped++;
            });
            return stamped;
        }

        static void ClearWaterIfSet(ushort[] flags, int idx, RasterStats stats)
        {
            if ((flags[idx] & (ushort)CellFlags.Water) != 0)
            {
                flags[idx] = (ushort)(flags[idx] & ~(ushort)CellFlags.Water);
                stats.waterClearedByLinear++;
            }
        }

        static ushort WalkwayBit(WalkwayKind kind)
        {
            switch (kind)
            {
                case WalkwayKind.Cycleway:
                    return (ushort)CellFlags.Cycleway;
                case WalkwayKind.Steps:
                    return (ushort)CellFlags.Steps;
                case WalkwayKind.Crossing:
                    return (ushort)CellFlags.Crosswalk;
                default:
                    return (ushort)CellFlags.Sidewalk;
            }
        }

        static Area[] SortAreasByOsmId(Area[] areas, AreaKind kind)
        {
            var list = new List<Area>();
            for (int i = 0; i < areas.Length; i++)
            {
                if (areas[i] != null && areas[i].kind == kind)
                {
                    list.Add(areas[i]);
                }
            }

            list.Sort((a, b) => a.osmId.CompareTo(b.osmId));
            return list.ToArray();
        }

        static Vec2d[][] AreaToRings(Area area)
        {
            if (area == null || area.points == null || area.points.Length == 0)
            {
                return Array.Empty<Vec2d[]>();
            }

            int[] starts = area.ringStart;
            if (starts == null || starts.Length == 0)
            {
                return new[] { area.points };
            }

            var rings = new Vec2d[starts.Length][];
            for (int r = 0; r < starts.Length; r++)
            {
                int start = starts[r];
                int end = r + 1 < starts.Length ? starts[r + 1] : area.points.Length;
                int len = Math.Max(0, end - start);
                var ring = new Vec2d[len];
                Array.Copy(area.points, start, ring, 0, len);
                rings[r] = ring;
            }

            return rings;
        }

        static void GetCellRangeAround(
            CityGrid grid, Vec2d center, double radius, out int i0, out int j0, out int i1, out int j1)
        {
            grid.WorldToCell(new Vec2d(center.x - radius, center.z - radius), out i0, out j0);
            grid.WorldToCell(new Vec2d(center.x + radius, center.z + radius), out i1, out j1);
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
        }

        static void CountTunnelOutsideStats(CityModel model, RasterStats stats)
        {
            // Tunnels intentionally not rasterized — do not count as outside.
            // CrossingNode / TrafficSignal / Entrance — not drawn (no outside count).
        }

        static void FinalizeStats(ushort[] flags, RasterStats stats)
        {
            stats.cellsByBit = new int[16];
            stats.cellsUnderBridges = 0;
            stats.cellsRoadAndCrosswalk = 0;
            for (int i = 0; i < flags.Length; i++)
            {
                ushort f = flags[i];
                for (int b = 0; b < 16; b++)
                {
                    if ((f & (1 << b)) != 0)
                    {
                        stats.cellsByBit[b]++;
                    }
                }

                if ((f & (ushort)CellFlags.Bridge) != 0)
                {
                    stats.cellsUnderBridges++;
                }

                if ((f & (ushort)(CellFlags.Road | CellFlags.Crosswalk)) ==
                    (ushort)(CellFlags.Road | CellFlags.Crosswalk))
                {
                    stats.cellsRoadAndCrosswalk++;
                }
            }
        }

        static ulong ComputeDataHash(
            CityGrid grid, ushort[] flags, ushort[] buildingId, BuildingRasterInfo[] buildings)
        {
            var w = new CanonicalWriter();
            w.WriteMeters(grid.origin.x);
            w.WriteMeters(grid.origin.z);
            w.WriteMeters(grid.cell);
            w.WriteInt32(grid.w);
            w.WriteInt32(grid.h);
            for (int i = 0; i < flags.Length; i++)
            {
                unchecked
                {
                    w.WriteInt32(flags[i]);
                }
            }

            for (int i = 0; i < buildingId.Length; i++)
            {
                unchecked
                {
                    w.WriteInt32(buildingId[i]);
                }
            }

            w.WriteInt32(buildings != null ? buildings.Length : 0);
            if (buildings != null)
            {
                for (int i = 0; i < buildings.Length; i++)
                {
                    BuildingRasterInfo b = buildings[i] ?? new BuildingRasterInfo();
                    w.WriteInt32(b.id);
                    w.WriteInt64(b.osmId);
                    w.WriteMeters(b.heightM);
                    w.WriteString(b.buildingType);
                    w.WriteInt32(b.minI);
                    w.WriteInt32(b.minJ);
                    w.WriteInt32(b.maxI);
                    w.WriteInt32(b.maxJ);
                    w.WriteInt32(b.cellCount);
                }
            }

            return Fnv1a64.Compute(w.ToArray());
        }
    }
}
