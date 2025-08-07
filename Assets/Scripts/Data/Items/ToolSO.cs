using Fusion;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "NewTool", menuName = "Inventory/ToolDefinition")]
    public class ToolSO : ItemSO
    {
        [Header("Tool")]
        public NetworkObject HandModelNetwork;
        public float durability;
        public float harvestSpeed;
    }
}