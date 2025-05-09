using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "Inventory/WeaponDefinition")]
    public class WeaponSO : ItemSO
    {
        [Header("Weapon")]
        public GameObject _handModel;
        [Range(1, 100)]
        public int maxAmmo;
        [Range((float)0.1, 5)]
        public float fireRate;
        public GameObject bulletPrefab;
        public float bulletSpeed;
    }
}