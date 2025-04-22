using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Network
{
    public sealed class NetworkService : INetworkService
    {
        private readonly NetworkCallbacks _callbacks;
        private NetworkRunner _runner;
        public NetworkRunner Runner => _runner;

        private readonly string _sessionName = "Session1";

        public NetworkService(NetworkCallbacks callbacks)
        {
            _callbacks = callbacks;
        }

        public async Task StartGame(GameMode mode)
        {
            var runnerGO = new GameObject("NetworkRunner");
            Object.DontDestroyOnLoad(runnerGO);

            _runner = runnerGO.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
            _runner.AddCallbacks(_callbacks);

            var sceneIndex = SceneManager.GetActiveScene().buildIndex;
            var sceneRef = SceneRef.FromIndex(sceneIndex);

            await _runner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                SessionName = _sessionName,
                Scene = sceneRef,
                SceneManager = runnerGO.AddComponent<NetworkSceneManagerDefault>()
            });
        }
    }
}
