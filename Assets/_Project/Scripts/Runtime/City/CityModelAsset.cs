using UnityEngine;
using ZombieCity.City.Core;

namespace ZombieCity.City
{
    /// <summary>
    /// ScriptableObject container for the vector city model (outside Core asmdef).
    /// </summary>
    [CreateAssetMenu(fileName = "CityModel", menuName = "ZombieCity/City/City Model")]
    public class CityModelAsset : ScriptableObject
    {
        public CityModel model = new CityModel();

        public ulong DataHash => model?.meta != null ? model.meta.dataHash : 0UL;
        public ulong SettingsHash => model?.meta != null ? model.meta.settingsHash : 0UL;

        /// <summary>
        /// Sorts entities, refreshes data hash from canonical bytes, keeps settingsHash as set by caller.
        /// </summary>
        public void RecomputeDataHash()
        {
            if (model == null)
            {
                model = new CityModel();
            }

            if (model.meta == null)
            {
                model.meta = new CityModelMeta();
            }

            CityModelHasher.SortEntities(model);
            model.meta.dataHash = CityModelHasher.ComputeDataHash(model);
        }
    }
}
