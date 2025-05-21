using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(menuName = "Inventory/ItemDatabase")]
    public class ItemDatabaseSO : ScriptableObject
    {
        public List<ItemSO> Items;
        private Dictionary<string, ItemSO> _lookup;

        public ItemSO Get(string id)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, ItemSO>();
                foreach (var i in Items) _lookup[i.Id] = i;
            }
            _lookup.TryGetValue(id, out var so);
            return so;
        }
    }
}
