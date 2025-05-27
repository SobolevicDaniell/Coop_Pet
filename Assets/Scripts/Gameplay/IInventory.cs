using System;

namespace Game
{
    public interface IInventory
    {
        InventorySlot[] GetInventorySlots();
        event Action OnInventoryChanged;

        bool TryAddItem(string itemId, int count);
        bool TryRemoveItem(int slotIndex, int count);

    }
}
