// Assets/Scripts/Network/PlayerSpawner.cs
using System.Collections.Generic;
using Fusion;
using Zenject;

namespace Game.Network
{
    public class PlayerSpawner
    {
        readonly IPlayerFactory _factory;
        readonly Dictionary<PlayerRef, NetworkObject> _spawned = new();

        [Inject]
        public PlayerSpawner(IPlayerFactory factory) => _factory = factory;

        public void SpawnPlayer(PlayerRef player)
        {
            if (player == PlayerRef.None || _spawned.ContainsKey(player)) return;
            _spawned[player] = _factory.Spawn(player);
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
