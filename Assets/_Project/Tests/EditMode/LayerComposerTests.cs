using NUnit.Framework;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    /// <summary>ADR-003 §5/§5.1 and TASK-002/2E palettes (6E-tests, red before implementation).</summary>
    public class LayerComposerTests
    {
        const float OverlayAlpha = 0.7f;

        [Test]
        public void Access_ThreeClasses()
        {
            var access = new byte[]
            {
                (byte)CityAccess.Obstacle,
                (byte)CityAccess.Roadway,
                (byte)CityAccess.Pedestrian
            };
            uint[] px = Compose(3, 1, new ushort[3], new ushort[3], access, CityLayer.Access, CityOverlay.None);
            Assert.AreEqual(Rgb(0x202020), px[0]);
            Assert.AreEqual(Rgb(0xD04040), px[1]);
            Assert.AreEqual(Rgb(0x40C080), px[2]);
        }

        [Test]
        public void Flags_OneBitPerGroup()
        {
            AssertFlag(CellFlags.None, 0x101010);
            AssertFlag(CellFlags.Building, 0x3A3A3A);
            AssertFlag(CellFlags.Water, 0x2E7DFF);
            AssertFlag(CellFlags.Barrier, 0xFF7A00);
            AssertFlag(CellFlags.Gate, 0xFFE000);
            AssertFlag(CellFlags.Rail, 0x9A4DFF);
            AssertFlag(CellFlags.Crosswalk, 0xFFFFFF);
            AssertFlag(CellFlags.Sidewalk, 0xC8C8C8);
            AssertFlag(CellFlags.Cycleway, 0xC8C8C8);
            AssertFlag(CellFlags.Steps, 0xC8C8C8);
            AssertFlag(CellFlags.SidewalkGenerated, 0xC8C8C8);
            AssertFlag(CellFlags.Road, 0x5A5A5A);
            AssertFlag(CellFlags.Green, 0x3FA34D);
            uint road = Rgb(0x5A5A5A);
            uint tinted = Blend(road, Rgb(0x00D1C1), 0.45f);
            uint[] bridge = Compose(1, 1, new[] { (ushort)(CellFlags.Road | CellFlags.Bridge) }, new ushort[1], new byte[1], CityLayer.Flags, CityOverlay.None);
            Assert.AreEqual(tinted, bridge[0]);
        }

        [Test]
        public void OverlayPriority_RoadRailCrosswalk_PicksRails()
        {
            ushort flags = (ushort)(CellFlags.Road | CellFlags.Rail | CellFlags.Crosswalk);
            var access = new[] { (byte)CityAccess.Roadway };
            uint[] on = Compose(1, 1, new[] { flags }, new ushort[1], access, CityLayer.Access, CityOverlay.RailOnRoad | CityOverlay.UnmarkedCrossing | CityOverlay.Crosswalk | CityOverlay.GatePassage | CityOverlay.Bridge);
            uint[] off = Compose(1, 1, new[] { flags }, new ushort[1], access, CityLayer.Access, CityOverlay.None);
            uint rails = Blend(Rgb(0xD04040), Rgb(0xFF0000), OverlayAlpha);
            uint crosswalk = Blend(Rgb(0xD04040), Rgb(0xFFE000), OverlayAlpha);
            Assert.AreEqual(Rgb(0xD04040), off[0]);
            Assert.AreEqual(rails, on[0]);
            Assert.AreNotEqual(crosswalk, on[0]);
        }

        [Test]
        public void BuildingId_StableAndZeroIsBackground()
        {
            var ids = new ushort[] { 0, 7, 8 };
            uint[] a = Compose(3, 1, new ushort[3], ids, new byte[3], CityLayer.BuildingId, CityOverlay.None);
            uint[] b = Compose(3, 1, new ushort[3], ids, new byte[3], CityLayer.BuildingId, CityOverlay.None);
            Assert.AreEqual(Rgb(0x101010), a[0]);
            Assert.AreEqual(a[1], b[1]);
            Assert.AreNotEqual(a[0], a[1]);
            Assert.AreNotEqual(a[1], a[2]);
            Assert.AreEqual(HashId(7), a[1]);
            Assert.AreEqual(HashId(8), a[2]);
        }

        [Test]
        public void Row0_IsMinimumJ()
        {
            var access = new byte[]
            {
                (byte)CityAccess.Obstacle,
                (byte)CityAccess.Roadway,
                (byte)CityAccess.Pedestrian,
                (byte)CityAccess.Obstacle
            };
            uint[] px = Compose(2, 2, new ushort[4], new ushort[4], access, CityLayer.Access, CityOverlay.None);
            Assert.AreEqual(Rgb(0x202020), px[0]);
            Assert.AreEqual(Rgb(0xD04040), px[1], "i=1, j=0");
            Assert.AreEqual(Rgb(0x40C080), px[2], "i=0, j=1");
        }

        static void AssertFlag(CellFlags bit, int rgb)
        {
            uint[] px = Compose(1, 1, new[] { (ushort)bit }, new ushort[1], new byte[1], CityLayer.Flags, CityOverlay.None);
            Assert.AreEqual(Rgb(rgb), px[0], bit.ToString());
        }

        static uint[] Compose(int w, int h, ushort[] flags, ushort[] ids, byte[] access, CityLayer layer, CityOverlay overlays)
        {
            var dst = new uint[w * h];
            LayerComposer.Compose(w, h, flags, ids, access, layer, overlays, dst);
            return dst;
        }

        static uint HashId(ushort id)
        {
            unchecked
            {
                uint x = id * 2654435761u;
                return Pack((byte)(40 + (x & 0x7F)), (byte)(40 + ((x >> 8) & 0x7F)), (byte)(40 + ((x >> 16) & 0x7F)));
            }
        }

        static uint Rgb(int rgb)
        {
            return Pack((byte)((rgb >> 16) & 255), (byte)((rgb >> 8) & 255), (byte)(rgb & 255));
        }

        static uint Blend(uint a, uint b, float t)
        {
            float u = 1f - t;
            return Pack(
                (byte)(((byte)a) * u + ((byte)b) * t),
                (byte)(((byte)(a >> 8)) * u + ((byte)(b >> 8)) * t),
                (byte)(((byte)(a >> 16)) * u + ((byte)(b >> 16)) * t));
        }

        static uint Pack(byte r, byte g, byte b)
        {
            return (uint)(r | (g << 8) | (b << 16) | (255 << 24));
        }
    }
}
