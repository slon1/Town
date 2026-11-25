using System;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// Low-level raster stamps on ushort[] flags (ADR-002 §3).
    /// Index = i + j·w. Stubbed in 2B-tests; algorithms filled in 2B.
    /// </summary>
    public static class RasterPrimitives
    {
        /// <summary>
        /// Mode A: mark cells whose centers lie inside the polygon (even-odd over all rings).
        /// Each ring is a polyline; closing edge is implied if first≠last.
        /// </summary>
        public static void FillPolygonEvenOddA(CityGrid grid, ushort[] flags, Vec2d[][] rings, ushort bit)
        {
            throw new NotImplementedException("RasterPrimitives.FillPolygonEvenOddA — implement in 2B");
        }

        /// <summary>
        /// Mode A: capsule polyline, halfWidth = max(width/2, cell/2). Center-in-capsule.
        /// </summary>
        public static void StampPolylineCapsuleA(CityGrid grid, ushort[] flags, Vec2d[] points, float width, ushort bit)
        {
            throw new NotImplementedException("RasterPrimitives.StampPolylineCapsuleA — implement in 2B");
        }

        /// <summary>
        /// Mode B: conservative capsule polyline, halfWidth = width/2 (may be 0).
        /// Marks cell if capsule intersects the closed cell square.
        /// </summary>
        public static void StampPolylineCapsuleB(CityGrid grid, ushort[] flags, Vec2d[] points, float width, ushort bit)
        {
            throw new NotImplementedException("RasterPrimitives.StampPolylineCapsuleB — implement in 2B");
        }
    }
}
