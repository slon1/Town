using System;
using System.Collections.Generic;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// Ring normalization and point-in-polygon (X east / Z north). ADR-001 §4.
    /// </summary>
    public static class RingOps
    {
        const double Eps = 1e-9;

        public static double SignedArea(IReadOnlyList<Vec2d> ring)
        {
            if (ring == null || ring.Count < 3)
            {
                return 0;
            }

            double sum = 0;
            int n = ring.Count;
            // If closed (first==last), skip last duplicate in sum
            int last = n;
            if (n > 1 && NearlyEqual(ring[0], ring[n - 1]))
            {
                last = n - 1;
            }

            for (int i = 0; i < last; i++)
            {
                Vec2d a = ring[i];
                Vec2d b = ring[(i + 1) % last];
                sum += a.x * b.z - b.x * a.z;
            }

            return 0.5 * sum;
        }

        public static bool IsCcw(IReadOnlyList<Vec2d> ring)
        {
            return SignedArea(ring) > 0;
        }

        public static void ReverseInPlace(List<Vec2d> ring)
        {
            if (ring == null || ring.Count < 2)
            {
                return;
            }

            bool closed = NearlyEqual(ring[0], ring[ring.Count - 1]);
            if (closed)
            {
                ring.RemoveAt(ring.Count - 1);
            }

            ring.Reverse();
            if (closed && ring.Count > 0)
            {
                ring.Add(ring[0]);
            }
        }

        /// <summary>
        /// Close ring, remove consecutive duplicates, ensure orientation (ccw=true → CCW).
        /// Returns false if fewer than 3 unique vertices.
        /// </summary>
        public static bool Normalize(List<Vec2d> ring, bool wantCcw)
        {
            if (ring == null)
            {
                return false;
            }

            RemoveConsecutiveDuplicates(ring);
            if (ring.Count >= 2 && NearlyEqual(ring[0], ring[ring.Count - 1]))
            {
                ring.RemoveAt(ring.Count - 1);
            }

            RemoveConsecutiveDuplicates(ring);
            if (ring.Count < 3)
            {
                return false;
            }

            ring.Add(ring[0]);
            bool ccw = IsCcw(ring);
            if (wantCcw != ccw)
            {
                ReverseInPlace(ring);
            }

            return ring.Count >= 4;
        }

        public static void RemoveConsecutiveDuplicates(List<Vec2d> ring)
        {
            if (ring == null || ring.Count < 2)
            {
                return;
            }

            for (int i = ring.Count - 1; i > 0; i--)
            {
                if (NearlyEqual(ring[i], ring[i - 1]))
                {
                    ring.RemoveAt(i);
                }
            }
        }

        public static bool PointInPolygon(Vec2d point, IReadOnlyList<Vec2d> ring)
        {
            if (ring == null || ring.Count < 3)
            {
                return false;
            }

            int n = ring.Count;
            int last = n;
            if (n > 1 && NearlyEqual(ring[0], ring[n - 1]))
            {
                last = n - 1;
            }

            bool inside = false;
            for (int i = 0, j = last - 1; i < last; j = i++)
            {
                Vec2d pi = ring[i];
                Vec2d pj = ring[j];
                bool intersect = ((pi.z > point.z) != (pj.z > point.z)) &&
                    (point.x < (pj.x - pi.x) * (point.z - pi.z) / (pj.z - pi.z + Eps) + pi.x);
                if (intersect)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>Interior sample point (centroid of first triangle); fallback to first vertex offset.</summary>
        public static Vec2d InteriorSample(IReadOnlyList<Vec2d> ring)
        {
            if (ring == null || ring.Count == 0)
            {
                return new Vec2d(0, 0);
            }

            int n = ring.Count;
            int last = n;
            if (n > 1 && NearlyEqual(ring[0], ring[n - 1]))
            {
                last = n - 1;
            }

            if (last >= 3)
            {
                double cx = (ring[0].x + ring[1].x + ring[2].x) / 3.0;
                double cz = (ring[0].z + ring[1].z + ring[2].z) / 3.0;
                var sample = new Vec2d(cx, cz);
                if (PointInPolygon(sample, ring))
                {
                    return sample;
                }
            }

            // Slight offset from first edge midpoint toward polygon
            Vec2d a = ring[0];
            Vec2d b = ring[Math.Min(1, last - 1)];
            double mx = (a.x + b.x) * 0.5;
            double mz = (a.z + b.z) * 0.5;
            double dx = -(b.z - a.z);
            double dz = b.x - a.x;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len > Eps)
            {
                dx /= len;
                dz /= len;
            }

            var left = new Vec2d(mx + dx * 0.01, mz + dz * 0.01);
            var right = new Vec2d(mx - dx * 0.01, mz - dz * 0.01);
            if (PointInPolygon(left, ring))
            {
                return left;
            }

            if (PointInPolygon(right, ring))
            {
                return right;
            }

            return a;
        }

        public static bool NearlyEqual(Vec2d a, Vec2d b)
        {
            return Math.Abs(a.x - b.x) < 1e-6 && Math.Abs(a.z - b.z) < 1e-6;
        }

        public static double PolylineLength(IReadOnlyList<Vec2d> points)
        {
            if (points == null || points.Count < 2)
            {
                return 0;
            }

            double len = 0;
            for (int i = 1; i < points.Count; i++)
            {
                double dx = points[i].x - points[i - 1].x;
                double dz = points[i].z - points[i - 1].z;
                len += Math.Sqrt(dx * dx + dz * dz);
            }

            return len;
        }
    }
}
