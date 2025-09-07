using Fusion;
using Game.Gameplay;
using UnityEngine;
using Zenject;
using Game.UI;

namespace Game.Network
{
    public class NetworkInstaller : MonoInstaller
    {
        [Header("Player Prefab")]
        [SerializeField] private GameObject _playerPrefab;

        [Header("Inventory")]
        [SerializeField] private ItemDatabaseSO _itemDatabase;

        [Header("Prefabs")]
        [SerializeField] private InventorySlotUI _slotPrefab;

        [Header("UI Panels")]
        [SerializeField] private InventoryPanel _playerInventoryPanel;
        [SerializeField] private OtherInventoryPanel _otherInventoryPanel;

        [Header("Config")]
        [SerializeField] private PlayerStatsSO _playerStats;
        

        public override void InstallBindings()
        {
            Container.BindInstance(_playerPrefab).WithId("PlayerPrefab");

            Container.Bind<ItemDatabaseSO>()
                .FromInstance(_itemDatabase)
                .AsSingle();

            // Container.BindInterfacesAndSelfTo<InventoryService>()
            //     .AsSingle();

            Container.Bind<UIController>()
                .FromComponentInHierarchy()
                .AsSingle();

            Container.Bind<InteractionPromptView>()
                .FromComponentInHierarchy()
                .AsSingle();


            Container.Bind<InventoryPanel>()
                .WithId("PlayerInventoryPanel")
                .FromComponentInHierarchy()
                .AsSingle();

            Container.Bind<OtherInventoryPanel>()
                .WithId("OtherInventoryPanel")
                .FromComponentInHierarchy()
                .AsSingle();

            Container.Bind<QuickSlotPanel>()
                .FromComponentInHierarchy()
                .AsSingle();

            Container.Bind<InputHandler>()
                .FromComponentInHierarchy()
                .AsSingle();

            Container.Bind<HandItemBehaviorFactory>()
                .AsSingle();

            Container.Bind<NetworkRunner>()
                .FromComponentInHierarchy()
                .AsSingle();

            Container.Bind<IPlayerFactory>()
                .To<PlayerFactory>()
                .AsSingle();

            Container.Bind<PlayerSpawner>()
                .AsSingle();

            Container.Bind<INetworkObjectProvider>()
                .To<ZenjectObjectProvider>()
                .AsSingle();

            Container.Bind<NetworkCallbacks>()
                .FromComponentInHierarchy()
                .AsSingle();

            Container.Bind<InventorySlotUI>()
                .WithId("InventorySlotPrefab")
                .FromInstance(_slotPrefab)
                .AsSingle();

            Container.Bind<Startup>()
                .FromComponentInHierarchy()
                .AsSingle();

            Container.Bind<MenuController>()
                .FromComponentInHierarchy()
                .AsSingle();

            Container.Bind<PlayerStatsSO>()
                .FromInstance(_playerStats)
                .AsSingle();

            Container.BindInterfacesAndSelfTo<Game.Network.FusionZenjectInjector>()
                .AsSingle();

            // Container.Bind<InventoryContainerRegistry>()
            //     .AsSingle()
            //     .NonLazy(); 


            ClientInventoryInstaller.Install(Container);
            ServerInventoryInstaller.Install(Container);

            if (!Application.isBatchMode)
            {
                Container.Bind<InventoryTransferController>()
                         .FromComponentInHierarchy()
                         .AsSingle();

                Container.Bind<UIHealthView>()
                    .FromComponentInHierarchy()
                    .AsSingle();

                Container.Bind<HealthClientModel>()
                    .AsSingle();
            }

            // Container.Bind<InventoryClientModel>().AsSingle();
            // Container.Bind<InventoryClientFacade>().AsSingle();



        }

    }
}