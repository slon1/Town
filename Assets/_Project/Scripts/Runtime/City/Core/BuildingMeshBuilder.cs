using System;
using System.Collections.Generic;
using LibTessDotNet;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// Extruded building mesh (ADR-004 §4). Positions are tightly packed xyz meters.
    /// Colors are RGBA32 little-endian. Indices are triangle lists.
    /// </summary>
    public sealed class BuildingMeshData
    {
        public float[] positions = Array.Empty<float>();
        public int[] indices = Array.Empty<int>();
        public uint[] colors = Array.Empty<uint>();
        public int buildingCount;
        public int skippedBuildings;
        public int skippedRings;
        public int vertexCount;
        public int triangleCount;
    }

    /// <summary>
    /// Roofs via LibTessDotNet EvenOdd, walls per ring edge, no floor.
    /// Front face is the one Unity renders with Cull Back. Roofs face +Y.
    /// </summary>
    public static class BuildingMeshBuilder
    {
        public const uint RoofTagged = 0xFFD8CCC5u;
        public const uint WallTagged = 0xFFA6988Fu;
        public const uint RoofDefault = 0xFF3D8AFFu;
        public const uint WallDefault = 0xFF1A5AC7u;

        const double MinRingArea = 1e-4;

        public static BuildingMeshData Build(CityModel model)
        {
            var positions = new List<float>(256);
            var indices = new List<int>(256);
            var colors = new List<uint>(256);
            var data = new BuildingMeshData();
            Area[] areas = model?.areas;
            if (areas == null || areas.Length == 0)
            {
                return data;
            }

            int[] order = new int[areas.Length];
            int buildingSlots = 0;
            for (int i = 0; i < areas.Length; i++)
            {
                if (areas[i] != null && areas[i].kind == AreaKind.Building)
                {
                    order[buildingSlots++] = i;
                }
            }

            Array.Sort(order, 0, buildingSlots, Comparer<int>.Create((a, b) =>
            {
                int cmp = areas[a].osmId.CompareTo(areas[b].osmId);
                return cmp != 0 ? cmp : a.CompareTo(b);
            }));

            for (int n = 0; n < buildingSlots; n++)
            {
                if (AppendBuilding(areas[order[n]], positions, indices, colors, data))
                {
                    data.buildingCount++;
                }
                else
                {
                    data.skippedBuildings++;
                }
            }

            data.positions = positions.ToArray();
            data.indices = indices.ToArray();
            data.colors = colors.ToArray();
            data.vertexCount = colors.Count;
            data.triangleCount = indices.Count / 3;
            return data;
        }

        static bool AppendBuilding(Area area, List<float> positions, List<int> indices, List<uint> colors, BuildingMeshData data)
        {
            if (area.points == null || area.ringStart == null || area.ringStart.Length == 0)
            {
                data.skippedRings++;
                return false;
            }

            if (area.heightM <= area.minHeightM + 1e-4f)
            {
                data.skippedRings++;
                return false;
            }

            int ringCount = area.ringStart.Length;
            var rings = new List<Vec2d[]>(ringCount);
            for (int r = 0; r < ringCount; r++)
            {
                if (!TryRing(area, r, out Vec2d[] ring))
                {
                    data.skippedRings++;
                    if (r == 0)
                    {
                        return false;
                    }

                    continue;
                }

                bool wantCcw = r == 0;
                double signed = SignedArea(ring);
                if (wantCcw && signed < 0 || !wantCcw && signed > 0)
                {
                    Array.Reverse(ring);
                }

                rings.Add(ring);
            }

            if (rings.Count == 0)
            {
                return false;
            }

            uint roof = area.heightSource == HeightSource.Default ? RoofDefault : RoofTagged;
            uint wall = area.heightSource == HeightSource.Default ? WallDefault : WallTagged;
            if (!AppendRoof(rings, area.heightM, roof, positions, indices, colors))
            {
                data.skippedRings++;
                return false;
            }

            for (int r = 0; r < rings.Count; r++)
            {
                AppendWalls(rings[r], area.minHeightM, area.heightM, wall, positions, indices, colors);
            }

            return true;
        }

        static bool TryRing(Area area, int ring, out Vec2d[] cleaned)
        {
            cleaned = null;
            int start = area.ringStart[ring];
            int end = ring + 1 < area.ringStart.Length ? area.ringStart[ring + 1] : area.points.Length;
            if (start < 0 || end > area.points.Length || end - start < 3)
            {
                return false;
            }

            var list = new List<Vec2d>(end - start);
            for (int i = start; i < end; i++)
            {
                Vec2d p = area.points[i];
                if (list.Count > 0 && NearlySame(list[list.Count - 1], p))
                {
                    continue;
                }

                list.Add(p);
            }

            if (list.Count >= 2 && NearlySame(list[0], list[list.Count - 1]))
            {
                list.RemoveAt(list.Count - 1);
            }

            if (list.Count < 3 || Math.Abs(SignedArea(list)) < MinRingArea)
            {
                return false;
            }

            cleaned = list.ToArray();
            return true;
        }

        static bool AppendRoof(List<Vec2d[]> rings, float y, uint color, List<float> positions, List<int> indices, List<uint> colors)
        {
            var tess = new Tess();
            for (int r = 0; r < rings.Count; r++)
            {
                Vec2d[] ring = rings[r];
                var contour = new ContourVertex[ring.Length];
                for (int i = 0; i < ring.Length; i++)
                {
                    contour[i] = new ContourVertex(new Vec3((float)ring[i].x, (float)ring[i].z, 0f));
                }

                tess.AddContour(contour, r == 0 ? ContourOrientation.CounterClockwise : ContourOrientation.Clockwise);
            }

            tess.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3);
            if (tess.ElementCount <= 0 || tess.Vertices == null)
            {
                return false;
            }

            int baseVertex = colors.Count;
            for (int i = 0; i < tess.VertexCount; i++)
            {
                Vec3 p = tess.Vertices[i].Position;
                positions.Add(p.X);
                positions.Add(y);
                positions.Add(p.Y);
                colors.Add(color);
            }

            for (int e = 0; e < tess.ElementCount; e++)
            {
                int i0 = tess.Elements[e * 3];
                int i1 = tess.Elements[e * 3 + 1];
                int i2 = tess.Elements[e * 3 + 2];
                if (i0 == Tess.Undef || i1 == Tess.Undef || i2 == Tess.Undef)
                {
                    continue;
                }

                int v0 = baseVertex + i0;
                int v1 = baseVertex + i1;
                int v2 = baseVertex + i2;
                if (RoofNormalY(positions, v0, v1, v2) < 0f)
                {
                    int swap = v1;
                    v1 = v2;
                    v2 = swap;
                }

                indices.Add(v0);
                indices.Add(v1);
                indices.Add(v2);
            }

            return true;
        }

        static void AppendWalls(Vec2d[] ring, float y0, float y1, uint color, List<float> positions, List<int> indices, List<uint> colors)
        {
            int n = ring.Length;
            for (int i = 0; i < n; i++)
            {
                Vec2d a = ring[i];
                Vec2d b = ring[(i + 1) % n];
                float ax = (float)a.x;
                float az = (float)a.z;
                float bx = (float)b.x;
                float bz = (float)b.z;
                int v = colors.Count;
                AddVert(positions, colors, ax, y0, az, color);
                AddVert(positions, colors, bx, y0, bz, color);
                AddVert(positions, colors, ax, y1, az, color);
                AddVert(positions, colors, bx, y1, bz, color);
                indices.Add(v);
                indices.Add(v + 2);
                indices.Add(v + 1);
                indices.Add(v + 1);
                indices.Add(v + 2);
                indices.Add(v + 3);
            }
        }

        static void AddVert(List<float> positions, List<uint> colors, float x, float y, float z, uint color)
        {
            positions.Add(x);
            positions.Add(y);
            positions.Add(z);
            colors.Add(color);
        }

        static float RoofNormalY(List<float> positions, int v0, int v1, int v2)
        {
            float ax = positions[v1 * 3] - positions[v0 * 3];
            float ay = positions[v1 * 3 + 1] - positions[v0 * 3 + 1];
            float az = positions[v1 * 3 + 2] - positions[v0 * 3 + 2];
            float bx = positions[v2 * 3] - positions[v0 * 3];
            float by = positions[v2 * 3 + 1] - positions[v0 * 3 + 1];
            float bz = positions[v2 * 3 + 2] - positions[v0 * 3 + 2];
            return az * bx - ax * bz;
        }

        static double SignedArea(IList<Vec2d> ring)
        {
            double s = 0;
            int n = ring.Count;
            for (int i = 0; i < n; i++)
            {
                Vec2d a = ring[i];
                Vec2d b = ring[(i + 1) % n];
                s += a.x * b.z - b.x * a.z;
            }

            return s * 0.5;
        }

        static double SignedArea(Vec2d[] ring)
        {
            double s = 0;
            int n = ring.Length;
            for (int i = 0; i < n; i++)
            {
                Vec2d a = ring[i];
                Vec2d b = ring[(i + 1) % n];
                s += a.x * b.z - b.x * a.z;
            }

            return s * 0.5;
        }

        static bool NearlySame(Vec2d a, Vec2d b)
        {
            return Math.Abs(a.x - b.x) < 1e-6 && Math.Abs(a.z - b.z) < 1e-6;
        }
    }
}
