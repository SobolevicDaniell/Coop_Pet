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

            // ������ ������
            Container.Bind<GameObject>()
                     .WithId("PlayerPrefab")
                     .FromInstance(_playerPrefab)
                     .AsSingle();

            // ������� ������ ������
            Container.Bind<IPlayerFactory>()
                     .To<PlayerFactory>()
                     .AsSingle();
            Container.Bind<PlayerSpawner>()
                     .AsSingle();

            // InputHandler
            Container.Bind<InputHandler>()
                     .FromComponentInHierarchy()
                     .AsSingle();

            // ������� Fusion
            Container.BindInterfacesAndSelfTo<NetworkCallbacks>()
                     .FromInstance(_networkCallbacks)
                     .AsSingle();

            // ���� ���������
            Container.Bind<ItemDatabaseSO>()
                     .FromInstance(itemDatabase)
                     .AsSingle();

            // ������ ���������: ������� ���� ��� lookup �� itemId
            Container.Bind<InventoryService>()
                     .AsSingle()
                     .WithArguments(itemDatabase)
                     .NonLazy();

            // UI ��� ���������
            Container.Bind<InteractionPromptView>()
                     .FromComponentInHierarchy()
                     .AsSingle();
        }
    }
}
