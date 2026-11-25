using System;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// Local equirectangular projection from (lat0, lon0). ADR-001 §3.
    /// X = east, Z = north. R = 6_371_008.8 m.
    /// </summary>
    public readonly struct LocalEquirectangularProjection
    {
        public const double EarthRadiusM = 6371008.8;

        public double Lat0 { get; }
        public double Lon0 { get; }

        readonly double _cosLat0;
        readonly double _metersPerDegLat;
        readonly double _metersPerDegLon;

        public LocalEquirectangularProjection(double lat0, double lon0)
        {
            Lat0 = lat0;
            Lon0 = lon0;
            double lat0Rad = lat0 * (Math.PI / 180.0);
            _cosLat0 = Math.Cos(lat0Rad);
            _metersPerDegLat = EarthRadiusM * (Math.PI / 180.0);
            _metersPerDegLon = EarthRadiusM * (Math.PI / 180.0) * _cosLat0;
        }

        public Vec2d Project(double lat, double lon)
        {
            double x = (lon - Lon0) * _metersPerDegLon;
            double z = (lat - Lat0) * _metersPerDegLat;
            return new Vec2d(x, z);
        }

        public static LocalEquirectangularProjection FromBBox(
            double minLat, double minLon, double maxLat, double maxLon)
        {
            double lat0 = (minLat + maxLat) * 0.5;
            double lon0 = (minLon + maxLon) * 0.5;
            return new LocalEquirectangularProjection(lat0, lon0);
        }
    }
}
