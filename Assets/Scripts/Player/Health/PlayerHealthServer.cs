using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerHealthServer : NetworkBehaviour
    {
        [SerializeField] private PlayerStatsSO _statsSerialized;      // резерв из инспектора
        [Inject(Optional = true)] private PlayerStatsSO _statsDI;     // DI-источник

        [Networked] public int Current { get; private set; }
        [Networked] public int Max     { get; private set; }

        public override void Spawned()
        {
            if (!Object.HasStateAuthority) return;

            var so  = _statsSerialized != null ? _statsSerialized : _statsDI;
            var max = so != null ? Mathf.Max(1, so.maxHealth) : 100; // замени на точное поле в твоём SO

            Max     = max;
            Current = Max;
        }

        // Только сервер меняет здоровье
        public void ApplyDamage(int amount)
        {
            if (!Object.HasStateAuthority || amount <= 0) return;
            Current = Mathf.Max(0, Current - amount);
        }

        public void ApplyHeal(int amount)
        {
            if (!Object.HasStateAuthority || amount <= 0) return;
            Current = Mathf.Min(Max, Current + amount);
        }
    }
}
