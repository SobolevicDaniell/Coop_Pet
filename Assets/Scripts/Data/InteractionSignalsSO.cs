// Assets/Scripts/Signals/InteractionSignalsSO.cs
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(menuName = "Signals/InteractionSignals")]
public class InteractionSignalsSO : ScriptableObject
{
    public UnityEvent OnShowPrompt;
    public UnityEvent OnHidePrompt;
}
