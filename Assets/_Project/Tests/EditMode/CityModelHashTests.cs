using NUnit.Framework;
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
