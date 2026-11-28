using System;
using System.Collections.Generic;
using NUnit.Framework;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    /// <summary>
    /// Tram is stamped after railways and before roads (ADR-002 v1.3). Red before the raster step.
    /// </summary>
    public class TramRasterTests
    {
        const float Cell = 0.5f;

        static CityBakeConfig Cfg() => new CityBakeConfig { cellSize = Cell };

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

        static Railway Tram(long id, bool tunnel, bool bridge, params Vec2d[] points)
        {
            return new Railway
            {
                osmId = id,
                kind = RailwayKind.Tram,
                isTunnel = tunnel,
                isBridge = bridge,
                points = points
            };
        }

        static ushort FlagAt(RasterBakeResult r, double x, double z)
        {
            r.grid.WorldToCell(new Vec2d(x, z), out int i, out int j);
            Assert.IsTrue(r.grid.InBounds(i, j), $"out of bounds ({x},{z}) -> ({i},{j})");
            return r.flags[r.grid.Index(i, j)];
        }

        static int ModeACells(CityGrid grid, Vec2d[] points, float width)
        {
            var seen = new HashSet<int>();
            RasterPrimitives.ForEachPolylineCapsuleA(grid, points, width, (i, j) => seen.Add(grid.Index(i, j)));
            return seen.Count;
        }

        [Test]
        public void SurfaceTram_StampedModeA_NotRail()
        {
            var points = new[] { new Vec2d(-10, 0), new Vec2d(10, 0) };
            var model = EmptyModel();
            model.railways = new[] { Tram(1, false, false, points) };

            RasterBakeResult baked = CityRasterizer.Bake(model, Cfg());
            ushort mid = FlagAt(baked, 0, 0);
            Assert.AreNotEqual(0, mid & (ushort)CellFlags.Tram);
            Assert.AreEqual(0, mid & (ushort)CellFlags.Rail);
            Assert.AreEqual(0, FlagAt(baked, 0, 3) & (ushort)CellFlags.Tram);
            Assert.AreEqual(1, baked.stats.tramStamped);
            Assert.AreEqual(0, baked.stats.tramSkipped);
            Assert.AreEqual(ModeACells(baked.grid, points, Cfg().tramTrackWidth), baked.stats.tramCells);
            Assert.AreEqual(0, baked.stats.cellsByBit[6]);
        }

        [Test]
        public void TunnelTram_Skipped_NotStamped()
        {
            var model = EmptyModel();
            model.railways = new[] { Tram(1, true, false, new Vec2d(-10, 0), new Vec2d(10, 0)) };
            RasterBakeResult baked = CityRasterizer.Bake(model, Cfg());
            Assert.AreEqual(1, baked.stats.tramSkipped);
            Assert.AreEqual(0, baked.stats.tramStamped);
            Assert.AreEqual(0, baked.stats.tramCells);
            Assert.AreEqual(0, FlagAt(baked, 0, 0) & (ushort)CellFlags.Tram);
        }

        [Test]
        public void Building_ClearsTram()
        {
            var model = EmptyModel();
            model.railways = new[] { Tram(1, false, false, new Vec2d(-10, 0), new Vec2d(10, 0)) };
            model.areas = new[]
            {
                new Area
                {
                    osmId = 9,
                    kind = AreaKind.Building,
                    heightM = 12f,
                    buildingType = "yes",
                    points = new[]
                    {
                        new Vec2d(-3, -3), new Vec2d(3, -3), new Vec2d(3, 3), new Vec2d(-3, 3), new Vec2d(-3, -3)
                    },
                    ringStart = new[] { 0 }
                }
            };

            ushort mid = FlagAt(CityRasterizer.Bake(model, Cfg()), 0, 0);
            Assert.AreNotEqual(0, mid & (ushort)CellFlags.Building);
            Assert.AreEqual(0, mid & (ushort)CellFlags.Tram);
            Assert.AreNotEqual(0, FlagAt(CityRasterizer.Bake(model, Cfg()), 8, 0) & (ushort)CellFlags.Tram);
        }

        [Test]
        public void RoadAndCrossing_KeepTram()
        {
            var model = EmptyModel();
            model.railways = new[] { Tram(1, false, false, new Vec2d(-12, 0), new Vec2d(12, 0)) };
            model.roads = new[]
            {
                new Road
                {
                    osmId = 2,
                    widthM = 6f,
                    points = new[] { new Vec2d(0, -12), new Vec2d(0, 12) }
                }
            };
            model.walkways = new[]
            {
                new Walkway
                {
                    osmId = 3,
                    kind = WalkwayKind.Crossing,
                    widthM = 4f,
                    points = new[] { new Vec2d(6, -8), new Vec2d(6, 8) }
                }
            };

            RasterBakeResult baked = CityRasterizer.Bake(model, Cfg());
            ushort road = FlagAt(baked, 0, 0);
            Assert.AreNotEqual(0, road & (ushort)CellFlags.Tram);
            Assert.AreNotEqual(0, road & (ushort)CellFlags.Road);
            Assert.AreEqual(0, road & (ushort)CellFlags.Rail);

            ushort cross = FlagAt(baked, 6, 0);
            Assert.AreNotEqual(0, cross & (ushort)CellFlags.Tram);
            Assert.AreNotEqual(0, cross & (ushort)CellFlags.Crosswalk);
        }

        [Test]
        public void TramOrder_DoesNotChangeDataHash()
        {
            Railway a = Tram(1, false, false, new Vec2d(-10, -4), new Vec2d(10, -4));
            Railway b = Tram(2, false, false, new Vec2d(-10, 4), new Vec2d(10, 4));
            var first = EmptyModel();
            first.railways = new[] { a, b };
            var second = EmptyModel();
            second.railways = new[] { b, a };
            RasterBakeResult baked = CityRasterizer.Bake(first, Cfg());
            ulong h2 = CityRasterizer.Bake(second, Cfg()).dataHash;
            Assert.Greater(baked.stats.tramCells, 0);
            Assert.AreEqual(baked.dataHash, h2);
        }

        [Test]
        public void Rail_StillRail_TramBridge_KeepsTramWithoutRail()
        {
            var model = EmptyModel();
            model.railways = new[]
            {
                Tram(1, false, true, new Vec2d(-10, 0), new Vec2d(10, 0)),
                new Railway
                {
                    osmId = 2,
                    kind = RailwayKind.Rail,
                    points = new[] { new Vec2d(0, -10), new Vec2d(0, 10) }
                }
            };

            RasterBakeResult baked = CityRasterizer.Bake(model, Cfg());
            ushort tram = FlagAt(baked, 8, 0);
            Assert.AreNotEqual(0, tram & (ushort)CellFlags.Tram);
            Assert.AreEqual(0, tram & (ushort)CellFlags.Rail);
            ushort rail = FlagAt(baked, 0, 8);
            Assert.AreNotEqual(0, rail & (ushort)CellFlags.Rail);
            Assert.AreEqual(0, rail & (ushort)CellFlags.Tram);
        }
    }
}
