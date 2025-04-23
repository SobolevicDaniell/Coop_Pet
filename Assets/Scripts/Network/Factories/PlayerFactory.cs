using Fusion;
using UnityEngine;
using Zenject;

namespace Game.Network
{
    /// <summary>
    /// ���������� IPlayerFactory: ������� ������ ����� NetworkRunner
    /// � ��������� GO ����� Zenject ��� �������� � ��� ����������.
    /// </summary>
    public class PlayerFactory : IPlayerFactory
    {
        readonly DiContainer _container;
        readonly NetworkRunner _runner;
        readonly GameObject _playerPrefab;

        [Inject]
        public PlayerFactory(
            DiContainer container,
            NetworkRunner runner,
            [Inject(Id = "PlayerPrefab")] GameObject playerPrefab
        )
        {
            _container = container;
            _runner = runner;
            _playerPrefab = playerPrefab;
        }

        public NetworkObject Spawn(PlayerRef playerRef)
        {
            // ������� ������, ������� ��� ����� InputAuthority
            var netObj = _runner.Spawn(
                _playerPrefab,
                Vector3.zero,        // ����� ����� ���������� ������ �������/�������
                Quaternion.identity,
                playerRef
            );

            if (netObj == null)
                return null;

            // �������� ����� ��������� ������ ����� ���������,
            // ����� ��� [Inject] ���� � �������� �����������
            _container.InjectGameObject(netObj.gameObject);

            return netObj;
        }
    }
}
