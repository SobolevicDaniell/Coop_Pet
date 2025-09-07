using Fusion;
using UnityEngine;
using Zenject;

namespace Game.Network
{
    public sealed class PlayerFactory : IPlayerFactory
    {
        private readonly NetworkRunner _runner;
        private readonly GameObject _playerPrefab;

        public PlayerFactory(NetworkRunner runner, [Inject(Id = "PlayerPrefab")] GameObject playerPrefab)
        {
            _runner = runner;
            _playerPrefab = playerPrefab;
        }

        public NetworkObject Spawn(PlayerRef player)
        {
            var no = _runner.Spawn(
                _playerPrefab.GetComponent<NetworkObject>(),
                Vector3.zero, Quaternion.identity,
                inputAuthority: player
            );
            

            // КРИТИЧЕСКО: привязываем PlayerObject → тогда TryGetPlayerObject работает
            _runner.SetPlayerObject(player, no);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Server] Spawned Player: {player}");
#endif
            return no;
        }
    }
}
