using UnityEngine;
using Zenject;
using System;

namespace Game.UI
{
    public class QuickSlotPanel : MonoBehaviour, IInventoryPanelUI
    {
        [SerializeField] private Transform _slotsParent;

        private InventoryService _inv;
        private ItemDatabaseSO _db;
        private InventorySlotUI _slotPrefab;
        private InventorySlotUI[] _slots;

        public event Action<InventorySlotUI> OnSlotBeginDrag;
        public event Action<InventorySlotUI> OnSlotEndDrag;
        public event Action<InventorySlotUI> OnSlotEnter;
        public event Action<InventorySlotUI> OnSlotExit;

        private bool _initialized = false;

        private InventorySlotUI _draggingSlot;
        private InventorySlotUI _targetSlot;

        [Inject]
        public void Construct(
            InventoryService inv,
            ItemDatabaseSO db,
            [Inject(Id = "InventorySlotPrefab")] InventorySlotUI slotPrefab)
        {
            _inv = inv;
            _db = db;
            _slotPrefab = slotPrefab;
        }

        public void InitializeIfLocal(InteractionController controller)
        {
            if (controller == null || !controller.Object.HasInputAuthority)
            {
                gameObject.SetActive(false);
                return;
            }

            if (_initialized)
                return;

            _initialized = true;

            CreateSlots();

            _inv.OnQuickSlotsChanged -= Refresh;
            _inv.OnQuickSlotSelectionChanged -= OnQuickSlotChanged;
            _inv.OnQuickSlotsChanged += Refresh;
            _inv.OnQuickSlotSelectionChanged += OnQuickSlotChanged;

            Refresh();
        }

        private void CreateSlots()
        {
            _slots = new InventorySlotUI[10];
            for (int i = 0; i < 10; i++)
            {
                var slot = Instantiate(_slotPrefab, _slotsParent);
                slot.Init(i, _inv, this);
                slot.SetActive(false);
                SubscribeSlot(slot);
                _slots[i] = slot;
            }
        }

        private void SubscribeSlot(InventorySlotUI slot)
        {
            slot.OnBeginDrag += HandleSlotBeginDrag;
            slot.OnEndDrag += HandleSlotEndDrag;
            slot.OnEnter += HandleSlotEnter;
            slot.OnExit += HandleSlotExit;

            // Сохраняем старую логику (прокидываем события дальше)
            slot.OnBeginDrag += slotUI => OnSlotBeginDrag?.Invoke(slotUI);
            slot.OnEndDrag += slotUI => OnSlotEndDrag?.Invoke(slotUI);
            slot.OnEnter += slotUI => OnSlotEnter?.Invoke(slotUI);
            slot.OnExit += slotUI => OnSlotExit?.Invoke(slotUI);
        }

        private void HandleSlotBeginDrag(InventorySlotUI slot)
        {
            _draggingSlot = slot;
        }

        private void HandleSlotEndDrag(InventorySlotUI slot)
        {
            if (_draggingSlot != null && _targetSlot != null && _draggingSlot != _targetSlot)
            {
                int from = _draggingSlot.SlotIndex;
                int to = _targetSlot.SlotIndex;

                // if (_inv.MoveQuickSlot(from, to))
                    // Debug.Log($"Moved quick slot from {from} to {to}");
            }

            _draggingSlot = null;
            _targetSlot = null;
        }

        private void HandleSlotEnter(InventorySlotUI slot) => _targetSlot = slot;

        private void HandleSlotExit(InventorySlotUI slot)
        {
            if (_targetSlot == slot)
                _targetSlot = null;
        }

        public void RefreshPanel() => Refresh();

        private void Refresh()
        {
            if (!_initialized || _inv == null || _db == null || _slots == null)
                return;

            var slots = _inv.GetQuickSlots();
            int selected = _inv.SelectedQuickSlot;

            for (int i = 0; i < _slots.Length; i++)
            {
                var s = slots[i];
                var item = s.Id != null ? _db.Get(s.Id) : null;
                _slots[i].Set(item, item is WeaponSO ? s.State?.Ammo ?? 0 : s.Count);
                _slots[i].SetActive(i == selected);
            }
        }

        private void OnQuickSlotChanged(int index)
        {
            // Debug.Log($"[QuickSlotPanel] OnQuickSlotChanged: {index}");
            Refresh();
        }

        public void SetInventory(InventoryService inv, ItemDatabaseSO db)
        {
            if (_inv != null)
            {
                _inv.OnQuickSlotsChanged -= Refresh;
                _inv.OnQuickSlotSelectionChanged -= OnQuickSlotChanged;
            }

            _inv = inv;
            _db = db;

            if (_inv != null)
            {
                _inv.OnQuickSlotsChanged += Refresh;
                _inv.OnQuickSlotSelectionChanged += OnQuickSlotChanged;
            }

            Refresh();
        }

        public void ClearInventory()
        {
            if (_inv != null)
            {
                _inv.OnQuickSlotsChanged -= Refresh;
                _inv.OnQuickSlotSelectionChanged -= OnQuickSlotChanged;
            }
            _inv = null;

            if (_slots == null)
                return;

            foreach (var slotUI in _slots)
                slotUI.Set(null, 0);
        }
    }
}
