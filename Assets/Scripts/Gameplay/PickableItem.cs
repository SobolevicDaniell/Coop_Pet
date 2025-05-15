using Fusion;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public class PickableItem : NetworkBehaviour
    {
        [Networked] public string ItemId { get; private set; }
        [Networked] public int Count { get; private set; }

        public ItemState State; // Обычное поле (не сетевое), синхронизация нужна только при спавне

        public void Initialize(string itemId, int count, ItemState state = null)
        {
            ItemId = itemId;
            Count = count;
            State = state ?? new ItemState();
        }

        public void Pick(NetworkRunner runner)
        {
            if (Object.HasStateAuthority)
                runner.Despawn(Object);
        }
    }
}
