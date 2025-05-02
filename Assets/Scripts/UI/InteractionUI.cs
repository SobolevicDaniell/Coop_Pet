// Assets/Scripts/UI/InteractionUI.cs
using UnityEngine;
using Zenject;
using Game.Gameplay;

namespace Game.UI
{
    public class InteractionUI : MonoBehaviour
    {
        [Inject] private InteractionSignalsSO _signals;
        [Inject] private InteractionPromptView _view;

        void Start()
        {
            _signals.OnShowPrompt.AddListener(_view.Show);
            _signals.OnHidePrompt.AddListener(_view.Hide);
            _view.Hide();
        }

        void OnDestroy()
        {
            _signals.OnShowPrompt.RemoveListener(_view.Show);
            _signals.OnHidePrompt.RemoveListener(_view.Hide);
        }
    }
}
