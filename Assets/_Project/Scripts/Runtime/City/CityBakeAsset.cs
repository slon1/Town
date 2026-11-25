using System;
using UnityEngine;
using ZombieCity.City.Core;

namespace ZombieCity.City
{
    /// <summary>
    /// Baked city rasters (ADR-002 §6). Implements <see cref="ICityMap"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "CityBake", menuName = "ZombieCity/City/City Bake")]
    public class CityBakeAsset : ScriptableObject, ICityMap
    {
        public double originX;
        public double originZ;
        public float cell = 0.5f;
        public int w;
        public int h;

        /// <summary>ushort little-endian, length w·h·2.</summary>
        public byte[] flagsBytes = Array.Empty<byte>();

        /// <summary>ushort little-endian, length w·h·2.</summary>
        public byte[] buildingIdBytes = Array.Empty<byte>();

        public BuildingRasterInfo[] buildings = Array.Empty<BuildingRasterInfo>();
        public RasterStats stats = new RasterStats();

        public ulong modelDataHash;
        public ulong configHash;
        public ulong dataHash;

        [NonSerialized] ushort[] _flagsCache;
        [NonSerialized] ushort[] _buildingIdCache;

        public CityGrid Grid => new CityGrid(new Vec2d(originX, originZ), cell, w, h);

        public CellFlags GetFlags(int i, int j)
        {
            EnsureCaches();
            if (i < 0 || j < 0 || i >= w || j >= h)
            {
                return CellFlags.None;
            }

            return (CellFlags)_flagsCache[i + j * w];
        }

        public ushort GetBuildingId(int i, int j)
        {
            EnsureCaches();
            if (i < 0 || j < 0 || i >= w || j >= h)
            {
                return 0;
            }

            return _buildingIdCache[i + j * w];
        }

        public void ApplyBakeResult(RasterBakeResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            originX = result.grid.origin.x;
            originZ = result.grid.origin.z;
            cell = result.grid.cell;
            w = result.grid.w;
            h = result.grid.h;
            flagsBytes = UshortToBytesLE(result.flags);
            buildingIdBytes = UshortToBytesLE(result.buildingId);
            buildings = result.buildings ?? Array.Empty<BuildingRasterInfo>();
            stats = result.stats ?? new RasterStats();
            modelDataHash = result.modelDataHash;
            configHash = result.configHash;
            dataHash = result.dataHash;
            _flagsCache = result.flags;
            _buildingIdCache = result.buildingId;
        }

        public ushort[] GetFlagsArray()
        {
            EnsureCaches();
            return _flagsCache;
        }

        public ushort[] GetBuildingIdArray()
        {
            EnsureCaches();
            return _buildingIdCache;
        }

        void EnsureCaches()
        {
            int expected = w * h;
            if (_flagsCache != null && _flagsCache.Length == expected &&
                _buildingIdCache != null && _buildingIdCache.Length == expected)
            {
                return;
            }

            _flagsCache = BytesToUshortLE(flagsBytes, expected);
            _buildingIdCache = BytesToUshortLE(buildingIdBytes, expected);
        }

        static byte[] UshortToBytesLE(ushort[] src)
        {
            if (src == null || src.Length == 0)
            {
                return Array.Empty<byte>();
            }

            var bytes = new byte[src.Length * 2];
            for (int i = 0; i < src.Length; i++)
            {
                bytes[i * 2] = (byte)src[i];
                bytes[i * 2 + 1] = (byte)(src[i] >> 8);
            }

            return bytes;
        }

        static ushort[] BytesToUshortLE(byte[] bytes, int count)
        {
            var arr = new ushort[count];
            if (bytes == null)
            {
                return arr;
            }

            int n = Math.Min(count, bytes.Length / 2);
            for (int i = 0; i < n; i++)
            {
                arr[i] = (ushort)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
            }

            return arr;
        }
    }
}
