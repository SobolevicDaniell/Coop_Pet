using Zenject;

namespace Game
{
    public sealed class ClientInventoryInstaller : Installer<ClientInventoryInstaller>
    {
        public override void InstallBindings()
        {
            Container.Bind<InventoryClientModel>().AsSingle();
            Container.Bind<InventoryClientFacade>().AsSingle();
            Container.Bind<ContainerViewSessionClient>().AsSingle();
            Container.BindInterfacesAndSelfTo<InventoryService>().AsSingle();



        }
    }
}