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

        public event Action<InventorySlotUI> OnSlotBeginDrag;
        public event Action<InventorySlotUI> OnSlotEndDrag;
        public event Action<InventorySlotUI> OnSlotEnter;
        public event Action<InventorySlotUI> OnSlotExit;

        [Inject]
        public void Construct(InventoryService inventory, ItemDatabaseSO database, [Inject(Id = "InventorySlotPrefab")] InventorySlotUI slotPrefab, PlayerStatsSO playerStats)
        {
            _slotPrefab = slotPrefab;
            _inventory = inventory;
            _database = database;

            CreateSlots(playerStats.inventorySlotsCount);
            _inventory.OnInventoryChanged += Refresh;
            Refresh();
        }

        private void CreateSlots(int count)
        {
            _slotsUI = new InventorySlotUI[count];
            for (int i = 0; i < count; i++)
            {
                var slot = Instantiate(_slotPrefab, _slotsParent);
                slot.Init(i, _inventory, this);
                slot.SetActive(false);
                SubscribeSlot(slot);
                _slotsUI[i] = slot;
            }
        }

        private void SubscribeSlot(InventorySlotUI slot)
        {
            slot.OnBeginDrag += slotUI => OnSlotBeginDrag?.Invoke(slotUI);
            slot.OnEndDrag += slotUI => OnSlotEndDrag?.Invoke(slotUI);
            slot.OnEnter += slotUI => OnSlotEnter?.Invoke(slotUI);
            slot.OnExit += slotUI => OnSlotExit?.Invoke(slotUI);
        }

        public void RefreshPanel() => Refresh();

        private void Refresh()
        {
            var slots = _inventory.GetInventorySlots();
            for (int i = 0; i < _slotsUI.Length; i++)
            {
                var slotUI = _slotsUI[i];
                var slotData = slots[i];
                var item = slotData.Id != null ? _database.Get(slotData.Id) : null;
                slotUI.Set(item, item is WeaponSO ? slotData.State?.Ammo ?? 0 : slotData.Count);
            }
        }

        public void SetInventory(IInventory inventory, ItemDatabaseSO database)
        {
            if (_inventory != null)
                _inventory.OnInventoryChanged -= Refresh;

            _inventory = inventory;
            _database = database;
            _inventory.OnInventoryChanged += Refresh;

            Refresh();
        }

        public void ClearInventory()
        {
            if (_inventory != null)
                _inventory.OnInventoryChanged -= Refresh;

            _inventory = null;

            foreach (var slotUI in _slotsUI)
                slotUI.Set(null, 0);
        }
    }
}
