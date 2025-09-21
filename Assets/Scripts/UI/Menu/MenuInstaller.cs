using UnityEngine;
using Zenject;

namespace Game.Network
{
    public sealed class MenuInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<MenuSessionService>().AsSingle();
            var storeGo = new GameObject("LaunchRequestStore");
            var store = storeGo.AddComponent<LaunchRequestStore>();
            Container.Bind<LaunchRequestStore>().FromInstance(store).AsSingle();
            Container.Bind<MenuController>().FromComponentInHierarchy().AsSingle();
        }
    }
}
