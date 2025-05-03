using UnityEngine;
namespace Game
{
    [CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/ItemDefinition")]
    public class ItemSO : ScriptableObject
    {
        [Header("Идентификация")]
        public string Id;
        public GameObject Prefab;

        [Header("UI")]
        public Sprite Icon;
        public int MaxStack = 1;
        public int priority = 1;
    }
}