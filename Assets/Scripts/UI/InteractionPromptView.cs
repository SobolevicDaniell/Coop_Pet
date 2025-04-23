using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game
{
    public class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] private GameObject promptRoot;

        public void Show()
        {
            if (promptRoot != null)
                promptRoot.SetActive(true);
        }

        public void Hide()
        {
            if (promptRoot != null)
                promptRoot.SetActive(false);
        }
    }
}
