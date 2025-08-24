using UnityEngine;

namespace Game
{
    public sealed class ItemEquipController : MonoBehaviour
    {
        private HandItemBehaviorFactory _factory;
        private ItemDatabaseSO _db;
        private InteractionController _ic;

        /// <summary> Вызывается из InteractionController.Spawned(). </summary>
        public void Initialize(HandItemBehaviorFactory factory, ItemDatabaseSO itemDatabase, InteractionController interactionController)
        {
            _factory = factory;
            _db      = itemDatabase;
            _ic      = interactionController;
        }

        public void Equip(int slotIdx, InventorySlot[] quickSlots)
        {
            if (_ic == null) return;

            // 1) Снять предыдущее поведение (без уничтожения компонента — поведение не MonoBehaviour)
            _ic.ClearBehavior();

            // 2) Не валидный индекс / пусто → снять hand-модель
            if (quickSlots == null || slotIdx < 0 || slotIdx >= quickSlots.Length)
            {
                _ic.handItemController?.RequestUnEquip();
                // Обновим репликацию выбранного слота
                if (_ic.Object.HasInputAuthority) _ic.netSelectedQuickSlot = -1;
                return;
            }

            var slot = quickSlots[slotIdx];
            if (slot == null || string.IsNullOrEmpty(slot.Id))
            {
                _ic.handItemController?.RequestUnEquip();
                if (_ic.Object.HasInputAuthority) _ic.netSelectedQuickSlot = -1;
                return;
            }

            string itemId = slot.Id;

            // 3) Попросим сервер заспавнить hand-модель (по itemId)
            _ic.handItemController?.RequestEquip(itemId);

            // 4) Локально создаём поведение и активируем
            var behavior = _factory.Create(_ic, itemId, slotIdx);
            behavior.OnEquip();
            _ic.SetCurrentBehavior(behavior);

            // 5) Для серверной логики (списание патронов и т.п.) публикуем выбранный слот
            if (_ic.Object.HasInputAuthority)
                _ic.netSelectedQuickSlot = slotIdx;
        }

        /// <summary>
        /// Быстрая валидация: если текущий выбранный слот пуст/невалиден — снимаем предмет.
        /// </summary>
        public void ValidateEquipped(int selectedQuickSlot, InventorySlot[] quickSlots)
        {
            if (quickSlots == null || selectedQuickSlot < 0 || selectedQuickSlot >= quickSlots.Length)
            {
                Equip(-1, quickSlots);
                return;
            }

            var slot = quickSlots[selectedQuickSlot];
            if (slot == null || string.IsNullOrEmpty(slot.Id))
                Equip(-1, quickSlots);
        }
    }
}
