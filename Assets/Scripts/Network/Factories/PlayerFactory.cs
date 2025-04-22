using Fusion;
using UnityEngine;
using Zenject;

namespace Game.Network
{
    public class PlayerFactory : IPlayerFactory
    {
        readonly DiContainer _container;
        readonly NetworkRunner _runner;
        readonly GameObject _playerPrefab;

        [Inject]
        public PlayerFactory(
            DiContainer container,
            NetworkRunner runner,
            [Inject(Id = "PlayerPrefab")] GameObject playerPrefab)
        {
            _container = container;
            _runner = runner;
            _playerPrefab = playerPrefab;
        }

        public NetworkObject Spawn(PlayerRef playerRef)
        {
            return _runner.Spawn(
                _playerPrefab,
                Vector3.zero, Quaternion.identity,
                playerRef,
                onBeforeSpawned: (runner, netObj) => {
                    _container.InjectGameObject(netObj.gameObject);
                });
        }

    }
}
