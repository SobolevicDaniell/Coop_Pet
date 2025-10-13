using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using Game.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

namespace Game.UI
{
    public sealed class MenuUIController : MonoBehaviour
    {
        [SerializeField] private Button _exitToMenuButton;
        [SerializeField] private Button _quitGameButton;
        [SerializeField] private string _mainMenuSceneName = "MainMenu";
        [SerializeField] private Slider _sensitivitySlider;

        [Inject(Optional = true)] private NetworkRunner _runner;
        [Inject(Optional = true)] private UIController _ui;
        [Inject] private PlayerStatsSO _stats;
        [Inject] private ISettingsService _settings;

        private bool _busy;

        void OnEnable()
        {
            if (_exitToMenuButton != null) _exitToMenuButton.onClick.AddListener(OnExitToMenuClicked);
            if (_quitGameButton != null) _quitGameButton.onClick.AddListener(OnQuitGameClicked);

            if (_sensitivitySlider != null && _stats != null)
            {
                _sensitivitySlider.minValue = _stats.minMouseLookSensitivity;
                _sensitivitySlider.maxValue = _stats.maxMouseLookSensitivity;
                _sensitivitySlider.wholeNumbers = false;
                _sensitivitySlider.value = _settings.MouseSensitivity;
                _sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            }
        }

        void OnDisable()
        {
            if (_exitToMenuButton) _exitToMenuButton.onClick.RemoveListener(OnExitToMenuClicked);
            if (_quitGameButton)   _quitGameButton.onClick.RemoveListener(OnQuitGameClicked);
            if (_sensitivitySlider) _sensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
        }

        private void OnSensitivityChanged(float value)
        {
            _settings.SetMouseSensitivity(value);
        }

        private async void OnExitToMenuClicked()
        {
            _ui?.SetPhase(UiPhase.Exit);
            if (_busy) return;
            _busy = true; SetInteractable(false);
            try
            {
                var runners = CollectRunners();
                foreach (var r in runners)
                    if (r != null && r.IsRunning)
                        try { await r.Shutdown(); } catch { }

                foreach (var r in runners)
                    if (r != null) Destroy(r.gameObject);

                SceneManager.LoadScene(_mainMenuSceneName, LoadSceneMode.Single);
            }
            finally { _busy = false; SetInteractable(true); }
        }

        private void OnQuitGameClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetInteractable(bool v)
        {
            if (_exitToMenuButton) _exitToMenuButton.interactable = v;
            if (_quitGameButton)   _quitGameButton.interactable = v;
        }

        private List<NetworkRunner> CollectRunners()
        {
            var list = new List<NetworkRunner>();
            if (_runner) list.Add(_runner);
            foreach (var r in FindObjectsOfType<NetworkRunner>(true))
                if (r && !list.Contains(r)) list.Add(r);
            return list.Where(r => r).ToList();
        }
    }
}