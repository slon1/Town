using Zenject;

namespace ZombieCity.City.Scene
{
    /// <summary>
    /// Временная привязка для smoke-check Extenject (TASK-006/6A).
    /// В 6F заменяется на <c>CityInstaller</c>.
    /// </summary>
    public sealed class CitySmokeInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<CityDiMarker>().AsSingle();
        }
    }

    /// <summary>Маркер, что контейнер сцены установил тестовую привязку.</summary>
    public sealed class CityDiMarker
    {
    }
}
