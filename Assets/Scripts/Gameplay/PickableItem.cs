// Assets/Scripts/Gameplay/PickableItem.cs
using Fusion;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public class PickableItem : NetworkBehaviour
    {
        [SerializeField] private string itemId;
        public string ItemId => itemId;

        public void Pick(NetworkRunner runner)
        {
            if (Object.HasStateAuthority)
            {
                runner.Despawn(Object);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
