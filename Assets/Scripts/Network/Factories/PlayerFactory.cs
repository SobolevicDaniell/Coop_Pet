// Assets/Scripts/Network/PlayerFactory.cs
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
            [Inject(Id = "PlayerPrefab")] GameObject playerPrefab,
            NetworkRunner runner)
        {
            _container = container;
            _playerPrefab = playerPrefab;
            _runner = runner;
        }

        public NetworkObject Spawn(PlayerRef playerRef)
        {
            var prefabNo = _playerPrefab.GetComponent<NetworkObject>();
            var netObj = _runner.Spawn(prefabNo, Vector3.zero, Quaternion.identity, playerRef);
            _container.InjectGameObject(netObj.gameObject);
            return netObj;
        }
    }
}
