using NUnit.Framework;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    /// <summary>
    /// ADR-003 §4.1 diagnostics (4C-tests, red before implementation).
    /// </summary>
    public class AccessStrictTests
    {
        static CityGrid Grid(int w, int h, float cell = 1f) =>
            new CityGrid(new Vec2d(0, 0), cell, w, h);

        static void Fill(byte[] access, ushort[] flags, int w, int i, int j, CityAccess a, CellFlags f)
        {
            int idx = i + j * w;
            access[idx] = (byte)a;
            flags[idx] = (ushort)f;
        }

        static void FillRect(
            byte[] access, ushort[] flags, int w, int i0, int j0, int i1, int j1, CityAccess a, CellFlags f)
        {
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
                Fill(access, flags, w, i, j, a, f);
        }

        static Walkway Crossing(long id, double x0, double z0, double x1, double z1)
        {
            return new Walkway
            {
                osmId = id,
                kind = WalkwayKind.Crossing,
                widthM = 1f,
                points = new[] { new Vec2d(x0, z0), new Vec2d(x1, z1) }
            };
        }

        [Test]
        public void Histogram_GivenComponentSizes()
        {
            AccessConnectivity.SizeHistogram h = AccessConnectivity.Histogram(
                new[] { 1, 10, 11, 50, 51, 199, 200, 500 });
            Assert.AreEqual(2, h.upTo10);
            Assert.AreEqual(2, h.from11To50);
            Assert.AreEqual(2, h.from51To199);
            Assert.AreEqual(2, h.atLeast200);
        }

        [Test]
        public void TwoStreetsAndCrosswalk_StrictJoins_GroupConnects()
        {
            int w = 7, h = 3;
            var access = new byte[w * h];
            var flags = new ushort[w * h];
            FillRect(access, flags, w, 0, 0, 2, 2, CityAccess.Pedestrian, CellFlags.Sidewalk);
            FillRect(access, flags, w, 3, 0, 3, 2, CityAccess.Roadway, CellFlags.Road);
            FillRect(access, flags, w, 4, 0, 6, 2, CityAccess.Pedestrian, CellFlags.Sidewalk);
            Fill(access, flags, w, 3, 1, CityAccess.Pedestrian, CellFlags.Road | CellFlags.Crosswalk);

            AccessConnectivity.StrictDiagnostics d = AccessConnectivity.AnalyzeStrict(
                Grid(w, h), access, flags, walkways: null, searchRadiusM: 3f, crosswalkWidthFallback: 3f);

            Assert.AreEqual(2, d.componentsWithoutCrosswalk);
            Assert.AreEqual(1, d.componentsWithCrosswalk);
            Assert.AreEqual(1, d.crosswalkGroupCount);
            Assert.AreEqual(1, d.connectingGroupCount);
        }

        [Test]
        public void SidewalkLineAcrossRoad_UnmarkedCrossing()
        {
            int w = 7, h = 3;
            var access = new byte[w * h];
            var flags = new ushort[w * h];
            FillRect(access, flags, w, 0, 0, 2, 2, CityAccess.Pedestrian, CellFlags.Sidewalk);
            FillRect(access, flags, w, 3, 0, 3, 2, CityAccess.Pedestrian, CellFlags.Road | CellFlags.Sidewalk);
            FillRect(access, flags, w, 4, 0, 6, 2, CityAccess.Pedestrian, CellFlags.Sidewalk);

            var ped = AccessConnectivity.Analyze(
                Grid(w, h), access, flags, 200, AccessConnectivity.Mask.Pedestrian);
            AccessConnectivity.StrictDiagnostics d = AccessConnectivity.AnalyzeStrict(
                Grid(w, h), access, flags, null, 3f, 3f);

            Assert.AreEqual(1, ped.componentCount);
            Assert.AreEqual(2, d.componentsWithoutCrosswalk);
            Assert.AreEqual(3, d.unmarkedCellCount);
            Assert.AreEqual(1, d.unmarkedClusterCount);
            Assert.GreaterOrEqual(d.unmarkedLargest.Length, 1);
            Assert.AreEqual(3, d.unmarkedLargest[0].cellCount);
        }

        [Test]
        public void CrossingEnds_FourStatuses()
        {
            AssertStatus(SameComponent(), AccessConnectivity.CrossingEndStatus.BothEndsSameComponent, true, true);
            AssertStatus(DifferentComponents(), AccessConnectivity.CrossingEndStatus.ConnectsDifferent, true, true);
            AccessConnectivity.CrossingRow one = OneEndMissing();
            Assert.AreEqual(AccessConnectivity.CrossingEndStatus.OneEndWithoutSidewalk, one.status);
            Assert.IsTrue(one.end0.hasSidewalk);
            Assert.IsFalse(one.end1.hasSidewalk);
            Assert.AreEqual("нет тротуара рядом", AccessConnectivity.FormatEnd(one.end1));
            AssertStatus(BothMissing(), AccessConnectivity.CrossingEndStatus.BothEndsWithoutSidewalk, false, false);
        }

        static void AssertStatus(
            AccessConnectivity.CrossingRow row,
            AccessConnectivity.CrossingEndStatus status,
            bool end0,
            bool end1)
        {
            Assert.AreEqual(status, row.status);
            Assert.AreEqual(end0, row.end0.hasSidewalk);
            Assert.AreEqual(end1, row.end1.hasSidewalk);
            if (end0 && end1 && status == AccessConnectivity.CrossingEndStatus.ConnectsDifferent)
            {
                Assert.AreNotEqual(row.end0.componentId, row.end1.componentId);
            }

            if (end0 && end1 && status == AccessConnectivity.CrossingEndStatus.BothEndsSameComponent)
            {
                Assert.AreEqual(row.end0.componentId, row.end1.componentId);
                Assert.Less(row.end0.distanceM, 0.01);
            }
        }

        static AccessConnectivity.CrossingRow SameComponent()
        {
            int w = 9, h = 2;
            var access = new byte[w * h];
            var flags = new ushort[w * h];
            FillRect(access, flags, w, 0, 0, 8, 0, CityAccess.Pedestrian, CellFlags.Sidewalk);
            var d = AccessConnectivity.AnalyzeStrict(
                Grid(w, h), access, flags,
                new[] { Crossing(1, 0.5, 0.5, 8.5, 0.5) }, 3f, 3f);
            Assert.AreEqual(1, d.crossings.Length);
            return d.crossings[0];
        }

        static AccessConnectivity.CrossingRow DifferentComponents()
        {
            int w = 9, h = 5;
            var access = new byte[w * h];
            var flags = new ushort[w * h];
            FillRect(access, flags, w, 0, 0, 0, 4, CityAccess.Pedestrian, CellFlags.Sidewalk);
            FillRect(access, flags, w, 8, 0, 8, 4, CityAccess.Pedestrian, CellFlags.Sidewalk);
            var d = AccessConnectivity.AnalyzeStrict(
                Grid(w, h), access, flags,
                new[] { Crossing(2, 0.5, 2.5, 8.5, 2.5) }, 3f, 3f);
            return d.crossings[0];
        }

        static AccessConnectivity.CrossingRow OneEndMissing()
        {
            int w = 10, h = 5;
            var access = new byte[w * h];
            var flags = new ushort[w * h];
            FillRect(access, flags, w, 0, 0, 0, 4, CityAccess.Pedestrian, CellFlags.Sidewalk);
            var d = AccessConnectivity.AnalyzeStrict(
                Grid(w, h), access, flags,
                new[] { Crossing(3, 0.5, 2.5, 8.5, 2.5) }, 3f, 3f);
            return d.crossings[0];
        }

        static AccessConnectivity.CrossingRow BothMissing()
        {
            int w = 10, h = 5;
            var access = new byte[w * h];
            var flags = new ushort[w * h];
            FillRect(access, flags, w, 0, 0, 0, 4, CityAccess.Pedestrian, CellFlags.Sidewalk);
            var d = AccessConnectivity.AnalyzeStrict(
                Grid(w, h), access, flags,
                new[] { Crossing(4, 5.5, 2.5, 8.5, 2.5) }, 3f, 3f);
            return d.crossings[0];
        }
    }
}
