using System;

namespace Game
{
    public class InventoryService
    {
        public event Action OnQuickSlotsChanged;

        private readonly InventorySlot[] quickSlots;

        public InventoryService()
        {
            quickSlots = new InventorySlot[10];
            for (int i = 0; i < quickSlots.Length; i++)
                quickSlots[i] = new InventorySlot();
        }

        public bool AddToQuickSlot(ItemSO item)
        {
            // сначала пытаемся добавить в уже существующий стак
            for (int i = 0; i < quickSlots.Length; i++)
            {
                var slot = quickSlots[i];
                if (slot.Item == item && item.MaxStack > 1)
                {
                    slot.Count++;
                    OnQuickSlotsChanged?.Invoke();
                    return true;
                }
            }
            // иначе ищем пустой
            for (int i = 0; i < quickSlots.Length; i++)
            {
                var slot = quickSlots[i];
                if (slot.Item == null)
                {
                    slot.Item = item;
                    slot.Count = 1;
                    OnQuickSlotsChanged?.Invoke();
                    return true;
                }
            }
            return false; // нет места
        }

        public InventorySlot[] GetQuickSlots() => quickSlots;
    }

    public class InventorySlot
    {
        public ItemSO Item;
        public int Count;
    }
}