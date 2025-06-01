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

        [Inject]
        public void Construct(InventoryService inv, ItemDatabaseSO db, [Inject(Id = "InventorySlotPrefab")] InventorySlotUI slotPrefab)
        {
            _inv = inv;
            _db = db;
            _slotPrefab = slotPrefab;
            CreateSlots();
            _inv.OnQuickSlotsChanged += Refresh;
            Refresh();

            _inv.OnQuickSlotsChanged += Refresh;
            _inv.OnQuickSlotSelectionChanged += OnQuickSlotChanged; // подписка

        }
        private void OnQuickSlotChanged(int index)
        {
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
            slot.OnBeginDrag += slotUI => OnSlotBeginDrag?.Invoke(slotUI);
            slot.OnEndDrag += slotUI => OnSlotEndDrag?.Invoke(slotUI);
            slot.OnEnter += slotUI => OnSlotEnter?.Invoke(slotUI);
            slot.OnExit += slotUI => OnSlotExit?.Invoke(slotUI);
        }

        public void RefreshPanel() => Refresh();

        private void Refresh()
        {
            var slots = _inv.GetQuickSlots();
            int selected = _inv.SelectedQuickSlot;

            for (int i = 0; i < _slots.Length; i++)
            {
                var s = slots[i];
                var item = s.Id != null ? _db.Get(s.Id) : null;
                _slots[i].Set(item, item is WeaponSO ? s.State?.Ammo ?? 0 : s.Count);
                _slots[i].SetActive(i == selected); // активация только выбранного слота
            }
        }

    }
}