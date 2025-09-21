using Fusion;
using Game.UI;
using UnityEngine;
using Zenject;

namespace Game.Network
{
    public sealed class PlayerFactory : IPlayerFactory
    {
        private readonly NetworkRunner _runner;
        private readonly GameObject _avatarPrefab;
        private readonly DiContainer _container;

        public PlayerFactory(NetworkRunner runner, [Inject(Id = "AvatarPrefab")] GameObject avatarPrefab, DiContainer container, [Inject(Optional = true)] UIController uiController)
        {
            _runner = runner;
            _avatarPrefab = avatarPrefab;
            _container = container;
        }

        public NetworkObject Spawn(PlayerRef player)
        {
            NetworkObject spawned = null;
            _runner.Spawn(
                _avatarPrefab.GetComponent<NetworkObject>(),
                Vector3.zero,
                Quaternion.identity,
                inputAuthority: player,
                onBeforeSpawned: (r, obj) =>
                {
                    _container.InjectGameObject(obj.gameObject);
                    spawned = obj;
                }
            );
            return spawned;
        }
    }
}
