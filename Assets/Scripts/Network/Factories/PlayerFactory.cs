using UnityEngine;
using Fusion;
using Zenject;

namespace Game.Network
{
    public class PlayerFactory : IPlayerFactory
    {
        readonly DiContainer _container;
        readonly GameObject _playerPrefab;
        readonly InventoryService _inventory;
        readonly NetworkRunner _runner;
        readonly InteractionPromptView _promptView;

        [Inject]
        public PlayerFactory(
            DiContainer container,
            [Inject(Id = "PlayerPrefab")] GameObject playerPrefab,
            InventoryService inventory,
            NetworkRunner runner,
            InteractionPromptView promptView)
        {
            _container = container;
            _playerPrefab = playerPrefab;
            _inventory = inventory;
            _runner = runner;
            _promptView = promptView;
        }

        public NetworkObject Spawn(PlayerRef playerRef)
        {
            // Fusion ��� ������� ���� ������������ ���������:
            var netObj = _runner.Spawn(
                _playerPrefab.GetComponent<NetworkObject>(),       // <- ����� ������� asset-������
                position: Vector3.zero,
                rotation: Quaternion.identity,
                inputAuthority: playerRef
            );

            // � Zenject �������� ����������� �� ����� ��������� GO:
            _container.InjectGameObject(netObj.gameObject);

            return netObj;
        }

    }
}
