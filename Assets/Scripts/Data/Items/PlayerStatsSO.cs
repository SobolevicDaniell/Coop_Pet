using UnityEngine;

namespace Game
{
    [CreateAssetMenu(menuName = "Game/PlayerStatsSO")]
    public class PlayerStatsSO : ScriptableObject
    {
        public int health = 100;
        public float speed = 5f;
        public int inventorySlotsCount = 24;
        public int quickSlotsCount = 10;
    }
}
