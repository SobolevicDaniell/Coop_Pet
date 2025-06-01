
namespace Game.UI
{
    public interface IInventoryPanelUI
    {
        event System.Action<InventorySlotUI> OnSlotBeginDrag;
        event System.Action<InventorySlotUI> OnSlotEndDrag;
        event System.Action<InventorySlotUI> OnSlotEnter;
        event System.Action<InventorySlotUI> OnSlotExit;
        void RefreshPanel();
    }
}