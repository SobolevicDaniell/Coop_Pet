using Fusion;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "Inventory/WeaponDefinition")]
    public class WeaponSO : ItemSO, IHandModelProvider
    {
        [Header("Weapon")]
        public NetworkObject HandModelNetwork;
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

        NetworkObject IHandModelProvider.HandModelNetwork => HandModelNetwork;
    }
}