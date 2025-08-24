using System.Threading.Tasks;
using Fusion;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Game.Network
{
    public sealed class MenuController : MonoBehaviour
    {
        [Inject] private Startup _startup;
        [Inject(Optional = true)] private UIController _ui; // не обязателен

        [Header("UI")]
        [SerializeField] private TMP_InputField _sessionInput;
        [SerializeField] private Button _connectButton;

        [Header("Confirm If Session Exists")]
        [SerializeField] private GameObject _confirmPanel;
        [SerializeField] private TMP_Text _confirmText;
        [SerializeField] private Button _yesButton;
        [SerializeField] private Button _noButton;

        [Header("Defaults")]
        [SerializeField] private string _defaultSessionName = "Session_1";

        private string _pendingSessionName;

        private void Awake()
        {
            if (_sessionInput == null) _sessionInput = GetComponentInChildren<TMP_InputField>(true);
            if (_connectButton == null) _connectButton = GetComponentInChildren<Button>(true);

            if (_connectButton != null) _connectButton.onClick.AddListener(OnConnectPressed);
            if (_yesButton != null) _yesButton.onClick.AddListener(OnYesPressed);
            if (_noButton != null) _noButton.onClick.AddListener(OnNoPressed);

            if (_confirmPanel != null) _confirmPanel.SetActive(false);
        }

        private void Start() => EnsureDefaultSessionName();
        private void OnEnable() => EnsureDefaultSessionName();

        private void OnDestroy()
        {
            if (_connectButton != null) _connectButton.onClick.RemoveListener(OnConnectPressed);
            if (_yesButton != null) _yesButton.onClick.RemoveListener(OnYesPressed);
            if (_noButton != null) _noButton.onClick.RemoveListener(OnNoPressed);
        }

        private void EnsureDefaultSessionName()
        {
            if (_sessionInput == null) return;
            var last = PlayerPrefs.GetString("LastSessionName", _defaultSessionName);
            if (string.IsNullOrWhiteSpace(_sessionInput.text))
                _sessionInput.text = string.IsNullOrWhiteSpace(last) ? _defaultSessionName : last;
        }

        private void SaveLastSessionName(string name)
        {
            PlayerPrefs.SetString("LastSessionName", name);
            PlayerPrefs.Save();
        }

        private void SetInteractable(bool value)
        {
            if (_connectButton != null) _connectButton.interactable = value;
        }

        private void OnConnectPressed() => _ = OnConnectPressedAsync();

        private async Task OnConnectPressedAsync()
        {
            if (_startup == null)
            {
                Debug.LogError("[Menu] Startup is null. Проверьте SceneContext/NetworkInstaller и наличие Startup в сцене.");
                return;
            }

            var name = string.IsNullOrWhiteSpace(_sessionInput?.text) ? _defaultSessionName : _sessionInput.text.Trim();
            SaveLastSessionName(name);

            SetInteractable(false);
            try
            {
                var exists = await _startup.CheckSessionExists(name);

                if (!exists)
                {
                    await Launch(GameMode.Host, name);
                }
                else
                {
                    _pendingSessionName = name;
                    if (_confirmPanel != null)
                    {
                        if (_confirmText != null)
                            _confirmText.text = $"Сессия \"{name}\" уже существует. Подключиться как клиент?";
                        _confirmPanel.SetActive(true);
                    }
                    else
                    {
                        await Launch(GameMode.Client, name);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                gameObject.SetActive(true);
            }
            finally
            {
                SetInteractable(true);
            }
        }

        public void OnNoPressed()
        {
            if (_confirmPanel != null) _confirmPanel.SetActive(false);
            _pendingSessionName = null;
        }

        public void OnYesPressed() => _ = OnYesPressedAsync();

        private async Task OnYesPressedAsync()
        {
            if (_confirmPanel != null) _confirmPanel.SetActive(false);
            var name = string.IsNullOrEmpty(_pendingSessionName)
                ? (string.IsNullOrWhiteSpace(_sessionInput?.text) ? _defaultSessionName : _sessionInput.text.Trim())
                : _pendingSessionName;

            _pendingSessionName = null;

            SetInteractable(false);
            try
            {
                await Launch(GameMode.Client, name);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                gameObject.SetActive(true);
            }
            finally
            {
                SetInteractable(true);
            }
        }

        private async Task Launch(GameMode mode, string sessionName)
        {
            gameObject.SetActive(false);
            await _startup.BeginSession(mode, sessionName);
            _ui?.ShowGameUI(); // показать игровой UI после старта
        }
    }
}
