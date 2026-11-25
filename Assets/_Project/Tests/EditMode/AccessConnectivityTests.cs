using System;
using NUnit.Framework;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    /// <summary>
    /// ADR-003 §4 — связность (3C-tests, до реализации).
    /// </summary>
    public class AccessConnectivityTests
    {
        static CityGrid Grid(int w, int h, float cell = 1f) =>
            new CityGrid(new Vec2d(0, 0), cell, w, h);

        static void FillRect(byte[] access, int w, int i0, int j0, int i1, int j1, CityAccess a)
        {
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
                access[i + j * w] = (byte)a;
        }

        [Test]
        public void TwoAreasSeparatedByWall_TwoComponents()
        {
            // 5×3: Pedestrian | Obstacle | Pedestrian
            int w = 5, h = 3;
            var access = new byte[w * h];
            FillRect(access, w, 0, 0, 1, 2, CityAccess.Pedestrian);
            FillRect(access, w, 2, 0, 2, 2, CityAccess.Obstacle);
            FillRect(access, w, 3, 0, 4, 2, CityAccess.Pedestrian);

            var report = AccessConnectivity.Analyze(
                Grid(w, h), access, flags: null, islandMinCells: 200,
                AccessConnectivity.Mask.Pedestrian);

            Assert.AreEqual(2, report.componentCount);
            Assert.AreEqual(2, report.components.Length);
            Assert.AreEqual(6, report.components[0].cellCount);
            Assert.AreEqual(6, report.components[1].cellCount);
        }

        [Test]
        public void DiagonalBetweenTwoCornerObstacles_Forbidden()
        {
            // P O
            // O P  — диагональ между двумя Obstacle запрещена → 2 компоненты
            int w = 2, h = 2;
            var access = new byte[]
            {
                (byte)CityAccess.Pedestrian, (byte)CityAccess.Obstacle,
                (byte)CityAccess.Obstacle, (byte)CityAccess.Pedestrian
            };
            var report = AccessConnectivity.Analyze(
                Grid(w, h), access, null, 200, AccessConnectivity.Mask.Pedestrian);
            Assert.AreEqual(2, report.componentCount);
        }

        [Test]
        public void DiagonalWithOneObstacle_Allowed()
        {
            // P P
            // O P  — одна ortho Obstacle → диагональ OK → 1 компонента
            int w = 2, h = 2;
            var access = new byte[]
            {
                (byte)CityAccess.Pedestrian, (byte)CityAccess.Pedestrian,
                (byte)CityAccess.Obstacle, (byte)CityAccess.Pedestrian
            };
            var report = AccessConnectivity.Analyze(
                Grid(w, h), access, null, 200, AccessConnectivity.Mask.Pedestrian);
            Assert.AreEqual(1, report.componentCount);
            Assert.AreEqual(3, report.components[0].cellCount);
        }

        [Test]
        public void IslandBelowThreshold_Listed()
        {
            int w = 20, h = 20;
            var access = new byte[w * h];
            // большая область 15×15 = 225 ≥ порога 200
            FillRect(access, w, 0, 0, 14, 14, CityAccess.Pedestrian);
            // остров 2×2 = 4 клетки < 200
            FillRect(access, w, 18, 18, 19, 19, CityAccess.Pedestrian);

            var report = AccessConnectivity.Analyze(
                Grid(w, h, 0.5f), access, null, islandMinCells: 200,
                AccessConnectivity.Mask.Pedestrian);

            Assert.AreEqual(2, report.componentCount);
            Assert.AreEqual(1, report.islandCount);
            Assert.AreEqual(4, report.islands[0].cellCount);
        }

        [Test]
        public void TwoStreetsPlusCrosswalk_JoinsComponents()
        {
            // Две полосы Pedestrian, разделённые Roadway; Crosswalk посередине.
            // Сетка 7×3:
            // PPP RRR PPP  без перехода — но R=Roadway не в PedestrianMask
            // Нужны flags с Crosswalk на клетках перехода.
            int w = 7, h = 3;
            var access = new byte[w * h];
            var flags = new ushort[w * h];
            FillRect(access, w, 0, 0, 2, 2, CityAccess.Pedestrian);
            FillRect(access, w, 3, 0, 3, 2, CityAccess.Roadway);
            FillRect(access, w, 4, 0, 6, 2, CityAccess.Pedestrian);
            for (int j = 0; j < h; j++)
            {
                flags[3 + j * w] = (ushort)CellFlags.Road;
            }

            // без Crosswalk
            var without = AccessConnectivity.Analyze(
                Grid(w, h), access, flags, 200, AccessConnectivity.Mask.Pedestrian);
            Assert.AreEqual(2, without.componentCount);

            // переход в середине ряда j=1
            access[3 + 1 * w] = (byte)CityAccess.Pedestrian;
            flags[3 + 1 * w] = (ushort)(CellFlags.Road | CellFlags.Crosswalk);

            var with = AccessConnectivity.Analyze(
                Grid(w, h), access, flags, 200, AccessConnectivity.Mask.Pedestrian);
            Assert.AreEqual(1, with.componentCount);
            Assert.AreEqual(2, with.pedestrianComponentsWithoutCrosswalk);
            Assert.AreEqual(1, with.pedestrianComponentsWithCrosswalk);
            Assert.GreaterOrEqual(with.crosswalkCellsConnecting, 1);
        }
    }
}
