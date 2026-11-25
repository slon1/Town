using NUnit.Framework;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    public class SmokeTests
    {
        [Test]
        public void CityBakeConfig_DefaultScalars_MatchAdr()
        {
            var config = new CityBakeConfig();

            Assert.AreEqual(0.5f, config.cellSize);
            Assert.AreEqual(12f, config.defaultBuildingHeight);
            Assert.AreEqual(3f, config.metersPerLevel);
            Assert.AreEqual(2f, config.sidewalkWidth);
            Assert.AreEqual(4f, config.waterwayWidth);
        }

        [Test]
        public void CityBakeConfig_DefaultRoadWidths_MatchAdr()
        {
            var config = new CityBakeConfig();

            Assert.AreEqual(10f, config.GetRoadWidth("primary"));
            Assert.AreEqual(6.5f, config.GetRoadWidth("residential"));
        }
    }
}
