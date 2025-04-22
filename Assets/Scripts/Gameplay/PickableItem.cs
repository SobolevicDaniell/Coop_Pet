// Assets/Scripts/Game/Gameplay/PickableItem.cs
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public class PickableItem : NetworkBehaviour
    {
        [SerializeField] private string itemId;
        private ItemSO itemDef;
        [Inject] private ItemDatabaseSO db;

        // публичный идентификатор для RPC
        public string ItemId => itemId;
        // локальное определение
        public ItemSO Definition => itemDef;

        private void Awake()
        {
            // на старте подгружаем ScriptableObject из базы
            itemDef = db.Get(itemId);
        }

        public void OnPicked(NetworkRunner runner)
        {
            // просто деспавним себя на сервере
            runner.Despawn(Object);
        }
    }
}
