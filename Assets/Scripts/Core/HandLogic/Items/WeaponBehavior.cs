// Assets/Scripts/Gameplay/WeaponBehavior.cs
using UnityEngine;
using Fusion;

namespace Game
{
    public class WeaponBehavior : MonoBehaviour, IHandItemBehavior
    {
        private WeaponSO _so;
        private Transform _handPoint;
        private InteractionController _interactionController;
        private GameObject _instance;
        private int _ammo;

        public WeaponBehavior Construct(WeaponSO so, Transform handParent, InteractionController ic)
        {
            _so = so;
            _handPoint = handParent;
            _interactionController = ic;
            _ammo = so.maxAmmo;
            return this;
        }

        public bool TryUseAmmo()
        {
            if (_ammo <= 0) return false;
            _ammo--;
            return true;
        }

        public NetworkObject GetBulletNetworkObject() =>
            _so.bulletPrefab.GetComponent<NetworkObject>();

        public float BulletSpeed => _so.bulletSpeed;
        public Vector3 MuzzlePosition => _instance.transform.position;
        public Quaternion MuzzleRotation => _instance.transform.rotation;
        public Vector3 MuzzleForward => _instance.transform.forward;

        public void OnEquip()
        {
            _instance = Instantiate(_so._handModel, _handPoint);
            _instance.transform.localPosition = Vector3.zero;
            _instance.transform.localRotation = Quaternion.identity;
        }

        public void OnUnequip()
        {
            if (_instance != null) Destroy(_instance);
        }

        public void OnUsePressed()
        {
            _interactionController.RPC_RequestShoot();
        }

        public void OnUseHeld(float delta) { }
        public void OnUseReleased() { }

        public void OnMuzzleFlash()
        {
            // VFX/звук
        }
    }
}
