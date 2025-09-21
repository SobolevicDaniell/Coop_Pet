using Fusion;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(NetworkCharacterController))]
    public sealed class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private NetworkCharacterController _ncc;
        [SerializeField] private Transform _cameraRoot;

        [SerializeField] private float _moveSpeed = 14f;
        [SerializeField] private float _groundAcceleration = 70f;
        [SerializeField] private float _groundFriction = 14f;
        [SerializeField] private float _airAcceleration = 42f;
        [SerializeField] private float _airDrag = 0.5f;
        [SerializeField] private float _jumpImpulse = 8f;
        [SerializeField] private float _gravity = -20f;

        private float _yaw;
        private float _pitch;
        private Vector3 _planarVelocity;
        private float _verticalVelocity;

        public override void Spawned()
        {
            if (_ncc == null) _ncc = GetComponent<NetworkCharacterController>();
            _yaw = transform.eulerAngles.y;
        }

        private void Simulate(InputData input, float dt)
        {
            _yaw += input.mouseX;
            _yaw = Mathf.Repeat(_yaw, 360f);
            var rotY = Quaternion.Euler(0f, _yaw, 0f);

            var dirLocal = new Vector3(input.movement.x, 0f, input.movement.y);
            if (dirLocal.sqrMagnitude > 1f) dirLocal.Normalize();
            var desiredPlanar = rotY * dirLocal * _moveSpeed;

            bool grounded = _ncc.Grounded;

            if (grounded)
            {
                _planarVelocity = Vector3.MoveTowards(_planarVelocity, desiredPlanar, _groundAcceleration * dt);
                if (dirLocal.sqrMagnitude < 0.0001f)
                    _planarVelocity = Vector3.MoveTowards(_planarVelocity, Vector3.zero, _groundFriction * dt);
                if (input.jump)
                    _verticalVelocity = _jumpImpulse;
            }
            else
            {
                _planarVelocity = Vector3.MoveTowards(_planarVelocity, desiredPlanar, _airAcceleration * dt);
                _planarVelocity *= 1f / (1f + _airDrag * dt);
            }

            _verticalVelocity += _gravity * dt;

            var delta = (_planarVelocity + Vector3.up * _verticalVelocity) * dt;
            _ncc.MoveRaw(delta, rotY);


            if (_ncc.Grounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;

            _pitch -= input.mouseY;
            _pitch = Mathf.Clamp(_pitch, -90f, 90f);
            if (_cameraRoot != null)
                _cameraRoot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        public override void FixedUpdateNetwork()
        {
            if (GetInput(out InputData input))
                Simulate(input, Runner.DeltaTime);
        }
    }
}