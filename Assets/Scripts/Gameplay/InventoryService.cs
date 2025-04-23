using System;

namespace Game
{
    public class InventoryService
    {
        public event Action OnQuickSlotsChanged;
        private readonly InventorySlot[] _quickSlots;
        private readonly ItemDatabaseSO _db;

        public InventoryService(ItemDatabaseSO db)
        {
            _db = db;
            _quickSlots = new InventorySlot[10];
            for (int i = 0; i < _quickSlots.Length; i++)
                _quickSlots[i] = new InventorySlot();
        }

        public bool AddToQuickSlot(string itemId)
        {
            var item = _db.Get(itemId);
            return AddToQuickSlot(item);
        }

        public bool AddToQuickSlot(ItemSO item)
        {
            if (item == null) return false;

            // попытка дополнить стак
            for (int i = 0; i < _quickSlots.Length; i++)
            {
                var slot = _quickSlots[i];
                if (slot.Item == item && item.MaxStack > 1)
                {
                    slot.Count++;
                    OnQuickSlotsChanged?.Invoke();
                    return true;
                }
            }
            // или положить в пустой
            for (int i = 0; i < _quickSlots.Length; i++)
            {
                var slot = _quickSlots[i];
                if (slot.Item == null)
                {
                    slot.Item = item;
                    slot.Count = 1;
                    OnQuickSlotsChanged?.Invoke();
                    return true;
                }
            }
            return false;
        }

        public InventorySlot[] GetQuickSlots() => _quickSlots;
    }

    public class InventorySlot
    {
        public ItemSO Item;
        public int Count;
    }
}
