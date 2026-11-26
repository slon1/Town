using UnityEngine;
using Zenject;
using ZombieCity.City;

namespace ZombieCity.City.Scene
{
    /// <summary>Scene bindings for the prototype (ADR-004 §6). Asset references are serialized, not loaded from Resources.</summary>
    public sealed class CityInstaller : MonoInstaller
    {
        [SerializeField] CityBakeAsset bake;
        [SerializeField] CityModelAsset model;
        [SerializeField] GroundView ground;
        [SerializeField] BuildingsView buildings;

        public override void InstallBindings()
        {
            Container.Bind<CityBakeAsset>().FromInstance(bake).AsSingle();
            Container.Bind<CityModelAsset>().FromInstance(model).AsSingle();
            Container.Bind<ICityMap>().FromInstance(bake).AsSingle();
            Container.Bind<ILayerView>().FromInstance(ground).AsSingle();
            Container.Bind<IBuildingsView>().FromInstance(buildings).AsSingle();
        }
    }
}
