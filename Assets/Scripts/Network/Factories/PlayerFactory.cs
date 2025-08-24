using Fusion;
using UnityEngine;
using Zenject;

namespace Game.Network
{
    public class PlayerFactory : IPlayerFactory
    {
        private readonly DiContainer _container;
        private readonly NetworkRunner _runner;
        private readonly GameObject _playerPrefab;

        [Inject]
        public PlayerFactory(
            DiContainer container,
            [Inject(Id = "PlayerPrefab")] GameObject playerPrefab,
            NetworkRunner runner
        )
        {
            _container = container;
            _playerPrefab = playerPrefab;
            _runner = runner;
        }

        public NetworkObject Spawn(PlayerRef playerRef)
        {
            var prefabNetObj = _playerPrefab.GetComponent<NetworkObject>();
            var netObj = _runner.Spawn(
                prefabNetObj,
                Vector3.zero,
                Quaternion.identity,
                playerRef
            );

            _runner.SetPlayerObject(playerRef, netObj);
            return netObj;
        }
    }
}
