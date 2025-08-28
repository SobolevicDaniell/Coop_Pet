using UnityEngine;
using Zenject;

namespace Game
{
    public sealed class ServerInventoryInstaller : Installer<ServerInventoryInstaller>
    {
        public override void InstallBindings()
        {
            Container.Bind<InventoryContainerRegistry>().AsSingle();
            Container.Bind<InventoryValidationService>().AsSingle();
            Container.Bind<InventoryServerService>().AsSingle();
            Container.Bind<InventorySessionServer>().AsSingle();
        }
    }
}
