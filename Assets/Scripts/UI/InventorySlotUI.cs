using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class InventorySlotUI : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _activeIcon;
        [SerializeField] private TextMeshProUGUI _countText;

        public void Set(ItemSO item, int count)
        {
            if (item != null)
            {
                _icon.sprite = item.Icon;
                _icon.enabled = true;
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
    }
}
