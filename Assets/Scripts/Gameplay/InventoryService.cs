using System;
using UnityEngine;

namespace Game
{
    public class InventoryService
    {
        public event Action OnQuickSlotsChanged;
        public event Action OnInventoryChanged;
        public event Action<int> OnQuickSlotSelectionChanged;

        public int SelectedQuickSlot { get; private set; } = -1;

        private readonly InventorySlot[] _quickSlots;
        private readonly InventorySlot[] _inventorySlots;
        private readonly ItemDatabaseSO _db;

        public InventoryService(ItemDatabaseSO db)
        {
            _db = db;
            _quickSlots = new InventorySlot[10];
            _inventorySlots = new InventorySlot[30];
            for (int i = 0; i < _quickSlots.Length; i++)
                _quickSlots[i] = new InventorySlot();
            for (int i = 0; i < _inventorySlots.Length; i++)
                _inventorySlots[i] = new InventorySlot();
        }

        public InventorySlot[] GetQuickSlots() => _quickSlots;
        public InventorySlot[] GetInventorySlots() => _inventorySlots;

        public void SelectQuickSlot(int idx)
        {
            if (idx < 0 || idx >= _quickSlots.Length) return;
            SelectedQuickSlot = (SelectedQuickSlot == idx) ? -1 : idx;
            OnQuickSlotSelectionChanged?.Invoke(SelectedQuickSlot);
        }

        public int HandlePick(string id, int count)
        {
            var item = _db.Get(id);
            if (item == null) return count;

            int rem = count;

            if (item.priority == 1)
                rem = TryQuick(item, rem);
            else
                rem = TryInventory(item, rem);

            if (rem > 0)
            {
                if (item.priority == 1)
                    rem = TryInventory(item, rem);
                else
                    rem = TryQuick(item, rem);
            }

            return rem;
        }

        private int TryQuick(ItemSO item, int rem)
        {
            if (item.MaxStack > 1)
            {
                foreach (var slot in _quickSlots)
                {
                    if (rem == 0) break;
                    if (slot.Id == item.Id && slot.Count < item.MaxStack)
                    {
                        int can = Mathf.Min(rem, item.MaxStack - slot.Count);
                        slot.Count += can;
                        rem -= can;
                        OnQuickSlotsChanged?.Invoke();
                    }
                }
            }
            foreach (var slot in _quickSlots)
            {
                if (rem == 0) break;
                if (slot.Id == null)
                {
                    slot.Id = item.Id;
                    int toPut = Mathf.Min(rem, item.MaxStack);
                    slot.Count = toPut;
                    rem -= toPut;
                    OnQuickSlotsChanged?.Invoke();
                }
            }
            return rem;
        }

        private int TryInventory(ItemSO item, int rem)
        {
            if (item.MaxStack > 1)
            {
                foreach (var slot in _inventorySlots)
                {
                    if (rem == 0) break;
                    if (slot.Id == item.Id && slot.Count < item.MaxStack)
                    {
                        int can = Mathf.Min(rem, item.MaxStack - slot.Count);
                        slot.Count += can;
                        rem -= can;
                        OnInventoryChanged?.Invoke();
                    }
                }
            }
            foreach (var slot in _inventorySlots)
            {
                if (rem == 0) break;
                if (slot.Id == null)
                {
                    slot.Id = item.Id;
                    int toPut = Mathf.Min(rem, item.MaxStack);
                    slot.Count = toPut;
                    rem -= toPut;
                    OnInventoryChanged?.Invoke();
                }
            }
            return rem;
        }
    }
}
