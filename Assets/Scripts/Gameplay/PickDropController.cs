using Fusion;
using UnityEngine;
using Game.UI;

namespace Game
{
    public class PickDropController : MonoBehaviour
    {
        public InventoryService Inventory { get; private set; }

        private InventoryPanel _playerInventoryPanel;
        private OtherInventoryPanel _otherInventoryPanel;
        private ItemDatabaseSO _itemDatabase;
        private InputHandler _inputHandler;
        private UIController _uiController;

        private InteractionController _ic;
        private ServerRpcHandler _rpc;
        private PickableItem _focusedItem;

        private ChestInventory _focusedChestInventory;
        private ChestInventory _openedChestInventory;

        public void Initialize(
            InteractionController controller,
            InventoryService inventory,
            InventoryPanel playerPanel,
            OtherInventoryPanel otherPanel,
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
                _rpc.RPC_RequestPick(_focusedItem.Object);
                return;
            }

            if (_focusedChestInventory != null)
            {
                OpenChestInventory(_focusedChestInventory);
            }
        }

        public void TryDrop()
        {
            var selected = _ic.NetSelectedQuickSlot;
            if (selected < 0) return;

            var slots = Inventory.GetQuickSlots();
            if (selected >= slots.Length) return;

            var slot = slots[selected];
            if (string.IsNullOrEmpty(slot.Id) || slot.Count <= 0) return;

            int ammo = slot.State?.Ammo ?? 0;

            _rpc.RPC_RequestDrop(
                _ic.DropPoint.position,
                _ic.Camera.transform.forward,
                slot.Id,
                slot.Count,
                ammo
            );

            slot.Id = null;
            slot.Count = 0;
            slot.State = null;

            Inventory.RaiseQuickSlotsChanged();

            if (_ic.NetSelectedQuickSlot == selected)
            {
                _rpc.RPC_SelectQuickSlot(-1);
                _rpc.RPC_RequestDespawnHandModel();
            }
        }

        public void UpdateRaycast()
        {
            var ray = _ic.Camera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f));
            var range = _ic.Range;

            Debug.DrawRay(ray.origin, ray.direction * range, Color.green);

            _focusedItem = null;
            _focusedChestInventory = null;
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

                var chestInventory = hit.collider.GetComponentInParent<ChestInventory>();
                if (chestInventory != null)
                {
                    _focusedChestInventory = chestInventory;
                    _ic.Prompt.Show();
                }
            }
        }

        private void OpenChestInventory(ChestInventory chestInventory)
        {
            if (_openedChestInventory == chestInventory) return;
            _openedChestInventory = chestInventory;

            _otherInventoryPanel.SetInventory(chestInventory, _itemDatabase);
            _playerInventoryPanel.SetInventory(Inventory, _itemDatabase);

            _uiController.ShowInventory(true);
        }

        public void CloseOpenedInventories()
        {
            _openedChestInventory = null;

            _otherInventoryPanel.ClearInventory();
            _playerInventoryPanel.ClearInventory();

            _uiController.ShowGameUI();
        }

        public void TogglePlayerInventory()
        {
            if (_openedChestInventory != null)
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
                _playerInventoryPanel.ClearInventory();
                _uiController.ShowGameUI();
            }
        }

        // Внутри PickDropController

        public void DropFromSlot(Game.UI.InventorySlotUI slotUI)
        {
            var parentInv = slotUI.ParentInventory;
            int absIdx = GetAbsIndex(slotUI);

            // Получаем слот
            InventorySlot slot;
            if (parentInv is InventoryService invService)
            {
                if (slotUI.ParentPanel is QuickSlotPanel)
                    slot = invService.GetQuickSlots()[slotUI.SlotIndex];
                else
                    slot = invService.GetInventorySlots()[slotUI.SlotIndex];
            }
            else if (parentInv is ChestInventory chestInv)
            {
                slot = chestInv.GetInventorySlots()[slotUI.SlotIndex];
            }
            else
            {
                Debug.LogWarning("[PickDropController] Неизвестный тип инвентаря");
                return;
            }

            if (slot == null || string.IsNullOrEmpty(slot.Id) || slot.Count <= 0)
                return;

            int ammo = slot.State?.Ammo ?? 0;

            // Выбросить в мир
            _rpc.RPC_RequestDrop(
                _ic.DropPoint.position,
                _ic.Camera.transform.forward,
                slot.Id,
                slot.Count,
                ammo
            );

            // Очистить слот
            slot.Id = null;
            slot.Count = 0;
            slot.State = null;

            // Сообщить системе об изменении
            parentInv.RaiseInventoryChanged();
            if (parentInv is InventoryService inv) inv.RaiseQuickSlotsChanged();

            // Если quick-слот активен, сбросить hand-модель
            if (parentInv is InventoryService service && slotUI.ParentPanel is QuickSlotPanel)
            {
                if (_ic.NetSelectedQuickSlot == slotUI.SlotIndex)
                {
                    _rpc.RPC_SelectQuickSlot(-1);
                    _rpc.RPC_RequestDespawnHandModel();
                }
            }
        }


        // Для получения абсолютного индекса
        private int GetAbsIndex(Game.UI.InventorySlotUI slotUI)
        {
            if (slotUI.ParentInventory is InventoryService)
            {
                if (slotUI.ParentPanel is QuickSlotPanel)
                    return slotUI.SlotIndex;
                if (slotUI.ParentPanel is InventoryPanel)
                    return 10 + slotUI.SlotIndex;
                return slotUI.SlotIndex;
            }
            else
            {
                return slotUI.SlotIndex;
            }
        }

    }
}