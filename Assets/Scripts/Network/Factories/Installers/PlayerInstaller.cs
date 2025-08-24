using Game.Network;
using Game.UI;
using Zenject;

namespace Game
{
    public class PlayerInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<InteractionController>().FromComponentOnRoot();
            Container.Bind<PlayerRpcHandler>().FromComponentOnRoot();
            Container.Bind<HandItemController>().FromComponentOnRoot();
            Container.Bind<QuickSlotController>().FromComponentOnRoot();
            Container.Bind<ItemEquipController>().FromComponentOnRoot();
            Container.Bind<PickDropController>().FromComponentOnRoot();
            Container.Bind<PlaceItemController>().FromComponentOnRoot();
            Container.Bind<HealthComponent>().FromComponentOnRoot();
            Container.Bind<PlayerInputRouter>().FromComponentOnRoot();
            Container.BindInterfacesAndSelfTo<InventoryService>().AsSingle();
        }
    }
}