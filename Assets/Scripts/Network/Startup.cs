using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;
using System.Threading.Tasks;

namespace Game.Network
{
    public class Startup : MonoBehaviour
    {
        [Inject] private NetworkRunner _runner;
        [Inject] private NetworkCallbacks _callbacks;

        [Header("Session")]
        [SerializeField] private string _sessionName = "TestRoom";
        [SerializeField] private GameMode _mode = GameMode.AutoHostOrClient;

        private async void Start()
        {
            // ќжидаем один кадр, чтобы Zenject успел выполнить InstallBindings
            await Task.Yield();

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
