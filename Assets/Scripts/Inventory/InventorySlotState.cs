using System;

namespace Game
{
    [Serializable]
    public class InventorySlotState
    {
        public string itemId;
        public int count;
        // Вложенное состояние предмета (пример: ammo, durability)
        public ItemState itemState;

        public bool IsEmpty => string.IsNullOrEmpty(itemId) || count <= 0;
        public InventorySlotState Clone() => new InventorySlotState { itemId = itemId, count = count, itemState = itemState?.Clone() };
    }
}
