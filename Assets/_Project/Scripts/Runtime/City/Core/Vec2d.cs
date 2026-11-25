using System;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// 2D point in local projected meters (X east, Z north stored as Y field name avoided — use X/Z).
    /// </summary>
    [Serializable]
    public struct Vec2d : IEquatable<Vec2d>
    {
        public double x;
        public double z;

        public Vec2d(double x, double z)
        {
            this.x = x;
            this.z = z;
        }

        public bool Equals(Vec2d other)
        {
            return x.Equals(other.x) && z.Equals(other.z);
        }

        public override bool Equals(object obj)
        {
            return obj is Vec2d other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (x.GetHashCode() * 397) ^ z.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"({x}, {z})";
        }
    }
}
