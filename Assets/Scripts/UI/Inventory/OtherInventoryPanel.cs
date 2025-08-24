using UnityEngine;
using Zenject;
using System;

namespace Game.UI
{
    public class OtherInventoryPanel : MonoBehaviour, IInventoryPanelUI
    {
        [SerializeField] private Transform _slotsParent;

        public PanelKind Kind => PanelKind.Chest;
        private IInventory _inventory;
        private ItemDatabaseSO _database;
        private InventorySlotUI _slotPrefab;
        private InventorySlotUI[] _slotsUI;

        public event Action<InventorySlotUI> OnSlotBeginDrag;
        public event Action<InventorySlotUI> OnSlotEndDrag;
        public event Action<InventorySlotUI> OnSlotEnter;
        public event Action<InventorySlotUI> OnSlotExit;

        [Inject]
        public void Construct(ItemDatabaseSO database, [Inject(Id = "InventorySlotPrefab")] InventorySlotUI slotPrefab)
        {
            _database = database;
            _slotPrefab = slotPrefab;
        }

        public void SetInventory(ChestInventory chestInventory, ItemDatabaseSO database)
        {
            if (_inventory != null)
                _inventory.OnInventoryChanged -= Refresh;

            _inventory = chestInventory;
            _database = database;

            CreateSlots(chestInventory.GetInventorySlots().Length);
            _inventory.OnInventoryChanged += Refresh;
            Refresh();
        }

        private void CreateSlots(int count)
        {
            foreach (Transform child in _slotsParent)
                Destroy(child.gameObject);

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

        public void RefreshPanel()
        {
            if (_inventory != null)
                Refresh();
        }


        private void Refresh()
        {
            if (_inventory == null || _slotsUI == null) return;

            var slots = _inventory.GetInventorySlots();
            for (int i = 0; i < _slotsUI.Length; i++)
            {
                var slotUI = _slotsUI[i];
                var slotData = slots[i];
                var item = slotData.Id != null ? _database.Get(slotData.Id) : null;
                slotUI.Set(item, slotData.Count);
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
    }
}