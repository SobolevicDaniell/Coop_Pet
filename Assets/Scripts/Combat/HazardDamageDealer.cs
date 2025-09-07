using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(Collider))]
    public sealed class HazardDamageDealer : MonoBehaviour
    {
        [SerializeField] private int       _damage      = 10;
        [SerializeField] private DamageKind _kind       = DamageKind.Generic;
        [SerializeField] private LayerMask _targetMask  = 0;   // назначь Player
        [SerializeField] private bool      _useTriggers = true;
        [SerializeField] private float     _cooldown    = 0.2f; // анти-спам на одного и того же

        // Если объект НЕ сетевой — смотрим на Runner и бьём только на сервере
        [Inject(Optional = true)] private NetworkRunner _runner;

        private readonly Dictionary<int, float> _nextHitTimeByTarget = new(); // key: target InstanceID

        private bool IsServerAuthority()
        {
            // сетевой опасный объект
            var no = GetComponent<NetworkObject>();
            if (no != null)
                return no.HasStateAuthority;

            // несетевой — используем Runner
            return _runner != null && _runner.IsServer;
        }

        private bool IsTarget(Collider other)
        {
            return ((_targetMask.value & (1 << other.gameObject.layer)) != 0);
        }

        private void TryHit(Collider other, Vector3 hitPoint, Vector3 dir)
        {
            if (!IsServerAuthority()) return;
            if (!IsTarget(other)) return;

            var id = other.GetInstanceID();
            var now = Time.time;
            if (_nextHitTimeByTarget.TryGetValue(id, out var t) && now < t)
                return;

            _nextHitTimeByTarget[id] = now + _cooldown;

            if (other.TryGetComponent<IDamageable>(out var dmg))
            {
                var src = PlayerRef.None; // при необходимости подставь владельца ловушки
                var info = new DamageInfo(_damage, _kind, hitPoint, dir, src);
                dmg.ApplyDamage(info);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_useTriggers) return;
            TryHit(other, other.ClosestPoint(transform.position), (other.transform.position - transform.position).normalized);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!_useTriggers) return;
            TryHit(other, other.ClosestPoint(transform.position), (other.transform.position - transform.position).normalized);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_useTriggers) return;
            var contact = collision.GetContact(0);
            TryHit(collision.collider, contact.point, contact.normal);
        }
    }
}
