using System.Collections.Generic;
using System.Linq;
using Fusion;
using Unity.VisualScripting;
using UnityEngine;
using Zenject;
using Game;
using ExitGames.Client.Photon.StructWrapping;

namespace Game.Network
{
    public class PlayerSpawner
    {
        private readonly IPlayerFactory _factory;
        private readonly Dictionary<PlayerRef, GameObject> _spawned = new();

        [Inject]
        public PlayerSpawner(IPlayerFactory factory)
        {
            _factory = factory;
        }

        public void SpawnPlayer(PlayerRef player)
        {
            if (player == PlayerRef.None || _spawned.ContainsKey(player))
            {
                // Debug.LogError($"[PlayerSpawner] Игрок с PlayerRef {player} уже существует или некорректен!");
                return;
            }

            var netObj = _factory.Spawn(player);

            if (netObj == null)
            {
                // Debug.LogError($"[PlayerSpawner] NetworkObject не создан для PlayerRef={player}");
                return;
            }

            _spawned[player] = netObj.gameObject;
            // Debug.Log($"[PlayerSpawner] Игрок {player} успешно добавлен в словарь.");

            GameObject _player = GetPlayerGameObject(player);
            InteractionController interactionController = _player.GetComponent<InteractionController>();
            // Debug.Log($"[PlayerSpawner] {interactionController} для игрока {player} успешно получен.");
        }


        public void RemovePlayer(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer)
                return;

            if (_spawned.TryGetValue(player, out var obj))
            {
                runner.Despawn(obj.GetComponent<NetworkObject>());
                _spawned.Remove(player);
            }
        }
        private GameObject GetPlayerGameObject(PlayerRef player)
        {
            if (_spawned.TryGetValue(player, out var obj))
            {
                // Debug.Log($"PlayerSpawner: GameObject {obj} найден для {player}!");
                return obj;
            }
            else
            {
                // Debug.LogError($"PlayerSpawner: playerRef {player} не найден!");
                return null;
            }
        }

        public void GetComponents(PlayerRef player, out HandItemController handItemController, out InteractionController interactionController)
        {
            var playerObj = GetPlayerGameObject(player);
            if (playerObj != null)
            {
                interactionController = playerObj.GetComponent<InteractionController>();
                handItemController = playerObj.GetComponent<HandItemController>();

                if (interactionController == null || handItemController == null)
                {
                    // Debug.LogError($"[PlayerSpawner] Один из компонентов не найден на игроке с PlayerRef {player}");
                }
            }
            else
            {
                interactionController = null;
                handItemController = null;
                // Debug.LogError($"[PlayerSpawner] Игрок с PlayerRef {player} не найден!");
            }
        }
            
    }
}