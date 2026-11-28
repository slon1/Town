using NUnit.Framework;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    /// <summary>
    /// Tram does not change access (ADR-003 v1.3). Classifier is unchanged; these pass before the raster step.
    /// </summary>
    public class AccessTramTests
    {
        [Test]
        public void TramAlone_IsPedestrian()
        {
            Assert.AreEqual(CityAccess.Pedestrian, AccessClassifier.Classify(CellFlags.Tram));
        }

        [Test]
        public void TramAndRoad_IsRoadway()
        {
            Assert.AreEqual(CityAccess.Roadway, AccessClassifier.Classify(CellFlags.Tram | CellFlags.Road));
        }

        [Test]
        public void TramAndCrosswalk_IsPedestrian()
        {
            Assert.AreEqual(CityAccess.Pedestrian, AccessClassifier.Classify(CellFlags.Tram | CellFlags.Crosswalk));
        }

        [Test]
        public void ClassifyAll_TramMatchesSingleCellRules()
        {
            var flags = new ushort[]
            {
                (ushort)CellFlags.Tram,
                (ushort)(CellFlags.Tram | CellFlags.Road),
                (ushort)(CellFlags.Tram | CellFlags.Crosswalk)
            };
            var access = new byte[flags.Length];
            AccessClassifier.ClassifyAll(flags, access);
            Assert.AreEqual((byte)CityAccess.Pedestrian, access[0]);
            Assert.AreEqual((byte)CityAccess.Roadway, access[1]);
            Assert.AreEqual((byte)CityAccess.Pedestrian, access[2]);
        }
    }
}
