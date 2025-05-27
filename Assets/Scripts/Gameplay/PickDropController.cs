using Fusion;
using UnityEngine;
using Game.UI;

namespace Game
{
    public class PickDropController : MonoBehaviour
    {
        public InventoryService Inventory { get; private set; }

        private InventoryPanel _playerInventoryPanel;
        private InventoryPanel _otherInventoryPanel;
        private ItemDatabaseSO _itemDatabase;
        private InputHandler _inputHandler;

        private InteractionController _ic;
        private ServerRpcHandler _rpc;
        private PickableItem _focusedItem;

        private IInventory _focusedInventory;
        private IInventory _openedInventory;

        public void Initialize(
            InteractionController controller,
            InventoryService inventory,
            InventoryPanel playerPanel,
            InventoryPanel otherPanel,
            ItemDatabaseSO itemDatabase,
            InputHandler input)
        {
            _ic = controller;
            _rpc = controller.RpcHandler;
            Inventory = inventory;
            _playerInventoryPanel = playerPanel;
            _otherInventoryPanel = otherPanel;
            _itemDatabase = itemDatabase;
            _inputHandler = input;

            _inputHandler.OnGlobalUiCloseRequested += CloseOpenedInventories;
            _inputHandler.OnInventoryToggle += TogglePlayerInventory;
        }

        private void OnDestroy()
        {
            if (_inputHandler != null)
            {
                _inputHandler.OnGlobalUiCloseRequested -= CloseOpenedInventories;
                _inputHandler.OnInventoryToggle -= TogglePlayerInventory;
            }
        }

        public void TryPick()
        {
            if (_focusedItem != null)
            {
                Debug.Log("TryPick: Picking " + _focusedItem.name);
                _rpc.RPC_RequestPick(_focusedItem.Object);
                return;
            }
            // Новое: если есть инвентарь, открываем его
            if (_focusedInventory != null)
            {
                OpenOtherInventory(_focusedInventory);
            }
        }

        public void TryDrop()
        {
            var selected = _ic.NetSelectedQuickSlot;
            if (selected < 0) return;

            var slots = Inventory.GetQuickSlots();
            if (selected >= slots.Length) return;

            var slot = slots[selected];
            if (slot.Id == null || slot.Count <= 0) return;

            int ammo = slot.State != null ? slot.State.Ammo : 0;

            _rpc.RPC_RequestDrop(
                _ic.DropPoint.position,
                _ic.Camera.transform.forward,
                slot.Id,
                slot.Count,
                ammo
            );

            slot.Id = null;
            slot.Count = 0;

            if (slot.State != null)
                slot.State.Ammo = 0;

            Inventory.RaiseQuickSlotsChanged();

            if (_ic.NetSelectedQuickSlot == selected)
            {
                _rpc.RPC_SelectQuickSlot(-1);
                _rpc.RPC_RequestDespawnHandModel();
            }
        }

        public void UpdateRaycast()
        {
            var ray = _ic.Camera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
            var range = _ic.Range;

            Debug.DrawRay(ray.origin, ray.direction * range, Color.green);

            _focusedItem = null;
            _focusedInventory = null;
            _ic.Prompt.Hide();

            if (Physics.Raycast(ray, out var hit, range))
            {
                // Сначала Pickable
                var pickable = hit.collider.GetComponentInParent<PickableItem>();
                if (pickable != null)
                {
                    _focusedItem = pickable;
                    _ic.Prompt.Show();
                    return;
                }

                // Потом сундук или другой инвентарь
                var inventory = hit.collider.GetComponentInParent<IInventory>();
                if (inventory != null && inventory != Inventory)
                {
                    _focusedInventory = inventory;
                    _ic.Prompt.Show();
                }
            }
        }

        private void OpenOtherInventory(IInventory otherInventory)
        {
            // Если уже открыт этот инвентарь, не открываем заново
            if (_openedInventory == otherInventory) return;
            _openedInventory = otherInventory;

            // Открываем панели: одна — только инвентарь игрока, вторая — только инвентарь объекта!
            if (_playerInventoryPanel != null)
            {
                _playerInventoryPanel.SetInventory(Inventory, _itemDatabase);
                _playerInventoryPanel.gameObject.SetActive(true);
            }
            if (_otherInventoryPanel != null)
            {
                _otherInventoryPanel.SetInventory(otherInventory, _itemDatabase);
                _otherInventoryPanel.gameObject.SetActive(true);
            }else
            {
                Debug.LogWarning("Other inventory panel is not set!");
            }
        }

        public void CloseOpenedInventories()
        {
            _openedInventory = null;
            if (_playerInventoryPanel != null)
                _playerInventoryPanel.gameObject.SetActive(false);
            if (_otherInventoryPanel != null)
                _otherInventoryPanel.gameObject.SetActive(false);
        }

        public void TogglePlayerInventory()
        {
            // Если открыт чужой инвентарь — просто закрой всё!
            if (_otherInventoryPanel != null && _otherInventoryPanel.gameObject.activeSelf)
            {
                CloseOpenedInventories();
                return;
            }

            // Тоглим только своё окно
            if (_playerInventoryPanel != null)
            {
                bool isActive = _playerInventoryPanel.gameObject.activeSelf;
                if (!isActive)
                    _playerInventoryPanel.SetInventory(Inventory, _itemDatabase);

                _playerInventoryPanel.gameObject.SetActive(!isActive);
            }
        }
    }
}
