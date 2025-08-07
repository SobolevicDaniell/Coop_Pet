using UnityEngine;
using Zenject;

namespace Game
{
    public class ItemEquipController : MonoBehaviour
    {
        private HandItemBehaviorFactory _factory;
        private ItemDatabaseSO _db;
        private InteractionController _controller;

        public void Initialize(HandItemBehaviorFactory factory, ItemDatabaseSO itemDatabase, InteractionController interactionController)
        {
            _factory = factory;
            _db = itemDatabase;
            _controller = interactionController;
        }
        public void Equip(int slotIdx, InventorySlot[] quickSlots)
        {
            // Снимаем старое поведение
            _controller.currentBehavior?.OnUnequip();
            if (_controller.currentBehavior is MonoBehaviour oldBehavior)
                Destroy(oldBehavior.gameObject);
            _controller.SetCurrentBehavior(null);

            // Выход если слот невалиден
            if (slotIdx < 0 || quickSlots == null || slotIdx >= quickSlots.Length) return;

            var slot = quickSlots[slotIdx];
            if (slot == null || string.IsNullOrEmpty(slot.Id)) return;

            var so = _db.Get(slot.Id);
            // var behavior = _factory.Create(so, _controller.handPoint, _controller, slotIdx);
            var behavior = _factory.Create(so, _controller.handPoint, _controller, slotIdx, slot);

            _controller.SetCurrentBehavior(behavior);
        }
        public void ValidateEquipped(int selectedQuickSlot, InventorySlot[] quickSlots)
        {
            if (selectedQuickSlot < 0 || quickSlots == null) {
                Equip(-1, quickSlots);
                return;
            }

            var slot = quickSlots[selectedQuickSlot];
            if (slot == null || string.IsNullOrEmpty(slot.Id))
                Equip(-1, quickSlots);
        }
    }
}
