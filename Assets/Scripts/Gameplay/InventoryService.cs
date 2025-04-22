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

        // ����� Zenject ������� ���� ���������
        public InventoryService(ItemDatabaseSO db)
        {
            this.db = db;
            quickSlots = new InventorySlot[10];
            for (int i = 0; i < quickSlots.Length; i++)
                quickSlots[i] = new InventorySlot();
        }

        // ���������� ������������� ScriptableObject
        public bool AddToQuickSlot(ItemSO item)
        {
            if (item == null) return false;

            // ������� �������� � ����
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
            // ����� ���� ������
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
            return false; // ����� ���
        }

        // ���������� �� �������� ��������������
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
