using Fusion;
using UnityEngine;
using Zenject;
using Game.Network;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerHealthServer : NetworkBehaviour
    {
        [SerializeField] private PlayerStatsSO _statsSerialized;
        [Inject(Optional = true)] private PlayerStatsSO _statsDI;
        [Inject] private PlayerSpawner _spawner;

        [Networked] public int Current { get; private set; }
        [Networked] public int Max { get; private set; }
        [Networked] public NetworkBool IsDead { get; private set; }
        [Networked] private TickTimer _despawnDelay { get; set; }

        public override void Spawned()
        {
            if (!Object.HasStateAuthority) return;
            var so = _statsSerialized != null ? _statsSerialized : _statsDI;
            Max = so != null ? Mathf.Max(1, so.maxHealth) : 100;
            Current = Max;
            IsDead = false;
            _despawnDelay = TickTimer.None;
            _spawner.RegisterAvatar(Object.InputAuthority, gameObject);
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;
            if (IsDead && _despawnDelay.Expired(Runner))
            {
                var pos = transform.position;
                var rot = transform.rotation;
                var owner = Object.InputAuthority;
                _spawner.SpawnDeathBox(Runner, owner, pos, rot);
                _spawner.DespawnAvatar(Runner, owner, Object);
                _despawnDelay = TickTimer.None;
            }
        }

        public void ApplyDamage(int amount)
        {
            if (!Object.HasStateAuthority || amount <= 0 || IsDead) return;
            Current = Mathf.Max(0, Current - amount);
            if (Current == 0)
            {
                IsDead = true;
                _despawnDelay = TickTimer.CreateFromSeconds(Runner, 0.25f);

                var owner = Object.InputAuthority;
                var po = Runner.GetPlayerObject(owner);
                if (po != null)
                {
                    var proxy = po.GetComponent<PlayerObject>();
                    if (proxy != null) proxy.RPC_ShowDeath();
                }
            }
        }

        public void ApplyHeal(int amount)
        {
            if (!Object.HasStateAuthority || amount <= 0 || IsDead) return;
            Current = Mathf.Min(Max, Current + amount);
        }
    }
}
