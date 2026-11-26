using System;
using System.Linq;
using NUnit.Framework;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    /// <summary>ADR-004 §4 building extrusion (6D-tests, red before implementation).</summary>
    public class BuildingMeshTests
    {
        const uint RoofTagged = 0xFFD8CCC5u;
        const uint WallTagged = 0xFFA6988Fu;
        const uint RoofDefault = 0xFF3D8AFFu;
        const uint WallDefault = 0xFF1A5AC7u;

        [Test]
        public void Box10_HasRoofAndWalls_NoFloor()
        {
            BuildingMeshData mesh = BuildingMeshBuilder.Build(Model(Box(1, 0, 0, 10, 10, 0, HeightSource.HeightTag)));
            Assert.AreEqual(20, mesh.vertexCount, "4 roof + 4 walls * 4");
            Assert.AreEqual(10, mesh.triangleCount, "2 roof + 8 wall; open bottom is 10, not 12");
            Assert.AreEqual(mesh.vertexCount, mesh.colors.Length);
            Assert.AreEqual(mesh.triangleCount * 3, mesh.indices.Length);
            AssertAllRoofNormalsUp(mesh);
            AssertWallsFaceAwayFrom(mesh, 5f, 5f);
            AssertNoFloor(mesh, 0f, 10f);
        }

        [Test]
        public void Hole_CourtyardWallsFaceInward()
        {
            var area = new Area
            {
                osmId = 2,
                kind = AreaKind.Building,
                heightM = 8f,
                minHeightM = 0f,
                heightSource = HeightSource.Levels,
                points = new[]
                {
                    new Vec2d(0, 0), new Vec2d(30, 0), new Vec2d(30, 30), new Vec2d(0, 30),
                    new Vec2d(10, 10), new Vec2d(10, 20), new Vec2d(20, 20), new Vec2d(20, 10)
                },
                ringStart = new[] { 0, 4 }
            };
            BuildingMeshData mesh = BuildingMeshBuilder.Build(Model(area));
            Assert.Greater(mesh.triangleCount, 10);
            int courtyard = 0;
            int outer = 0;
            ForEachTriangle(mesh, (x0, y0, z0, x1, y1, z1, x2, y2, z2) =>
            {
                if (Math.Abs(y0 - y1) < 1e-3 && Math.Abs(y1 - y2) < 1e-3)
                {
                    return;
                }

                LhNormal(x0, y0, z0, x1, y1, z1, x2, y2, z2, out float nx, out _, out float nz);
                float cx = (x0 + x1 + x2) / 3f;
                float cz = (z0 + z1 + z2) / 3f;
                float dx = 15f - cx;
                float dz = 15f - cz;
                float dist = (float)Math.Sqrt(dx * dx + dz * dz);
                float dotIn = nx * dx + nz * dz;
                if (dist < 8f)
                {
                    courtyard++;
                    Assert.Greater(dotIn, 0f, "courtyard wall faces into the hole");
                }
                else
                {
                    outer++;
                    Assert.Less(dotIn, 0f, "outer wall faces away from the building");
                }
            });
            Assert.Greater(courtyard, 0);
            Assert.Greater(outer, 0);
        }

        [Test]
        public void MinHeight_RaisesWalls_AndSkipsFloor()
        {
            BuildingMeshData mesh = BuildingMeshBuilder.Build(Model(Box(3, 4, 0, 6, 9, 3f, HeightSource.HeightTag)));
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int i = 1; i < mesh.positions.Length; i += 3)
            {
                minY = Math.Min(minY, mesh.positions[i]);
                maxY = Math.Max(maxY, mesh.positions[i]);
            }

            Assert.AreEqual(3f, minY, 1e-3);
            Assert.AreEqual(9f, maxY, 1e-3);
            AssertNoFloor(mesh, 3f, 9f);
        }

        [Test]
        public void DegenerateRing_IsSkipped()
        {
            var good = Box(10, 0, 0, 4, 6, 0, HeightSource.HeightTag);
            var bad = new Area
            {
                osmId = 11,
                kind = AreaKind.Building,
                heightM = 6f,
                heightSource = HeightSource.Default,
                points = new[] { new Vec2d(0, 0), new Vec2d(1, 0) },
                ringStart = new[] { 0 }
            };
            var hole = Box(12, 40, 0, 10, 8, 0, HeightSource.Levels);
            hole.points = hole.points.Concat(new[] { new Vec2d(1, 1), new Vec2d(2, 1) }).ToArray();
            hole.ringStart = new[] { 0, 4 };
            BuildingMeshData mesh = BuildingMeshBuilder.Build(Model(good, bad, hole));
            Assert.AreEqual(2, mesh.buildingCount);
            Assert.AreEqual(1, mesh.skippedBuildings);
            Assert.GreaterOrEqual(mesh.skippedRings, 1);
            BuildingMeshData plain = BuildingMeshBuilder.Build(Model(
                Box(10, 0, 0, 4, 6, 0, HeightSource.HeightTag),
                Box(12, 40, 0, 10, 8, 0, HeightSource.Levels)));
            Assert.AreEqual(plain.triangleCount, mesh.triangleCount);
        }

        [Test]
        public void DefaultColors_DifferFromTagged()
        {
            BuildingMeshData tagged = BuildingMeshBuilder.Build(Model(Box(1, 0, 0, 10, 5, 0, HeightSource.HeightTag)));
            BuildingMeshData fallback = BuildingMeshBuilder.Build(Model(Box(1, 0, 0, 10, 5, 0, HeightSource.Default)));
            Assert.AreEqual(RoofTagged, RoofColor(tagged));
            Assert.AreEqual(WallTagged, WallColor(tagged));
            Assert.AreEqual(RoofDefault, RoofColor(fallback));
            Assert.AreEqual(WallDefault, WallColor(fallback));
            Assert.AreNotEqual(RoofColor(tagged), RoofColor(fallback));
        }

        [Test]
        public void Deterministic_RepeatAndPermute()
        {
            Area a = Box(1, 0, 0, 10, 4, 0, HeightSource.HeightTag);
            Area b = Box(2, 30, 0, 6, 7, 1f, HeightSource.Default);
            BuildingMeshData first = BuildingMeshBuilder.Build(Model(a, b));
            BuildingMeshData second = BuildingMeshBuilder.Build(Model(a, b));
            BuildingMeshData swapped = BuildingMeshBuilder.Build(Model(b, a));
            Assert.IsTrue(first.positions.SequenceEqual(second.positions));
            Assert.IsTrue(first.indices.SequenceEqual(second.indices));
            Assert.IsTrue(first.colors.SequenceEqual(second.colors));
            Assert.IsTrue(first.positions.SequenceEqual(swapped.positions));
            Assert.IsTrue(first.indices.SequenceEqual(swapped.indices));
            Assert.IsTrue(first.colors.SequenceEqual(swapped.colors));
        }

        static uint RoofColor(BuildingMeshData mesh)
        {
            for (int t = 0; t < mesh.triangleCount; t++)
            {
                int i0 = mesh.indices[t * 3];
                if (Math.Abs(mesh.positions[i0 * 3 + 1] - mesh.positions[mesh.indices[t * 3 + 1] * 3 + 1]) < 1e-3
                    && mesh.positions[i0 * 3 + 1] > 1f)
                {
                    return mesh.colors[i0];
                }
            }

            Assert.Fail("no roof triangle");
            return 0;
        }

        static uint WallColor(BuildingMeshData mesh)
        {
            for (int t = 0; t < mesh.triangleCount; t++)
            {
                int i0 = mesh.indices[t * 3];
                int i1 = mesh.indices[t * 3 + 1];
                if (Math.Abs(mesh.positions[i0 * 3 + 1] - mesh.positions[i1 * 3 + 1]) > 1e-3)
                {
                    return mesh.colors[i0];
                }
            }

            Assert.Fail("no wall triangle");
            return 0;
        }

        static void AssertNoFloor(BuildingMeshData mesh, float minY, float roofY)
        {
            ForEachTriangle(mesh, (x0, y0, z0, x1, y1, z1, x2, y2, z2) =>
            {
                if (Math.Abs(y0 - minY) < 1e-3 && Math.Abs(y1 - minY) < 1e-3 && Math.Abs(y2 - minY) < 1e-3)
                {
                    Assert.Fail("floor triangle");
                }

                Assert.GreaterOrEqual(y0, minY - 1e-3);
                Assert.LessOrEqual(y0, roofY + 1e-3);
            });
        }

        static void AssertAllRoofNormalsUp(BuildingMeshData mesh)
        {
            int roofs = 0;
            ForEachTriangle(mesh, (x0, y0, z0, x1, y1, z1, x2, y2, z2) =>
            {
                if (Math.Abs(y0 - y1) > 1e-3 || Math.Abs(y1 - y2) > 1e-3)
                {
                    return;
                }

                LhNormal(x0, y0, z0, x1, y1, z1, x2, y2, z2, out _, out float ny, out _);
                Assert.Greater(ny, 0.5f);
                roofs++;
            });
            Assert.Greater(roofs, 0);
        }

        static void AssertWallsFaceAwayFrom(BuildingMeshData mesh, float cx, float cz)
        {
            int walls = 0;
            ForEachTriangle(mesh, (x0, y0, z0, x1, y1, z1, x2, y2, z2) =>
            {
                if (Math.Abs(y0 - y1) < 1e-3 && Math.Abs(y1 - y2) < 1e-3)
                {
                    return;
                }

                LhNormal(x0, y0, z0, x1, y1, z1, x2, y2, z2, out float nx, out float ny, out float nz);
                Assert.AreEqual(0f, ny, 0.05f);
                float mx = (x0 + x1 + x2) / 3f - cx;
                float mz = (z0 + z1 + z2) / 3f - cz;
                Assert.Greater(nx * mx + nz * mz, 0f);
                walls++;
            });
            Assert.Greater(walls, 0);
        }

        static void ForEachTriangle(BuildingMeshData mesh, Action<float, float, float, float, float, float, float, float, float> body)
        {
            for (int t = 0; t < mesh.triangleCount; t++)
            {
                int i0 = mesh.indices[t * 3];
                int i1 = mesh.indices[t * 3 + 1];
                int i2 = mesh.indices[t * 3 + 2];
                body(
                    mesh.positions[i0 * 3], mesh.positions[i0 * 3 + 1], mesh.positions[i0 * 3 + 2],
                    mesh.positions[i1 * 3], mesh.positions[i1 * 3 + 1], mesh.positions[i1 * 3 + 2],
                    mesh.positions[i2 * 3], mesh.positions[i2 * 3 + 1], mesh.positions[i2 * 3 + 2]);
            }
        }

        static void LhNormal(
            float x0, float y0, float z0,
            float x1, float y1, float z1,
            float x2, float y2, float z2,
            out float nx, out float ny, out float nz)
        {
            float ax = x1 - x0, ay = y1 - y0, az = z1 - z0;
            float bx = x2 - x0, by = y2 - y0, bz = z2 - z0;
            nx = ay * bz - az * by;
            ny = az * bx - ax * bz;
            nz = ax * by - ay * bx;
        }

        static CityModel Model(params Area[] areas)
        {
            return new CityModel { areas = areas };
        }

        static Area Box(long id, double x, double z, double sx, double height, float minHeight, HeightSource source)
        {
            return new Area
            {
                osmId = id,
                kind = AreaKind.Building,
                heightM = (float)height,
                minHeightM = minHeight,
                heightSource = source,
                buildingType = "yes",
                points = new[]
                {
                    new Vec2d(x, z),
                    new Vec2d(x + sx, z),
                    new Vec2d(x + sx, z + sx),
                    new Vec2d(x, z + sx)
                },
                ringStart = new[] { 0 }
            };
        }
    }
}
