using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using Zenject;
using System;

namespace Game.Network
{
    public class Startup : MonoBehaviour
    {
        [Inject] private NetworkRunner _runner;
        [Inject] private NetworkCallbacks _callbacks;

        public static event Action OnSessionStarted;

        private TaskCompletionSource<List<SessionInfo>> _sessionListAwaiter;

        private void Awake()
        {
            // Назначаем делегат ДО любого JoinSessionLobby!
            _callbacks.OnSessionListReceived = OnSessionListUpdatedHandler;
            _runner.AddCallbacks(_callbacks); // обязательно!
        }

        public async Task<List<SessionInfo>> GetSessionList()
        {
            _sessionListAwaiter = new TaskCompletionSource<List<SessionInfo>>();

            Debug.Log("[Startup] JoinSessionLobby...");
            await _runner.JoinSessionLobby(SessionLobby.Shared);
            Debug.Log("[Startup] Waiting for session list...");

            var completedTask = await Task.WhenAny(_sessionListAwaiter.Task, Task.Delay(5000));
            if (completedTask != _sessionListAwaiter.Task)
            {
                Debug.LogWarning("[Startup] Timeout waiting for session list, returning empty list");
                return new List<SessionInfo>();
            }
            return await _sessionListAwaiter.Task;
        }



        public async Task<bool> CheckSessionExists(string sessionName)
        {
            var sessionList = await GetSessionList();
            Debug.Log("[Startup] Session list received. Checking...");
            foreach (var session in sessionList)
            {
                Debug.Log($"[Startup] Found session: {session.Name}");
                if (session.Name == sessionName)
                    return true;
            }
            return false;
        }

        public async Task BeginSession(GameMode mode, string sessionName)
        {
            Debug.Log($"[Startup] BeginSession: mode={mode}, session={sessionName}");

            _runner.ProvideInput = true;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var sceneRef = SceneRef.FromIndex(scene.buildIndex);
            var sceneInfo = new NetworkSceneInfo();
            if (sceneRef.IsValid)
                sceneInfo.AddSceneRef(sceneRef, UnityEngine.SceneManagement.LoadSceneMode.Additive);

            Debug.Log("[Startup] Calling StartGame...");
            var result = await _runner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                SessionName = sessionName,
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
                Scene = sceneInfo,
            });

            Debug.Log($"[Startup] StartGame result: {result.Ok}, reason: {result.ShutdownReason}");

            if (!result.Ok)
            {
                Debug.LogError($"[Startup] Runner start failed: {result.ShutdownReason}");
                return;
            }

            Debug.Log($"[Startup] Fusion started as {mode}");

            if (_runner.IsServer)
                OnSessionStarted?.Invoke();
        }

        // Вызовется из NetworkCallbacks
        public void OnSessionListUpdatedHandler(List<SessionInfo> sessionList)
        {
            Debug.Log("[Startup] OnSessionListUpdatedHandler called.");
            _sessionListAwaiter?.TrySetResult(sessionList);
        }
    }
}
