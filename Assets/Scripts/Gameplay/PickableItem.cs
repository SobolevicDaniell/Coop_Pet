using Fusion;
using UnityEngine;
using Zenject;

namespace Game.Gameplay
{
    [RequireComponent(typeof(NetworkObject))]
    public class PickableItem : NetworkBehaviour
    {
        // сетевые поля, синхронизируемые всем клиентам
        [Networked] public string ItemId { get; private set; }
        [Networked] public int Count { get; private set; }

        // этот метод вызывается в OnBeforeSpawned делегате
        public void Initialize(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }

        public void Pick(NetworkRunner runner)
        {
            if (Object.HasStateAuthority)
                runner.Despawn(Object);
        }
    }
}
