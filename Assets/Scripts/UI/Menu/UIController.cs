using UnityEngine;

namespace Game
{
    public class UIController : MonoBehaviour
    {

        [Header("UI References")]
        [SerializeField] private GameObject _inventoryPanel;
        [SerializeField] private GameObject _otherInventoryPanel;
        [SerializeField] private GameObject _interactionPrompt;
        [SerializeField] private GameObject _dot;
        [SerializeField] private GameObject _gameUI;
        [SerializeField] private GameObject _uiCamera;

        private void OnEnable()
        {
            Network.Startup.OnSessionStarted += GameUI;
        }

        private void OnDisable()
        {
            Network.Startup.OnSessionStarted -= GameUI;
        }

        private void Start()
        {
            MenuUI();
        }

        private void GameUI()
        {
            _gameUI.SetActive(true);
            _inventoryPanel.SetActive(false);
            _otherInventoryPanel.SetActive(false);
            _uiCamera.SetActive(false);
            _dot.SetActive(true);
        }
        private void MenuUI()
        {
            _gameUI.SetActive(false);
            _inventoryPanel.SetActive(false);
            _otherInventoryPanel.SetActive(false);
            _interactionPrompt.SetActive(false);
            _dot.SetActive(false);
            _uiCamera.SetActive(true);
        }
    }
}