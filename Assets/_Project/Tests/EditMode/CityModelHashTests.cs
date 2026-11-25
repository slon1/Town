using System.Collections.Generic;
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

        /// <summary>
        /// Golden settings hash for default <see cref="CityBakeConfig"/> (ADR-002 §7: +railwayWidth/+gateRadius).
        /// Was 0x24472141C5F413C9 before P2 (CityModel_map.asset).
        /// </summary>
        public const ulong DefaultSettingsHash = 0x09A4C90D0BFD89D7UL;

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
        public void CityBakeConfig_DefaultSettingsHash_MatchesGolden()
        {
            Assert.AreEqual(DefaultSettingsHash, new CityBakeConfig().ComputeSettingsHash(),
                "Update DefaultSettingsHash if CanonicalBytes intentionally changed (ADR-002 §7).");
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

        [Test]
        public void TagParser_WidthHeightFormats()
        {
            Assert.IsTrue(TagValueParser.TryParseMeters("6", out float a));
            Assert.AreEqual(6f, a);
            Assert.IsTrue(TagValueParser.TryParseMeters("6.5", out float b));
            Assert.AreEqual(6.5f, b);
            Assert.IsTrue(TagValueParser.TryParseMeters("6 m", out float c));
            Assert.AreEqual(6f, c);
            Assert.IsTrue(TagValueParser.TryParseMeters("6,5", out float d));
            Assert.AreEqual(6.5f, d);
        }

        [Test]
        public void RingOps_OuterCcw_HolesCw()
        {
            var outer = new List<Vec2d>
            {
                new Vec2d(0, 0), new Vec2d(0, 4), new Vec2d(4, 4), new Vec2d(4, 0), new Vec2d(0, 0)
            };
            Assert.IsTrue(RingOps.Normalize(outer, wantCcw: true));
            Assert.IsTrue(RingOps.IsCcw(outer));

            var hole = new List<Vec2d>
            {
                new Vec2d(1, 1), new Vec2d(2, 1), new Vec2d(2, 2), new Vec2d(1, 2), new Vec2d(1, 1)
            };
            Assert.IsTrue(RingOps.Normalize(hole, wantCcw: false));
            Assert.IsFalse(RingOps.IsCcw(hole));
        }

        [Test]
        public void Builder_MultipolygonWithHole_FromFixture()
        {
            CityModel model = BuildFromFixture("multipolygon_hole.osm.txt");
            Assert.AreEqual(1, model.areas.Length);
            Assert.AreEqual(AreaKind.Building, model.areas[0].kind);
            Assert.GreaterOrEqual(model.areas[0].ringStart.Length, 2);
        }

        [Test]
        public void Builder_MultipolygonTwoOuterWays_FromFixture()
        {
            CityModel model = BuildFromFixture("multipolygon_two_outers.osm.txt");
            Assert.AreEqual(1, model.areas.Length);
            Assert.AreEqual(AreaKind.Building, model.areas[0].kind);
            Assert.AreEqual(1, model.areas[0].ringStart.Length);
        }

        [Test]
        public void Builder_IncompleteMultipolygon_FromFixture()
        {
            CityModel model = BuildFromFixture("incomplete_multipolygon.osm.txt");
            Assert.GreaterOrEqual(model.stats.warningIncompleteMultipolygons, 1);
        }

        [Test]
        public void Builder_TunnelBridgeLayer_FromFixture()
        {
            CityModel model = BuildFromFixture("tunnel_bridge_layer.osm.txt");
            Walkway passage = System.Array.Find(model.walkways, w => w.osmId == 10);
            Assert.IsTrue(passage.isBuildingPassage);
            Assert.IsFalse(passage.isTunnel);
            Road tunnel = System.Array.Find(model.roads, r => r.osmId == 11);
            Assert.IsTrue(tunnel.isTunnel);
            Road bridge = System.Array.Find(model.roads, r => r.osmId == 12);
            Assert.IsTrue(bridge.isBridge);
            Assert.AreEqual(1, bridge.layer);
        }

        [Test]
        public void Builder_LivingTrackWaterwayBuildingPart_FromFixture()
        {
            CityModel model = BuildFromFixture("living_track_waterway_part.osm.txt");
            Road living = System.Array.Find(model.roads, r => r.osmId == 11);
            Assert.IsTrue(living.isShared);
            Road trackMotor = System.Array.Find(model.roads, r => r.osmId == 12);
            Assert.IsNotNull(trackMotor);
            Assert.AreEqual(3f, trackMotor.widthM);
            Assert.IsNull(System.Array.Find(model.roads, r => r.osmId == 13));
            Assert.IsNotNull(System.Array.Find(model.walkways, w => w.osmId == 13));
            Waterway canal = System.Array.Find(model.waterways, w => w.osmId == 14);
            Assert.IsNotNull(canal);
            Assert.AreEqual(WaterwayKind.Canal, canal.kind);
            Assert.GreaterOrEqual(model.stats.warningBuildingPartsIgnored, 1);
            Assert.IsNull(System.Array.Find(model.areas, a => a.osmId == 15));
            Assert.IsNotNull(System.Array.Find(model.areas, a => a.osmId == 16 && a.kind == AreaKind.Building));
        }

        [Test]
        public void Builder_Hash_XmlElementReorder_SameHash()
        {
            CityModel a = BuildFromFixture("hash_order_a.osm.txt", "hash.osm");
            CityModel b = BuildFromFixture("hash_order_b.osm.txt", "hash.osm");
            Assert.AreEqual(a.meta.dataHash, b.meta.dataHash);
            Assert.AreEqual(a.meta.dataHash, CityModelHasher.ComputeDataHash(a));
        }

        [Test]
        public void Builder_FootwayCrossing_Found()
        {
            const string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<osm version=""0.6"">
 <bounds minlat=""0"" minlon=""0"" maxlat=""1"" maxlon=""1""/>
 <node id=""1"" lat=""0.1"" lon=""0.1""/>
 <node id=""2"" lat=""0.1"" lon=""0.2""/>
 <way id=""10"">
  <nd ref=""1""/><nd ref=""2""/>
  <tag k=""highway"" v=""footway""/>
  <tag k=""footway"" v=""crossing""/>
 </way>
</osm>";
            CityModel model = CityModelBuilder.Build(OsmXmlParser.ParseXml(xml), new CityBakeConfig());
            Assert.AreEqual(1, model.stats.crossingCount);
            Assert.AreEqual(WalkwayKind.Crossing, model.walkways[0].kind);
        }

        [Test]
        public void Builder_MapOsm_HasBuildingsWithHoles_Tunnels_Crossings()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "Source/map.osm"));
            OsmDocument doc = OsmXmlParser.ParseFile(path);
            CityModel model = CityModelBuilder.Build(doc, new CityBakeConfig(), path);
            Assert.Greater(model.stats.buildingWithHolesCount, 0, "expected buildings with holes");
            Assert.Greater(model.stats.tunnelCount, 0, "expected tunnels");
            Assert.Greater(model.stats.crossingCount, 0, "expected footway crossings");
            // map.osm contains no bridge=* tags (data); bridge classification covered by synthetic test
            Assert.AreEqual(0, model.stats.bridgeCount);
        }

        static string LoadFixtureText(string fileName)
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath, "_Project/Tests/EditMode/Fixtures", fileName));
            Assert.IsTrue(File.Exists(path), "fixture missing: " + path);
            return File.ReadAllText(path);
        }

        static CityModel BuildFromFixture(string fileName, string sourceName = null)
        {
            OsmDocument doc = OsmXmlParser.ParseXml(LoadFixtureText(fileName));
            return CityModelBuilder.Build(doc, new CityBakeConfig(), sourceName ?? fileName);
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
