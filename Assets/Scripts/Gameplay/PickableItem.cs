// Assets/Scripts/Gameplay/PickableItem.cs
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public class PickableItem : NetworkBehaviour
    {
        [SerializeField] private string itemId;
        private ItemSO _itemDef;
        private ItemDatabaseSO _db;

        [Inject]
        public void Construct(ItemDatabaseSO db)
        {
            _db = db;
            _itemDef = _db.Get(itemId);
        }

        public string ItemId => itemId;
        public ItemSO Definition => _itemDef;

        public void OnPicked(NetworkRunner runner)
        {
            runner.Despawn(Object);
        }
    }
}
