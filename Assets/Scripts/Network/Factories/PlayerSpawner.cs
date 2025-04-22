using System.Collections.Generic;
using Fusion;
using Zenject;

namespace Game.Network
{
    public class PlayerSpawner
    {
        readonly IPlayerFactory _factory;
        readonly NetworkRunner _runner;
        readonly Dictionary<PlayerRef, NetworkObject> _spawned = new();

        [Inject]
        public PlayerSpawner(IPlayerFactory factory, NetworkRunner runner)
        {
            _factory = factory;
            _runner = runner;
        }

        public void SpawnPlayer(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            var netObj = _factory.Spawn(player);
            if (netObj != null) _spawned[player] = netObj;
        }

        public void RemovePlayer(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            if (_spawned.TryGetValue(player, out var obj))
            {
                runner.Despawn(obj);
                _spawned.Remove(player);
            }
        }
    }
}
