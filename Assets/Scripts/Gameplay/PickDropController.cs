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
        private UIController _uiController;

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
            InputHandler input,
            UIController uiController)
        {
            _ic = controller;
            _rpc = controller.RpcHandler;
            Inventory = inventory;
            _playerInventoryPanel = playerPanel;
            _otherInventoryPanel = otherPanel;
            _itemDatabase = itemDatabase;
            _inputHandler = input;
            _uiController = uiController;

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
                var pickable = hit.collider.GetComponentInParent<PickableItem>();
                if (pickable != null)
                {
                    _focusedItem = pickable;
                    _ic.Prompt.Show();
                    return;
                }

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
            if (_openedInventory == otherInventory) return;
            _openedInventory = otherInventory;

            _playerInventoryPanel.SetInventory(Inventory, _itemDatabase);
            _otherInventoryPanel.SetInventory(otherInventory, _itemDatabase);

            _uiController.ShowInventory(true); // Через UIController!
        }

        public void CloseOpenedInventories()
        {
            _openedInventory = null;
            _uiController.ShowGameUI();
        }

        public void TogglePlayerInventory()
        {
            // Если открыт чужой инвентарь — просто закрой всё!
            if (_otherInventoryPanel != null && _otherInventoryPanel.gameObject.activeSelf)
            {
                CloseOpenedInventories();
                return;
            }

            bool isActive = _playerInventoryPanel.gameObject.activeSelf;
            if (!isActive)
            {
                _playerInventoryPanel.SetInventory(Inventory, _itemDatabase);
                _uiController.ShowInventory(false);
            }
            else
            {
                _uiController.ShowGameUI();
            }
        }
    }
}
