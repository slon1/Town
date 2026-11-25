using System;
using NUnit.Framework;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    /// <summary>
    /// TASK-002 / 2G — независимое ревью ADR-002 §8 (граничные случаи, не дублирующие 2B/2C намеренно).
    /// </summary>
    public class RasterReview002Tests
    {
        const float Cell = 0.5f;

        static CityBakeConfig Cfg() => new CityBakeConfig { cellSize = Cell };

        static CityBakeConfig CfgZeroBarrier() =>
            new CityBakeConfig { cellSize = Cell, barrierWidth = 0f };

        static CityModel EmptyModel(double size = 40)
        {
            return new CityModel
            {
                meta = new CityModelMeta
                {
                    lat0 = 0,
                    lon0 = 0,
                    bboxMinLat = -size / 2 / LocalEquirectangularProjection.EarthRadiusM * (180.0 / Math.PI),
                    bboxMaxLat = size / 2 / LocalEquirectangularProjection.EarthRadiusM * (180.0 / Math.PI),
                    bboxMinLon = -size / 2 / LocalEquirectangularProjection.EarthRadiusM * (180.0 / Math.PI),
                    bboxMaxLon = size / 2 / LocalEquirectangularProjection.EarthRadiusM * (180.0 / Math.PI),
                    sizeXM = size,
                    sizeZM = size
                }
            };
        }

        static Vec2d[] Rect(double x0, double z0, double x1, double z1) => new[]
        {
            new Vec2d(x0, z0), new Vec2d(x1, z0), new Vec2d(x1, z1), new Vec2d(x0, z1), new Vec2d(x0, z0)
        };

        static Area Building(long id, Vec2d[] outer, Vec2d[] hole = null, float h = 12f)
        {
            if (hole == null)
            {
                return new Area
                {
                    osmId = id,
                    kind = AreaKind.Building,
                    heightM = h,
                    buildingType = "yes",
                    points = outer,
                    ringStart = new[] { 0 }
                };
            }

            var pts = new Vec2d[outer.Length + hole.Length];
            Array.Copy(outer, 0, pts, 0, outer.Length);
            Array.Copy(hole, 0, pts, outer.Length, hole.Length);
            return new Area
            {
                osmId = id,
                kind = AreaKind.Building,
                heightM = h,
                buildingType = "yes",
                points = pts,
                ringStart = new[] { 0, outer.Length }
            };
        }

        static ushort FlagAt(RasterBakeResult r, double x, double z)
        {
            r.grid.WorldToCell(new Vec2d(x, z), out int i, out int j);
            Assert.IsTrue(r.grid.InBounds(i, j), $"out of bounds ({x},{z})");
            return r.flags[r.grid.Index(i, j)];
        }

        static int CountBits(ushort[] flags, ushort bit)
        {
            int n = 0;
            for (int i = 0; i < flags.Length; i++)
            {
                if ((flags[i] & bit) != 0)
                {
                    n++;
                }
            }

            return n;
        }

        static int CountFlag(RasterBakeResult r, CellFlags bit)
        {
            ushort b = (ushort)bit;
            int n = 0;
            for (int k = 0; k < r.flags.Length; k++)
            {
                if ((r.flags[k] & b) != 0)
                {
                    n++;
                }
            }

            return n;
        }

        [Test]
        public void Review01_ThinAngleRoad_StillContinuous()
        {
            // Две почти коллинеарные полилинии (~3° между сегментами).
            var model = EmptyModel();
            model.roads = new[]
            {
                new Road
                {
                    osmId = 1,
                    widthM = 2f,
                    points = new[]
                    {
                        new Vec2d(-12, -0.5),
                        new Vec2d(0, 0),
                        new Vec2d(12, 0.65)
                    }
                }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            Assert.Greater(CountFlag(r, CellFlags.Road), 8);
            Assert.AreNotEqual(0, FlagAt(r, 0, 0) & (ushort)CellFlags.Road);
            Assert.AreNotEqual(0, FlagAt(r, 6, 0.3) & (ushort)CellFlags.Road);
        }

        [Test]
        public void Review02_LineCenterOnCellBoundary_ModeA()
        {
            var model = EmptyModel();
            model.roads = new[]
            {
                new Road
                {
                    osmId = 1,
                    widthM = 1f,
                    points = new[] { new Vec2d(-10, 0), new Vec2d(10, 0) }
                }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            // z=0 лежит на границе клеток; центры ближайших рядов должны попасть в капсулу.
            Assert.AreNotEqual(0, FlagAt(r, 0, 0.25) & (ushort)CellFlags.Road);
            Assert.AreNotEqual(0, FlagAt(r, 0, -0.25) & (ushort)CellFlags.Road);
        }

        [Test]
        public void Review03_RingVerticesOnCellCenters_PredictableFill()
        {
            // Вершины ровно в центрах клеток; внутренняя клетка и повторный прогон совпадают.
            var grid = CityGrid.FromBBox(new Vec2d(0, 0), new Vec2d(10, 10), Cell);
            var ring = new[]
            {
                new Vec2d(2.75, 2.75),
                new Vec2d(4.75, 2.75),
                new Vec2d(4.75, 4.75),
                new Vec2d(2.75, 4.75),
                new Vec2d(2.75, 2.75)
            };

            var flags = new ushort[grid.CellCount];
            RasterPrimitives.FillPolygonEvenOddA(grid, flags, new[] { ring }, (ushort)CellFlags.Green);
            int n1 = CountBits(flags, (ushort)CellFlags.Green);

            var flags2 = new ushort[grid.CellCount];
            RasterPrimitives.FillPolygonEvenOddA(grid, flags2, new[] { ring }, (ushort)CellFlags.Green);
            Assert.AreEqual(n1, CountBits(flags2, (ushort)CellFlags.Green));

            grid.WorldToCell(new Vec2d(3.75, 3.75), out int mi, out int mj);
            Assert.AreNotEqual(0, flags[grid.Index(mi, mj)] & (ushort)CellFlags.Green);
            Assert.AreEqual(16, n1, "2×2 m square ⇒ 4×4 marked centers (mode A)");
        }

        [Test]
        public void Review04_BuildingHolesTouching_EvenOddCourtyard()
        {
            // Две дыры, соприкасающиеся по ребру; участок внутри обеих дыр — не Building.
            var model = EmptyModel();
            var outer = Rect(-6, -6, 6, 6);
            var holeA = Rect(-4, -2, -2, 2);
            var holeB = Rect(-2, -2, 0, 2);
            var pts = new Vec2d[outer.Length + holeA.Length + holeB.Length];
            Array.Copy(outer, 0, pts, 0, outer.Length);
            Array.Copy(holeA, 0, pts, outer.Length, holeA.Length);
            Array.Copy(holeB, 0, pts, outer.Length + holeA.Length, holeB.Length);
            model.areas = new[]
            {
                new Area
                {
                    osmId = 1,
                    kind = AreaKind.Building,
                    heightM = 12f,
                    buildingType = "yes",
                    points = pts,
                    ringStart = new[] { 0, outer.Length, outer.Length + holeA.Length }
                }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            Assert.AreEqual(0, FlagAt(r, -3, 0) & (ushort)CellFlags.Building);
            Assert.AreNotEqual(0, FlagAt(r, 4, 0) & (ushort)CellFlags.Building);
        }

        [Test]
        public void Review05_BarrierDiagonalThroughCellCorner_NoEightLeak()
        {
            var grid = CityGrid.FromBBox(new Vec2d(0, 0), new Vec2d(10, 10), Cell);
            var flags = new ushort[grid.CellCount];
            grid.WorldToCell(new Vec2d(2, 2), out int ci, out int cj);
            var a = grid.CellMin(ci, cj);
            var b = new Vec2d(a.x + Cell, a.z + Cell);
            var pts = new[] { a, b };

            RasterPrimitives.StampPolylineCapsuleB(grid, flags, pts, 0f, (ushort)CellFlags.Barrier);

            Assert.IsFalse(HasEightNeighborPairAcrossBarrier(grid, flags, pts[0], pts[1]),
                "диагональ через угол клетки не должна давать 8-протечку");
        }

        [Test]
        public void Review06_GateAtWallEndpoint()
        {
            var model = EmptyModel();
            model.barriers = new[]
            {
                new Barrier
                {
                    osmId = 1,
                    kind = BarrierKind.Wall,
                    points = new[] { new Vec2d(-8, 0), new Vec2d(8, 0) }
                }
            };
            model.pointFeatures = new[]
            {
                new PointFeature { osmId = 2, kind = PointKind.Gate, position = new Vec2d(8, 0) }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            ushort atEnd = FlagAt(r, 8, 0);
            ushort onWall = FlagAt(r, 0, 0);
            Assert.AreNotEqual(0, atEnd & (ushort)CellFlags.Gate);
            Assert.AreEqual(0, atEnd & (ushort)CellFlags.Barrier);
            Assert.AreNotEqual(0, onWall & (ushort)CellFlags.Barrier);
        }

        [Test]
        public void Review07_BridgeOverBridge_HigherLayerReplacesLower()
        {
            var model = EmptyModel();
            model.roads = new[]
            {
                new Road
                {
                    osmId = 1,
                    widthM = 4f,
                    isBridge = true,
                    layer = 1,
                    points = new[] { new Vec2d(-10, 0), new Vec2d(10, 0) }
                },
                new Road
                {
                    osmId = 2,
                    widthM = 6f,
                    isBridge = true,
                    layer = 2,
                    points = new[] { new Vec2d(-10, 0), new Vec2d(10, 0) }
                }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            ushort f = FlagAt(r, 0, 0);
            Assert.AreNotEqual(0, f & (ushort)CellFlags.Bridge);
            Assert.AreNotEqual(0, f & (ushort)CellFlags.Road);
            // Верхний мост шире — должны быть клетки только с верхним классом, без «протечки» воды ниже.
            Assert.Greater(r.stats.cellsUnderBridges, 0);
        }

        [Test]
        public void Review08_BuildingPassageClippedAtGridEdge()
        {
            var model = EmptyModel(16);
            model.areas = new[] { Building(10, Rect(-4, -4, 4, 4)) };
            model.walkways = new[]
            {
                new Walkway
                {
                    osmId = 20,
                    kind = WalkwayKind.Footway,
                    widthM = 2f,
                    isBuildingPassage = true,
                    points = new[] { new Vec2d(-12, 0), new Vec2d(12, 0) }
                }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            Assert.AreEqual(0, FlagAt(r, 0, 0) & (ushort)CellFlags.Building);
            Assert.AreNotEqual(0, FlagAt(r, 0, 0) & (ushort)CellFlags.BuildingPassage);
            Assert.AreNotEqual(0, FlagAt(r, 0, 0) & (ushort)CellFlags.Sidewalk);
        }

        [Test]
        public void Review09_BarrierZeroWidth_StillBlocks_ModeB()
        {
            var model = EmptyModel();
            model.barriers = new[]
            {
                new Barrier
                {
                    osmId = 1,
                    kind = BarrierKind.Fence,
                    points = new[] { new Vec2d(-6, 5), new Vec2d(6, 5) }
                }
            };

            var r = CityRasterizer.Bake(model, CfgZeroBarrier());
            Assert.AreNotEqual(0, FlagAt(r, 0, 5) & (ushort)CellFlags.Barrier);
        }

        [Test]
        public void Review10_ObjectEntirelyOutsideGrid_Counted()
        {
            var model = EmptyModel(20);
            model.areas = new[]
            {
                new Area
                {
                    osmId = 99,
                    kind = AreaKind.Green,
                    points = Rect(50, 50, 60, 60),
                    ringStart = new[] { 0 }
                }
            };
            model.roads = new[]
            {
                new Road
                {
                    osmId = 1,
                    widthM = 2f,
                    points = new[] { new Vec2d(100, 100), new Vec2d(105, 100) }
                }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            Assert.AreEqual(0, CountFlag(r, CellFlags.Green));
            Assert.GreaterOrEqual(r.stats.objectsOutsideGrid, 2);
        }

        [Test]
        public void Review11_RoadClearsWaterWithoutBridge_StatIncrements()
        {
            var model = EmptyModel();
            model.waterways = new[]
            {
                new Waterway
                {
                    osmId = 1,
                    kind = WaterwayKind.Stream,
                    points = new[] { new Vec2d(-10, 0), new Vec2d(10, 0) }
                }
            };
            model.roads = new[]
            {
                new Road
                {
                    osmId = 2,
                    widthM = 4f,
                    isBridge = false,
                    points = new[] { new Vec2d(-10, 0), new Vec2d(10, 0) }
                }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            ushort f = FlagAt(r, 0, 0);
            Assert.AreNotEqual(0, f & (ushort)CellFlags.Road);
            Assert.AreEqual(0, f & (ushort)CellFlags.Water);
            Assert.Greater(r.stats.waterClearedByLinear, 0);
        }

        [Test]
        public void Review12_SharedLivingStreet_SetsSharedBit()
        {
            var model = EmptyModel();
            model.roads = new[]
            {
                new Road
                {
                    osmId = 1,
                    widthM = 5f,
                    isShared = true,
                    points = new[] { new Vec2d(-10, 0), new Vec2d(10, 0) }
                }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            ushort f = FlagAt(r, 0, 0);
            Assert.AreNotEqual(0, f & (ushort)CellFlags.Road);
            Assert.AreNotEqual(0, f & (ushort)CellFlags.Shared);
        }

        [Test]
        public void Review13_EntranceAndTrafficSignal_NotRasterized()
        {
            var model = EmptyModel();
            model.pointFeatures = new[]
            {
                new PointFeature { osmId = 1, kind = PointKind.Entrance, position = new Vec2d(0, 0) },
                new PointFeature { osmId = 2, kind = PointKind.TrafficSignal, position = new Vec2d(2, 0) }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            Assert.AreEqual(0, FlagAt(r, 0, 0));
            Assert.AreEqual(0, FlagAt(r, 2, 0));
        }

        [Test]
        public void Review14_BuildingHalfOutsideGrid_PartialFill()
        {
            var model = EmptyModel(20);
            model.areas = new[]
            {
                Building(1, Rect(-12, -2, 0, 2))
            };

            var r = CityRasterizer.Bake(model, Cfg());
            int stamped = CountFlag(r, CellFlags.Building);
            Assert.Greater(stamped, 0);
            // Полная заливка 12×4 м дала бы 24×8 = 192 клетки; в сетке 20×20 м видна только часть.
            Assert.Less(stamped, 24 * 8);
            r.grid.WorldToCell(new Vec2d(-11, 0), out int oi, out int oj);
            if (r.grid.InBounds(oi, oj))
            {
                Assert.AreEqual(0, r.flags[r.grid.Index(oi, oj)] & (ushort)CellFlags.Building,
                    "west of grid origin must stay empty");
            }

            Assert.AreNotEqual(0, FlagAt(r, -5, 0) & (ushort)CellFlags.Building);
        }

        [Test]
        public void Review15_WaterwayTunnel_NotInRaster()
        {
            var model = EmptyModel();
            model.waterways = new[]
            {
                new Waterway
                {
                    osmId = 1,
                    kind = WaterwayKind.River,
                    isTunnel = true,
                    points = new[] { new Vec2d(-10, 0), new Vec2d(10, 0) }
                }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            Assert.AreEqual(0, CountFlag(r, CellFlags.Water));
        }

        [Test]
        public void Review16_BridgeClearsBarrierUnderneath()
        {
            var model = EmptyModel();
            model.barriers = new[]
            {
                new Barrier
                {
                    osmId = 1,
                    kind = BarrierKind.Wall,
                    points = new[] { new Vec2d(-10, 0), new Vec2d(10, 0) }
                }
            };
            model.walkways = new[]
            {
                new Walkway
                {
                    osmId = 2,
                    kind = WalkwayKind.Footway,
                    widthM = 2f,
                    isBridge = true,
                    layer = 1,
                    points = new[] { new Vec2d(-10, 0), new Vec2d(10, 0) }
                }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            ushort f = FlagAt(r, 0, 0);
            Assert.AreNotEqual(0, f & (ushort)CellFlags.Bridge);
            Assert.AreEqual(0, f & (ushort)CellFlags.Barrier);
        }

        [Test]
        public void Review17_GreenHoleAtOuterBoundary_EvenOdd()
        {
            var model = EmptyModel();
            model.areas = new[]
            {
                new Area
                {
                    osmId = 1,
                    kind = AreaKind.Green,
                    points = ConcatRings(Rect(-8, -8, 8, 8), Rect(-8, -2, -4, 2)),
                    ringStart = new[] { 0, 5 }
                }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            Assert.AreEqual(0, FlagAt(r, -6, 0) & (ushort)CellFlags.Green);
            Assert.AreNotEqual(0, FlagAt(r, 6, 0) & (ushort)CellFlags.Green);
        }

        [Test]
        public void Review18_RailwayClearsWater_WithoutBridge()
        {
            var model = EmptyModel();
            model.areas = new[]
            {
                new Area { osmId = 1, kind = AreaKind.Water, points = Rect(-5, -5, 5, 5), ringStart = new[] { 0 } }
            };
            model.railways = new[]
            {
                new Railway { osmId = 2, points = new[] { new Vec2d(-10, 0), new Vec2d(10, 0) } }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            Assert.AreNotEqual(0, FlagAt(r, 0, 0) & (ushort)CellFlags.Rail);
            Assert.AreEqual(0, FlagAt(r, 0, 0) & (ushort)CellFlags.Water);
        }

        static Vec2d[] ConcatRings(Vec2d[] outer, Vec2d[] hole)
        {
            var pts = new Vec2d[outer.Length + hole.Length];
            Array.Copy(outer, 0, pts, 0, outer.Length);
            Array.Copy(hole, 0, pts, outer.Length, hole.Length);
            return pts;
        }

        static bool HasEightNeighborPairAcrossBarrier(CityGrid grid, ushort[] flags, Vec2d segA, Vec2d segB)
        {
            ushort bit = (ushort)CellFlags.Barrier;
            int[] di = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dj = { -1, -1, -1, 0, 0, 1, 1, 1 };

            for (int j = 0; j < grid.h; j++)
            {
                for (int i = 0; i < grid.w; i++)
                {
                    if ((flags[grid.Index(i, j)] & bit) != 0)
                    {
                        continue;
                    }

                    Vec2d c0 = grid.CellCenter(i, j);
                    for (int k = 0; k < 8; k++)
                    {
                        int ni = i + di[k];
                        int nj = j + dj[k];
                        if (!grid.InBounds(ni, nj) || (flags[grid.Index(ni, nj)] & bit) != 0)
                        {
                            continue;
                        }

                        if (ni < i || (ni == i && nj <= j))
                        {
                            continue;
                        }

                        Vec2d c1 = grid.CellCenter(ni, nj);
                        if (SegmentsProperlyIntersect(c0, c1, segA, segB))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        static bool SegmentsProperlyIntersect(Vec2d p, Vec2d q, Vec2d a, Vec2d b)
        {
            double d1 = Cross(a, b, p);
            double d2 = Cross(a, b, q);
            double d3 = Cross(p, q, a);
            double d4 = Cross(p, q, b);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                   ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        static double Cross(Vec2d a, Vec2d b, Vec2d p) =>
            (b.x - a.x) * (p.z - a.z) - (b.z - a.z) * (p.x - a.x);
    }
}