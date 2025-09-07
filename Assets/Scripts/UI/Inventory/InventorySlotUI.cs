using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using System;
using Game; // чтобы видеть ItemState

namespace Game.UI
{
    public class InventorySlotUI : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _activeIcon;        // только подсветка
        [SerializeField] private TextMeshProUGUI _countText;

        public int SlotIndex { get; private set; }
        public IInventory ParentInventory { get; private set; }
        public IInventoryPanelUI ParentPanel { get; private set; }
        public ItemSO Item { get; private set; }

        public event Action<InventorySlotUI> OnBeginDrag;
        public event Action<InventorySlotUI> OnEndDrag;
        public event Action<InventorySlotUI> OnEnter;
        public event Action<InventorySlotUI> OnExit;

     
        public void Set(ItemSO item, int count, ItemState state = null)
        {
            Item = item;

            // ИКОНКА
            if (item != null && _icon != null)
            {
                _icon.sprite = item.Icon;
                _icon.enabled = true;
            }
            else if (_icon != null)
            {
                _icon.sprite = null;
                _icon.enabled = false; // ← прячем только иконку, НЕ слот
            }

            // ТЕКСТ
            if (_countText != null)
            {
                if (item == null)
                {
                    _countText.text = string.Empty; // ← очищаем текст
                }
                else if (item is WeaponSO)
                {
                    int ammo = state != null ? Mathf.Max(0, state.ammo) : 0;
                    _countText.text = ammo.ToString(); // оружие — просто число
                }
                else
                {
                    _countText.text = (count > 0) ? $"x{count}" : string.Empty; // не оружие — xN
                }
            }
        }

        // Подсветка выбранного слота (только рамка/акцент)
        public void SetActive(bool active)
        {
            if (_activeIcon != null)
                _activeIcon.enabled = active;
        }

        public void Init(int index, IInventory inventory, IInventoryPanelUI panel)
        {
            SlotIndex       = index;
            ParentInventory = inventory;
            ParentPanel     = panel;

            // При создании — без подсветки
            SetActive(false);
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
        public void OnPointerExit (PointerEventData eventData) => OnExit?.Invoke(this);
    }
}
