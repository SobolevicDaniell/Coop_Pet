using UnityEngine;
using Fusion;

namespace Game
{
    public class WeaponBehavior : MonoBehaviour, IHandItemBehavior
    {
        WeaponSO _so;
        Transform _handPoint;
        InteractionController _ic;
        int _ammo;

        public WeaponBehavior Construct(WeaponSO so, Transform handParent, InteractionController ic)
        {
            _so = so;
            _handPoint = handParent;
            _ic = ic;
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
        public Vector3 MuzzlePosition => _handPoint.position;
        public Quaternion MuzzleRotation => _handPoint.rotation;
        public Vector3 MuzzleForward => _handPoint.forward;

        public void OnEquip()
        {
            _ic.RpcHandler.RPC_RequestSpawnHandModel(_so.Id);
        }
        public void OnUnequip()
        {
            _ic.RpcHandler.RPC_RequestDespawnHandModel();
        }
        public void OnUsePressed()
        {
            _ic.RpcHandler.RPC_RequestShoot();
        }
        public void OnUseHeld(float d) { }
        public void OnUseReleased() { }
        public void OnMuzzleFlash() { /* VFX/SFX */ }
    }
}
