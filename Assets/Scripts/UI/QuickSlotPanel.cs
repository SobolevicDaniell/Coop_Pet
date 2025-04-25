using Game;
using UnityEngine;
using Zenject;

public class QuickSlotPanel : MonoBehaviour
{
    [SerializeField] private QuickSlotSlotUI[] slotUIs;
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
        for (int i = 0; i < slotUIs.Length; i++)
            slotUIs[i].Set(slots[i].Item, slots[i].Count);
    }

    private void OnDestroy()
    {
        if (_inventory != null)
            _inventory.OnQuickSlotsChanged -= Refresh;
    }
}
