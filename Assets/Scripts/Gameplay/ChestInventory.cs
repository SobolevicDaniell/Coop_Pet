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

        // Можешь добавить AddItem, RemoveItem, логику для сундука и т.п.
    }
}