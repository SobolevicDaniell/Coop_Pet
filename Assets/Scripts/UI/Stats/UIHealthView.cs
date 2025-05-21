using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game
{
    public class UIHealthView : MonoBehaviour
    {
        [SerializeField] private Slider healthSlider;
        [SerializeField] private GameObject deathText;

        public void SetMaxHealth(int max)
        {
            healthSlider.maxValue = max;
            healthSlider.value = max;
            deathText.SetActive(false);
        }

        public void UpdateHealth(int current)
        {
            healthSlider.value = current;
        }

        public void ShowDeath()
        {
            deathText.SetActive(true);
        }
    }
}
