using System;
using System.Collections.Generic;
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
                _quickSlots[i] = new InventorySlot(null, 0);
            for (int i = 0; i < _inventorySlots.Length; i++)
                _inventorySlots[i] = new InventorySlot(null, 0);
        }

        public InventorySlot[] GetQuickSlots() => _quickSlots;
        public InventorySlot[] GetInventorySlots() => _inventorySlots;

        // ... ВСЯ ТВОЯ СТАРАЯ ЛОГИКА ЗДЕСЬ (SelectQuickSlot, SetQuickSlot, TryQuick и т.д.) ...

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

        public void RaiseQuickSlotsChanged()
        {
            OnQuickSlotsChanged?.Invoke();
        }
        public void RaiseQuickSlotSelectionChanged(int sel)
        {
            OnQuickSlotSelectionChanged?.Invoke(sel);
        }

        // === ДОПОЛНЕНИЕ: универсальные методы ресурсов ===

        /// <summary>
        /// Получить общее количество ресурса по его Id (поиск в quick+inventory)
        /// </summary>
        public int GetResourceCount(string resourceId)
        {
            int count = 0;
            foreach (var slot in _quickSlots)
                if (slot.Id == resourceId) count += slot.Count;
            foreach (var slot in _inventorySlots)
                if (slot.Id == resourceId) count += slot.Count;
            return count;
        }

        /// <summary>
        /// Списать amount ресурса из quick-слотов, потом из inventory.
        /// Возвращает true, если удалось списать полностью.
        /// </summary>
        public bool SpendResource(string resourceId, int amount)
        {
            List<InventorySlot> slots = new List<InventorySlot>();
            slots.AddRange(_quickSlots);
            slots.AddRange(_inventorySlots);

            int toSpend = amount;

            foreach (var slot in slots)
            {
                if (toSpend <= 0) break;
                if (slot.Id == resourceId && slot.Count > 0)
                {
                    int take = Mathf.Min(slot.Count, toSpend);
                    slot.Count -= take;
                    toSpend -= take;
                    if (slot.Count <= 0)
                        slot.Id = null;
                }
            }

            OnQuickSlotsChanged?.Invoke();
            OnInventoryChanged?.Invoke();

            return toSpend <= 0;
        }

        /// <summary>
        /// Вернёт первый слот с данным ресурсом (или null)
        /// </summary>
        public InventorySlot FindResourceSlot(string resourceId)
        {
            foreach (var slot in _quickSlots)
                if (slot.Id == resourceId && slot.Count > 0)
                    return slot;
            foreach (var slot in _inventorySlots)
                if (slot.Id == resourceId && slot.Count > 0)
                    return slot;
            return null;
        }

        public void SetQuickSlot(int idx)
        {
            if (idx < 0 || idx >= _quickSlots.Length) return;
            if (SelectedQuickSlot == idx) return;
            SelectedQuickSlot = idx;
            OnQuickSlotSelectionChanged?.Invoke(idx);
        }

        public void ClearQuickSlot()
        {
            if (SelectedQuickSlot < 0) return;
            SelectedQuickSlot = -1;
            OnQuickSlotSelectionChanged?.Invoke(-1);
        }


        /// <summary>
        /// Найти все слоты с данным ресурсом.
        /// </summary>
        public System.Collections.Generic.IEnumerable<InventorySlot> FindAllResourceSlots(string resourceId)
        {
            foreach (var slot in _quickSlots)
                if (slot.Id == resourceId && slot.Count > 0)
                    yield return slot;
            foreach (var slot in _inventorySlots)
                if (slot.Id == resourceId && slot.Count > 0)
                    yield return slot;
        }
    }
}
