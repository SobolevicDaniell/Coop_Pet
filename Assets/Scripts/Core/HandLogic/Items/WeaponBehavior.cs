using UnityEngine;
using Fusion;

namespace Game
{
    public class WeaponBehavior : MonoBehaviour, IHandItemBehavior
    {
        WeaponSO _so;
        Transform _handPoint;
        InteractionController _ic;
        ItemState _state;

        public WeaponBehavior Construct(WeaponSO so, Transform handParent, InteractionController ic, ItemState state)
        {
            _so = so;
            _handPoint = handParent;
            _ic = ic;
            _state = state;

            if (_state.Ammo == 0)
                _state.Ammo = so.maxAmmo;

            return this;
        }

        public bool TryUseAmmo()
        {
            if (_state.Ammo <= 0) return false;
            _state.Ammo--;
            return true;
        }

        public void Reload() => _state.Ammo = _so.maxAmmo;

        public NetworkObject GetBulletNetworkObject() => _so.bulletPrefab.GetComponent<NetworkObject>();

        public float BulletSpeed => _so.bulletSpeed;
        public Vector3 MuzzlePosition => _handPoint.position;
        public Quaternion MuzzleRotation => _handPoint.rotation;
        public Vector3 MuzzleForward => _handPoint.forward;

        public void OnEquip() => _ic.RpcHandler.RPC_RequestSpawnHandModel(_so.Id);

        public void OnUnequip() => _ic.RpcHandler.RPC_RequestDespawnHandModel();

        public void OnUsePressed() => _ic.RpcHandler.RPC_RequestShoot();

        public void OnUseHeld(float d) { }
        public void OnUseReleased() { }
        public void OnMuzzleFlash() { }
    }
}
