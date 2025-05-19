using UnityEngine;

namespace Game
{
    [CreateAssetMenu(menuName = "Game/Placeable Item SO")]
    public class PlaceableItemSO : ItemSO
    {
        [Header("Prefab для размещения в мире (стационарный)")]
        public GameObject PlaceablePrefab;

        [Header("Настройки размещения")]
        public float PlaceDistance = 4f;
    }
}
