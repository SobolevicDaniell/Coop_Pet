// Assets/Scripts/Game/Installers/NetworkInstaller.cs
using Fusion;
using Game.Network;
using UnityEngine;
using Zenject;

namespace Game
{
    public class NetworkInstaller : MonoInstaller
    {
        [Header("Player Prefab & Callbacks")]
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private NetworkCallbacks _networkCallbacks;
        [SerializeField] private ItemDatabaseSO itemDatabase;

        public override void InstallBindings()
        {
            // Fusion Runner
            Container.Bind<NetworkRunner>()
                     .FromComponentInHierarchy()
                     .AsSingle();

            // Префаб игрока
            Container.Bind<GameObject>()
                     .WithId("PlayerPrefab")
                     .FromInstance(_playerPrefab)
                     .AsSingle();

            // Фабрика спавна игрока
            Container.Bind<IPlayerFactory>()
                     .To<PlayerFactory>()
                     .AsSingle();
            Container.Bind<PlayerSpawner>()
                     .AsSingle();

            // InputHandler
            Container.Bind<InputHandler>()
                     .FromComponentInHierarchy()
                     .AsSingle();

            // Колбэки Fusion
            Container.BindInterfacesAndSelfTo<NetworkCallbacks>()
                     .FromInstance(_networkCallbacks)
                     .AsSingle();

            // База предметов
            Container.Bind<ItemDatabaseSO>()
                     .FromInstance(itemDatabase)
                     .AsSingle();

            // Сервис инвентаря: передаём базу для lookup по itemId
            Container.Bind<InventoryService>()
                     .AsSingle()
                     .WithArguments(itemDatabase)
                     .NonLazy();

            // UI для подсказок
            Container.Bind<InteractionPromptView>()
                     .FromComponentInHierarchy()
                     .AsSingle();
        }
    }
}
