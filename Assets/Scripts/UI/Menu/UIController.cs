using System;
using UnityEngine;

namespace Game.UI
{
    public enum UiPhase
    {
        Hidden,
        Loading,
        Gameplay,
        Inventory,
        OtherInventory,
        Spawn
    }
    public class UIController : MonoBehaviour
    {
        // [SerializeField] private GameObject _gameHud;
        [SerializeField] private GameObject _deathScreen;
        [SerializeField] private GameObject _quickSlotPanel;
        [SerializeField] private GameObject _inventoryPanel;
        [SerializeField] private GameObject _otherInventoryPanel;
        [SerializeField] private GameObject _interactionPrompt;
        [SerializeField] private GameObject _dot;
        [SerializeField] private GameObject _healthBar;
        [SerializeField] private GameObject _drugIcon;
        [SerializeField] private GameObject _background;
        [SerializeField] private GameObject _quickbackground;
        [SerializeField] private GameObject _uiCamera;
        [SerializeField] private GameObject _sceneCamera;
        [SerializeField] private InputHandler _inputHandler;
        [SerializeField] private UiPhase _defaultPhase = UiPhase.Gameplay;

        public UiPhase Phase { get; private set; }
        public bool InventoryOpened => Phase == UiPhase.Inventory || Phase == UiPhase.OtherInventory;

        public event Action<UiPhase, UiPhase> OnPhaseChanged;

        public void Awake()
        {
            ApplyPhase(_defaultPhase);
            Debug.Log($"UI Phase changed-> {_defaultPhase}");
        }

        public void SetPhase(UiPhase phase)
        {
            if (Phase == phase) return;
            var prev = Phase;
            ApplyPhase(phase);
            OnPhaseChanged?.Invoke(phase, prev);

            // Debug.Log($"UI Phase changed-> {phase}");
        }

        private void ApplyPhase(UiPhase phase)
        {
            Phase = phase;
            Debug.Log($"UI Phase changed-> {phase}");

            var showQuick = phase == UiPhase.Gameplay || phase == UiPhase.Inventory || phase == UiPhase.OtherInventory;
            var quickbackground = phase == UiPhase.Gameplay;
            var showInv = phase == UiPhase.Inventory || phase == UiPhase.OtherInventory;
            var showOtherInv = phase == UiPhase.OtherInventory;
            var showSpawn = phase == UiPhase.Spawn;
            var showPrompt = phase == UiPhase.Gameplay;
            var showDot = phase == UiPhase.Gameplay;
            var drugIcon = phase == UiPhase.Inventory || phase == UiPhase.OtherInventory;
            var healthBar = phase == UiPhase.Gameplay || phase == UiPhase.Inventory || phase == UiPhase.OtherInventory;
            var uiCam = phase == UiPhase.Loading;
            var sceneCamera = phase == UiPhase.Spawn;

            if (_quickSlotPanel != null) _quickSlotPanel.SetActive(showQuick);
            if (_quickbackground != null) _quickbackground.SetActive(quickbackground);
            if (_inventoryPanel != null) _inventoryPanel.SetActive(showInv);
            if (_background != null) _background.SetActive(showInv);
            if (_otherInventoryPanel != null) _otherInventoryPanel.SetActive(showOtherInv);
            if (_deathScreen != null) _deathScreen.SetActive(showSpawn);
            if (_interactionPrompt != null) _interactionPrompt.SetActive(showPrompt);
            if (_healthBar != null) _healthBar.SetActive(healthBar);
            if (_dot != null) _dot.SetActive(showDot);
            if (_drugIcon != null) _drugIcon.SetActive(drugIcon);
            if (_uiCamera != null)
            {
                _uiCamera.SetActive(uiCam);
                var cam = _uiCamera.GetComponentInChildren<Camera>(true);
                if (cam != null) cam.enabled = uiCam;
                var al = _uiCamera.GetComponentInChildren<AudioListener>(true);
                if (al != null) al.enabled = uiCam;
            }
            if (_sceneCamera != null)
            {
                _sceneCamera.SetActive(sceneCamera);
                var cam = _sceneCamera.GetComponentInChildren<Camera>(true);
                if (cam != null) cam.enabled = sceneCamera;
                var al = _sceneCamera.GetComponentInChildren<AudioListener>(true);
                if (al != null) al.enabled = sceneCamera;
            }

            var cursor = phase == UiPhase.Inventory || phase == UiPhase.OtherInventory || phase == UiPhase.Spawn || phase == UiPhase.Loading || phase == UiPhase.Hidden;
            Cursor.lockState = cursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = cursor;

            if (_inputHandler != null) _inputHandler.SetInventoryOpen(InventoryOpened);
        }
    }
}