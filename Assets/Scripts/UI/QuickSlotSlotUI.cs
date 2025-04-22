using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public class QuickSlotSlotUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI countText;

        public void Set(ItemSO item, int count)
        {
            if (item != null)
            {
                icon.sprite = item.Icon;
                //icon.enabled = true;
                countText.text = count > 1 ? count.ToString() : string.Empty;
            }
            else
            {
                //icon.enabled = false;
                countText.text = string.Empty;
            }
        }
    }
}