using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(PlayerMovement))]
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private Camera _playerCamera;

        public override void Spawned()
        {
            bool isLocal = HasInputAuthority;
            _movement.enabled = isLocal;
            if (_playerCamera != null)
                _playerCamera.gameObject.SetActive(isLocal);
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority && GetInput(out InputData d))
                _movement.HandleInput(d, Runner.DeltaTime);
        }
    }
}
