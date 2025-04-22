using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace Game.Network
{
    public class Startup : MonoBehaviour
    {
        [Inject] private NetworkRunner _runner;
        [Inject] private NetworkCallbacks _callbacks;

        [Header("Session")]
        [SerializeField] string _sessionName = "TestRoom";
        [SerializeField] GameMode _mode = GameMode.AutoHostOrClient;

        async void Start()
        {
            var sceneRef = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
            var info = new NetworkSceneInfo();
            if (sceneRef.IsValid)
                info.AddSceneRef(sceneRef, LoadSceneMode.Additive);

            var args = new StartGameArgs
            {
                GameMode = _mode,
                SessionName = _sessionName,
                Scene = info,
                SceneManager = GetComponent<NetworkSceneManagerDefault>()
            };

            var result = await _runner.StartGame(args);
            if (!result.Ok)
            {
                Debug.LogError($"Runner start failed: {result.ShutdownReason}");
                return;
            }

            Debug.Log($"{_mode} started");
            _runner.AddCallbacks(_callbacks);
        }
    }
}
