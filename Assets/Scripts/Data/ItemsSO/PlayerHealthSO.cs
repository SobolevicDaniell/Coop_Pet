using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "NewPlayerHealth", menuName = "Game/PlayerHealthDefinition")]
    public class PlayerHealthSO : ScriptableObject
    {
        public int health = 100;
    }
}
