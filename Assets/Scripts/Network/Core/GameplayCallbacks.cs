using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Zenject;
using Game.Gameplay;
using Game.UI;
using Fusion.Sockets;
using System;

namespace Game.Network
{
    public class GameplayCallbacks : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Inject(Optional = true)] private NetworkRunner _runner;
        [Inject] private PlayerSpawner _playerSpawner;
        [Inject] private InputHandler _inputHandler;
        [Inject] private DiContainer _container;

        private void OnEnable()
        {
            if (_runner != null) _runner.AddCallbacks(this);
        }

        private void OnDisable()
        {
            if (_runner != null) _runner.RemoveCallbacks(this);
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            var all = FindObjectsOfType<NetworkObject>();
            for (int i = 0; i < all.Length; i++)
                _container.InjectGameObject(all[i].gameObject);

            var ui = FindObjectOfType<UIController>(true);
            if (ui != null && ui.Phase != UiPhase.Death && ui.Phase != UiPhase.Loading)
                ui.SetPhase(UiPhase.Gameplay);
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            _playerSpawner.EnsurePlayerObject(runner, player);
            // _playerSpawner.SpawnAvatar(runner, player);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            _playerSpawner.DespawnAvatar(runner, player, null);
            _playerSpawner.RemovePlayerObject(runner, player);
        }



        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            _inputHandler.ProvideNetworkInput(runner, input);
        }

        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnDisconnectedFromServer(NetworkRunner runner) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
            if (obj != null) _container.InjectGameObject(obj.gameObject);
        }


        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    }
}