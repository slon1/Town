using System;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// Pure Access classifier (ADR-003 §2).
    /// </summary>
    public static class AccessClassifier
    {
        const CellFlags ObstacleBits =
            CellFlags.Building | CellFlags.Water | CellFlags.Barrier | CellFlags.Rail;

        const CellFlags PedestrianOnRoadBits =
            CellFlags.Sidewalk | CellFlags.SidewalkGenerated | CellFlags.Crosswalk |
            CellFlags.Steps | CellFlags.Cycleway | CellFlags.Shared;

        public static CityAccess Classify(CellFlags flags)
        {
            if ((flags & ObstacleBits) != 0)
            {
                return CityAccess.Obstacle;
            }

            if ((flags & CellFlags.Road) != 0 && (flags & PedestrianOnRoadBits) == 0)
            {
                return CityAccess.Roadway;
            }

            return CityAccess.Pedestrian;
        }

        public static void ClassifyAll(ushort[] flags, byte[] access)
        {
            if (flags == null)
            {
                throw new ArgumentNullException(nameof(flags));
            }

            if (access == null)
            {
                throw new ArgumentNullException(nameof(access));
            }

            if (access.Length != flags.Length)
            {
                throw new ArgumentException("access length must equal flags length");
            }

            for (int i = 0; i < flags.Length; i++)
            {
                access[i] = (byte)Classify((CellFlags)flags[i]);
            }
        }
    }
}
