using NUnit.Framework;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    /// <summary>IEC 61966-2-1 channel conversion (8A-tests, red before the formula).</summary>
    public class ColorSpaceUtilTests
    {
        [Test]
        public void Endpoints()
        {
            Assert.AreEqual(0f, ColorSpaceUtil.SrgbToLinear(0), 1e-4f);
            Assert.AreEqual(1f, ColorSpaceUtil.SrgbToLinear(255), 1e-4f);
        }

        [Test]
        public void ControlChannels()
        {
            Assert.AreEqual(0.05126946f, ColorSpaceUtil.SrgbToLinear(0x40), 1e-4f);
            Assert.AreEqual(0.21586055f, ColorSpaceUtil.SrgbToLinear(0x80), 1e-4f);
            Assert.AreEqual(0.52711523f, ColorSpaceUtil.SrgbToLinear(0xC0), 1e-4f);
        }

        [Test]
        public void RoundTrip_AllBytes()
        {
            for (int i = 0; i < 256; i++)
            {
                byte b = (byte)i;
                byte back = ColorSpaceUtil.LinearToSrgb(ColorSpaceUtil.SrgbToLinear(b));
                Assert.AreEqual(b, back, "channel " + b);
            }
        }
    }
}
