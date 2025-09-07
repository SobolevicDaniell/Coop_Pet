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
        [Inject(Optional = true)] private InventoryClientFacade _facade;


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

            _inv.ForceSetQuickSlot(-1);

            CreateSlots();

            _inv.OnQuickSlotsChanged += Refresh;
            _inv.OnQuickSlotSelectionChanged += OnQuickSlotChanged;

            Refresh();
            OnQuickSlotChanged(_inv.SelectedQuickSlot);
        }

        private void CreateSlots()
        {
            int cap = Mathf.Max(1, _inv?.GetQuickSlots()?.Length ?? 10);
            _slots = new InventorySlotUI[cap];

            for (int i = 0; i < cap; i++)
            {
                var slot = Instantiate(_slotPrefab, _slotsParent);
                slot.gameObject.SetActive(true); 
                slot.Init(i, _inv, this);
                slot.SetActive(false);

                slot.OnBeginDrag += HandleBeginDrag;
                slot.OnEndDrag   += HandleEndDrag;
                slot.OnEnter     += HandleSlotEnter;
                slot.OnExit      += HandleSlotExit;

                _slots[i] = slot;
            }
        }

        private void HandleBeginDrag(InventorySlotUI slot)
        {
            _draggingSlot = slot;
            OnSlotBeginDrag?.Invoke(slot);
        }

        private void HandleEndDrag(InventorySlotUI slot)
        {
            OnSlotEndDrag?.Invoke(slot);

            _draggingSlot = null;
            _targetSlot = null;
        }

        private void HandleSlotEnter(InventorySlotUI slot)
        {
            _targetSlot = slot;
            OnSlotEnter?.Invoke(slot);
        }

        private void HandleSlotExit(InventorySlotUI slot)
        {
            if (_targetSlot == slot) _targetSlot = null;
            OnSlotExit?.Invoke(slot);
        }

        public void RefreshPanel() => Refresh();

        private void Refresh()
        {
            if (!_initialized || _inv == null || _db == null || _slots == null) return;

            var data = _inv.GetQuickSlots();
            int selected = _inv.SelectedQuickSlot;

            for (int i = 0; i < _slots.Length; i++)
            {
                var ui = _slots[i];
                if (ui == null) continue;

                ItemSO item = null; int count = 0; Game.ItemState state = null;
                if (data != null && i < data.Length)
                {
                    var backend = data[i];
                    if (backend != null && !string.IsNullOrEmpty(backend.Id))
                    {
                        item = _db.Get(backend.Id);
                        count = backend.Count;
                        state = backend.State;
                    }
                }

                ui.Set(item, count, state);
                ui.SetActive(selected >= 0 && i == selected);
            }
        }

        private void OnQuickSlotChanged(int selected)
        {
            if (_slots == null) return;
            for (int i = 0; i < _slots.Length; i++)
                _slots[i]?.SetActive(selected >= 0 && i == selected);
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
                slotUI.Set(null, 0, null);

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
                    s.OnEndDrag   -= HandleEndDrag;
                    s.OnEnter     -= HandleSlotEnter;
                    s.OnExit      -= HandleSlotExit;
                }
            }
        }
    }
}
