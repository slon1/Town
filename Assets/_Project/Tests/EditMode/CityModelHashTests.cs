using System.IO;
using NUnit.Framework;
using UnityEngine;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    public class CityModelHashTests
    {
        /// <summary>
        /// Golden FNV-1a for <see cref="CreateFixtureModel"/> — locked after first compute in 1B.
        /// </summary>
        public const ulong FixtureDataHash = 0x7C5E533F40D6361BUL;

        [Test]
        public void CityBakeConfig_CanonicalBytes_StableAndNonEmpty()
        {
            var a = new CityBakeConfig();
            var b = new CityBakeConfig();
            byte[] ba = a.CanonicalBytes();
            byte[] bb = b.CanonicalBytes();
            Assert.Greater(ba.Length, 0);
            Assert.AreEqual(ba, bb);
            Assert.AreEqual(a.ComputeSettingsHash(), b.ComputeSettingsHash());
        }

        [Test]
        public void CityBakeConfig_Hash_IndependentOfTableOrder()
        {
            var a = new CityBakeConfig();
            var b = new CityBakeConfig();
            System.Array.Reverse(b.roadWidths);
            System.Array.Reverse(b.highwayRoadTags);
            Assert.AreEqual(a.ComputeSettingsHash(), b.ComputeSettingsHash());
        }

        [Test]
        public void CityModel_DataHash_StableAcrossCalls()
        {
            CityModel model = CreateFixtureModel();
            ulong h1 = CityModelHasher.ComputeDataHash(model);
            ulong h2 = CityModelHasher.ComputeDataHash(model);
            Assert.AreEqual(h1, h2);
        }

        [Test]
        public void CityModel_DataHash_IndependentOfEntityOrder()
        {
            CityModel a = CreateFixtureModel();
            CityModel b = CreateFixtureModel();
            System.Array.Reverse(b.roads);
            System.Array.Reverse(b.pointFeatures);
            CityModelHasher.SortEntities(a);
            CityModelHasher.SortEntities(b);
            Assert.AreEqual(CityModelHasher.ComputeDataHash(a), CityModelHasher.ComputeDataHash(b));
        }

        [Test]
        public void CityModel_DataHash_MatchesGoldenFixture()
        {
            CityModel model = CreateFixtureModel();
            CityModelHasher.SortEntities(model);
            ulong hash = CityModelHasher.ComputeDataHash(model);
            Assert.AreEqual(FixtureDataHash, hash,
                $"Update FixtureDataHash to 0x{hash:X16} if canonical format intentionally changed.");
        }

        [Test]
        public void Projection_EastIsPositiveX_NorthIsPositiveZ()
        {
            var proj = new LocalEquirectangularProjection(52.555, 13.375);
            Vec2d east = proj.Project(52.555, 13.376);
            Vec2d north = proj.Project(52.556, 13.375);
            Assert.Greater(east.x, 0);
            Assert.AreEqual(0, east.z, 1e-6);
            Assert.Greater(north.z, 0);
            Assert.AreEqual(0, north.x, 1e-6);
        }

        [Test]
        public void Projection_KnownPoint_Within5cm()
        {
            var proj = new LocalEquirectangularProjection(0, 0);
            Vec2d p = proj.Project(0.001, 0.001);
            double expected = LocalEquirectangularProjection.EarthRadiusM * (0.001 * System.Math.PI / 180.0);
            Assert.AreEqual(expected, p.x, 0.05);
            Assert.AreEqual(expected, p.z, 0.05);
        }

        [Test]
        public void Parser_ReadsBoundsWaysMultipolygon_AndSkipsInvisible()
        {
            const string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<osm version=""0.6"">
 <bounds minlat=""52.55"" minlon=""13.37"" maxlat=""52.56"" maxlon=""13.38""/>
 <node id=""1"" visible=""true"" lat=""52.551"" lon=""13.371""/>
 <node id=""2"" visible=""false"" lat=""52.552"" lon=""13.372""/>
 <node id=""3"" lat=""52.553"" lon=""13.373"">
  <tag k=""name"" v=""Café Straße""/>
 </node>
 <way id=""10"" visible=""true"">
  <nd ref=""1""/>
  <nd ref=""3""/>
  <nd ref=""999""/>
  <tag k=""highway"" v=""residential""/>
 </way>
 <relation id=""20"">
  <member type=""way"" ref=""10"" role=""outer""/>
  <member type=""way"" ref=""888"" role=""inner""/>
  <tag k=""type"" v=""multipolygon""/>
  <tag k=""building"" v=""yes""/>
 </relation>
 <relation id=""21"">
  <tag k=""type"" v=""route""/>
 </relation>
</osm>";

            OsmDocument doc = OsmXmlParser.ParseXml(xml);
            Assert.IsTrue(doc.Stats.HasBounds);
            Assert.AreEqual(52.55, doc.Stats.MinLat, 1e-9);
            Assert.AreEqual(2, doc.Stats.NodeCount);
            Assert.AreEqual(1, doc.Stats.SkippedInvisible);
            Assert.AreEqual(1, doc.Stats.WayCount);
            Assert.AreEqual(2, doc.Stats.RelationCount);
            Assert.AreEqual(1, doc.Stats.MultipolygonCount);
            Assert.AreEqual(1, doc.Stats.MissingWayMemberRefs);
            Assert.AreEqual("Café Straße", doc.Nodes[3].Tags.Get("name"));

            var proj = doc.CreateProjection();
            Vec2d[] pts = doc.ProjectWayPoints(doc.Ways[10], proj);
            Assert.AreEqual(2, pts.Length);
            Assert.AreEqual(1, doc.Stats.MissingNodeRefs);
        }

        [Test]
        public void Parser_NoBounds_ComputesFromNodes()
        {
            const string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<osm version=""0.6"">
 <node id=""1"" lat=""10"" lon=""20""/>
 <node id=""2"" lat=""11"" lon=""21""/>
</osm>";
            OsmDocument doc = OsmXmlParser.ParseXml(xml);
            Assert.IsTrue(doc.Stats.HasBounds);
            Assert.IsTrue(doc.Stats.BoundsComputedFromNodes);
            Assert.AreEqual(10, doc.Stats.MinLat, 1e-9);
            Assert.AreEqual(21, doc.Stats.MaxLon, 1e-9);
        }

        [Test]
        public void Parser_MapOsm_ReadsWithoutThrow()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "Source/map.osm"));
            Assert.IsTrue(File.Exists(path), "map.osm missing: " + path);
            OsmDocument doc = OsmXmlParser.ParseFile(path);
            Assert.Greater(doc.Stats.NodeCount, 1000);
            Assert.Greater(doc.Stats.WayCount, 100);
            Assert.Greater(doc.Stats.RelationCount, 0);
            Assert.IsTrue(doc.Stats.HasBounds);
            Assert.Greater(doc.Stats.ElapsedMs, 0);
        }

        public static CityModel CreateFixtureModel()
        {
            return new CityModel
            {
                meta = new CityModelMeta
                {
                    sourceName = "fixture.osm",
                    bboxMinLat = 52.55,
                    bboxMinLon = 13.37,
                    bboxMaxLat = 52.56,
                    bboxMaxLon = 13.38,
                    lat0 = 52.555,
                    lon0 = 13.375,
                    sizeXM = 680.0,
                    sizeZM = 480.0,
                    settingsHash = 0xABCDEF0123456789UL,
                    dataHash = 0
                },
                roads = new[]
                {
                    new Road
                    {
                        osmId = 20,
                        highwayClass = HighwayClass.Residential,
                        widthM = 6.5f,
                        layer = 0,
                        isBridge = false,
                        isTunnel = false,
                        isBuildingPassage = false,
                        isShared = false,
                        sidewalkMode = SidewalkMode.Both,
                        points = new[] { new Vec2d(0, 0), new Vec2d(10, 0) }
                    },
                    new Road
                    {
                        osmId = 10,
                        highwayClass = HighwayClass.Primary,
                        widthM = 10f,
                        layer = 1,
                        isBridge = true,
                        isTunnel = false,
                        isBuildingPassage = false,
                        isShared = false,
                        sidewalkMode = SidewalkMode.None,
                        points = new[] { new Vec2d(1.5, 2.5), new Vec2d(3.5, 4.5) }
                    }
                },
                walkways = new[]
                {
                    new Walkway
                    {
                        osmId = 30,
                        kind = WalkwayKind.Crossing,
                        widthM = 3f,
                        layer = 0,
                        points = new[] { new Vec2d(0, 1), new Vec2d(0, 2) }
                    }
                },
                areas = new[]
                {
                    new Area
                    {
                        osmId = 40,
                        kind = AreaKind.Building,
                        heightM = 12f,
                        minHeightM = 0f,
                        buildingType = "yes",
                        points = new[]
                        {
                            new Vec2d(0, 0), new Vec2d(5, 0), new Vec2d(5, 5), new Vec2d(0, 5),
                            new Vec2d(1, 1), new Vec2d(1, 2), new Vec2d(2, 2), new Vec2d(2, 1)
                        },
                        ringStart = new[] { 0, 4 }
                    }
                },
                barriers = new[]
                {
                    new Barrier
                    {
                        osmId = 50,
                        kind = BarrierKind.Fence,
                        points = new[] { new Vec2d(8, 8), new Vec2d(9, 9) }
                    }
                },
                railways = System.Array.Empty<Railway>(),
                waterways = new[]
                {
                    new Waterway
                    {
                        osmId = 60,
                        kind = WaterwayKind.Canal,
                        isTunnel = false,
                        points = new[] { new Vec2d(-1, 0), new Vec2d(-1, 10) }
                    }
                },
                pointFeatures = new[]
                {
                    new PointFeature
                    {
                        osmId = 70,
                        kind = PointKind.Gate,
                        position = new Vec2d(2, 3)
                    }
                },
                stats = new CityModelStats()
            };
        }
    }
}
