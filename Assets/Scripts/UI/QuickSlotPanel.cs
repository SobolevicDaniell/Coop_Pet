using UnityEngine;
using Zenject;

namespace Game
{
    public class QuickSlotPanel : MonoBehaviour
    {
        [SerializeField] private QuickSlotSlotUI[] slotUIs;
        [Inject] private InventoryService _inventory;

        void Start()
        {
            _inventory.OnQuickSlotsChanged += Refresh;
            Refresh();
        }

        void Refresh()
        {
            var slots = _inventory.GetQuickSlots();
            for (int i = 0; i < slotUIs.Length; i++)
                slotUIs[i].Set(slots[i].Item, slots[i].Count);
        }

        void OnDestroy()
        {
            _inventory.OnQuickSlotsChanged -= Refresh;
        }
    }
}
