using UnityEngine;
using Zenject;

namespace Game
{
    public class HandItemController
    {
        private readonly HandItemBehaviorFactory _factory;
        private readonly Transform _handPoint;
        private readonly ItemDatabaseSO _db;
        private InteractionController _ic;
        private IHandItemBehavior _current;

        public HandItemController(HandItemBehaviorFactory factory, Transform handPoint, ItemDatabaseSO db)
        {
            _factory = factory;
            _handPoint = handPoint;
            _db = db;
        }

        public void Initialize(InteractionController ic)
        {
            _ic = ic;
        }

        public IHandItemBehavior CurrentBehavior => _current;

        public void Equip(int slotIdx, InventorySlot[] quickSlots)
        {
            _current?.OnUnequip();
            _current = null;
            if (slotIdx < 0) return;

            var slot = quickSlots[slotIdx];
            if (slot.Id == null) return;

            var so = _db.Get(slot.Id);
            _current = _factory.Create(so, _handPoint, _ic);
            _current.OnEquip();
        }

        public void MuzzleFlash()
        {
            _current?.OnMuzzleFlash();
        }
    }
}
