using Fusion;
using Fusion.Sockets;
using UnityEngine;
using Zenject;
using Game.Gameplay;
using System.Collections;

namespace Game.Network
{
    public class NetworkCallbacks : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Inject] private PlayerSpawner _playerSpawner;
        [Inject] private InputHandler _inputHandler;
        [Inject] private DiContainer _container;

        public void OnConnectedToServer(NetworkRunner runner)
        {
            foreach (var no in FindObjectsOfType<NetworkObject>())
            {
                _container.InjectGameObject(no.gameObject);
            }
        }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
            
        }


        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer)
            {
                _playerSpawner.SpawnPlayer(player);
                Debug.Log($"[Server] Spawned Player: {player}");
            }
        }



        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer)
                _playerSpawner.RemovePlayer(runner, player);
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
            => _inputHandler.ProvideNetworkInput(runner, input);

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest req, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken token) { }
        public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason reason) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
