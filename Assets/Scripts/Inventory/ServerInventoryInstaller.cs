// Assets/Scripts/Inventory/ServerInventoryInstaller.cs
using Zenject;

namespace Game
{
    public sealed class ServerInventoryInstaller : Installer<ServerInventoryInstaller>
    {
        public override void InstallBindings()
        {
            // Реестр контейнеров — нужен один раз и заранее
            Container.Bind<InventoryContainerRegistry>()
                     .AsSingle()
                     .NonLazy();

            // Серверные сервисы инвентаря
            Container.Bind<InventoryServerService>().AsSingle();
            Container.Bind<InventorySnapshotBuilder>().AsSingle();
            Container.Bind<InventorySessionServer>().AsSingle();
        }
    }
}
