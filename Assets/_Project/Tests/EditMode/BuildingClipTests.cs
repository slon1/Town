using NUnit.Framework;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    /// <summary>Buildings wholly outside the grid are omitted (8B-tests, red before the clip).</summary>
    public class BuildingClipTests
    {
        static readonly CityGrid Grid = new CityGrid(new Vec2d(0, 0), 10f, 1, 1);

        [Test]
        public void Inside_IsKept()
        {
            BuildingMeshData mesh = BuildingMeshBuilder.Build(Model(Box(1, 1, 1, 2)), Grid);
            Assert.AreEqual(1, mesh.buildingCount);
            Assert.AreEqual(0, mesh.outsideGrid);
            Assert.Greater(mesh.vertexCount, 0);
        }

        [Test]
        public void WhollyOutside_IsOmitted()
        {
            BuildingMeshData mesh = BuildingMeshBuilder.Build(Model(Box(2, 20, 20, 2)), Grid);
            Assert.AreEqual(0, mesh.buildingCount);
            Assert.AreEqual(0, mesh.vertexCount);
            Assert.AreEqual(1, mesh.outsideGrid);
            Assert.AreEqual(0, mesh.skippedBuildings);
        }

        [Test]
        public void CrossingEdge_StaysWhole()
        {
            BuildingMeshData mesh = BuildingMeshBuilder.Build(Model(Box(3, 8, 1, 4)), Grid);
            Assert.AreEqual(1, mesh.buildingCount);
            Assert.AreEqual(0, mesh.outsideGrid);
            Assert.Greater(mesh.vertexCount, 0);
        }

        [Test]
        public void TouchingEdge_Stays()
        {
            BuildingMeshData mesh = BuildingMeshBuilder.Build(Model(Box(4, -2, 1, 2)), Grid);
            Assert.AreEqual(1, mesh.buildingCount);
            Assert.AreEqual(0, mesh.outsideGrid);
        }

        [Test]
        public void Counter_CountsOnlyWhollyOutsideBuildings()
        {
            var water = Box(5, 40, 40, 2);
            water.kind = AreaKind.Water;
            BuildingMeshData mesh = BuildingMeshBuilder.Build(
                Model(Box(1, 1, 1, 2), Box(2, 20, 20, 2), water),
                Grid);
            Assert.AreEqual(1, mesh.buildingCount);
            Assert.AreEqual(1, mesh.outsideGrid);
        }

        [Test]
        public void Deterministic()
        {
            CityModel model = Model(Box(1, 1, 1, 2), Box(9, -30, 4, 3), Box(2, 20, 20, 2));
            BuildingMeshData a = BuildingMeshBuilder.Build(model, Grid);
            BuildingMeshData b = BuildingMeshBuilder.Build(model, Grid);
            Assert.AreEqual(a.outsideGrid, b.outsideGrid);
            Assert.AreEqual(a.buildingCount, b.buildingCount);
            CollectionAssert.AreEqual(a.positions, b.positions);
            CollectionAssert.AreEqual(a.indices, b.indices);
            CollectionAssert.AreEqual(a.colors, b.colors);
        }

        static CityModel Model(params Area[] areas)
        {
            return new CityModel { areas = areas };
        }

        static Area Box(long id, double x, double z, double size)
        {
            return new Area
            {
                osmId = id,
                kind = AreaKind.Building,
                heightM = 10f,
                minHeightM = 0f,
                heightSource = HeightSource.Levels,
                points = new[]
                {
                    new Vec2d(x, z),
                    new Vec2d(x + size, z),
                    new Vec2d(x + size, z + size),
                    new Vec2d(x, z + size)
                },
                ringStart = new[] { 0 }
            };
        }
    }
}
