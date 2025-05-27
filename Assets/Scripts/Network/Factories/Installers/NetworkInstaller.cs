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
        
        [Header("UI Panels")]
        [SerializeField] private InventoryPanel _playerInventoryPanel;
        [SerializeField] private InventoryPanel _otherInventoryPanel;



        public override void InstallBindings()
        {
            Debug.Log("NetworkInstaller: InstallBindings");

            // --- СЕТЬ, CORE ---
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

            // --- GAMEPLAY / CONTROLLERS (Singleton, глобальные) ---
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

            Container.Bind<InventoryPanel>()
                .WithId("OtherInventoryPanel")
                .FromInstance(_otherInventoryPanel)
                .AsCached();


        }
    }
}
