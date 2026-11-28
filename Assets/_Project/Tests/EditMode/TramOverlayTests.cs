using NUnit.Framework;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    /// <summary>
    /// Flags Tram sits under Crosswalk and over Road. Overlay Tram is priority 5, Bridge is 6 (ADR-003 v1.3).
    /// </summary>
    public class TramOverlayTests
    {
        const float OverlayAlpha = 0.7f;

        [Test]
        public void Flags_TramOnly_IsPaletteColor()
        {
            Assert.AreEqual(Rgb(0x9A4DFF), Flags(CellFlags.Tram));
        }

        [Test]
        public void Flags_TramOverRoad_UnderCrosswalk()
        {
            Assert.AreEqual(Rgb(0x9A4DFF), Flags(CellFlags.Tram | CellFlags.Road));
            Assert.AreEqual(Rgb(0x9A4DFF), Flags(CellFlags.Tram | CellFlags.Sidewalk));
            Assert.AreEqual(Rgb(0x9A4DFF), Flags(CellFlags.Tram | CellFlags.Green));
            Assert.AreEqual(Rgb(0xFFFFFF), Flags(CellFlags.Tram | CellFlags.Crosswalk));
        }

        [Test]
        public void Overlay_Tram_OnAndOff()
        {
            ushort flags = (ushort)CellFlags.Tram;
            var access = new[] { (byte)CityAccess.Pedestrian };
            uint[] on = Compose(flags, access, CityOverlay.Tram);
            uint[] off = Compose(flags, access, CityOverlay.None);
            uint expected = Blend(Rgb(0x40C080), Rgb(0xB060FF), OverlayAlpha);
            Assert.AreEqual(Rgb(0x40C080), off[0]);
            Assert.AreEqual(expected, on[0]);
        }

        [Test]
        public void Overlay_TramLosesToCrosswalkAndGate_BeatsBridge()
        {
            var access = new[] { (byte)CityAccess.Pedestrian };
            uint cross = Blend(Rgb(0x40C080), Rgb(0xFFE000), OverlayAlpha);
            uint gate = Blend(Rgb(0x40C080), Rgb(0xFFFFFF), OverlayAlpha);
            uint tram = Blend(Rgb(0x40C080), Rgb(0xB060FF), OverlayAlpha);
            CityOverlay all = CityOverlay.Crosswalk | CityOverlay.GatePassage | CityOverlay.Tram | CityOverlay.Bridge;

            Assert.AreEqual(cross, Compose((ushort)(CellFlags.Tram | CellFlags.Crosswalk), access, all)[0]);
            Assert.AreEqual(gate, Compose((ushort)(CellFlags.Tram | CellFlags.Gate), access, all)[0]);
            Assert.AreEqual(tram, Compose((ushort)(CellFlags.Tram | CellFlags.Bridge), access, all)[0]);
        }

        static uint Flags(CellFlags bits)
        {
            return Pixel(CityLayer.Flags, (ushort)bits, 0, CityOverlay.None);
        }

        static uint[] Compose(ushort flags, byte[] access, CityOverlay overlays)
        {
            return new[] { Pixel(CityLayer.Access, flags, access[0], overlays) };
        }

        static uint Pixel(CityLayer layer, ushort flags, byte access, CityOverlay overlays)
        {
            var dst = new uint[1];
            LayerComposer.Compose(1, 1, new[] { flags }, new ushort[1], new[] { access }, layer, overlays, dst);
            return dst[0];
        }

        static uint Rgb(int rgb)
        {
            return (uint)(((rgb >> 16) & 255) | (((rgb >> 8) & 255) << 8) | ((rgb & 255) << 16) | (255 << 24));
        }

        static uint Blend(uint a, uint b, float t)
        {
            float u = 1f - t;
            return (uint)(
                (byte)(((byte)a) * u + ((byte)b) * t)
                | ((byte)(((byte)(a >> 8)) * u + ((byte)(b >> 8)) * t) << 8)
                | ((byte)(((byte)(a >> 16)) * u + ((byte)(b >> 16)) * t) << 16)
                | (255u << 24));
        }
    }
}
