using UnityEngine;
using Fusion;

namespace Game
{
    public class WeaponBehavior : MonoBehaviour, IHandItemBehavior
    {
        private WeaponSO _so;
        private Transform _handPoint;
        private InteractionController _ic;
        private InventorySlot _slot;
        private Transform _muzzlePoint;

        private float _autoFireTimer = 0f;
        private bool _isHolding = false;

        public WeaponBehavior Construct(WeaponSO so, Transform handParent, InteractionController ic, InventorySlot slot)
        {
            _so = so;
            _handPoint = handParent;
            _ic = ic;
            _slot = slot;

            // !!! Не сбрасываем Ammo на 0, иначе потеряешь заряд при переключении!
            // При первом появлении оружия (TryQuick/TryInventory) — State создаётся пустым, Ammo = 0.
            // Пользователь должен перезарядить.
            var netObj = _ic.GetHandModelNetworkInstance();
            if (netObj != null)
                _muzzlePoint = netObj.transform.GetComponentInChildren<MuzzlePoint>()?.transform;
            return this;
        }

        private ItemState State => _slot?.State;

        public bool TryUseAmmo()
        {
            if (State == null || State.Ammo <= 0) return false;
            State.Ammo--;
            _ic.inventory.RaiseQuickSlotsChanged();
            return true;
        }

        public void Reload()
        {
            if (_so.ammoResource == null || State == null) return;
            var inventory = _ic.inventory;
            int need = _so.maxAmmo - State.Ammo;
            if (need <= 0) return;
            int available = inventory.GetResourceCount(_so.ammoResource.Id);
            int toLoad = Mathf.Min(need, available);
            if (toLoad > 0)
            {
                if (inventory.SpendResource(_so.ammoResource.Id, toLoad))
                {
                    State.Ammo += toLoad;
                }
            }
            _ic.inventory.RaiseQuickSlotsChanged();
        }

        public NetworkObject GetBulletNetworkObject() => _so.bulletPrefab.GetComponent<NetworkObject>();
        public float BulletDamage => _so.bulletDamage;
        public float BulletSpeed => _so.bulletSpeed;
        public Vector3 MuzzlePosition => (_muzzlePoint != null) ? _muzzlePoint.position : _handPoint.position;
        public Quaternion MuzzleRotation => (_muzzlePoint != null) ? _muzzlePoint.rotation : _handPoint.rotation;
        public Vector3 MuzzleForward => (_muzzlePoint != null) ? _muzzlePoint.forward : _handPoint.forward;

        public void OnUsePressed()
        {
            _isHolding = true;
            _autoFireTimer = 0f;
            TryShoot();
        }

        public void OnUseReleased()
        {
            _isHolding = false;
            _autoFireTimer = 0f;
        }

        public void OnUseHeld(float delta)
        {
            if (!_so.isAutomatic || !_isHolding) return;
            _autoFireTimer += delta;
            float interval = 1f / _so.fireRate;
            while (_autoFireTimer >= interval)
            {
                _autoFireTimer -= interval;
                TryShoot();
            }
        }

        private void TryShoot()
        {
            if (TryUseAmmo())
                _ic.RpcHandler.RPC_RequestShoot();
            // else: звук "нет патронов"
        }

        public void OnEquip() => _ic.RpcHandler.RPC_RequestSpawnHandModel(_so.Id);
        public void OnUnequip() => _ic.RpcHandler.RPC_RequestDespawnHandModel();
        public void OnMuzzleFlash() { /* vfx */ }
    }
}
