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
        // [SerializeField] private InventoryDropZone _inventoryDropZone;
        // [SerializeField] private InventoryTransferController  _inventoryTransferController;

        [Header("Config")]
        [SerializeField] private PlayerStatsSO _playerStats;



        public override void InstallBindings()
        {
            Debug.Log("NetworkInstaller: InstallBindings");

            Container.Bind<Startup>()
                .FromComponentInHierarchy()
                .AsSingle().NonLazy();

            Container.Bind<NetworkRunner>()
                .FromComponentInHierarchy()
                .AsSingle().NonLazy();

            Container.BindInterfacesAndSelfTo<NetworkCallbacks>()
                .FromComponentInHierarchy()
                .AsSingle().NonLazy();

            Container.Bind<IPlayerFactory>()
                .To<PlayerFactory>()
                .AsSingle().NonLazy();

            Container.Bind<PlayerSpawner>()
                .AsSingle().NonLazy();

            Container.Bind<PickableSpawner>()
                .FromComponentInHierarchy()
                .AsSingle().NonLazy();

            Container.Bind<GameObject>()
                .WithId("PlayerPrefab")
                .FromInstance(_playerPrefab)
                .AsSingle();

            Container.Bind<InputHandler>()
                .FromComponentInHierarchy()
                .AsSingle();

            Container.Bind<InteractionPromptView>()
                .FromComponentInHierarchy()
                .AsSingle();

            Container.Bind<InventoryService>()
                .AsSingle()
                .WithArguments(_itemDatabase)
                .NonLazy();

            Container.Bind<PlayerCameraController>()
                .FromComponentInHierarchy()
                .AsSingle();

            Container.Bind<HandItemBehaviorFactory>()
                .AsSingle()
                .NonLazy();

            Container.Bind<ItemDatabaseSO>()
                .FromInstance(_itemDatabase)
                .AsSingle();

            Container.Bind<UIHealthView>()
                .FromComponentInHierarchy()
                .AsSingle();

            Container.Bind<InventoryPanel>()
                .WithId("PlayerInventoryPanel")
                .FromInstance(_playerInventoryPanel)
                .AsCached();

            Container.Bind<OtherInventoryPanel>()
                .WithId("OtherInventoryPanel")
                .FromInstance(_otherInventoryPanel)
                .AsCached();

            Container.Bind<UIController>()
                 .FromComponentInHierarchy()
                 .AsSingle();

            Container.Bind<InventorySlotUI>()
                    .WithId("InventorySlotPrefab")
                    .FromInstance(_slotPrefab)
                    .AsSingle();

            Container.Bind<PlayerStatsSO>()
                    .FromInstance(_playerStats)
                    .AsSingle();

            // Container.Bind<InventoryDropZone>()
            //         .FromInstance(_inventoryDropZone)
            //         .AsSingle();
                    
            // Container.Bind<InventoryTransferController>()
            //         .FromInstance(_inventoryTransferController)
            //         .AsSingle();
        }
    }
}
