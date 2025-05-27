using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Game.UI
{
    public class InventorySlotUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _activeIcon;
        [SerializeField] private TextMeshProUGUI _countText;

        public Image ItemIcon => _icon;


        public int SlotIndex { get; private set; }
        public IInventory ParentInventory { get; private set; }

        public event System.Action<InventorySlotUI> OnBeginDrag;
        public event System.Action<InventorySlotUI> OnEndDrag;
        public event System.Action<InventorySlotUI> OnPointerEnterSlot;
        public event System.Action<InventorySlotUI> OnPointerExitSlot;

        public void Set(ItemSO item, int count)
        {
            if (item != null)
            {
                _icon.sprite = item.Icon;
                _icon.enabled = true;
                if (item is WeaponSO)
                    _countText.text = count.ToString(); // count = Ammo
                else
                    _countText.text = count > 1 ? count.ToString() : "";
            }
            else
            {
                _icon.enabled = false;
                _countText.text = "";
            }
        }
        public void SetActive(bool active)
        {
            _activeIcon.enabled = active;
        }

        public void Init(int index, IInventory inventory)
        {
            SlotIndex = index;
            ParentInventory = inventory;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                OnBeginDrag?.Invoke(this);
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                OnEndDrag?.Invoke(this);
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            OnPointerEnterSlot?.Invoke(this);
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            OnPointerExitSlot?.Invoke(this);
        }
    }
}
