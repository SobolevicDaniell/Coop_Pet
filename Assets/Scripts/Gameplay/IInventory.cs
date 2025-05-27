using System;

namespace Game
{
    public interface IInventory
    {
        InventorySlot[] GetInventorySlots();
        event Action OnInventoryChanged;
    }
}
