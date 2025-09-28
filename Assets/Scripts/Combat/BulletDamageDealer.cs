using Fusion;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(Collider))]
    public sealed class BulletDamageDealer : NetworkBehaviour
    {
        [SerializeField] private LayerMask _targetMask;
        [SerializeField] private bool _useTriggers = false;
        [SerializeField] private DamageKind _kind = DamageKind.Bullet;

        private int _damage = 1;
        public PlayerRef Source { get; set; } = PlayerRef.None;

        public override void Spawned()
        {
            if (Source == PlayerRef.None && Object != null)
                Source = Object.InputAuthority;
        }

        public void Configure(int damage, PlayerRef source)
        {
            _damage = Mathf.Max(0, damage);
            Source = source;
        }

        public void ApplyInitialPhysics(float mass, Vector3 velocity)
        {
            if (TryGetComponent<Rigidbody>(out var rb))
            {
                if (mass > 0f) rb.mass = mass;
                rb.linearVelocity = velocity;
            }
        }

        private bool IsTarget(Collider other) =>
            ((_targetMask.value & (1 << other.gameObject.layer)) != 0);

        private void HitAndDespawn(Collider other, Vector3 point, Vector3 dir)
        {
            if (!Object.HasStateAuthority) return;

            int amount = _damage;
            var b = GetComponentInParent<Bullet>();
            if (b == null) b = GetComponentInChildren<Bullet>();
            if (b != null) amount = b.Damage;

            if (IsTarget(other) && other.TryGetComponent<IDamageable>(out var dmg))
                dmg.ApplyDamage(new DamageInfo(amount, _kind, point, dir, Source));

            if (Runner != null && Object != null) Runner.Despawn(Object);
            else Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_useTriggers) return;
            var pt  = other.ClosestPoint(transform.position);
            var dir = (other.transform.position - transform.position).normalized;
            HitAndDespawn(other, pt, dir);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_useTriggers) return;
            var c = collision.GetContact(0);
            HitAndDespawn(collision.collider, c.point, c.normal);
        }
    }
}
