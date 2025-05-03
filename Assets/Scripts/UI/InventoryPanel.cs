using UnityEngine;
using Zenject;

namespace Game.UI
{
    public class InventoryPanel : MonoBehaviour
    {
        [SerializeField] private InventorySlotUI[] _slotsUI;
        private InventoryService _inventory;

        [Inject]
        public void Construct(InventoryService inventory)
        {
            _inventory = inventory;
            _inventory.OnInventoryChanged += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            var slots = _inventory.GetInventorySlots();
            int length = Mathf.Min(slots.Length, _slotsUI.Length);

            for (int i = 0; i < length; i++)
                _slotsUI[i].Set(slots[i].Item, slots[i].Count);

            for (int i = length; i < _slotsUI.Length; i++)
                _slotsUI[i].Set(null, 0);
        }

        private void OnDestroy()
        {
            if (_inventory != null)
                _inventory.OnInventoryChanged -= Refresh;
        }
    }
}
