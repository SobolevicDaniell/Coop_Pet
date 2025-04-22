using Fusion;
using Game;
using Game.Network;
using UnityEngine;
using Zenject;

public class NetworkInstaller : MonoInstaller
{
    [Header("Player Prefab & Callbacks")]
    [SerializeField] private GameObject _playerPrefab;
    [SerializeField] private NetworkCallbacks _networkCallbacks;
    [SerializeField] private ItemDatabaseSO itemDatabase;

    public override void InstallBindings()
    {
        // 0) Runner из Logic
        Container
          .Bind<NetworkRunner>()
          .FromComponentInHierarchy()
          .AsSingle();

        // 1) Префаб игрока
        Container
          .Bind<GameObject>()
          .WithId("PlayerPrefab")
          .FromInstance(_playerPrefab)
          .AsSingle();

        // 2) Фабрика и спавнер
        Container.Bind<IPlayerFactory>().To<PlayerFactory>().AsSingle();
        Container.Bind<PlayerSpawner>().AsSingle();

        // 3) InputHandler из Logic
        Container.Bind<InputHandler>()
                 .FromComponentInHierarchy()
                 .AsSingle();

        // 4) Колбэки Fusion
        Container.BindInterfacesAndSelfTo<NetworkCallbacks>()
                 .FromInstance(_networkCallbacks)
                 .AsSingle();

        // 5) База предметов для PickableItem и InventoryService
        Container.Bind<ItemDatabaseSO>()
                 .FromInstance(itemDatabase)
                 .AsSingle();

        // 6) Сервис инвентаря (быстрых слотов и полного)
        Container.Bind<InventoryService>()
                 .AsSingle()
                 .NonLazy();  // создаём сразу, чтобы подписки в UI работали в Start()

        // 7) InteractionPromptView из Canvas
        Container.Bind<InteractionPromptView>()
                 .FromComponentInHierarchy()
                 .AsSingle();
    }
}
