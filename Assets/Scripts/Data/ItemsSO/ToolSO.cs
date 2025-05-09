using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "NewTool", menuName = "Inventory/ToolDefinition")]
    public class ToolSO : ItemSO
    {
        [Header("Tool")]
        public GameObject _handModel;
        public float durability;
        public float harvestSpeed;
    }
}