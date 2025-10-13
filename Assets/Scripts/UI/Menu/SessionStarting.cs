using System;
using System.Threading.Tasks;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;
using Game.UI;

namespace Game.Network
{
    public sealed class SessionStarting : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_InputField _sessionInput;
        [SerializeField] private Button _connectButton;
        [SerializeField] private string _defaultSessionName = "DefaultSession";

        [Header("Scenes")]
        [SerializeField] private string _scenrToLoad = "Level 1";

        [Inject] private MenuSessionService _sessionService;
        [Inject] private LaunchRequestStore _store;
        [Inject] private MainMenuUIStateController _ui;

        private bool _busy;

        private void Awake()
        {
            EnsureDefaultSessionName();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnEnable()
        {
            if (_connectButton) _connectButton.onClick.AddListener(OnConnectClicked);
            _ui.SetPhase(MainMenuUiPhase.Main);
            EnsureDefaultSessionName();
        }

        private void OnDisable()
        {
            if (_connectButton) _connectButton.onClick.RemoveListener(OnConnectClicked);
        }

        private async void OnConnectClicked()
        {
            if (_busy) return;

            var sessionName = (_sessionInput ? _sessionInput.text : string.Empty)?.Trim();
            if (string.IsNullOrEmpty(sessionName)) sessionName = _defaultSessionName;

            _busy = true;
            _ui.SetPhase(MainMenuUiPhase.Loading);

            try
            {
                var check = await _sessionService.Check(sessionName, 5f);
                var mode = check == SessionCheck.Exists ? GameMode.Client : GameMode.Host;
                _store.Set(mode, sessionName);
                SceneManager.LoadScene(_scenrToLoad);
            }
            catch
            {
                _ui.SetPhase(MainMenuUiPhase.Confirmation);
                _busy = false;
                EnsureDefaultSessionName();
            }
        }

        private void EnsureDefaultSessionName()
        {
            if (_sessionInput == null) return;
            var current = _sessionInput.text;
            if (string.IsNullOrWhiteSpace(current)) _sessionInput.text = _defaultSessionName;
        }
    }
}
