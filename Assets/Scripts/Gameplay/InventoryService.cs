// Assets/Scripts/Game/InventoryService.cs
using System;
using UnityEngine;

namespace Game
{
    public class InventoryService
    {
        public event Action OnQuickSlotsChanged;
        private readonly InventorySlot[] quickSlots;
        private readonly ItemDatabaseSO db;

        // через Zenject передаём базу предметов
        public InventoryService(ItemDatabaseSO db)
        {
            this.db = db;
            quickSlots = new InventorySlot[10];
            for (int i = 0; i < quickSlots.Length; i++)
                quickSlots[i] = new InventorySlot();
        }

        // добавление существующего ScriptableObject
        public bool AddToQuickSlot(ItemSO item)
        {
            if (item == null) return false;

            // пробуем докинуть в стак
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
            return false; // места нет
        }

        // добавление по сетевому идентификатору
        public bool AddToQuickSlot(string itemId)
        {
            var item = db.Get(itemId);
            return AddToQuickSlot(item);
        }

        public InventorySlot[] GetQuickSlots() => quickSlots;
    }

    public class InventorySlot
    {
        public ItemSO Item;
        public int Count;
    }
}
