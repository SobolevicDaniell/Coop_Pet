using Fusion;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public class HealthComponent : NetworkBehaviour
    {
        private UIHealthView _ui;
        [SerializeField] private PlayerStatsSO _playerStatsSO;

        [Networked] public int CurrentHealth { get; set; }
        private int _lastHealth;
        private bool _initialized;

        public void Initialize(UIHealthView ui, bool hasStateAuthority, bool hasInputAuthority)
        {
            _ui = ui;
            _initialized = true;

            if (hasStateAuthority)
                CurrentHealth = _playerStatsSO.health;

            if (hasInputAuthority && _ui != null)
            {
                _ui.SetMaxHealth(_playerStatsSO.health);
                _ui.UpdateHealth(CurrentHealth);
                _lastHealth = CurrentHealth;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!_initialized || _ui == null) return;

            if (Object.HasInputAuthority)
            {
                if (_lastHealth != CurrentHealth)
                {
                    _ui.UpdateHealth(CurrentHealth);
                    _lastHealth = CurrentHealth;

                    if (CurrentHealth == 0)
                        _ui.ShowDeath();
                }
            }
        }

        public void TakeDamage(int damage)
        {
            if (!Object.HasStateAuthority) return;
            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        }
    }
}
