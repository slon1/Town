using System;
using NUnit.Framework;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    /// <summary>
    /// ADR-003 §2 — таблица истинности AccessClassifier (3B-tests, до реализации).
    /// </summary>
    public class AccessClassifierTests
    {
        [TestCase(CellFlags.Building, CityAccess.Obstacle)]
        [TestCase(CellFlags.Water, CityAccess.Obstacle)]
        [TestCase(CellFlags.Barrier, CityAccess.Obstacle)]
        [TestCase(CellFlags.Rail, CityAccess.Obstacle)]
        [TestCase(CellFlags.Rail | CellFlags.Bridge, CityAccess.Obstacle)]
        [TestCase(CellFlags.Building | CellFlags.Road, CityAccess.Obstacle)]
        [TestCase(CellFlags.Water | CellFlags.Bridge, CityAccess.Obstacle)]
        [TestCase(CellFlags.Road, CityAccess.Roadway)]
        [TestCase(CellFlags.Road | CellFlags.Bridge, CityAccess.Roadway)]
        [TestCase(CellFlags.Road | CellFlags.BuildingPassage, CityAccess.Roadway)]
        [TestCase(CellFlags.Road | CellFlags.Crosswalk, CityAccess.Pedestrian)]
        [TestCase(CellFlags.Road | CellFlags.Sidewalk, CityAccess.Pedestrian)]
        [TestCase(CellFlags.Road | CellFlags.SidewalkGenerated, CityAccess.Pedestrian)]
        [TestCase(CellFlags.Road | CellFlags.Shared, CityAccess.Pedestrian)]
        [TestCase(CellFlags.Road | CellFlags.Steps, CityAccess.Pedestrian)]
        [TestCase(CellFlags.Road | CellFlags.Cycleway, CityAccess.Pedestrian)]
        [TestCase(CellFlags.Sidewalk | CellFlags.BuildingPassage, CityAccess.Pedestrian)]
        [TestCase(CellFlags.None, CityAccess.Pedestrian)]
        [TestCase(CellFlags.Green, CityAccess.Pedestrian)]
        [TestCase(CellFlags.Sidewalk, CityAccess.Pedestrian)]
        [TestCase(CellFlags.Crosswalk, CityAccess.Pedestrian)]
        [TestCase(CellFlags.Cycleway, CityAccess.Pedestrian)]
        [TestCase(CellFlags.Steps, CityAccess.Pedestrian)]
        [TestCase(CellFlags.Shared, CityAccess.Pedestrian)]
        [TestCase(CellFlags.Gate, CityAccess.Pedestrian)]
        public void Classify_TruthTable(CellFlags flags, CityAccess expected)
        {
            Assert.AreEqual(expected, AccessClassifier.Classify(flags), flags.ToString());
        }

        [Test]
        public void Classify_BarrierClearedByGate_IsPedestrian()
        {
            // После растеризатора ворота снимают Barrier; в flags остаётся Gate.
            Assert.AreEqual(CityAccess.Pedestrian, AccessClassifier.Classify(CellFlags.Gate));
            Assert.AreEqual(CityAccess.Obstacle, AccessClassifier.Classify(CellFlags.Barrier));
        }

        [Test]
        public void ClassifyAll_MatchesClassify_AndIsStable()
        {
            var flags = new ushort[]
            {
                (ushort)CellFlags.None,
                (ushort)CellFlags.Road,
                (ushort)CellFlags.Road | (ushort)CellFlags.Crosswalk,
                (ushort)CellFlags.Building,
                (ushort)CellFlags.Rail | (ushort)CellFlags.Bridge,
                (ushort)CellFlags.Green,
                (ushort)CellFlags.Road | (ushort)CellFlags.Shared,
                (ushort)CellFlags.Water
            };
            var accessA = new byte[flags.Length];
            var accessB = new byte[flags.Length];
            AccessClassifier.ClassifyAll(flags, accessA);
            AccessClassifier.ClassifyAll(flags, accessB);

            for (int i = 0; i < flags.Length; i++)
            {
                Assert.AreEqual((byte)AccessClassifier.Classify((CellFlags)flags[i]), accessA[i]);
                Assert.AreEqual(accessA[i], accessB[i]);
            }

            ulong h1 = Fnv1a64.Compute(accessA);
            ulong h2 = Fnv1a64.Compute(accessB);
            Assert.AreEqual(h1, h2);
        }

        [Test]
        public void ClassifyAll_NullOrLengthMismatch_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => AccessClassifier.ClassifyAll(null, new byte[1]));
            Assert.Throws<ArgumentNullException>(() => AccessClassifier.ClassifyAll(new ushort[1], null));
            Assert.Throws<ArgumentException>(() => AccessClassifier.ClassifyAll(new ushort[2], new byte[1]));
        }
    }
}
