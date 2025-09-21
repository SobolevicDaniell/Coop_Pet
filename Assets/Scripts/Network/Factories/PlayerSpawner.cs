using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Zenject;

namespace Game.Network
{
    public class PlayerSpawner
    {
        private readonly IPlayerFactory _factory;
        private readonly GameObject _deathBoxPrefab;
        private readonly GameObject _playerObjectPrefab;
        private readonly DiContainer _container;

        private readonly Dictionary<PlayerRef, NetworkObject> _avatars = new();
        private readonly Dictionary<PlayerRef, NetworkObject> _playerObjects = new();

        public PlayerSpawner(
            IPlayerFactory factory,
            [Inject(Id = "DeathBoxPrefab")] GameObject deathBoxPrefab,
            [Inject(Id = "PlayerObjectPrefab")] GameObject playerObjectPrefab,
            DiContainer container)
        {
            _factory = factory;
            _deathBoxPrefab = deathBoxPrefab;
            _playerObjectPrefab = playerObjectPrefab;
            _container = container;
        }

        public void SpawnAvatar(NetworkRunner runner, PlayerRef player)
        {
            if (_avatars.TryGetValue(player, out var existing) && existing != null) return;
            var no = _factory.Spawn(player);
            _avatars[player] = no;
            var po = runner.GetPlayerObject(player);
            if (po != null)
            {
                var proxy = po.GetComponent<PlayerObject>();
                proxy.RPC_ShowGameplay();
            }
        }

        public void DespawnAvatar(NetworkRunner runner, PlayerRef player, NetworkObject fallbackAvatar)
        {
            if (!runner.IsServer) return;
            if (_avatars.TryGetValue(player, out var no) && no != null && no.Runner == runner)
            {
                runner.Despawn(no);
                _avatars[player] = null;
                return;
            }
            if (fallbackAvatar != null && fallbackAvatar.Runner == runner)
            {
                runner.Despawn(fallbackAvatar);
            }
            _avatars[player] = null;
        }

        public void EnsurePlayerObject(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            if (_playerObjects.TryGetValue(player, out var existing) && existing != null) return;

            var poExisting = runner.GetPlayerObject(player);
            if (poExisting != null)
            {
                _playerObjects[player] = poExisting;
                return;
            }

            NetworkObject po = null;
            runner.Spawn(
                _playerObjectPrefab.GetComponent<NetworkObject>(),
                Vector3.zero,
                Quaternion.identity,
                player,
                (r, obj) =>
                {
                    _container.InjectGameObject(obj.gameObject);
                    po = obj;
                }
            );
            if (po != null)
            {
                runner.SetPlayerObject(player, po);
                _playerObjects[player] = po;
            }
        }

        public void RemovePlayerObject(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            var po = runner.GetPlayerObject(player);
            if (po != null) runner.Despawn(po);
            _playerObjects[player] = null;
        }

        public NetworkObject SpawnDeathBox(NetworkRunner runner, PlayerRef owner, Vector3 position, Quaternion rotation)
        {
            NetworkObject spawned = null;
            runner.Spawn(
                _deathBoxPrefab.GetComponent<NetworkObject>(),
                position,
                rotation,
                PlayerRef.None,
                (r, obj) =>
                {
                    _container.InjectGameObject(obj.gameObject);
                    spawned = obj;
                });
            return spawned;
        }

        public void RegisterAvatar(PlayerRef player, GameObject avatarGo)
        {
            var no = avatarGo != null ? avatarGo.GetComponent<NetworkObject>() : null;
            if (no == null) return;
            _avatars[player] = no;
        }

        public bool IsAvatarSpawned(PlayerRef player)
        {
            return _avatars.TryGetValue(player, out var no) && no != null;
        }

        public void RespawnPlayer(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            if (IsAvatarSpawned(player)) return;

            SpawnAvatar(runner, player);

            
        }
    }
}
