using System;
using System.Collections.Generic;
using System.Text;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// Little-endian canonical byte writer for FNV-1a (ADR-001 §4).
    /// </summary>
    public sealed class CanonicalWriter
    {
        readonly List<byte> _bytes = new List<byte>(256);

        public int Length => _bytes.Count;

        public byte[] ToArray()
        {
            return _bytes.ToArray();
        }

        public void WriteBool(bool value)
        {
            _bytes.Add(value ? (byte)1 : (byte)0);
        }

        public void WriteInt32(int value)
        {
            unchecked
            {
                _bytes.Add((byte)value);
                _bytes.Add((byte)(value >> 8));
                _bytes.Add((byte)(value >> 16));
                _bytes.Add((byte)(value >> 24));
            }
        }

        public void WriteInt64(long value)
        {
            unchecked
            {
                _bytes.Add((byte)value);
                _bytes.Add((byte)(value >> 8));
                _bytes.Add((byte)(value >> 16));
                _bytes.Add((byte)(value >> 24));
                _bytes.Add((byte)(value >> 32));
                _bytes.Add((byte)(value >> 40));
                _bytes.Add((byte)(value >> 48));
                _bytes.Add((byte)(value >> 56));
            }
        }

        public void WriteEnum<T>(T value) where T : Enum
        {
            WriteInt32(Convert.ToInt32(value));
        }

        /// <summary>Metric value as integer millimeters.</summary>
        public void WriteMeters(double meters)
        {
            WriteInt64((long)Math.Round(meters * 1000.0));
        }

        public void WriteMeters(float meters)
        {
            WriteMeters((double)meters);
        }

        /// <summary>Geographic degrees as 1e-7 deg units (deterministic across platforms).</summary>
        public void WriteLatLon(double degrees)
        {
            WriteInt64((long)Math.Round(degrees * 10000000.0));
        }

        public void WriteVec2d(Vec2d v)
        {
            WriteMeters(v.x);
            WriteMeters(v.z);
        }

        public void WriteString(string value)
        {
            if (value == null)
            {
                WriteInt32(0);
                return;
            }

            byte[] utf8 = Encoding.UTF8.GetBytes(value);
            WriteInt32(utf8.Length);
            _bytes.AddRange(utf8);
        }

        public void WriteVec2dArray(Vec2d[] points)
        {
            if (points == null)
            {
                WriteInt32(0);
                return;
            }

            WriteInt32(points.Length);
            for (int i = 0; i < points.Length; i++)
            {
                WriteVec2d(points[i]);
            }
        }

        public void WriteInt32Array(int[] values)
        {
            if (values == null)
            {
                WriteInt32(0);
                return;
            }

            WriteInt32(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                WriteInt32(values[i]);
            }
        }

        public void WriteStringArray(string[] values)
        {
            if (values == null)
            {
                WriteInt32(0);
                return;
            }

            WriteInt32(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                WriteString(values[i]);
            }
        }
    }
}
