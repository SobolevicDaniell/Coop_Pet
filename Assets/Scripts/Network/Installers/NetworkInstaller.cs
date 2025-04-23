// Assets/Scripts/Game/Installers/NetworkInstaller.cs
using Fusion;
using Game;
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

        [Header("Inventory")]
        [SerializeField] private ItemDatabaseSO _itemDatabase;

        public override void InstallBindings()
        {
            // Runner из сцены
            Container
                .Bind<NetworkRunner>()
                .FromComponentInHierarchy()
                .AsCached();

            // Префаб игрока
            Container
                .Bind<GameObject>()
                .WithId("PlayerPrefab")
                .FromInstance(_playerPrefab)
                .AsSingle();

            // Factory & Spawner
            Container
                .Bind<IPlayerFactory>()
                .To<PlayerFactory>()
                .AsSingle();
            Container
                .Bind<PlayerSpawner>()
                .AsSingle();

            // InputHandler из сцены
            Container
                .Bind<InputHandler>()
                .FromComponentInHierarchy()
                .AsCached();

            // Fusion callbacks
            Container
                .BindInterfacesAndSelfTo<NetworkCallbacks>()
                .FromInstance(_networkCallbacks)
                .AsSingle();

            // База предметов
            Container
                .Bind<ItemDatabaseSO>()
                .FromInstance(_itemDatabase)
                .AsSingle();

            // Сервис инвентаря
            Container
                .Bind<InventoryService>()
                .AsSingle()                        // ← Сначала указываем scope
                .WithArguments(_itemDatabase)      // ← Потом аргументы для конструктора
                .NonLazy();                        // ← И создаём сразу

            // UI подсказок
            Container
                .Bind<InteractionPromptView>()
                .FromComponentInHierarchy()
                .AsCached();
        }
    }
}
