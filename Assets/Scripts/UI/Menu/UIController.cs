using UnityEngine;

namespace Game.UI
{
    public class UIController : MonoBehaviour
    {
        [SerializeField] private GameObject _gameUI;
        [SerializeField] private GameObject _inventoryPanel;
        [SerializeField] private GameObject _otherInventoryPanel;
        [SerializeField] private GameObject _interactionPrompt;
        [SerializeField] private GameObject _dot;
        [SerializeField] private GameObject _uiCamera;

        // private void Awake()
        // {
        //     HideGameUI();
        // }

        private void OnEnable()
        {
            Network.Startup.OnSessionStarted += ShowGameUI;
        }

        private void OnDisable()
        {
            Network.Startup.OnSessionStarted -= ShowGameUI;
        }
        
        public void ShowGameUI()
        {
            Debug.Log("[UIController] ShowGameUI called");
            _gameUI.SetActive(true);
            _inventoryPanel.SetActive(false);
            _otherInventoryPanel.SetActive(false);
            _uiCamera.SetActive(false);
            _dot.SetActive(true);
            _interactionPrompt.SetActive(true);
            SetCursor(false);
        }

        public void ShowInventory(bool showOther = false)
        {
            _gameUI.SetActive(true);
            _inventoryPanel.SetActive(true);
            _otherInventoryPanel.SetActive(showOther);
            _interactionPrompt.SetActive(false);
            _uiCamera.SetActive(false);
            _dot.SetActive(false);
            SetCursor(true);
        }

        public void HideGameUI()
        {
            _gameUI.SetActive(false);
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
