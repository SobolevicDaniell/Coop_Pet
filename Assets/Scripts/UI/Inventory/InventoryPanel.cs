using UnityEngine;
using Zenject;
using System;

namespace Game.UI
{
    public class InventoryPanel : MonoBehaviour, IInventoryPanelUI
    {
        [SerializeField] private Transform _slotsParent;

        private IInventory _inventory;
        private ItemDatabaseSO _database;
        private InventorySlotUI _slotPrefab;
        private InventorySlotUI[] _slotsUI;

        public PanelKind Kind => PanelKind.Player;

        public event Action<InventorySlotUI> OnSlotBeginDrag;
        public event Action<InventorySlotUI> OnSlotEndDrag;
        public event Action<InventorySlotUI> OnSlotEnter;
        public event Action<InventorySlotUI> OnSlotExit; 

        [Inject]
        public void Construct(ItemDatabaseSO database, [Inject(Id = "InventorySlotPrefab")] InventorySlotUI slotPrefab, PlayerStatsSO playerStats)
        {
            _database = database;
            _slotPrefab = slotPrefab;

            int count = playerStats != null ? playerStats.inventorySlotsCount : 30;
            CreateSlots(count);
        }

        public void Construct(IInventory inventory)
        {
            if (_inventory != null)
                _inventory.OnInventoryChanged -= Refresh;

            _inventory = inventory;

            if (_slotsUI != null)
            {
                for (int i = 0; i < _slotsUI.Length; i++)
                    _slotsUI[i]?.Init(i, _inventory, this);
            }

            if (_inventory != null)
                _inventory.OnInventoryChanged += Refresh;

            Refresh();
        }

        public void RefreshPanel() => Refresh();

        private void CreateSlots(int count)
        {
            if (_slotsUI != null && _slotsUI.Length > 0)
            {
                foreach (var s in _slotsUI)
                    if (s != null) Destroy(s.gameObject);
            }

            _slotsUI = new InventorySlotUI[count];

            for (int i = 0; i < count; i++)
            {
                var slot = Instantiate(_slotPrefab, _slotsParent);
                slot.Init(i, _inventory, this);

                slot.OnBeginDrag += HandleBeginDrag;
                slot.OnEndDrag += HandleEndDrag;
                slot.OnEnter += HandleEnter;
                slot.OnExit += HandleExit;

                _slotsUI[i] = slot;
            }
        }

        private void HandleBeginDrag(InventorySlotUI slot) => OnSlotBeginDrag?.Invoke(slot);
        private void HandleEndDrag(InventorySlotUI slot)   => OnSlotEndDrag?.Invoke(slot);
        private void HandleEnter(InventorySlotUI slot)     => OnSlotEnter?.Invoke(slot);
        private void HandleExit(InventorySlotUI slot)      => OnSlotExit?.Invoke(slot);

        private void Refresh()
        {
            if (_slotsUI == null || _database == null)
                return;

            var slots = _inventory != null ? _inventory.GetInventorySlots() : null;

            for (int i = 0; i < _slotsUI.Length; i++)
            {
                var ui = _slotsUI[i];
                if (ui == null) continue;

                ui.Init(i, _inventory, this);

                ItemSO item = null;
                int count = 0;

                if (slots != null && i < slots.Length)
                {
                    var backend = slots[i];
                    if (backend != null && !string.IsNullOrEmpty(backend.Id))
                    {
                        item = _database.Get(backend.Id);
                        count = backend.Count;
                    }
                }

                ui.Set(item, count);
            }
        }

        public void ClearInventory()
        {
            if (_inventory != null)
                _inventory.OnInventoryChanged -= Refresh;

            _inventory = null;

            if (_slotsUI == null) return;

            foreach (var slotUI in _slotsUI)
                slotUI.Set(null, 0);
        }

        private void OnDestroy()
        {
            if (_inventory != null)
                _inventory.OnInventoryChanged -= Refresh;

            if (_slotsUI != null)
            {
                foreach (var slot in _slotsUI)
                {
                    if (slot == null) continue;
                    slot.OnBeginDrag -= HandleBeginDrag;
                    slot.OnEndDrag -= HandleEndDrag;
                    slot.OnEnter -= HandleEnter;
                    slot.OnExit  -= HandleExit;
                }
            }
        }
    }
}
