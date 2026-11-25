using System;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// Discrete metric grid over the city bbox (ADR-002 §2).
    /// Cell index = i + j·w; i along X (east), j along Z (north).
    /// </summary>
    public readonly struct CityGrid : IEquatable<CityGrid>
    {
        public readonly Vec2d origin;
        public readonly float cell;
        public readonly int w;
        public readonly int h;

        public CityGrid(Vec2d origin, float cell, int w, int h)
        {
            if (cell <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cell), "cell must be > 0");
            }

            if (w < 0 || h < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(w), "w and h must be >= 0");
            }

            this.origin = origin;
            this.cell = cell;
            this.w = w;
            this.h = h;
        }

        public int CellCount => w * h;

        public bool InBounds(int i, int j)
        {
            return i >= 0 && j >= 0 && i < w && j < h;
        }

        public int Index(int i, int j)
        {
            return i + j * w;
        }

        public void WorldToCell(Vec2d world, out int i, out int j)
        {
            double inv = 1.0 / cell;
            i = (int)Math.Floor((world.x - origin.x) * inv);
            j = (int)Math.Floor((world.z - origin.z) * inv);
        }

        public Vec2d CellCenter(int i, int j)
        {
            double half = cell * 0.5;
            return new Vec2d(origin.x + (i + 0.5) * cell, origin.z + (j + 0.5) * cell);
        }

        public Vec2d CellMin(int i, int j)
        {
            return new Vec2d(origin.x + i * cell, origin.z + j * cell);
        }

        /// <summary>
        /// Builds grid covering [bboxMin, bboxMax] with origin = floor(bboxMin/cell)·cell (ADR-002 §2).
        /// </summary>
        public static CityGrid FromBBox(Vec2d bboxMin, Vec2d bboxMax, float cell)
        {
            if (cell <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cell), "cell must be > 0");
            }

            double ox = Math.Floor(bboxMin.x / cell) * cell;
            double oz = Math.Floor(bboxMin.z / cell) * cell;
            int w = (int)Math.Ceiling((bboxMax.x - ox) / cell);
            int h = (int)Math.Ceiling((bboxMax.z - oz) / cell);
            if (w < 0)
            {
                w = 0;
            }

            if (h < 0)
            {
                h = 0;
            }

            return new CityGrid(new Vec2d(ox, oz), cell, w, h);
        }

        /// <summary>
        /// Metric bbox from model meta (lat/lon bounds projected at meta.lat0/lon0).
        /// </summary>
        public static CityGrid FromModel(CityModel model, float cell)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            CityModelMeta meta = model.meta ?? new CityModelMeta();
            var proj = new LocalEquirectangularProjection(meta.lat0, meta.lon0);
            Vec2d a = proj.Project(meta.bboxMinLat, meta.bboxMinLon);
            Vec2d b = proj.Project(meta.bboxMaxLat, meta.bboxMaxLon);
            var min = new Vec2d(Math.Min(a.x, b.x), Math.Min(a.z, b.z));
            var max = new Vec2d(Math.Max(a.x, b.x), Math.Max(a.z, b.z));
            return FromBBox(min, max, cell);
        }

        public bool Equals(CityGrid other)
        {
            return origin.Equals(other.origin) && cell.Equals(other.cell) && w == other.w && h == other.h;
        }

        public override bool Equals(object obj)
        {
            return obj is CityGrid other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = origin.GetHashCode();
                hash = (hash * 397) ^ cell.GetHashCode();
                hash = (hash * 397) ^ w;
                hash = (hash * 397) ^ h;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"CityGrid(origin={origin}, cell={cell}, {w}x{h})";
        }
    }
}
