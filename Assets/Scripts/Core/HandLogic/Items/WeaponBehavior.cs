using UnityEngine;
using Fusion;

namespace Game
{
    public class WeaponBehavior : MonoBehaviour, IHandItemBehavior
    {
        private WeaponSO _so;
        private Transform _handPoint;
        private InteractionController _ic;
        private ItemState _state;
        private Transform _muzzlePoint;

        private float _autoFireTimer = 0f;
        private bool _isHolding = false;

        public WeaponBehavior Construct(WeaponSO so, Transform handParent, InteractionController ic, ItemState state)
        {
            _so = so;
            _handPoint = handParent;
            _ic = ic;
            _state = state;

            // Оружие появляется без патронов!
            _state.Ammo = 0;

            var netObj = _ic.GetHandModelNetworkInstance();
            if (netObj != null)
                _muzzlePoint = netObj.transform.GetComponentInChildren<MuzzlePoint>()?.transform;

            return this;
        }

        public bool TryUseAmmo()
        {
            if (_state.Ammo <= 0) return false;
            _state.Ammo--;
            // (!) Тут можно отправлять обновление UI, если нужно.
            return true;
        }

        // Главное: перезарядка тратит ресурс-патроны из инвентаря/квикслота
        public void Reload()
        {
            if (_so.ammoResource == null) return;
            var inventory = _ic.Inventory;

            int need = _so.maxAmmo - _state.Ammo;
            if (need <= 0) return; // уже полон

            // Получаем всего сколько есть (например, если мало патронов)
            int available = inventory.GetResourceCount(_so.ammoResource.Id);
            int toLoad = Mathf.Min(need, available);

            if (toLoad > 0)
            {
                if (inventory.SpendResource(_so.ammoResource.Id, toLoad))
                {
                    _state.Ammo += toLoad;
                }
                // else: если не хватило — может часть перезарядить, см above
            }
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
            // else: Можно воспроизвести звук "нет патронов"
        }

        public void OnEquip() => _ic.RpcHandler.RPC_RequestSpawnHandModel(_so.Id);
        public void OnUnequip() => _ic.RpcHandler.RPC_RequestDespawnHandModel();
        public void OnMuzzleFlash() { /* vfx */ }
    }
}
