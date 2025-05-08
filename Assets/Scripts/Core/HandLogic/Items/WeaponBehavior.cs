using UnityEngine;

namespace Game
{
    public class WeaponBehavior : MonoBehaviour, IHandItemBehavior
    {
        private WeaponSO _so;
        private float _cooldown;

        public WeaponBehavior Construct(WeaponSO so)
        {
            _so = so;
            return this;
        }

        public void OnEquip()
        {
            _cooldown = 0f;
            // сюда можно запустить анимацию «поднять оружие»
        }

        public void OnUnequip()
        {
            // «опустить оружие»
            Destroy(gameObject);
        }

        public void OnUsePressed()
        {
            TryFire();
        }

        public void OnUseHeld(float delta)
        {
            _cooldown -= delta;
            if (_cooldown <= 0f)
                TryFire();
        }

        public void OnUseReleased()
        {
            // для полуавтомата ничего
        }

        private void TryFire()
        {
            if (_so.maxAmmo <= 0) return;
            // 1) создаем пулю
            Instantiate(_so.bulletPrefab, transform.position, transform.rotation);
            // 2) обновляем обойму
            _so.maxAmmo--;
            _cooldown = 1f / _so.fireRate;
            // 3) тут можно запустить звук и отдачу камеры и UI‑обновление патронов
        }
    }
}
