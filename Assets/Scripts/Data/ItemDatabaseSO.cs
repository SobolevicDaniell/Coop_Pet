using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(menuName = "Inventory/ItemDatabase")]
    public class ItemDatabaseSO : ScriptableObject
    {
        public List<ItemSO> Items;
        private Dictionary<string, ItemSO> lookup;

        public ItemSO Get(string id)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<string, ItemSO>();
                foreach (var it in Items)
                    lookup[it.Id] = it;
            }
            lookup.TryGetValue(id, out var so);
            return so;
        }
    }
}