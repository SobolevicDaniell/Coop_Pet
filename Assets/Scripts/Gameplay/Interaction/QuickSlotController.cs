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

            // поддерживаем сетевое поле (для клиентов и late-join)
            _ic.netSelectedQuickSlot = Inventory.SelectedQuickSlot;

            // 1) визуальная часть (модель в руках через RPC)
            HandleQuickSlotsChanged();

            // 2) логика поведения (Equip через единую точку)
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
                _ic.handItemController.RequestUnEquip();
            else
                _ic.handItemController.RequestEquip(id);
        }
    }
}
