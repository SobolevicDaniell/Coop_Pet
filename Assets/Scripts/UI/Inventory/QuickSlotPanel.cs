using Game;
using UnityEngine;
using Zenject;

namespace Game.UI
{
    public class QuickSlotPanel : MonoBehaviour
    {
        [SerializeField] private InventorySlotUI[] _slots;
        private InventoryService _inv;
        private ItemDatabaseSO _db;

        [Inject]
        public void Construct(InventoryService inv, ItemDatabaseSO db)
        {
            _inv = inv;
            _db = db;
            _inv.OnQuickSlotsChanged += Refresh;
            _inv.OnQuickSlotSelectionChanged += Highlight;
            Refresh();
            Highlight(_inv.SelectedQuickSlot);
        }

        private void Refresh()
        {
            var slots = _inv.GetQuickSlots();
            for (int i = 0; i < _slots.Length; i++)
            {
                var s = slots[i];
                var item = s.Id != null ? _db.Get(s.Id) : null;
                if (item is WeaponSO)
                    _slots[i].Set(item, s.State.Ammo);
                else
                    _slots[i].Set(item, s.Count);
            }
        }

        private void Highlight(int sel)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].SetActive(i == sel);
            }
        }

        private void OnDestroy()
        {
            _inv.OnQuickSlotsChanged -= Refresh;
            _inv.OnQuickSlotSelectionChanged -= Highlight;
        }
    }
}
