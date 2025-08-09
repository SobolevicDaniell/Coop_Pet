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
        private UIController _uiController;
        private InteractionController _ic;
        private PlayerRpcHandler _rpc;
        private PickableItem _focusedItem;
        private ChestInventory _focusedChestInventory;
        private ChestInventory _openedChestInventory;

        // [SerializeField] private Camera _camera;

        private Camera _camera;

        public void Initialize(
            InteractionController controller,
            InventoryService inventory,
            InventoryPanel playerPanel,
            OtherInventoryPanel otherPanel,
            ItemDatabaseSO itemDatabase,
            UIController uiController,
            Camera camera)
        {
            _ic = controller;
            _rpc = controller.playerRpcHandler;
            Inventory = inventory;
            _playerInventoryPanel = playerPanel;
            _otherInventoryPanel = otherPanel;
            _itemDatabase = itemDatabase;
            _uiController = uiController;
            _camera = camera;
        }

        public void TryPick()
        {
            if (!_ic.Object.HasInputAuthority) return;

            if (_focusedItem != null)
                _rpc.RPC_RequestPick(_focusedItem.Object);
            else if (_focusedChestInventory != null)
                OpenChestInventory(_focusedChestInventory);
        }

        public void TryDrop()
        {
            Debug.Log("TryDrop called");

            if (!_ic.Object.HasInputAuthority)
            {
                Debug.LogWarning("No InputAuthority!");
                return;
            }

            int selected = Inventory.SelectedQuickSlot; // используем локальное значение
            if (selected < 0)
            {
                Debug.LogWarning("No slot selected!");
                return;
            }

            var slots = Inventory.GetQuickSlots();
            if (slots == null || selected >= slots.Length)
            {
                Debug.LogWarning("Invalid slots array or index!");
                return;
            }

            var slot = slots[selected];
            if (string.IsNullOrEmpty(slot.Id) || slot.Count <= 0)
            {
                Debug.LogWarning("Selected slot is empty!");
                return;
            }

            Debug.Log($"Dropping item {slot.Id}, count {slot.Count}");

            _rpc.RPC_RequestDrop(
                _ic.dropPoint.position,
                _camera.transform.forward,
                slot.Id,
                slot.Count,
                slot.State?.Ammo ?? 0);

            slot.Id = null;
            slot.Count = 0;
            slot.State = null;

            Inventory.RaiseQuickSlotsChanged();

            Inventory.ForceSetQuickSlot(-1);
        }

        public void UpdateRaycast()
        {
            if (_ic == null || !_ic.Object.HasInputAuthority)
                return;

            if (_camera == null || _ic.prompt == null)
                return;

            var ray = _camera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f));
            float range = _ic._rangePlace;

            Debug.DrawRay(ray.origin, ray.direction * range, Color.green);

            _focusedItem = null;
            _focusedChestInventory = null;

            _ic.prompt.Hide();

            if (Physics.Raycast(ray, out var hit, range))
            {
                var pickable = hit.collider.GetComponentInParent<PickableItem>();
                if (pickable != null)
                {
                    _focusedItem = pickable;
                    _ic.prompt.Show();
                    return;
                }

                var chestInventory = hit.collider.GetComponentInParent<ChestInventory>();
                if (chestInventory != null)
                {
                    _focusedChestInventory = chestInventory;
                    _ic.prompt.Show();
                    return;
                }
            }
        }


        public void OpenPlayerInventory()
        {
            _playerInventoryPanel.SetInventory(Inventory, _itemDatabase);
        }

        public void OpenChestInventory(ChestInventory chestInventory)
        {
            if (!_ic.Object.HasInputAuthority) return;

            if (_openedChestInventory == chestInventory) return;

            _openedChestInventory = chestInventory;

            _otherInventoryPanel.SetInventory(chestInventory, _itemDatabase);
            _playerInventoryPanel.SetInventory(Inventory, _itemDatabase);

            _uiController.ShowInventory(true);
        }

        public void CloseOpenedInventories()
        {
            if (!_ic.Object.HasInputAuthority) return;

            _openedChestInventory = null;
            _otherInventoryPanel.ClearInventory();
            _playerInventoryPanel.ClearInventory();

            _uiController.ShowGameUI();
        }

        public void DropFromSlot(InventorySlotUI slotUI)
        {
            if (!_ic.Object.HasInputAuthority) return;

            // 1) достаём ссылку на реальный слот
            var parentInv = slotUI.ParentInventory;
            InventorySlot slot = null;

            if (parentInv is InventoryService svc)
            {
                slot = slotUI.ParentPanel is QuickSlotPanel
                    ? svc.GetQuickSlots()[slotUI.SlotIndex]
                    : svc.GetInventorySlots()[slotUI.SlotIndex];
            }
            else
            {
                slot = parentInv.GetInventorySlots()[slotUI.SlotIndex];
            }

            if (slot == null || string.IsNullOrEmpty(slot.Id) || slot.Count <= 0)
                return;

            // 2) выбираем камеру и считаем позицию/направление
            var cam = _camera != null ? _camera : Camera.main;      // <<< НИ КАКОГО GetComponent<Camera>() на игроке
            Vector3 forward = cam != null ? cam.transform.forward : _ic.transform.forward;
            Vector3 pos = _ic.dropPoint != null ? _ic.dropPoint.position : _ic.transform.position + forward * 1.0f;

            // рейкаст под курсор для более точного дропа
            if (cam != null)
            {
                var ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, _ic._rangePlace))
                {
                    pos = hit.point;
                    forward = cam.transform.forward;
                }
            }

            // 3) сетевой дроп (как по Q)
            _rpc.RPC_RequestDrop(
                pos,
                forward,
                slot.Id,
                slot.Count,
                slot.State?.Ammo ?? 0
            );

            // 4) очистка слота и события (иначе «в инвентаре не удаляется»)
            slot.Id = null;
            slot.Count = 0;
            slot.State = null;

            parentInv.RaiseInventoryChanged();
            if (parentInv is InventoryService isvc)
                isvc.RaiseQuickSlotsChanged();
        }

        private int GetAbsIndex(InventorySlotUI slotUI)
        {
            if (slotUI.ParentInventory is InventoryService)
            {
                return slotUI.ParentPanel is QuickSlotPanel
                    ? slotUI.SlotIndex
                    : 10 + slotUI.SlotIndex;
            }

            return slotUI.SlotIndex;
        }
    }
}