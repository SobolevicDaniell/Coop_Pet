using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using Zenject;
using System;
using Game.UI;

namespace Game.Network
{
    public class Startup : MonoBehaviour
    {
        [Inject] private NetworkRunner _runner;
        [Inject] private NetworkCallbacks _callbacks;
        [Inject] private UIController _uiController;
        [Inject] private INetworkObjectProvider _provider;

        public static event Action OnSessionStarted;

        private TaskCompletionSource<List<SessionInfo>> _sessionListAwaiter;

        private void Awake()
        {
            _callbacks.OnSessionListReceived = OnSessionListUpdatedHandler;
            _runner.AddCallbacks(_callbacks);

            if (_uiController != null)
                _uiController.HideGameUI();
        }

        public async Task<List<SessionInfo>> GetSessionList()
        {
            _sessionListAwaiter = new TaskCompletionSource<List<SessionInfo>>();

            await _runner.JoinSessionLobby(SessionLobby.Shared);

            var completedTask = await Task.WhenAny(_sessionListAwaiter.Task, Task.Delay(5000));
            if (completedTask != _sessionListAwaiter.Task)
            {
                return new List<SessionInfo>();
            }
            return await _sessionListAwaiter.Task;
        }

        public async Task<bool> CheckSessionExists(string sessionName)
        {
            var sessionList = await GetSessionList();
            foreach (var session in sessionList)
            {
                if (session.Name == sessionName)
                    return true;
            }
            return false;
        }

        public async Task BeginSession(GameMode mode, string sessionName)
        {
            _runner.ProvideInput = true;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var sceneRef = SceneRef.FromIndex(scene.buildIndex);
            var sceneInfo = new NetworkSceneInfo();
            if (sceneRef.IsValid)
                sceneInfo.AddSceneRef(sceneRef, UnityEngine.SceneManagement.LoadSceneMode.Additive);

            var args = new StartGameArgs
            {
                GameMode = mode,
                SessionName = sessionName,
                Scene = sceneInfo,
                ObjectProvider = _provider
            };

            await _runner.StartGame(args);
        }

        public void OnSessionListUpdatedHandler(List<SessionInfo> sessionList)
        {
            _sessionListAwaiter?.TrySetResult(sessionList);
        }
    }
}
