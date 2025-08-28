using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game
{
    public class UIHealthView : MonoBehaviour
    {
        [SerializeField] private Slider _healthSlider;
        [SerializeField] private GameObject _deathScreen;

        public void SetMaxHealth(int max)
        {
            _healthSlider.maxValue = max;
            _healthSlider.value = max;
            _deathScreen.SetActive(false);
        }

        public void UpdateHealth(int current)
        {
            _healthSlider.value = current;
        }

        public void ShowDeath()
        {
            _deathScreen.SetActive(true);
        }
    }
}
