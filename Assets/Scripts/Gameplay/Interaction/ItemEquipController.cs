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

        /// <summary>
        /// Экипировать предмет по индексу слота quickSlots (или снять, если индекс -1 или слот пустой)
        /// </summary>
        public void Equip(int slotIdx, InventorySlot[] quickSlots)
        {
            _controller.CurrentBehavior?.OnUnequip();
            if (_controller.CurrentBehavior is MonoBehaviour oldBehavior)
                Destroy(oldBehavior.gameObject);

            _controller.SetCurrentBehavior(null);

            if (slotIdx < 0) return;

            var slot = quickSlots[slotIdx];
            if (slot == null || string.IsNullOrEmpty(slot.Id)) return;

            var so = _db.Get(slot.Id);
            var behavior = _factory.Create(so, _controller.HandPoint, _controller, slot);
            _controller.SetCurrentBehavior(behavior);

            _controller.RpcHandler.RPC_RequestSpawnHandModel(slot.Id);
        }

        /// <summary>
        /// Проверка актуальности предмета в руках (если quick-слот очистился/изменился)
        /// </summary>
        public void ValidateEquipped(int selectedQuickSlot, InventorySlot[] quickSlots)
        {
            // если предмет был экипирован, а слот теперь пуст — снять с рук
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
