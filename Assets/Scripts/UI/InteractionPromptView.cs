using UnityEngine;

public class InteractionPromptView : MonoBehaviour
{
    public void Show(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
