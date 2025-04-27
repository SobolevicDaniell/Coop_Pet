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

        private async void Start()
        {
            //await Task.Yield();

            GameMode mode = GameMode.AutoHostOrClient;
            if (PlayerPrefs.HasKey("GameMode"))
            {
                mode = (GameMode)System.Enum.Parse(typeof(GameMode), PlayerPrefs.GetString("GameMode"));
            }

            var sceneRef = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
            var info = new NetworkSceneInfo();
            if (sceneRef.IsValid)
                info.AddSceneRef(sceneRef, LoadSceneMode.Additive);

            var args = new StartGameArgs
            {
                GameMode = mode,
                SessionName = _sessionName,
                Scene = info,
                SceneManager = GetComponent<NetworkSceneManagerDefault>(),
            };

            var result = await _runner.StartGame(args);
            if (!result.Ok)
            {
                Debug.LogError($"Runner start failed: {result.ShutdownReason}");
                return;
            }

            Debug.Log($"{mode} started");

            _runner.AddCallbacks(_callbacks);
        }
    }
}
