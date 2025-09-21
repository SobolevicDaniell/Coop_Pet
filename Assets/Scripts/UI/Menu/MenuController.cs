using System.Threading.Tasks;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

namespace Game.Network
{
    public sealed class MenuController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_InputField _sessionInput;
        [SerializeField] private Button _connectButton;
        [SerializeField] private GameObject _confirmPanel;
        [SerializeField] private TMP_Text _confirmText;
        [SerializeField] private Button _yesButton;
        [SerializeField] private Button _noButton;

        [Header("Config")]
        [SerializeField] private string _levelSceneName = "Level 1";
        [SerializeField] private string _defaultSessionName = "MySession";

        [Inject(Optional = true)] private MenuSessionService _sessions;
        [Inject(Optional = true)] private LaunchRequestStore _store;

        private bool _busy;
        private string _pendingName;

        private enum PendingAction
        {
            None,
            JoinClient,
            StartHost,
            ShutdownThenJoinClient
        }

        private PendingAction _pendingAction = PendingAction.None;

        private void Awake()
        {
            if (_connectButton) _connectButton.onClick.AddListener(OnConnectPressed);
            if (_yesButton) _yesButton.onClick.AddListener(OnYes);
            if (_noButton) _noButton.onClick.AddListener(OnNo);

            if (_confirmPanel) _confirmPanel.SetActive(false);
            if (_sessionInput && string.IsNullOrWhiteSpace(_sessionInput.text))
                _sessionInput.text = _defaultSessionName;

            EnsureStore();
            EnsureSessionService();
        }

        private void EnsureStore()
        {
            if (_store == null)
                _store = FindAnyObjectByType<LaunchRequestStore>(FindObjectsInactive.Include);
        }

        private void EnsureSessionService()
        {
            if (_sessions == null)
                _sessions = new MenuSessionService();
        }

        private static bool HasActiveRunner()
        {
            var runners = Object.FindObjectsByType<NetworkRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var r in runners)
            {
                if (r == null) continue;
                try { if (r.IsRunning) return true; }
                catch { return true; } // на старых версиях — перестраховка
            }
            return false;
        }

        private async void OnConnectPressed()
        {
            if (_busy) return;
            _busy = true;
            if (_connectButton) _connectButton.interactable = false;

            try
            {
                EnsureStore();
                EnsureSessionService();

                var name = !string.IsNullOrWhiteSpace(_sessionInput?.text)
                    ? _sessionInput.text.Trim()
                    : _defaultSessionName;

                // SINGLE: если уже есть активный Runner в процессе — не лезем в лобби,
                // сначала спросим, нужно ли остановить текущую сессию и подключиться как клиент.
                if (HasActiveRunner())
                {
                    _pendingName = name;
                    _pendingAction = PendingAction.ShutdownThenJoinClient;
                    ShowConfirm($"В этом процессе уже запущена сессия.\nОстановить её и подключиться к «{name}» как клиент?");
                    return;
                }

                // Нет активного Runner — можно безопасно проверить лобби
                var check = await _sessions.Check(name);

                if (check == SessionCheck.Exists)
                {
                    _pendingName = name;
                    _pendingAction = PendingAction.JoinClient;
                    ShowConfirm($"Сессия «{name}» найдена. Подключиться как клиент?");
                    return;
                }

                if (check == SessionCheck.Unknown)
                {
                    // Листинг недоступен (или Single-защита сработала) — не блокируем UX
                    _pendingName = name;
                    _pendingAction = PendingAction.JoinClient;
                    ShowConfirm($"Не удалось проверить наличие «{name}». Подключиться как клиент?");
                    return;
                }

                // NotFound → создаём как Host
                _pendingAction = PendingAction.StartHost;
                await LoadGameAs(GameMode.Host, name);
            }
            finally
            {
                if (_connectButton) _connectButton.interactable = true;
                _busy = false;
            }
        }

        private async void OnYes()
        {
            if (_confirmPanel) _confirmPanel.SetActive(false);

            var name = !string.IsNullOrWhiteSpace(_pendingName)
                ? _pendingName
                : (!string.IsNullOrWhiteSpace(_sessionInput?.text) ? _sessionInput.text.Trim() : _defaultSessionName);

            var act = _pendingAction;
            _pendingName = null;
            _pendingAction = PendingAction.None;

            switch (act)
            {
                case PendingAction.ShutdownThenJoinClient:
                    // Полностью очищаем процесс от других Runner-ов перед заходом клиента
                    await RunnerGuard.KillAllExcept(null);
                    await LoadGameAs(GameMode.Client, name);
                    break;

                case PendingAction.JoinClient:
                    await LoadGameAs(GameMode.Client, name);
                    break;

                case PendingAction.StartHost:
                    await LoadGameAs(GameMode.Host, name);
                    break;

                default:
                    break;
            }
        }

        private void OnNo()
        {
            if (_confirmPanel) _confirmPanel.SetActive(false);
            _pendingName = null;
            _pendingAction = PendingAction.None;
        }

        private void ShowConfirm(string message)
        {
            if (_confirmPanel)
            {
                if (_confirmText) _confirmText.text = message;
                _confirmPanel.SetActive(true);
            }
            else
            {
                // Если панель не настроена — сразу выполняем действие
                OnYes();
            }
        }

        private async Task LoadGameAs(GameMode mode, string sessionName)
        {
            EnsureStore();
            if (_store == null)
            {
                Debug.LogError("[MenuController] LaunchRequestStore not found — не могу продолжить.");
                return;
            }

            _store.Set(mode, string.IsNullOrWhiteSpace(sessionName) ? _defaultSessionName : sessionName);

            // На всякий случай — зачистим любые живые Runner-ы
            await RunnerGuard.KillAllExcept(null);
            RunnerGuard.DumpRunners("Menu before load Level1");

            SceneManager.LoadScene(_levelSceneName);
        }
    }
}
