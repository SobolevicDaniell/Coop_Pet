// Assets/Scripts/Gameplay/Bullet.cs
using Fusion;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(Rigidbody), typeof(NetworkObject))]
    public class Bullet : NetworkBehaviour
    {
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private float drag = 0f;

        private Rigidbody _rigidbody;
        private TickTimer _timer;

        public override void Spawned()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.linearDamping = drag;

            _timer = TickTimer.CreateFromSeconds(Runner, lifetime);
        }

        public override void FixedUpdateNetwork()
        {
            if (Object.HasStateAuthority && _timer.Expired(Runner))
            {
                Runner.Despawn(Object);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!Runner.IsServer)
                return;

            //Runner.Despawn(Object);
        }

        
        public void InitializeVelocity(Vector3 velocity)
        {
            if (_rigidbody == null)
                _rigidbody = GetComponent<Rigidbody>();

            _rigidbody.linearVelocity = velocity;
        }
    }
}