using Fusion;
using Game.Gameplay;
using UnityEngine;
using Zenject;

namespace Game.Network
{
    public class NetworkInstaller : MonoInstaller
    {
        [Header("Player Prefab")][SerializeField] private GameObject _playerPrefab;
        [Header("Inventory")][SerializeField] private ItemDatabaseSO _itemDatabase;

        public override void InstallBindings()
        {
            Debug.Log("NetworkInstaller: InstallBindings");

            Container
                .Bind<Startup>()
                .FromComponentInHierarchy()
                .AsSingle()
                .NonLazy();

            Container
                .Bind<NetworkRunner>()
                .FromComponentInHierarchy()
                .AsSingle()
                .NonLazy();

            Container
                .Bind<GameObject>()
                .WithId("PlayerPrefab")
                .FromInstance(_playerPrefab)
                .AsSingle();

            Container
                .Bind<IPlayerFactory>()
                .To<PlayerFactory>()
                .AsSingle()
                .NonLazy();

            Container
                .Bind<PlayerSpawner>()
                .AsSingle()
                .NonLazy();

            Container
                .Bind<PickableSpawner>()
                .FromComponentInHierarchy()
                .AsSingle()
                .NonLazy();

            Container
                .BindInterfacesAndSelfTo<NetworkCallbacks>()
                .FromComponentInHierarchy()
                .AsSingle()
                .NonLazy();

            Container.Bind<InputHandler>()
                     .FromComponentInHierarchy()
                     .AsSingle();

            Container.Bind<ItemDatabaseSO>()
                     .FromInstance(_itemDatabase)
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

        }
    }
}
