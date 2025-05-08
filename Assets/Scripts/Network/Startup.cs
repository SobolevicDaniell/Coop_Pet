// Startup.cs
using Fusion;
using UnityEngine;
using Zenject;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using Game.Gameplay;
using System;

namespace Game.Network
{
    public class Startup : MonoBehaviour
    {
        [Inject] private NetworkRunner _runner;
        [Inject] private NetworkCallbacks _callbacks;
        [Inject] private PickableSpawner _pickableSpawner;

        [Header("Session")]
        [SerializeField] private string _sessionName = "TestRoom";

        public static event Action OnSessionStarted;

        public async Task BeginSession(GameMode mode)
        {
            // 1) Включаем ввод
            _runner.ProvideInput = true;

            // 2) Регистрируем колбэки до старта
            _runner.AddCallbacks(_callbacks);

            // 3) Готовим сцену (оставляем текущую)
            var scene = SceneManager.GetActiveScene();
            var sceneRef = SceneRef.FromIndex(scene.buildIndex);
            var sceneInfo = new NetworkSceneInfo();
            if (sceneRef.IsValid)
                sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Additive);

            // 4) Запускаем Fusion
            var result = await _runner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                SessionName = _sessionName,
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
                Scene = sceneInfo,
            });

            if (!result.Ok)
            {
                Debug.LogError($"[Startup] Runner start failed: {result.ShutdownReason}");
                return;
            }

            Debug.Log($"[Startup] Fusion started as {mode}");

            if (_runner.IsServer)
                OnSessionStarted?.Invoke();
        }
    }
}