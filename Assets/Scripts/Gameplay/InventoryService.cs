using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class InventoryService : IInventory
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



        public void ToggleQuickSlot(int idx)
        {
            if (SelectedQuickSlot == idx)
            {
                SelectedQuickSlot = -1;
            }
            else
            {
                SelectedQuickSlot = idx;
            }
            OnQuickSlotSelectionChanged?.Invoke(SelectedQuickSlot);
        }

        public bool MoveQuickSlot(int from, int to)
        {
            if (from == to) return false;
            if (from < 0 || from >= _quickSlots.Length) return false;
            if (to < 0 || to >= _quickSlots.Length) return false;

            (_quickSlots[from], _quickSlots[to]) = (_quickSlots[to], _quickSlots[from]);

            // Если выбранный слот был перемещён, сбрасываем выбор
            if (SelectedQuickSlot == from || SelectedQuickSlot == to)
                SelectedQuickSlot = -1;

            OnQuickSlotsChanged?.Invoke();
            OnQuickSlotSelectionChanged?.Invoke(SelectedQuickSlot);
            return true;
        }


        public int HandlePick(string itemId, int count, int ammo)
        {
            var item = _db.Get(itemId);
            if (item == null) return count;

            int rem = count;
            if (item.priority == 1)
                rem = TryQuick(item, rem, ammo);
            else
                rem = TryInventory(item, rem, ammo);

            if (rem > 0)
            {
                if (item.priority == 1)
                    rem = TryInventory(item, rem, ammo);
                else
                    rem = TryQuick(item, rem, ammo);
            }

            return rem;
        }

        private int TryQuick(ItemSO item, int rem, int ammo = 0)
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
                    slot.State = new ItemState(ammo);
                    rem -= toPut;
                    OnQuickSlotsChanged?.Invoke();
                }
            }
            return rem;
        }

        private int TryInventory(ItemSO item, int rem, int ammo = 0)
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
                    slot.State = new ItemState(ammo);
                    rem -= toPut;
                    OnInventoryChanged?.Invoke();
                }
            }
            return rem;
        }

        public void RaiseQuickSlotsChanged() => OnQuickSlotsChanged?.Invoke();
        public void RaiseQuickSlotSelectionChanged(int sel) => OnQuickSlotSelectionChanged?.Invoke(sel);

        // ==== Методы для ресурсов (патроны) ====

        public int GetResourceCount(string resourceId)
        {
            int count = 0;
            foreach (var slot in _quickSlots)
                if (slot.Id == resourceId) count += slot.Count;
            foreach (var slot in _inventorySlots)
                if (slot.Id == resourceId) count += slot.Count;
            return count;
        }

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
        public IEnumerable<InventorySlot> FindAllResourceSlots(string resourceId)
        {
            foreach (var slot in _quickSlots)
                if (slot.Id == resourceId && slot.Count > 0)
                    yield return slot;
            foreach (var slot in _inventorySlots)
                if (slot.Id == resourceId && slot.Count > 0)
                    yield return slot;
        }


        public void ForceSetQuickSlot(int idx)
        {
            if (idx < -1 || idx >= _quickSlots.Length) return;
            if (SelectedQuickSlot == idx) return;
            SelectedQuickSlot = idx;
            OnQuickSlotSelectionChanged?.Invoke(SelectedQuickSlot);
        }



        public bool TryAddItem(string itemId, int count)
        {
            var itemSo = _db.Get(itemId);
            if (itemSo == null) return false;
            int left = count;

            // Quick slots (если игроку разрешено класть туда такие предметы)
            foreach (var slot in _quickSlots)
            {
                if (slot.Id == itemId && slot.Count < itemSo.MaxStack)
                {
                    int canPut = Mathf.Min(left, itemSo.MaxStack - slot.Count);
                    slot.Count += canPut;
                    left -= canPut;
                    if (left <= 0) { OnQuickSlotsChanged?.Invoke(); return true; }
                }
            }
            // Inventory slots
            foreach (var slot in _inventorySlots)
            {
                if (slot.Id == itemId && slot.Count < itemSo.MaxStack)
                {
                    int canPut = Mathf.Min(left, itemSo.MaxStack - slot.Count);
                    slot.Count += canPut;
                    left -= canPut;
                    if (left <= 0) { OnInventoryChanged?.Invoke(); return true; }
                }
            }
            foreach (var slot in _quickSlots)
            {
                if (slot.Id == null)
                {
                    int canPut = Mathf.Min(left, itemSo.MaxStack);
                    slot.Id = itemId;
                    slot.Count = canPut;
                    left -= canPut;
                    if (left <= 0) { OnQuickSlotsChanged?.Invoke(); return true; }
                }
            }
            foreach (var slot in _inventorySlots)
            {
                if (slot.Id == null)
                {
                    int canPut = Mathf.Min(left, itemSo.MaxStack);
                    slot.Id = itemId;
                    slot.Count = canPut;
                    left -= canPut;
                    if (left <= 0) { OnInventoryChanged?.Invoke(); return true; }
                }
            }
            OnQuickSlotsChanged?.Invoke();
            OnInventoryChanged?.Invoke();
            return left == 0;
        }

        // Новый метод — перенос предмета именно в выбранный слот, например, в quick slot!
        public bool TryMoveToSlot(string itemId, int count, int targetSlot, ItemState state = null)
        {
            InventorySlot[] allSlots;
            int slotCount;
            bool isQuick = false;

            if (targetSlot < 10)
            {
                allSlots = _quickSlots;
                slotCount = _quickSlots.Length;
                isQuick = true;
            }
            else
            {
                int invIndex = targetSlot - 10;
                if (invIndex < 0 || invIndex >= _inventorySlots.Length) return false;
                allSlots = _inventorySlots;
                slotCount = _inventorySlots.Length;
                targetSlot = invIndex;
            }

            if (targetSlot < 0 || targetSlot >= slotCount)
                return false;

            var itemSo = _db.Get(itemId);
            if (itemSo == null) return false;

            var slot = allSlots[targetSlot];

            // Если пусто — кладём весь стек
            if (slot.Id == null)
            {
                slot.Id = itemId;
                slot.Count = count;
                slot.State = state != null ? new ItemState(state) : null;
                if (isQuick) OnQuickSlotsChanged?.Invoke();
                else OnInventoryChanged?.Invoke();
                return true;
            }
            // Если такой же предмет — стакаем до максимума
            if (slot.Id == itemId && slot.Count < itemSo.MaxStack)
            {
                int canPut = Mathf.Min(count, itemSo.MaxStack - slot.Count);
                slot.Count += canPut;
                if (slot.State == null && state != null)
                    slot.State = new ItemState(state);
                if (isQuick) OnQuickSlotsChanged?.Invoke();
                else OnInventoryChanged?.Invoke();
                return canPut == count;
            }

            // Если другой предмет — не кладём, возвращаем false
            return false;
        }

        public bool TryRemoveItem(int slotIndex, int amount)
        {
            InventorySlot[] allSlots;
            int slotCount;
            bool isQuick = false;

            if (slotIndex < 10)
            {
                allSlots = _quickSlots;
                slotCount = _quickSlots.Length;
                isQuick = true;
            }
            else
            {
                int invIndex = slotIndex - 10;
                if (invIndex < 0 || invIndex >= _inventorySlots.Length) return false;
                allSlots = _inventorySlots;
                slotCount = _inventorySlots.Length;
                slotIndex = invIndex;
            }

            if (slotIndex < 0 || slotIndex >= slotCount) return false;
            var slot = allSlots[slotIndex];
            if (slot.Id == null || slot.Count < amount) return false;
            slot.Count -= amount;
            if (slot.Count <= 0)
            {
                slot.Id = null;
                slot.Count = 0;
                slot.State = null;
            }
            if (isQuick) OnQuickSlotsChanged?.Invoke();
            else OnInventoryChanged?.Invoke();
            return true;
        }
        public void RaiseInventoryChanged()
        {
            OnInventoryChanged?.Invoke();
        }

        


    }
}
