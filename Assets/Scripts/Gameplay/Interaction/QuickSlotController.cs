using Fusion;
using UnityEngine;

namespace Game
{
    public class QuickSlotController : MonoBehaviour
    {
        public InventoryService Inventory { get; private set; }

        private InteractionController _ic;

        public void Initialize(InteractionController controller, InventoryService inventory)
        {
            _ic = controller;
            Inventory = inventory;
            if (_ic.Object.HasInputAuthority)
                Inventory.OnQuickSlotsChanged += HandleQuickSlotsChanged;
        }
        public void ChangeSlotAbsolute(int slot)
        {
            if (!_ic.Object.HasInputAuthority || Inventory == null) return;

            Inventory.ToggleQuickSlot(slot);

            // Всегда вызываем HandleQuickSlotsChanged явно после изменения слота
            HandleQuickSlotsChanged();

            // Также вызываем активацию поведения
            _ic.InvokeOnQuickSlotsChanged();
        }


        public void ChangeSlotRelative(int delta)
        {
            if (_ic == null || !_ic.Object.HasInputAuthority || Inventory == null) return;

            var slots = Inventory.GetQuickSlots();
            int cnt = slots.Length;
            int cur = Inventory.SelectedQuickSlot < 0 ? 0 : Inventory.SelectedQuickSlot;
            int next = (cur + delta + cnt) % cnt;

            ChangeSlotAbsolute(next);
        }
        private void HandleQuickSlotsChanged()
        {
            if (!_ic.Object.HasInputAuthority || Inventory == null) return;

            int idx = Inventory.SelectedQuickSlot;
            var slots = Inventory.GetQuickSlots();

            if (idx < 0 || idx >= slots.Length)
            {
                _ic.handItemController.RequestUnEquip();
                return;
            }

            var slot = slots[idx];
            string id = slot.Id;

            if (string.IsNullOrEmpty(id))
                _ic.handItemController.RequestUnEquip();   // слот опустел
            else
                _ic.handItemController.RequestEquip(id);   // в слоте новый предмет
        }
    }
}
