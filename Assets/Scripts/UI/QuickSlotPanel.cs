using UnityEngine;
using Zenject;

namespace Game
{
    public class QuickSlotPanel : MonoBehaviour
    {
        [SerializeField] private QuickSlotSlotUI[] slotUIs;
        [Inject] private InventoryService inventory;

        void Start()
        {
            inventory.OnQuickSlotsChanged += Refresh;
            Refresh();
        }

        void Refresh()
        {
            var slots = inventory.GetQuickSlots();
            for (int i = 0; i < slotUIs.Length; i++)
                slotUIs[i].Set(slots[i].Item, slots[i].Count);
        }
    }
}