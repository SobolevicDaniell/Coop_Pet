using UnityEngine;
using Zenject;
using System;

namespace Game.UI
{
    public sealed class QuickSlotPanel : MonoBehaviour, IInventoryPanelUI
    {
        [SerializeField] private Transform _slotsParent;

        public PanelKind Kind => PanelKind.Quick;
        private Game.InventoryService _inv;
        private ItemDatabaseSO _db;
        private InventorySlotUI _slotPrefab;
        private InventorySlotUI[] _slots;

        public event Action<InventorySlotUI> OnSlotBeginDrag;
        public event Action<InventorySlotUI> OnSlotEndDrag;
        public event Action<InventorySlotUI> OnSlotEnter;
        public event Action<InventorySlotUI> OnSlotExit;

        private bool _initialized;
        private InventorySlotUI _draggingSlot;
        private InventorySlotUI _targetSlot;

        [Inject]
        public void Construct(ItemDatabaseSO db, [Inject(Id = "InventorySlotPrefab")] InventorySlotUI slotPrefab)
        {
            _db = db;
            _slotPrefab = slotPrefab;
        }

        public void InitializeIfLocal(Game.InteractionController controller)
        {
            if (controller == null || !controller.Object.HasInputAuthority)
            {
                gameObject.SetActive(false);
                return;
            }

            if (_initialized) return;

            _inv = controller.inventory;
            if (_inv == null || _db == null || _slotPrefab == null || _slotsParent == null)
            {
                Debug.LogError("[QuickSlotPanel] Not initialized: missing dependencies.");
                gameObject.SetActive(false);
                return;
            }

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
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = Instantiate(_slotPrefab, _slotsParent);
                slot.Init(i, _inv, this);
                slot.SetActive(false);

                slot.OnBeginDrag += HandleBeginDrag;
                slot.OnEndDrag += HandleEndDrag;
                slot.OnEnter += HandleSlotEnter;
                slot.OnExit += HandleSlotExit;

                _slots[i] = slot;
            }
        }

        private void HandleBeginDrag(InventorySlotUI slot) => _draggingSlot = slot;

        private void HandleEndDrag(InventorySlotUI slot)
        {
            if (_draggingSlot != null && _targetSlot != null && _inv != null)
            {
                int from = _draggingSlot.SlotIndex;
                int to = _targetSlot.SlotIndex;
                _inv.MoveQuickSlot(from, to);
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
            if (!_initialized || _inv == null || _db == null || _slots == null) return;

            var slots = _inv.GetQuickSlots();
            int selected = _inv.SelectedQuickSlot;

            for (int i = 0; i < _slots.Length; i++)
            {
                var ui = _slots[i];
                if (ui == null) continue;

                ui.Init(i, _inv, this);

                ItemSO item = null;
                int count = 0;

                if (slots != null && i < slots.Length)
                {
                    var backend = slots[i];
                    if (backend != null && !string.IsNullOrEmpty(backend.Id))
                    {
                        item = _db.Get(backend.Id);
                        count = backend.Count;
                    }
                }

                ui.Set(item, count);
                ui.SetActive(i == selected);
            }
        }

        private void OnQuickSlotChanged(int selected)
        {
            if (_slots == null) return;
            for (int i = 0; i < _slots.Length; i++)
            {
                var ui = _slots[i];
                if (ui != null) ui.SetActive(i == selected);
            }
        }

        public void ClearInventory()
        {
            if (_inv != null)
            {
                _inv.OnQuickSlotsChanged -= Refresh;
                _inv.OnQuickSlotSelectionChanged -= OnQuickSlotChanged;
            }
            _inv = null;

            if (_slots == null) return;

            foreach (var slotUI in _slots)
                slotUI.Set(null, 0);
        }

        private void OnDestroy()
        {
            if (_inv != null)
            {
                _inv.OnQuickSlotsChanged -= Refresh;
                _inv.OnQuickSlotSelectionChanged -= OnQuickSlotChanged;
            }

            if (_slots != null)
            {
                foreach (var s in _slots)
                {
                    if (s == null) continue;
                    s.OnBeginDrag -= HandleBeginDrag;
                    s.OnEndDrag -= HandleEndDrag;
                    s.OnEnter -= HandleSlotEnter;
                    s.OnExit  -= HandleSlotExit;
                }
            }
        }
    }
}
