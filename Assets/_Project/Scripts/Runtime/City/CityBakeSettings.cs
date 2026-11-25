using UnityEngine;
using ZombieCity.City.Core;

namespace ZombieCity.City
{
    /// <summary>
    /// ScriptableObject wrapper around <see cref="CityBakeConfig"/> for Inspector editing.
    /// </summary>
    [CreateAssetMenu(fileName = "CityBakeSettings", menuName = "ZombieCity/City/Bake Settings")]
    public class CityBakeSettings : ScriptableObject
    {
        public CityBakeConfig config = new CityBakeConfig();
    }
}
