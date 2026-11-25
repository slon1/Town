using System;
using NUnit.Framework;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    /// <summary>
    /// ADR-002 §3 / §8 contracts for raster primitives (2B). Written from ADR, not from impl.
    /// </summary>
    public class RasterPrimitivesTests
    {
        const ushort Bit = (ushort)CellFlags.Building;
        const ushort BarrierBit = (ushort)CellFlags.Barrier;
        const float Cell = 0.5f;

        static CityGrid Grid(double maxX, double maxZ)
        {
            return CityGrid.FromBBox(new Vec2d(0, 0), new Vec2d(maxX, maxZ), Cell);
        }

        static ushort[] NewFlags(CityGrid g) => new ushort[g.CellCount];

        static int CountBits(ushort[] flags, ushort bit)
        {
            int n = 0;
            for (int i = 0; i < flags.Length; i++)
            {
                if ((flags[i] & bit) != 0)
                {
                    n++;
                }
            }

            return n;
        }

        static Vec2d[] RectRing(double minX, double minZ, double maxX, double maxZ)
        {
            return new[]
            {
                new Vec2d(minX, minZ),
                new Vec2d(maxX, minZ),
                new Vec2d(maxX, maxZ),
                new Vec2d(minX, maxZ),
                new Vec2d(minX, minZ)
            };
        }

        static Vec2d[] CircleRing(double cx, double cz, double r, int segments)
        {
            var pts = new Vec2d[segments + 1];
            for (int i = 0; i < segments; i++)
            {
                double a = i * (2.0 * Math.PI / segments);
                pts[i] = new Vec2d(cx + r * Math.Cos(a), cz + r * Math.Sin(a));
            }

            pts[segments] = pts[0];
            return pts;
        }

        static double CellArea(CityGrid g) => g.cell * (double)g.cell;

        [Test]
        public void FillPolygon_Rectangle_AreaWithinTolerance()
        {
            // 10×10 m square → analytical area 100. Perimeter 40 → tol = perimeter·cell/2 = 10.
            var grid = Grid(20, 20);
            var flags = NewFlags(grid);
            Vec2d[][] rings = { RectRing(0, 0, 10, 10) };

            RasterPrimitives.FillPolygonEvenOddA(grid, flags, rings, Bit);

            int cells = CountBits(flags, Bit);
            double rasterArea = cells * CellArea(grid);
            const double analytical = 100.0;
            const double perimeter = 40.0;
            double tol = perimeter * Cell / 2.0;
            Assert.That(Math.Abs(rasterArea - analytical), Is.LessThanOrEqualTo(tol + 1e-6),
                $"cells={cells} area={rasterArea} tol={tol}");
            Assert.Greater(cells, 0);
        }

        [Test]
        public void FillPolygon_Circle_AreaWithinTolerance()
        {
            const double r = 5.0;
            var grid = Grid(20, 20);
            var flags = NewFlags(grid);
            Vec2d[][] rings = { CircleRing(10, 10, r, 64) };

            RasterPrimitives.FillPolygonEvenOddA(grid, flags, rings, Bit);

            int cells = CountBits(flags, Bit);
            double rasterArea = cells * CellArea(grid);
            double analytical = Math.PI * r * r;
            double perimeter = 2.0 * Math.PI * r;
            double tol = perimeter * Cell / 2.0;
            Assert.That(Math.Abs(rasterArea - analytical), Is.LessThanOrEqualTo(tol + 1e-6),
                $"cells={cells} area={rasterArea} analytical={analytical} tol={tol}");
        }

        [Test]
        public void FillPolygon_SquareWithHole_EvenOdd()
        {
            var grid = Grid(20, 20);
            var flags = NewFlags(grid);
            Vec2d[][] rings =
            {
                RectRing(0, 0, 10, 10),
                RectRing(3, 3, 7, 7)
            };

            RasterPrimitives.FillPolygonEvenOddA(grid, flags, rings, Bit);

            // Center of hole ~ (5,5) must be empty; a point in the ring body ~ (1,1) marked.
            grid.WorldToCell(new Vec2d(5, 5), out int hi, out int hj);
            grid.WorldToCell(new Vec2d(1, 1), out int bi, out int bj);
            Assert.IsTrue(grid.InBounds(hi, hj) && grid.InBounds(bi, bj));
            Assert.AreEqual(0, flags[grid.Index(hi, hj)] & Bit, "hole cell must be empty (even-odd)");
            Assert.AreNotEqual(0, flags[grid.Index(bi, bj)] & Bit, "body cell must be filled");

            int cells = CountBits(flags, Bit);
            double rasterArea = cells * CellArea(grid);
            double analytical = 100.0 - 16.0;
            double perimeter = 40.0 + 16.0;
            double tol = perimeter * Cell / 2.0;
            Assert.That(Math.Abs(rasterArea - analytical), Is.LessThanOrEqualTo(tol + 1e-6));
        }

        [Test]
        public void FillPolygon_HoleTouchingOuterBoundary_EvenOdd()
        {
            // Outer [0,10]², hole sharing the left edge [0,0]–[0,10] inset: hole [0,3]–[4,7].
            var grid = Grid(20, 20);
            var flags = NewFlags(grid);
            Vec2d[][] rings =
            {
                RectRing(0, 0, 10, 10),
                RectRing(0, 3, 4, 7)
            };

            RasterPrimitives.FillPolygonEvenOddA(grid, flags, rings, Bit);

            grid.WorldToCell(new Vec2d(2, 5), out int hi, out int hj);
            grid.WorldToCell(new Vec2d(8, 5), out int bi, out int bj);
            Assert.AreEqual(0, flags[grid.Index(hi, hj)] & Bit, "hole near boundary empty");
            Assert.AreNotEqual(0, flags[grid.Index(bi, bj)] & Bit, "remaining body filled");
        }

        [Test]
        public void FillPolygon_TwoNestedHoles_EvenOdd()
        {
            // Outer, hole, island inside hole → island filled again (even-odd).
            var grid = Grid(20, 20);
            var flags = NewFlags(grid);
            Vec2d[][] rings =
            {
                RectRing(0, 0, 12, 12),
                RectRing(2, 2, 10, 10),
                RectRing(4, 4, 8, 8)
            };

            RasterPrimitives.FillPolygonEvenOddA(grid, flags, rings, Bit);

            grid.WorldToCell(new Vec2d(6, 6), out int ii, out int ij); // island
            grid.WorldToCell(new Vec2d(3, 3), out int hi, out int hj); // hole ring
            grid.WorldToCell(new Vec2d(1, 1), out int oi, out int oj); // outer body
            Assert.AreNotEqual(0, flags[grid.Index(ii, ij)] & Bit, "inner island filled");
            Assert.AreEqual(0, flags[grid.Index(hi, hj)] & Bit, "middle hole empty");
            Assert.AreNotEqual(0, flags[grid.Index(oi, oj)] & Bit, "outer ring filled");
        }

        [Test]
        public void LineA_Horizontal_StableAndNonEmpty()
        {
            var grid = Grid(20, 20);
            var flags = NewFlags(grid);
            var pts = new[] { new Vec2d(2, 10), new Vec2d(18, 10) };

            RasterPrimitives.StampPolylineCapsuleA(grid, flags, pts, width: 1f, Bit);

            int n1 = CountBits(flags, Bit);
            Assert.Greater(n1, 0);

            var flags2 = NewFlags(grid);
            RasterPrimitives.StampPolylineCapsuleA(grid, flags2, pts, width: 1f, Bit);
            Assert.AreEqual(n1, CountBits(flags2, Bit));
        }

        [Test]
        public void LineA_Vertical_StableAndNonEmpty()
        {
            var grid = Grid(20, 20);
            var flags = NewFlags(grid);
            var pts = new[] { new Vec2d(10, 2), new Vec2d(10, 18) };

            RasterPrimitives.StampPolylineCapsuleA(grid, flags, pts, width: 1f, Bit);
            Assert.Greater(CountBits(flags, Bit), 0);
        }

        [Test]
        public void LineA_Diagonal_StableAndNonEmpty()
        {
            var grid = Grid(20, 20);
            var flags = NewFlags(grid);
            var pts = new[] { new Vec2d(2, 2), new Vec2d(18, 18) };

            RasterPrimitives.StampPolylineCapsuleA(grid, flags, pts, width: 1f, Bit);
            int n = CountBits(flags, Bit);
            Assert.Greater(n, 0);

            var flags2 = NewFlags(grid);
            RasterPrimitives.StampPolylineCapsuleA(grid, flags2, pts, width: 1f, Bit);
            Assert.AreEqual(n, CountBits(flags2, Bit));
        }

        [Test]
        public void LineA_ThinWidth_DoesNotDisappear()
        {
            // ADR: halfWidth = max(width/2, cell/2) → thin line still stamps ≥ 1 cell thick.
            var grid = Grid(20, 20);
            var flags = NewFlags(grid);
            var pts = new[] { new Vec2d(2, 10), new Vec2d(18, 10) };

            RasterPrimitives.StampPolylineCapsuleA(grid, flags, pts, width: 0.01f, Bit);

            Assert.Greater(CountBits(flags, Bit), 10, "thin line must not vanish (min halfWidth=cell/2)");
        }

        [Test]
        public void LineB_Barrier_NoEightConnectedLeak_On200RandomSegments()
        {
            const int trials = 200;
            const int seed = 42;
            var rng = new Random(seed);
            var grid = Grid(30, 30);
            int failures = 0;

            for (int t = 0; t < trials; t++)
            {
                double x0 = 2 + rng.NextDouble() * 26;
                double z0 = 2 + rng.NextDouble() * 26;
                double ang = rng.NextDouble() * Math.PI * 2;
                double len = 4 + rng.NextDouble() * 12;
                double x1 = x0 + Math.Cos(ang) * len;
                double z1 = z0 + Math.Sin(ang) * len;
                // Clamp endpoints into grid interior
                x1 = Math.Max(1, Math.Min(29, x1));
                z1 = Math.Max(1, Math.Min(29, z1));

                float width = rng.NextDouble() < 0.3 ? 0f : (float)(0.2 + rng.NextDouble() * 1.5);
                var pts = new[] { new Vec2d(x0, z0), new Vec2d(x1, z1) };
                var flags = NewFlags(grid);
                RasterPrimitives.StampPolylineCapsuleB(grid, flags, pts, width, BarrierBit);

                if (HasEightConnectedLeakAcrossCapsule(grid, flags, BarrierBit, pts[0], pts[1], width))
                {
                    failures++;
                }
            }

            Assert.AreEqual(0, failures,
                $"Mode B barrier leaked on {failures}/{trials} random segments (seed={seed})");
        }

        /// <summary>
        /// Leak = two 8-adjacent free cells whose center-to-center segment intersects the open capsule.
        /// That means an 8-path can step across the barrier without entering a barrier cell.
        /// </summary>
        static bool HasEightConnectedLeakAcrossCapsule(
            CityGrid grid, ushort[] flags, ushort bit, Vec2d a, Vec2d b, float width)
        {
            double half = width * 0.5;
            int[] di = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dj = { -1, -1, -1, 0, 0, 1, 1, 1 };

            for (int j = 0; j < grid.h; j++)
            {
                for (int i = 0; i < grid.w; i++)
                {
                    if ((flags[grid.Index(i, j)] & bit) != 0)
                    {
                        continue;
                    }

                    Vec2d c0 = grid.CellCenter(i, j);
                    for (int k = 0; k < 8; k++)
                    {
                        int ni = i + di[k];
                        int nj = j + dj[k];
                        if (!grid.InBounds(ni, nj))
                        {
                            continue;
                        }

                        // Check each unordered pair once
                        if (ni < i || (ni == i && nj <= j))
                        {
                            continue;
                        }

                        if ((flags[grid.Index(ni, nj)] & bit) != 0)
                        {
                            continue;
                        }

                        Vec2d c1 = grid.CellCenter(ni, nj);
                        if (SegmentIntersectsOpenCapsule(c0, c1, a, b, half))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        static bool SegmentIntersectsOpenCapsule(Vec2d p0, Vec2d p1, Vec2d a, Vec2d b, double halfWidth)
        {
            if (halfWidth <= 1e-12)
            {
                // Zero-width barrier: leak if free-cell edge properly crosses the barrier segment.
                return SegmentsProperlyIntersect(p0, p1, a, b);
            }

            // Sample along free-cell edge; interior sample inside open capsule → leak.
            const int samples = 8;
            for (int s = 1; s < samples; s++)
            {
                double t = s / (double)samples;
                var p = new Vec2d(p0.x + (p1.x - p0.x) * t, p0.z + (p1.z - p0.z) * t);
                if (DistancePointToSegment(p, a, b) < halfWidth - 1e-9)
                {
                    return true;
                }
            }

            return false;
        }

        static bool SegmentsProperlyIntersect(Vec2d a, Vec2d b, Vec2d c, Vec2d d)
        {
            double d1 = Cross(c, d, a);
            double d2 = Cross(c, d, b);
            double d3 = Cross(a, b, c);
            double d4 = Cross(a, b, d);
            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            {
                return true;
            }

            return false;
        }

        static double Cross(Vec2d a, Vec2d b, Vec2d p)
        {
            return (b.x - a.x) * (p.z - a.z) - (b.z - a.z) * (p.x - a.x);
        }

        static double DistancePointToSegment(Vec2d p, Vec2d a, Vec2d b)
        {
            double dx = b.x - a.x;
            double dz = b.z - a.z;
            double len2 = dx * dx + dz * dz;
            if (len2 < 1e-18)
            {
                double ex = p.x - a.x;
                double ez = p.z - a.z;
                return Math.Sqrt(ex * ex + ez * ez);
            }

            double t = ((p.x - a.x) * dx + (p.z - a.z) * dz) / len2;
            if (t < 0)
            {
                t = 0;
            }
            else if (t > 1)
            {
                t = 1;
            }

            double qx = a.x + t * dx - p.x;
            double qz = a.z + t * dz - p.z;
            return Math.Sqrt(qx * qx + qz * qz);
        }
    }
}
