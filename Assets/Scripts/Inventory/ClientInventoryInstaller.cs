using Game.UI;
using Zenject;

namespace Game
{
    public sealed class ClientInventoryInstaller : Installer<ClientInventoryInstaller>
    {
        public override void InstallBindings()
        {
            Container.Bind<InventoryClientModel>()
                .AsSingle()
                .NonLazy(); // чтобы модель создалась до подписки фасада

            Container.Bind<InventoryClientFacade>()
                .FromComponentInHierarchy()
                .AsSingle()
                .NonLazy(); // чтобы сразу пройти инжект и подписки

            Container.Bind<ContainerViewSessionClient>()
                .AsSingle();

            Container.BindInterfacesAndSelfTo<InventoryService>()
                .AsSingle()
                .NonLazy(); // инвентарь нужен сразу UI/геймплею

            // Container.Bind<InventoryTransferController>()
            //     .FromComponentInHierarchy()
            //     .AsSingle();
        }
    }
}
