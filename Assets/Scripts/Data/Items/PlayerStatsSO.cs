using UnityEngine;

namespace Game
{
    [CreateAssetMenu(menuName = "Game/PlayerStatsSO")]
    public class PlayerStatsSO : ScriptableObject
    {
        [Header("Health")]
        public int maxHealth = 100;

        [Header("Inventory")]
        public int inventorySlotsCount = 24;
        public int quickSlotsCount = 10;


        [Header("Look")]
        public float keyboardLookSensitivity = 2f;
        public float mouseLookSensitivity = 10f;

        [Header("Movement")]
        public float moveSpeed = 14f;
        public float groundAcceleration = 70f;
        public float groundFriction = 14f;
        public float airAcceleration = 42f;
        public float airDrag = 0.5f;
        public float jumpImpulse = 8f;
        public float gravity = -20f;
        public float groundedCoyoteTime = 0.08f;
    }
}
