// Assets/Scripts/Game/InputHandler.cs
using Fusion;
using UnityEngine;

namespace Game
{
    public class InputHandler : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _inventoryPanel;

        private InputData _networkInput;
        public bool InventoryOpen { get; private set; }

        private void Awake()
        {
            if (_inventoryPanel != null)
                _inventoryPanel.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                InventoryOpen = !InventoryOpen;
                _inventoryPanel?.SetActive(InventoryOpen);
            }

            if (InventoryOpen)
            {
                _networkInput = new InputData(); // no move/look while UI open
            }
            else
            {
                _networkInput.movement = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
                _networkInput.mouseX = Input.GetAxis("Mouse X");
                _networkInput.mouseY = Input.GetAxis("Mouse Y");
                _networkInput.jump = Input.GetKey(KeyCode.Space);
            }
        }

        public void ProvideNetworkInput(NetworkRunner runner, NetworkInput input)
        {
            input.Set(_networkInput);
        }
    }
}
