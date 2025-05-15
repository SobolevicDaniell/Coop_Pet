// Assets/Scripts/Gameplay/PlayerHealth.cs
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public class HealthComponent : NetworkBehaviour
    {
        [Inject] private PlayerHealthSO _healthSO;
        [Inject] private UIHealthView _ui;

        // Сетевое текущее здоровье
        [Networked] public int CurrentHealth { get; set; }

        // Для локального клиента — хранить предыдущее значение
        private int _lastHealth;

        public override void Spawned()
        {
            // На сервере инициализируем здоровье
            if (Object.HasStateAuthority)
            {
                CurrentHealth = _healthSO.health;
            }

            // У клиента на локальном игроке настраиваем UI
            if (Object.HasInputAuthority)
            {
                _ui.SetMaxHealth(_healthSO.health);
                _ui.UpdateHealth(CurrentHealth);
                _lastHealth = CurrentHealth;
            }
        }

        public override void FixedUpdateNetwork()
        {
            // Только для локального игрока обновляем UI, когда здоровье меняется
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

        // Вызываем на сервере, чтобы нанести урон
        public void TakeDamage(int damage)
        {
            if (!Object.HasStateAuthority) return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);

            // Можно здесь сразу отправить RPC_OnDeath, 
            // но мы обрабатываем смерть в FixedUpdateNetwork на клиенте
        }
    }
}
