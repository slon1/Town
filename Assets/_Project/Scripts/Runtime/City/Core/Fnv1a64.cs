namespace ZombieCity.City.Core
{
    /// <summary>
    /// FNV-1a 64-bit hash (ADR-001 §4).
    /// </summary>
    public static class Fnv1a64
    {
        public const ulong OffsetBasis = 14695981039346656037UL;
        public const ulong Prime = 1099511628211UL;

        public static ulong Compute(byte[] data)
        {
            ulong hash = OffsetBasis;
            if (data == null)
            {
                return hash;
            }

            for (int i = 0; i < data.Length; i++)
            {
                hash ^= data[i];
                hash *= Prime;
            }

            return hash;
        }
    }
}
