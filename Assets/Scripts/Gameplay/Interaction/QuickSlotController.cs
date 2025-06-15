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
            if (Inventory == null)
            {
                // Debug.LogError("[QuickSlotController] InventoryService is NULL!");
                return;
            }
            // Debug.Log("[QuickSlotController] Initialized");
        }

        public void ChangeSlotAbsolute(int slot)
        {
            // Debug.Log($"[{name}] InputAuthority: {_ic.Object.HasInputAuthority}, NetworkId: {_ic.Object.Id}");

            if (!_ic.Object.HasInputAuthority) return;

            int prev = Inventory.SelectedQuickSlot;
            Inventory.ToggleQuickSlot(slot);

            int curr = Inventory.SelectedQuickSlot;

            if (curr == -1)
            {
                _ic.handItemController.RequestUnEquip();
                // Debug.Log($"[QuickSlotController] Слот {slot} сброшен");
            }
            else
            {
                var quickSlots = Inventory.GetQuickSlots();
                if (quickSlots != null && slot >= 0 && slot < quickSlots.Length)
                {
                    var id = quickSlots[slot].Id;
                    if (!string.IsNullOrEmpty(id))
                    {
                        var so = _ic.db.Get(id);
                        if (so != null)
                        {
                            // string itemId = so.Id;
                            _ic.handItemController.RequestEquip(id);
                        }
                        else
                        {
                            _ic.handItemController.RequestUnEquip();
                        }
                    }
                    else
                    {
                        _ic.handItemController.RequestUnEquip();
                    }
                }
                // Debug.Log($"[QuickSlotController] Слот {slot} выбран");
            }
        }

        public void ChangeSlotRelative(int d)
        {
            if (_ic == null || !_ic.Object.HasInputAuthority || Inventory == null)
                return;

            var slots = Inventory.GetQuickSlots();
            int cnt = slots.Length;
            int cur = Inventory.SelectedQuickSlot < 0 ? 0 : Inventory.SelectedQuickSlot;
            int next = (cur + d + cnt) % cnt;

            ChangeSlotAbsolute(next);
        }
    }
}
