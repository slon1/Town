using System;
using LibTessDotNet;
using NUnit.Framework;

namespace ZombieCity.City.Core.Tests
{
    /// <summary>TASK-006/6C: LibTessDotNet inside Core, EvenOdd hole.</summary>
    public class LibTessHoleTests
    {
        [Test]
        public void SquareWithSquareHole_TriangleArea_EqualsOuterMinusHole()
        {
            var tess = new Tess();
            tess.AddContour(new[]
            {
                V(0f, 0f), V(10f, 0f), V(10f, 10f), V(0f, 10f)
            }, ContourOrientation.CounterClockwise);
            tess.AddContour(new[]
            {
                V(2f, 2f), V(2f, 4f), V(4f, 4f), V(4f, 2f)
            }, ContourOrientation.Clockwise);
            tess.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3);

            double area = 0;
            for (int e = 0; e < tess.ElementCount; e++)
            {
                Vec3 a = tess.Vertices[tess.Elements[e * 3]].Position;
                Vec3 b = tess.Vertices[tess.Elements[e * 3 + 1]].Position;
                Vec3 c = tess.Vertices[tess.Elements[e * 3 + 2]].Position;
                area += Math.Abs((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y)) * 0.5;
            }

            Assert.Greater(tess.ElementCount, 0);
            Assert.AreEqual(96.0, area, 0.05, "10*10 - 2*2");
        }

        static ContourVertex V(float x, float y)
        {
            return new ContourVertex(new Vec3(x, y, 0f));
        }
    }
}
