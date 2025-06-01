using Fusion;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Game.Network
{
    public class MenuController : MonoBehaviour
    {
        [Inject] private Startup _startup;

        [Header("UI")]
        [SerializeField] private TMP_InputField _sessionInput;
        [SerializeField] private Button _connectButton;
        [SerializeField] private GameObject _confirmPanel;
        [SerializeField] private Button _yesButton;
        [SerializeField] private Button _noButton;
        [SerializeField] private TextMeshProUGUI _confirmText;

        private void Awake()
        {
            _sessionInput.text = "Session_1";
            _connectButton.onClick.AddListener(OnConnectPressed);
            _yesButton.onClick.AddListener(OnYesPressed);
            _noButton.onClick.AddListener(OnNoPressed);

            _confirmPanel.SetActive(false);
        }

        private async void OnConnectPressed()
        {
            string sessionName = _sessionInput.text.Trim();
            Debug.Log("[Menu] Connect pressed, checking session: " + sessionName);

            var exists = await _startup.CheckSessionExists(sessionName);
            Debug.Log("[Menu] Session exists: " + exists);

            if (!exists)
            {
                Debug.Log("[Menu] Launching as Host: " + sessionName);
                
                await Launch(GameMode.Host, sessionName);
            }
            else
            {
                Debug.Log("[Menu] Session already exists, showing panel");
                _confirmText.text = "The session already exists. Do you want to connect?";
                _confirmPanel.SetActive(true);
            }
        }

        private async void OnYesPressed()
        {
            string sessionName = _sessionInput.text.Trim();
            _confirmPanel.SetActive(false);
            Debug.Log("[Menu] Connecting as client to session: " + sessionName);
            await Launch(GameMode.Client, sessionName);
        }

        private void OnNoPressed()
        {
            Debug.Log("[Menu] Cancel connect");
            _confirmPanel.SetActive(false);
        }

        private async Task Launch(GameMode mode, string sessionName)
        {
            Debug.Log($"[Menu] Launch called, mode={mode}, session={sessionName}");
            gameObject.SetActive(false);
            await _startup.BeginSession(mode, sessionName);
        }
    }
}
