using NUnit.Framework;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    public class CityGridTests
    {
        [Test]
        public void FromBBox_KnownSize_MatchesAdrFormula()
        {
            // bbox [0.1, 0.1] .. [10.1, 5.1], cell 0.5
            // origin = floor(0.1/0.5)*0.5 = 0
            // w = ceil(10.1/0.5) = 21, h = ceil(5.1/0.5) = 11
            var grid = CityGrid.FromBBox(new Vec2d(0.1, 0.1), new Vec2d(10.1, 5.1), 0.5f);
            Assert.AreEqual(0.0, grid.origin.x, 1e-9);
            Assert.AreEqual(0.0, grid.origin.z, 1e-9);
            Assert.AreEqual(0.5f, grid.cell);
            Assert.AreEqual(21, grid.w);
            Assert.AreEqual(11, grid.h);
        }

        [Test]
        public void FromBBox_NegativeOrigin_FloorsTowardMinusInfinity()
        {
            var grid = CityGrid.FromBBox(new Vec2d(-10.2, -3.1), new Vec2d(-0.1, 2.0), 0.5f);
            Assert.AreEqual(-10.5, grid.origin.x, 1e-9);
            Assert.AreEqual(-3.5, grid.origin.z, 1e-9);
            Assert.AreEqual(21, grid.w); // ceil((-0.1 - (-10.5))/0.5) = ceil(20.8) = 21
            Assert.AreEqual(11, grid.h); // ceil((2.0 - (-3.5))/0.5) = ceil(11) = 11
        }

        [Test]
        public void WorldToCell_And_CellCenter_RoundTrip()
        {
            var grid = CityGrid.FromBBox(new Vec2d(0, 0), new Vec2d(20, 10), 0.5f);
            var world = new Vec2d(3.25, 4.75);
            grid.WorldToCell(world, out int i, out int j);
            Assert.AreEqual(6, i); // floor(3.25/0.5)=6
            Assert.AreEqual(9, j); // floor(4.75/0.5)=9
            Vec2d center = grid.CellCenter(i, j);
            Assert.AreEqual(3.25, center.x, 1e-9);
            Assert.AreEqual(4.75, center.z, 1e-9);
            Assert.IsTrue(grid.InBounds(i, j));
        }

        [Test]
        public void InBounds_And_Index()
        {
            var grid = new CityGrid(new Vec2d(0, 0), 0.5f, 4, 3);
            Assert.IsTrue(grid.InBounds(0, 0));
            Assert.IsTrue(grid.InBounds(3, 2));
            Assert.IsFalse(grid.InBounds(-1, 0));
            Assert.IsFalse(grid.InBounds(4, 0));
            Assert.IsFalse(grid.InBounds(0, 3));
            Assert.AreEqual(0, grid.Index(0, 0));
            Assert.AreEqual(5, grid.Index(1, 1)); // 1 + 1*4
            Assert.AreEqual(12, grid.CellCount);
        }

        [Test]
        public void FromModel_UsesProjectedMetaBBox()
        {
            var model = new CityModel
            {
                meta = new CityModelMeta
                {
                    lat0 = 52.555636,
                    lon0 = 13.3801725,
                    bboxMinLat = 52.554548,
                    bboxMinLon = 13.377662,
                    bboxMaxLat = 52.556724,
                    bboxMaxLon = 13.382683
                }
            };

            var grid = CityGrid.FromModel(model, 0.5f);
            Assert.AreEqual(0.5f, grid.cell);
            Assert.Greater(grid.w, 600);
            Assert.Less(grid.w, 720);
            Assert.Greater(grid.h, 400);
            Assert.Less(grid.h, 520);

            // Round-trip a point near origin of projection
            var center = new Vec2d(0, 0);
            grid.WorldToCell(center, out int i, out int j);
            Assert.IsTrue(grid.InBounds(i, j));
            Vec2d back = grid.CellCenter(i, j);
            Assert.AreEqual(center.x, back.x, grid.cell);
            Assert.AreEqual(center.z, back.z, grid.cell);
        }

        [Test]
        public void CellFlags_SharedBit_IsBit14()
        {
            Assert.AreEqual((ushort)(1 << 14), (ushort)CellFlags.Shared);
            Assert.AreEqual((ushort)(1 << 0), (ushort)CellFlags.Road);
            Assert.AreEqual((ushort)(1 << 10), (ushort)CellFlags.BuildingPassage);
        }
    }
}
