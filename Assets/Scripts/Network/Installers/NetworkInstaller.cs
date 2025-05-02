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
        [Header("Interaction Signals")][SerializeField] private InteractionSignalsSO _interactionSignals;

        public override void InstallBindings()
        {
            Debug.Log("NetworkInstaller: InstallBindings");

            // 1) Биндим Startup
            Container
                .Bind<Startup>()
                .FromComponentInHierarchy()
                .AsSingle()
                .NonLazy();

            // 2) NetworkRunner
            Container
                .Bind<NetworkRunner>()
                .FromComponentInHierarchy()
                .AsSingle()
                .NonLazy();

            // 3) Префаб игрока — ВАЖНО до фабрики
            Container
                .Bind<GameObject>()
                .WithId("PlayerPrefab")
                .FromInstance(_playerPrefab)
                .AsSingle();

            // 4) Фабрика игроков (зависит от PlayerPrefab)
            Container
                .Bind<IPlayerFactory>()
                .To<PlayerFactory>()
                .AsSingle()
                .NonLazy();

            // 5) Спавнер игроков
            Container
                .Bind<PlayerSpawner>()
                .AsSingle()
                .NonLazy();

            // 6) PickableSpawner
            Container
                .Bind<PickableSpawner>()
                .FromComponentInHierarchy()
                .AsSingle()
                .NonLazy();

            // 7) NetworkCallbacks
            Container
                .BindInterfacesAndSelfTo<NetworkCallbacks>()
                .FromComponentInHierarchy()
                .AsSingle()
                .NonLazy();

            // 8) Всё остальное...
            Container.Bind<InputHandler>()
                     .FromComponentInHierarchy()
                     .AsSingle();

            Container.Bind<ItemDatabaseSO>()
                     .FromInstance(_itemDatabase)
                     .AsSingle();

            Container.Bind<InteractionSignalsSO>()
                     .FromInstance(_interactionSignals)
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
