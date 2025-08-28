using UnityEngine;

namespace Game.UI
{
    public class UIController : MonoBehaviour
    {
        [SerializeField] private GameObject _gameHud;
        [SerializeField] private GameObject _menuHud;
        [SerializeField] private GameObject _deathScreen;
        [SerializeField] private GameObject _quickSlotPanel;
        [SerializeField] private GameObject _inventoryPanel;
        [SerializeField] private GameObject _otherInventoryPanel;
        [SerializeField] private GameObject _interactionPrompt;
        [SerializeField] private GameObject _dot;
        [SerializeField] private GameObject _uiCamera;

        [SerializeField] private InputHandler _inputHandler;

        public bool InventoryOpened => _inventoryPanel.activeSelf;

        private void OnEnable()
        {
            Network.Startup.OnSessionStarted += ShowGameHUD;
        }

        private void OnDisable()
        {
            Network.Startup.OnSessionStarted -= ShowGameHUD;
        }

        public void ShowGameHUD()
        {
            _gameHud.SetActive(true);
            _menuHud.SetActive(false);
            _quickSlotPanel.SetActive(true);
            _inventoryPanel.SetActive(false);
            _otherInventoryPanel.SetActive(false);
            _uiCamera.SetActive(false);
            _dot.SetActive(true);
            _interactionPrompt.SetActive(false);
            SetCursor(false);
            _inputHandler.SetInventoryOpen(false);
            _deathScreen.SetActive(false);
        }

        public void ShowInventory(bool showOther = false)
        {
            _gameHud.SetActive(true);
            _inventoryPanel.SetActive(true);
            _otherInventoryPanel.SetActive(showOther);
            _interactionPrompt.SetActive(false);
            _uiCamera.SetActive(false);
            _dot.SetActive(false);
            SetCursor(true);
            _inputHandler.SetInventoryOpen(true);
        }

        public void HideGameHUD()
        {
            _gameHud.SetActive(false);
            _inventoryPanel.SetActive(false);
            _otherInventoryPanel.SetActive(false);
            _interactionPrompt.SetActive(false);
            _dot.SetActive(false);
            _uiCamera.SetActive(true);
            SetCursor(true);
        }

        public void SetCursor(bool visible)
        {
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = visible;
        }
    }
}
