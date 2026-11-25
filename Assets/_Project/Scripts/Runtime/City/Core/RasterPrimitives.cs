using System;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// Low-level raster stamps on ushort[] flags (ADR-002 §3).
    /// Index = i + j·w.
    /// </summary>
    public static class RasterPrimitives
    {
        const double Eps = 1e-9;

        /// <summary>
        /// Mode A: mark cells whose centers lie inside the polygon (even-odd over all rings).
        /// Each ring is a polyline; closing edge is implied if first≠last.
        /// </summary>
        public static void FillPolygonEvenOddA(CityGrid grid, ushort[] flags, Vec2d[][] rings, ushort bit)
        {
            if (flags == null || flags.Length < grid.CellCount)
            {
                throw new ArgumentException("flags length must be >= grid.CellCount", nameof(flags));
            }

            if (rings == null || rings.Length == 0)
            {
                return;
            }

            if (!TryGetRingsBBox(rings, out double minX, out double minZ, out double maxX, out double maxZ))
            {
                return;
            }

            grid.WorldToCell(new Vec2d(minX, minZ), out int i0, out int j0);
            grid.WorldToCell(new Vec2d(maxX, maxZ), out int i1, out int j1);
            if (i0 > i1)
            {
                int t = i0;
                i0 = i1;
                i1 = t;
            }

            if (j0 > j1)
            {
                int t = j0;
                j0 = j1;
                j1 = t;
            }

            i0 = Math.Max(0, i0);
            j0 = Math.Max(0, j0);
            i1 = Math.Min(grid.w - 1, i1);
            j1 = Math.Min(grid.h - 1, j1);

            for (int j = j0; j <= j1; j++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    Vec2d c = grid.CellCenter(i, j);
                    if (PointInPolygonEvenOdd(c, rings))
                    {
                        flags[grid.Index(i, j)] |= bit;
                    }
                }
            }
        }

        /// <summary>
        /// Mode A: capsule polyline, halfWidth = max(width/2, cell/2). Center-in-capsule.
        /// </summary>
        public static void StampPolylineCapsuleA(CityGrid grid, ushort[] flags, Vec2d[] points, float width, ushort bit)
        {
            if (flags == null || flags.Length < grid.CellCount)
            {
                throw new ArgumentException("flags length must be >= grid.CellCount", nameof(flags));
            }

            if (points == null || points.Length < 2)
            {
                return;
            }

            double half = Math.Max(width * 0.5, grid.cell * 0.5);
            for (int s = 0; s < points.Length - 1; s++)
            {
                StampSegmentCapsuleA(grid, flags, points[s], points[s + 1], half, bit);
            }
        }

        /// <summary>
        /// Mode B: conservative capsule polyline, halfWidth = width/2 (may be 0).
        /// Marks cell if capsule intersects the closed cell square.
        /// </summary>
        public static void StampPolylineCapsuleB(CityGrid grid, ushort[] flags, Vec2d[] points, float width, ushort bit)
        {
            if (flags == null || flags.Length < grid.CellCount)
            {
                throw new ArgumentException("flags length must be >= grid.CellCount", nameof(flags));
            }

            if (points == null || points.Length < 2)
            {
                return;
            }

            double half = width * 0.5;
            for (int s = 0; s < points.Length - 1; s++)
            {
                StampSegmentCapsuleB(grid, flags, points[s], points[s + 1], half, bit);
            }
        }

        static void StampSegmentCapsuleA(
            CityGrid grid, ushort[] flags, Vec2d a, Vec2d b, double half, ushort bit)
        {
            double pad = half + grid.cell;
            double minX = Math.Min(a.x, b.x) - pad;
            double maxX = Math.Max(a.x, b.x) + pad;
            double minZ = Math.Min(a.z, b.z) - pad;
            double maxZ = Math.Max(a.z, b.z) + pad;

            grid.WorldToCell(new Vec2d(minX, minZ), out int i0, out int j0);
            grid.WorldToCell(new Vec2d(maxX, maxZ), out int i1, out int j1);
            if (i0 > i1)
            {
                int t = i0;
                i0 = i1;
                i1 = t;
            }

            if (j0 > j1)
            {
                int t = j0;
                j0 = j1;
                j1 = t;
            }

            i0 = Math.Max(0, i0);
            j0 = Math.Max(0, j0);
            i1 = Math.Min(grid.w - 1, i1);
            j1 = Math.Min(grid.h - 1, j1);

            for (int j = j0; j <= j1; j++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    Vec2d c = grid.CellCenter(i, j);
                    if (DistancePointToSegment(c, a, b) <= half + Eps)
                    {
                        flags[grid.Index(i, j)] |= bit;
                    }
                }
            }
        }

        static void StampSegmentCapsuleB(
            CityGrid grid, ushort[] flags, Vec2d a, Vec2d b, double half, ushort bit)
        {
            double pad = half + grid.cell;
            double minX = Math.Min(a.x, b.x) - pad;
            double maxX = Math.Max(a.x, b.x) + pad;
            double minZ = Math.Min(a.z, b.z) - pad;
            double maxZ = Math.Max(a.z, b.z) + pad;

            grid.WorldToCell(new Vec2d(minX, minZ), out int i0, out int j0);
            grid.WorldToCell(new Vec2d(maxX, maxZ), out int i1, out int j1);
            if (i0 > i1)
            {
                int t = i0;
                i0 = i1;
                i1 = t;
            }

            if (j0 > j1)
            {
                int t = j0;
                j0 = j1;
                j1 = t;
            }

            i0 = Math.Max(0, i0);
            j0 = Math.Max(0, j0);
            i1 = Math.Min(grid.w - 1, i1);
            j1 = Math.Min(grid.h - 1, j1);

            double cell = grid.cell;
            for (int j = j0; j <= j1; j++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    Vec2d mn = grid.CellMin(i, j);
                    // Capsule∩closed square: Euclidean distance(segment, AABB) ≤ half.
                    if (DistanceSegmentToAabb(a, b, mn.x, mn.z, mn.x + cell, mn.z + cell) <= half + Eps)
                    {
                        flags[grid.Index(i, j)] |= bit;
                    }
                }
            }
        }

        static bool PointInPolygonEvenOdd(Vec2d p, Vec2d[][] rings)
        {
            bool inside = false;
            for (int r = 0; r < rings.Length; r++)
            {
                Vec2d[] ring = rings[r];
                if (ring == null || ring.Length < 2)
                {
                    continue;
                }

                int n = ring.Length;
                int edgeCount = n;
                if (ApproxEqual(ring[0], ring[n - 1]))
                {
                    edgeCount = n - 1;
                }

                if (edgeCount < 2)
                {
                    continue;
                }

                for (int i = 0, j = edgeCount - 1; i < edgeCount; j = i++)
                {
                    Vec2d a = ring[j];
                    Vec2d b = ring[i];
                    bool crosses = (a.z > p.z) != (b.z > p.z);
                    if (!crosses)
                    {
                        continue;
                    }

                    double xInt = (b.x - a.x) * (p.z - a.z) / (b.z - a.z) + a.x;
                    if (p.x < xInt)
                    {
                        inside = !inside;
                    }
                }
            }

            return inside;
        }

        static bool TryGetRingsBBox(
            Vec2d[][] rings,
            out double minX, out double minZ, out double maxX, out double maxZ)
        {
            minX = double.PositiveInfinity;
            minZ = double.PositiveInfinity;
            maxX = double.NegativeInfinity;
            maxZ = double.NegativeInfinity;
            bool any = false;
            for (int r = 0; r < rings.Length; r++)
            {
                Vec2d[] ring = rings[r];
                if (ring == null)
                {
                    continue;
                }

                for (int i = 0; i < ring.Length; i++)
                {
                    Vec2d p = ring[i];
                    if (p.x < minX)
                    {
                        minX = p.x;
                    }

                    if (p.z < minZ)
                    {
                        minZ = p.z;
                    }

                    if (p.x > maxX)
                    {
                        maxX = p.x;
                    }

                    if (p.z > maxZ)
                    {
                        maxZ = p.z;
                    }

                    any = true;
                }
            }

            return any;
        }

        static bool ApproxEqual(Vec2d a, Vec2d b)
        {
            return Math.Abs(a.x - b.x) < 1e-12 && Math.Abs(a.z - b.z) < 1e-12;
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

        /// <summary>
        /// Minimum distance from segment AB to closed AABB [minX,maxX]×[minZ,maxZ].
        /// </summary>
        static double DistanceSegmentToAabb(
            Vec2d a, Vec2d b, double minX, double minZ, double maxX, double maxZ)
        {
            if (SegmentIntersectsAabb(a, b, minX, minZ, maxX, maxZ))
            {
                return 0;
            }

            // Endpoints → AABB, and AABB corners → segment (covers closest features for convex AABB).
            double d = Math.Min(
                DistancePointToAabb(a, minX, minZ, maxX, maxZ),
                DistancePointToAabb(b, minX, minZ, maxX, maxZ));
            d = Math.Min(d, DistancePointToSegment(new Vec2d(minX, minZ), a, b));
            d = Math.Min(d, DistancePointToSegment(new Vec2d(maxX, minZ), a, b));
            d = Math.Min(d, DistancePointToSegment(new Vec2d(maxX, maxZ), a, b));
            d = Math.Min(d, DistancePointToSegment(new Vec2d(minX, maxZ), a, b));
            return d;
        }

        static double DistancePointToAabb(Vec2d p, double minX, double minZ, double maxX, double maxZ)
        {
            double dx = 0;
            if (p.x < minX)
            {
                dx = minX - p.x;
            }
            else if (p.x > maxX)
            {
                dx = p.x - maxX;
            }

            double dz = 0;
            if (p.z < minZ)
            {
                dz = minZ - p.z;
            }
            else if (p.z > maxZ)
            {
                dz = p.z - maxZ;
            }

            return Math.Sqrt(dx * dx + dz * dz);
        }

        static bool SegmentIntersectsAabb(
            Vec2d a, Vec2d b, double minX, double minZ, double maxX, double maxZ)
        {
            // Liang–Barsky slabs (p<0 entering → raise t0; p>0 leaving → lower t1).
            double dx = b.x - a.x;
            double dz = b.z - a.z;
            double t0 = 0;
            double t1 = 1;

            if (!ClipSlab(-dx, a.x - minX, ref t0, ref t1))
            {
                return false;
            }

            if (!ClipSlab(dx, maxX - a.x, ref t0, ref t1))
            {
                return false;
            }

            if (!ClipSlab(-dz, a.z - minZ, ref t0, ref t1))
            {
                return false;
            }

            if (!ClipSlab(dz, maxZ - a.z, ref t0, ref t1))
            {
                return false;
            }

            return t0 <= t1;
        }

        static bool ClipSlab(double p, double q, ref double t0, ref double t1)
        {
            if (Math.Abs(p) < 1e-18)
            {
                return q >= 0;
            }

            double r = q / p;
            if (p < 0)
            {
                if (r > t1)
                {
                    return false;
                }

                if (r > t0)
                {
                    t0 = r;
                }
            }
            else
            {
                if (r < t0)
                {
                    return false;
                }

                if (r < t1)
                {
                    t1 = r;
                }
            }

            return true;
        }

    }
}
