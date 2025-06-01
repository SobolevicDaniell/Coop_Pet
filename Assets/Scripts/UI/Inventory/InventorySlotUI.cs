using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;

namespace Game.UI
{
    public class InventorySlotUI : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _activeIcon;
        [SerializeField] private TextMeshProUGUI _countText;

        public int SlotIndex { get; private set; }
        public IInventory ParentInventory { get; private set; }
        public IInventoryPanelUI ParentPanel { get; private set; }
        public ItemSO Item { get; private set; }

        public event System.Action<InventorySlotUI> OnBeginDrag;
        public event System.Action<InventorySlotUI> OnEndDrag;
        public event System.Action<InventorySlotUI> OnEnter;
        public event System.Action<InventorySlotUI> OnExit;

        public void Set(ItemSO item, int count)
        {
            Item = item;
            if (item != null)
            {
                _icon.sprite = item.Icon;
                _icon.enabled = true;
                // Показывай ammo если WeaponSO, иначе count если >1
                if (item is WeaponSO)
                    _countText.text = count > 0 ? count.ToString() : "";
                else
                    _countText.text = count > 1 ? count.ToString() : "";
            }
            else
            {
                _icon.enabled = false;
                _countText.text = "";
            }
        }

        public void SetActive(bool active) => _activeIcon.enabled = active;

        public void Init(int index, IInventory inventory, IInventoryPanelUI panel)
        {
            SlotIndex = index;
            ParentInventory = inventory;
            ParentPanel = panel;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && Item != null)
                OnBeginDrag?.Invoke(this);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                OnEndDrag?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData) => OnEnter?.Invoke(this);
        public void OnPointerExit(PointerEventData eventData) => OnExit?.Invoke(this);
    }
}
