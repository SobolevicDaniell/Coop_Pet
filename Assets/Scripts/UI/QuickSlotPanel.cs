using Game;
using Game.UI;
using UnityEngine;
using Zenject;

namespace Game.UI
{
    public class QuickSlotPanel : MonoBehaviour
    {
        [SerializeField] private InventorySlotUI[] slotsUI;
        private InventoryService _inventory;

        [Inject]
        public void Construct(InventoryService inventory)
        {
            _inventory = inventory;
            _inventory.OnQuickSlotsChanged += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            var slots = _inventory.GetQuickSlots();
            for (int i = 0; i < slotsUI.Length; i++)
                slotsUI[i].Set(slots[i].Item, slots[i].Count);
        }

        private void OnDestroy()
        {
            if (_inventory != null)
                _inventory.OnQuickSlotsChanged -= Refresh;
        }
    }
}