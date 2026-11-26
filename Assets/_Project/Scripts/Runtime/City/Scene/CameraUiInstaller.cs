using UnityEngine;
using Zenject;

namespace ZombieCity.City.Scene
{
    public sealed class CameraUiInstaller : MonoInstaller
    {
        [SerializeField] CameraRig cameraRig;
        [SerializeField] CameraRigSettings settings;

        public override void InstallBindings()
        {
            Container.Bind<CameraRigSettings>().FromInstance(settings).AsSingle();
            Container.Bind<ICameraRig>().FromInstance(cameraRig).AsSingle();
        }
    }
}
