using System;
using UnityEngine;

namespace Game
{
    public class ChestInventory : MonoBehaviour, IInventory
    {
        [SerializeField] private int slotsCount = 30;
        [SerializeField] private ItemDatabaseSO _database;

        private InventorySlot[] _slots;
        public event Action OnInventoryChanged;

        private void Awake()
        {
            _slots = new InventorySlot[slotsCount];
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = new InventorySlot(null, 0);
        }

        public void Init(ItemDatabaseSO database)
        {
            _database = database;
        }

        public InventorySlot[] GetInventorySlots() => _slots;

        /// <summary>
        /// Добавляет предмет в первый доступный слот (stack или пустой).
        /// </summary>
        public bool TryAddItem(string itemId, int amount)
        {
            if (_database == null)
            {
                Debug.LogError("[ChestInventory] ItemDatabaseSO is not set!");
                return false;
            }

            var itemSo = _database.Get(itemId);
            if (itemSo == null)
            {
                Debug.LogError($"[ChestInventory] Item with id {itemId} not found in database!");
                return false;
            }
            int toAdd = amount;

            // 1. Сначала stack к существующим
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
            // 2. Потом в пустой
            foreach (var slot in _slots)
            {
                if (slot.Id == null)
                {
                    int canPut = Mathf.Min(toAdd, itemSo.MaxStack);
                    slot.Id = itemId;
                    slot.Count = canPut;
                    slot.State = new ItemState();
                    toAdd -= canPut;
                    if (toAdd <= 0) { OnInventoryChanged?.Invoke(); return true; }
                }
            }
            OnInventoryChanged?.Invoke();
            return toAdd == 0;
        }

        /// <summary>
        /// Переместить предмет в КОНКРЕТНЫЙ слот. Используется для drag&drop.
        /// </summary>
        public bool TryMoveToSlot(string itemId, int count, int targetSlot, ItemState state = null)
        {
            if (_database == null || string.IsNullOrEmpty(itemId)) return false;
            if (targetSlot < 0 || targetSlot >= _slots.Length) return false;

            var itemSo = _database.Get(itemId);
            if (itemSo == null) return false;

            var slot = _slots[targetSlot];

            // Если слот пустой — просто кладём предмет
            if (slot.Id == null)
            {
                slot.Id = itemId;
                slot.Count = count;
                slot.State = state != null ? new ItemState(state) : null;
                OnInventoryChanged?.Invoke();
                return true;
            }

            // Если это тот же предмет, пробуем стакать
            if (slot.Id == itemId && slot.Count < itemSo.MaxStack)
            {
                int spaceLeft = itemSo.MaxStack - slot.Count;
                int canStack = Mathf.Min(count, spaceLeft);
                slot.Count += canStack;

                if (slot.State == null && state != null)
                    slot.State = new ItemState(state);

                OnInventoryChanged?.Invoke();
                return canStack == count;
            }

            // Если предметы разные, сделаем swap (обмен)
            slot.Id = slot.Id;
            slot.Count = slot.Count;
            var tempState = slot.State;

            slot.Id = itemId;
            slot.Count = count;
            slot.State = state != null ? new ItemState(state) : null;

            // Вернём старый предмет обратно в исходный слот (нужно реализовать в UI-логике)
            // Можно добавить событие, сообщающее о необходимости вернуть предмет

            OnInventoryChanged?.Invoke();
            return true;
        }


        /// <summary>
        /// Удаляет amount предметов из заданного слота.
        /// </summary>
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
                slot.State = null;
            }
            OnInventoryChanged?.Invoke();
            return true;
        }

        public void RaiseInventoryChanged()
        {
            OnInventoryChanged?.Invoke();
        }

    }
}