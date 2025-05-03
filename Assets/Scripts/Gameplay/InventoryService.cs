// Assets/Scripts/Inventory/InventoryService.cs
using System;
using UnityEngine;

namespace Game
{
    public class InventoryService
    {
        public event Action OnQuickSlotsChanged;
        public event Action OnInventoryChanged;

        private readonly InventorySlot[] _quickSlots;
        private readonly InventorySlot[] _inventorySlots;
        private readonly ItemDatabaseSO _db;

        public InventoryService(ItemDatabaseSO db)
        {
            _db = db;

            _quickSlots = new InventorySlot[10];
            for (int i = 0; i < _quickSlots.Length; i++)
                _quickSlots[i] = new InventorySlot();

            _inventorySlots = new InventorySlot[30]; // 6×5
            for (int i = 0; i < _inventorySlots.Length; i++)
                _inventorySlots[i] = new InventorySlot();
        }

        public InventorySlot[] GetQuickSlots() => _quickSlots;
        public InventorySlot[] GetInventorySlots() => _inventorySlots;

        /// <summary>
        /// Пытаемся положить count штук itemId. Возвращаем остаток.
        /// </summary>
        public int HandlePick(string itemId, int count)
        {
            var item = _db.Get(itemId);
            if (item == null) return count;

            int remaining = count;

            // Первый проход — по приоритету
            bool placed;
            if (item.priority == 1)
                placed = TryAddToQuickSlot(item, remaining);
            else
                placed = TryAddToInventory(item, remaining);

            if (placed)
                remaining = 0;
            else
            {
                // Второй проход — в другую "очередь"
                if (item.priority == 1)
                    placed = TryAddToInventory(item, remaining);
                else
                    placed = TryAddToQuickSlot(item, remaining);

                if (placed)
                    remaining = 0;
            }

            //if (remaining > 0)
            //    Debug.LogWarning($"[Inventory] No space for {itemId}×{remaining}");

            return remaining;
        }

        private bool TryAddToQuickSlot(ItemSO item, int count)
        {
            int remaining = count;

            // добиваем в стеки
            if (item.MaxStack > 1)
            {
                foreach (var slot in _quickSlots)
                {
                    if (remaining == 0) break;
                    if (slot.Item == item && slot.Count < item.MaxStack)
                    {
                        int can = Mathf.Min(remaining, item.MaxStack - slot.Count);
                        slot.Count += can;
                        remaining -= can;
                        OnQuickSlotsChanged?.Invoke();
                    }
                }
            }

            // новые слоты
            foreach (var slot in _quickSlots)
            {
                if (remaining == 0) break;
                if (slot.Item == null)
                {
                    slot.Item = item;
                    int toPut = Mathf.Min(remaining, item.MaxStack);
                    slot.Count = toPut;
                    remaining -= toPut;
                    OnQuickSlotsChanged?.Invoke();
                }
            }

            return remaining == 0;
        }

        private bool TryAddToInventory(ItemSO item, int count)
        {
            int remaining = count;

            if (item.MaxStack > 1)
            {
                foreach (var slot in _inventorySlots)
                {
                    if (remaining == 0) break;
                    if (slot.Item == item && slot.Count < item.MaxStack)
                    {
                        int can = Mathf.Min(remaining, item.MaxStack - slot.Count);
                        slot.Count += can;
                        remaining -= can;
                        OnInventoryChanged?.Invoke();
                    }
                }
            }

            foreach (var slot in _inventorySlots)
            {
                if (remaining == 0) break;
                if (slot.Item == null)
                {
                    slot.Item = item;
                    int toPut = Mathf.Min(remaining, item.MaxStack);
                    slot.Count = toPut;
                    remaining -= toPut;
                    OnInventoryChanged?.Invoke();
                }
            }

            return remaining == 0;
        }
    }

    public class InventorySlot
    {
        public ItemSO Item;
        public int Count;
    }
}
