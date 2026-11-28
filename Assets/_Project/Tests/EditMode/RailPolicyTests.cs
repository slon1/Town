using System;
using NUnit.Framework;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    /// <summary>
    /// ADR-002 v1.2 rails and ADR-001 v1.3 RailwayKind (4B-tests, red before implementation).
    /// </summary>
    public class RailPolicyTests
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

        static ushort FlagAt(RasterBakeResult r, double x, double z)
        {
            r.grid.WorldToCell(new Vec2d(x, z), out int i, out int j);
            Assert.IsTrue(r.grid.InBounds(i, j), $"out of bounds ({x},{z}) -> ({i},{j})");
            return r.flags[r.grid.Index(i, j)];
        }

        static Railway Rail(long id, RailwayKind kind, bool bridge, Vec2d a, Vec2d b)
        {
            return new Railway
            {
                osmId = id,
                kind = kind,
                isBridge = bridge,
                points = new[] { a, b }
            };
        }

        [Test]
        public void Parser_RailwayTag_MapsToKind()
        {
            Assert.AreEqual(RailwayKind.Rail, RailwayKindParser.Parse("rail"));
            Assert.AreEqual(RailwayKind.LightRail, RailwayKindParser.Parse("light_rail"));
            Assert.AreEqual(RailwayKind.Subway, RailwayKindParser.Parse("SUBWAY"));
            Assert.AreEqual(RailwayKind.Tram, RailwayKindParser.Parse("tram"));
            Assert.AreEqual(RailwayKind.NarrowGauge, RailwayKindParser.Parse("narrow_gauge"));
            Assert.AreEqual(RailwayKind.Other, RailwayKindParser.Parse("monorail"));
            Assert.AreEqual(RailwayKind.Other, RailwayKindParser.Parse(null));
            Assert.AreEqual(RailwayKind.Other, RailwayKindParser.Parse("  "));
        }

        [Test]
        public void Builder_TramWay_StaysInModelWithKind()
        {
            const string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<osm version=""0.6"">
 <bounds minlat=""0"" minlon=""0"" maxlat=""0.01"" maxlon=""0.01""/>
 <node id=""1"" lat=""0.001"" lon=""0.001""/>
 <node id=""2"" lat=""0.001"" lon=""0.002""/>
 <way id=""77"">
  <nd ref=""1""/><nd ref=""2""/>
  <tag k=""railway"" v=""tram""/>
 </way>
</osm>";
            CityModel model = CityModelBuilder.Build(OsmXmlParser.ParseXml(xml), new CityBakeConfig());
            Assert.AreEqual(1, model.railways.Length);
            Assert.AreEqual(77L, model.railways[0].osmId);
            Assert.AreEqual(RailwayKind.Tram, model.railways[0].kind);
        }

        [Test]
        public void Hasher_RailwayKind_ChangesModelDataHash()
        {
            CityModel rail = ModelWithRailway(RailwayKind.Rail);
            CityModel tram = ModelWithRailway(RailwayKind.Tram);
            Assert.AreNotEqual(
                CityModelHasher.ComputeDataHash(rail),
                CityModelHasher.ComputeDataHash(tram));
        }

        [Test]
        public void RoadCrossingRails_ClearsRail_SidesStay()
        {
            var model = EmptyModel();
            model.railways = new[]
            {
                Rail(1, RailwayKind.Rail, false, new Vec2d(-14, 0), new Vec2d(14, 0))
            };
            model.roads = new[]
            {
                new Road
                {
                    osmId = 2,
                    widthM = 4f,
                    points = new[] { new Vec2d(0, -14), new Vec2d(0, 14) }
                }
            };

            RasterBakeResult baked = CityRasterizer.Bake(model, Cfg());
            ushort mid = FlagAt(baked, 0, 0);
            Assert.AreNotEqual(0, mid & (ushort)CellFlags.Road);
            Assert.AreEqual(0, mid & (ushort)CellFlags.Rail);

            ushort east = FlagAt(baked, 8, 0);
            ushort west = FlagAt(baked, -8, 0);
            Assert.AreNotEqual(0, east & (ushort)CellFlags.Rail);
            Assert.AreEqual(0, east & (ushort)CellFlags.Road);
            Assert.AreNotEqual(0, west & (ushort)CellFlags.Rail);
            Assert.AreEqual(0, west & (ushort)CellFlags.Road);

            ushort north = FlagAt(baked, 0, 8);
            Assert.AreNotEqual(0, north & (ushort)CellFlags.Road);
            Assert.AreEqual(0, north & (ushort)CellFlags.Rail);
        }

        [Test]
        public void WalkwayAndCrossing_ClearRail()
        {
            var model = EmptyModel();
            model.railways = new[]
            {
                Rail(1, RailwayKind.Rail, false, new Vec2d(-14, 0), new Vec2d(14, 0))
            };
            model.walkways = new[]
            {
                new Walkway
                {
                    osmId = 2,
                    kind = WalkwayKind.Footway,
                    widthM = 2f,
                    points = new[] { new Vec2d(-6, -10), new Vec2d(-6, 10) }
                },
                new Walkway
                {
                    osmId = 3,
                    kind = WalkwayKind.Crossing,
                    widthM = 3f,
                    points = new[] { new Vec2d(6, -10), new Vec2d(6, 10) }
                }
            };

            RasterBakeResult baked = CityRasterizer.Bake(model, Cfg());
            ushort foot = FlagAt(baked, -6, 0);
            Assert.AreNotEqual(0, foot & (ushort)CellFlags.Sidewalk);
            Assert.AreEqual(0, foot & (ushort)CellFlags.Rail);

            ushort cross = FlagAt(baked, 6, 0);
            Assert.AreNotEqual(0, cross & (ushort)CellFlags.Crosswalk);
            Assert.AreEqual(0, cross & (ushort)CellFlags.Rail);

            ushort side = FlagAt(baked, 0, 0);
            Assert.AreNotEqual(0, side & (ushort)CellFlags.Rail);
            Assert.AreEqual(0, side & (ushort)CellFlags.Sidewalk);
            Assert.AreEqual(0, side & (ushort)CellFlags.Crosswalk);
        }

        [Test]
        public void RailBridge_StaysRailBridge_Obstacle()
        {
            var model = EmptyModel();
            model.railways = new[]
            {
                Rail(1, RailwayKind.Rail, true, new Vec2d(-12, 0), new Vec2d(12, 0))
            };
            model.roads = new[]
            {
                new Road
                {
                    osmId = 2,
                    widthM = 4f,
                    points = new[] { new Vec2d(0, -12), new Vec2d(0, 12) }
                }
            };

            RasterBakeResult baked = CityRasterizer.Bake(model, Cfg());
            ushort mid = FlagAt(baked, 0, 0);
            Assert.AreNotEqual(0, mid & (ushort)CellFlags.Rail);
            Assert.AreNotEqual(0, mid & (ushort)CellFlags.Bridge);
            Assert.AreEqual(0, mid & (ushort)CellFlags.Road);
            Assert.AreEqual(CityAccess.Obstacle, AccessClassifier.Classify((CellFlags)mid));

            ushort side = FlagAt(baked, 8, 0);
            Assert.AreNotEqual(0, side & (ushort)CellFlags.Rail);
            Assert.AreNotEqual(0, side & (ushort)CellFlags.Bridge);
        }

        [Test]
        public void Tram_StampedSeparately_RailUnchanged()
        {
            var model = EmptyModel();
            model.railways = new[]
            {
                Rail(1, RailwayKind.Tram, false, new Vec2d(-12, 0), new Vec2d(12, 0)),
                Rail(2, RailwayKind.Rail, false, new Vec2d(10, -12), new Vec2d(10, 12))
            };

            RasterBakeResult baked = CityRasterizer.Bake(model, Cfg());
            Assert.AreEqual(0, baked.stats.tramSkipped);
            Assert.AreEqual(1, baked.stats.tramStamped);
            Assert.AreEqual(RailwayKind.Tram, model.railways[0].kind);
            ushort tram = FlagAt(baked, 0, 0);
            Assert.AreNotEqual(0, tram & (ushort)CellFlags.Tram);
            Assert.AreEqual(0, tram & (ushort)CellFlags.Rail);
            ushort rail = FlagAt(baked, 10, 8);
            Assert.AreNotEqual(0, rail & (ushort)CellFlags.Rail);
            Assert.AreEqual(0, rail & (ushort)CellFlags.Tram);
        }

        [Test]
        public void RailClearedByLinear_MatchesCellsRemoved_NoRoadRailWithoutBridge()
        {
            var rails = new[]
            {
                Rail(1, RailwayKind.Rail, false, new Vec2d(-14, 0), new Vec2d(14, 0))
            };
            var only = EmptyModel();
            only.railways = rails;
            var both = EmptyModel();
            both.railways = rails;
            both.roads = new[]
            {
                new Road
                {
                    osmId = 2,
                    widthM = 4f,
                    points = new[] { new Vec2d(0, -14), new Vec2d(0, 14) }
                }
            };

            RasterBakeResult before = CityRasterizer.Bake(only, Cfg());
            RasterBakeResult after = CityRasterizer.Bake(both, Cfg());
            int removed = 0;
            for (int i = 0; i < before.flags.Length; i++)
            {
                bool had = (before.flags[i] & (ushort)CellFlags.Rail) != 0;
                bool has = (after.flags[i] & (ushort)CellFlags.Rail) != 0;
                if (had && !has)
                {
                    removed++;
                }

                ushort f = after.flags[i];
                if ((f & (ushort)CellFlags.Road) != 0 && (f & (ushort)CellFlags.Rail) != 0)
                {
                    Assert.AreNotEqual(0, f & (ushort)CellFlags.Bridge, "Road|Rail without Bridge");
                }
            }

            Assert.Greater(removed, 0);
            Assert.AreEqual(removed, after.stats.railClearedByLinear);
        }

        [Test]
        public void InputOrder_DoesNotChangeDataHash()
        {
            Railway tram = Rail(3, RailwayKind.Tram, false, new Vec2d(-4, 4), new Vec2d(4, 4));
            Railway rail = Rail(1, RailwayKind.Rail, false, new Vec2d(-10, 0), new Vec2d(10, 0));
            var roadA = new Road
            {
                osmId = 8,
                widthM = 4f,
                points = new[] { new Vec2d(0, -8), new Vec2d(0, 8) }
            };
            var roadB = new Road
            {
                osmId = 4,
                widthM = 3f,
                points = new[] { new Vec2d(-8, 2), new Vec2d(8, 2) }
            };

            var first = EmptyModel();
            first.railways = new[] { tram, rail };
            first.roads = new[] { roadA, roadB };
            var second = EmptyModel();
            second.railways = new[] { rail, tram };
            second.roads = new[] { roadB, roadA };

            ulong a = CityRasterizer.Bake(first, Cfg()).dataHash;
            ulong b = CityRasterizer.Bake(second, Cfg()).dataHash;
            ulong c = CityRasterizer.Bake(first, Cfg()).dataHash;
            Assert.AreEqual(a, b);
            Assert.AreEqual(a, c);
        }

        static CityModel ModelWithRailway(RailwayKind kind)
        {
            return new CityModel
            {
                meta = new CityModelMeta(),
                railways = new[]
                {
                    new Railway
                    {
                        osmId = 5,
                        kind = kind,
                        points = new[] { new Vec2d(0, 0), new Vec2d(1, 0) }
                    }
                }
            };
        }
    }
}
