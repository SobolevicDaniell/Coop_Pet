using System.Collections.Generic;
using Fusion;
using Zenject;

namespace Game.Network
{
    public class PlayerSpawner
    {
        private readonly IPlayerFactory _factory;
        private readonly Dictionary<PlayerRef, NetworkObject> _spawned = new();

        [Inject]
        public PlayerSpawner(IPlayerFactory factory)
        {
            _factory = factory;
        }

        public void SpawnPlayer(PlayerRef player)
        {
            if (player == PlayerRef.None || _spawned.ContainsKey(player))
                return;

            var netObj = _factory.Spawn(player);
            _spawned[player] = netObj;
        }

        public void RemovePlayer(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer)
                return;

            if (_spawned.TryGetValue(player, out var obj))
            {
                runner.Despawn(obj);
                _spawned.Remove(player);
            }
        }
    }
}
