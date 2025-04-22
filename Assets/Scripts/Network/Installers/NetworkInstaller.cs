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
        // 0) Runner �� Logic
        Container
          .Bind<NetworkRunner>()
          .FromComponentInHierarchy()
          .AsSingle();

        // 1) ������ ������
        Container
          .Bind<GameObject>()
          .WithId("PlayerPrefab")
          .FromInstance(_playerPrefab)
          .AsSingle();

        // 2) ������� � �������
        Container.Bind<IPlayerFactory>().To<PlayerFactory>().AsSingle();
        Container.Bind<PlayerSpawner>().AsSingle();

        // 3) InputHandler �� Logic
        Container.Bind<InputHandler>()
                 .FromComponentInHierarchy()
                 .AsSingle();

        // 4) ������� Fusion
        Container.BindInterfacesAndSelfTo<NetworkCallbacks>()
                 .FromInstance(_networkCallbacks)
                 .AsSingle();

        // 5) ���� ��������� ��� PickableItem � InventoryService
        Container.Bind<ItemDatabaseSO>()
                 .FromInstance(itemDatabase)
                 .AsSingle();

        // 6) ������ ��������� (������� ������ � �������)
        Container.Bind<InventoryService>()
                 .AsSingle()
                 .NonLazy();  // ������ �����, ����� �������� � UI �������� � Start()

        // 7) InteractionPromptView �� Canvas
        Container.Bind<InteractionPromptView>()
                 .FromComponentInHierarchy()
                 .AsSingle();
    }
}
