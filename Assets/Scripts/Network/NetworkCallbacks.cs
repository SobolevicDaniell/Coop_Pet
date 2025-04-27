using Fusion;
using Fusion.Sockets;
using UnityEngine;
using Zenject;

namespace Game.Network
{
    public class NetworkCallbacks : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Inject] private PlayerSpawner _spawner;
        [Inject] private InputHandler _inputHandler;

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer)
                return;

            // Убираем фильтрацию по LocalPlayer
            // Просто прогоняем каждого через вашу фабрику:
            _spawner.SpawnPlayer(runner, player);
            Debug.Log($"[Server] Spawned Player: {player}");
        }


        public void OnConnectedToServer(NetworkRunner runner)
        {
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer)
                _spawner.RemovePlayer(runner, player);
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            _inputHandler.ProvideNetworkInput(runner, input);
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken token) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason reason) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    }
}
