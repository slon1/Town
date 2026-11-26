using UnityEngine;
using Zenject;

namespace ZombieCity.City.Scene
{
    /// <summary>Пишет <c>[City] DI ok</c>, если Extenject внедрил маркер из <see cref="CitySmokeInstaller"/>.</summary>
    public sealed class CitySmokeProbe : MonoBehaviour
    {
        [Inject]
        public void Construct(CityDiMarker marker)
        {
            Debug.Log("[City] DI ok");
        }
    }
}
