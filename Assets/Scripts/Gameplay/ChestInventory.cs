using System;
using UnityEngine;

namespace Game
{
    public class ChestInventory : MonoBehaviour, IInventory
    {
        [SerializeField] private int slotsCount = 30;
        private InventorySlot[] _slots;
        public event Action OnInventoryChanged;

        private void Awake()
        {
            _slots = new InventorySlot[slotsCount];
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = new InventorySlot(null, 0);
        }

        public InventorySlot[] GetInventorySlots() => _slots;

        public bool TryAddItem(string itemId, int amount)
        {
            // Найди описание предмета (если нужно ограничение по MaxStack)
            var itemSo = FindObjectOfType<ItemDatabaseSO>().Get(itemId);
            if (itemSo == null) return false;
            int toAdd = amount;

            // 1. Сначала дополняем существующие слоты (stack)
            foreach (var slot in _slots)
            {
                if (slot.Id == itemId && slot.Count < itemSo.MaxStack)
                {
                    int canPut = Mathf.Min(toAdd, itemSo.MaxStack - slot.Count);
                    slot.Count += canPut;
                    toAdd -= canPut;
                    if (toAdd <= 0) { OnInventoryChanged?.Invoke(); return true; }
                }
            }
            // 2. Добавляем в пустые слоты
            foreach (var slot in _slots)
            {
                if (slot.Id == null)
                {
                    int canPut = Mathf.Min(toAdd, itemSo.MaxStack);
                    slot.Id = itemId;
                    slot.Count = canPut;
                    toAdd -= canPut;
                    if (toAdd <= 0) { OnInventoryChanged?.Invoke(); return true; }
                }
            }
            OnInventoryChanged?.Invoke();
            return toAdd == 0;
        }

        public bool TryRemoveItem(int slotIndex, int amount)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length) return false;
            var slot = _slots[slotIndex];
            if (slot.Id == null || slot.Count < amount) return false;
            slot.Count -= amount;
            if (slot.Count <= 0)
            {
                slot.Id = null;
                slot.Count = 0;
            }
            OnInventoryChanged?.Invoke();
            return true;
        }
    }
}
