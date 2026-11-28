using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZombieCity.City.Core.Tests
{
    /// <summary>
    /// District bake after TASK-014: tram cells exist and accessHash is unchanged.
    /// </summary>
    public class DistrictTramBakeTests
    {
        const string BakePath = "Assets/_Project/City/Baked/CityBake_map.asset";
        const ulong ExpectedAccessHash = 0x7EB6425EF2B7EC93UL;

        [Test]
        public void AfterRebake_TramCellsPositive_AccessHashUnchanged()
        {
            Object bake = AssetDatabase.LoadMainAssetAtPath(BakePath);
            Assert.IsNotNull(bake, BakePath);
            var type = bake.GetType();
            ulong accessHash = (ulong)type.GetField("accessHash").GetValue(bake);
            var flags = (byte[])type.GetField("flagsBytes").GetValue(bake);
            Assert.IsNotNull(flags);

            int tramCells = 0;
            for (int i = 0; i + 1 < flags.Length; i += 2)
            {
                if ((flags[i + 1] & 0x80) != 0)
                {
                    tramCells++;
                }
            }

            Assert.AreEqual(ExpectedAccessHash, accessHash, "accessHash must stay 0x7EB6425EF2B7EC93");
            Assert.Greater(tramCells, 0, "tram cells");
        }
    }
}
