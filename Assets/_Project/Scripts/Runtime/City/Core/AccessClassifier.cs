using System;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// Pure Access classifier (ADR-003 §2). Stub until 3B implementation commit.
    /// </summary>
    public static class AccessClassifier
    {
        public static CityAccess Classify(CellFlags flags)
        {
            // Intentional stub — replaced in [TASK-003/3B].
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
