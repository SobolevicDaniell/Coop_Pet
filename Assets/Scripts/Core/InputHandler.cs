using Fusion;
using UnityEngine;

namespace Game
{
    public class InputHandler : MonoBehaviour
    {
        [Header("UI References")]
        //[SerializeField] private QuickSlotManager _quickSlotManager;
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
            // Инвентарь
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                InventoryOpen = !InventoryOpen;
                if (_inventoryPanel != null)
                    _inventoryPanel.SetActive(InventoryOpen);
            }

            // Быстрая панель
            //if (!InventoryOpen && _quickSlotManager != null)
            //{
            //    for (int i = 0; i < _quickSlotManager.SlotCount && i < 10; i++)
            //    {
            //        KeyCode key = (i == 9) ? KeyCode.Alpha0 : KeyCode.Alpha1 + i;
            //        if (Input.GetKeyDown(key))
            //        {
            //            _quickSlotManager.SetActiveSlot(i);
            //            break;
            //        }
            //    }
            //}

            // Сетевой ввод остаётся без правок…
            if (InventoryOpen)
            {
                _networkInput = new InputData();
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
