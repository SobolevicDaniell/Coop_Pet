using UnityEngine;

namespace Game
{
    [CreateAssetMenu(menuName = "Game/PlayerStatsSO")]
    public class PlayerStatsSO : ScriptableObject
    {
        public int maxHealth = 100;
        public int inventorySlotsCount = 24;
        public int quickSlotsCount = 10;

        public float keyboardLookSensitivity = 2f;
        public float mouseLookSensitivity = 10f;
    }
}
