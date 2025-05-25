using UnityEngine;

namespace Game
{
    public class UIController : MonoBehaviour
    {

        [Header("UI References")]
        [SerializeField] private GameObject _inventoryPanel;
        [SerializeField] private GameObject _otherInventoryPanel;
        [SerializeField] private GameObject _uiGame;
        [SerializeField] private GameObject _uiCamera;

        private void OnEnable()
        {
            Network.Startup.OnSessionStarted += GameUI;
        }

        private void OnDisable()
        {
            Network.Startup.OnSessionStarted -= GameUI;
        }

        private void GameUI()
        {
            if (_uiGame != null) _uiGame.SetActive(true);
            if (_inventoryPanel != null) _inventoryPanel.SetActive(false);
            if (_otherInventoryPanel != null) _otherInventoryPanel.SetActive(false);
            if (_uiCamera != null) _uiCamera.SetActive(false);
        }
    }
}