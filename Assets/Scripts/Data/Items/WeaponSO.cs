using Fusion;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "Inventory/WeaponDefinition")]
    public class WeaponSO : ItemSO
    {
        [Header("Weapon")]
        public NetworkObject _handModelNetwork;
        [Range(1, 200)]
        public int maxAmmo;
        public bool isAutomatic;
        [Range((float)1, 15)]
        public float fireRate;
        public GameObject bulletPrefab;
        public float bulletSpeed;
        public float bulletDamage;

        [Header("Ammo")]
        public ResourceSO ammoResource;
    }
}