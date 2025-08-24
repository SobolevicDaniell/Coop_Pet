using Fusion;
using UnityEngine;

namespace Game
{
    public sealed class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private CharacterController _controller;
        [SerializeField] private Transform _cameraRoot;
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _mouseSensitivity = 2f;
        [SerializeField] private float _gravity = -9.81f;
        [SerializeField] private float _jumpForce = 5f;

        [Header("Ground Check")]
        [SerializeField] private Transform _groundCheck;
        [SerializeField] private float _groundRadius = 0.25f;
        [SerializeField] private LayerMask _groundMask = ~0;

        private Vector3 _velocity;   // ось Y для гравитации/прыжка
        private float _xRotation;

        // Вызывается из NetworkPlayer один раз за тик (и на StateAuthority, и на InputAuthority)
        public void HandleInput(InputData input, float dt)
        {
            Vector3 move = (transform.right * input.movement.x + transform.forward * input.movement.y) * _moveSpeed;

            bool grounded =
                _groundCheck != null
                ? Physics.CheckSphere(_groundCheck.position, _groundRadius, _groundMask, QueryTriggerInteraction.Ignore)
                : _controller.isGrounded;

            if (grounded && _velocity.y < 0f) _velocity.y = -2f;
            if (input.jump && grounded) _velocity.y = Mathf.Sqrt(_jumpForce * -2f * _gravity);
            _velocity.y += _gravity * dt;

            _controller.Move(new Vector3(move.x, _velocity.y, move.z) * dt);

            _xRotation -= input.mouseY * _mouseSensitivity * dt;
            _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);
            if (_cameraRoot != null) _cameraRoot.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * input.mouseX * _mouseSensitivity * dt);
        }

        public override void FixedUpdateNetwork() { }

    }
}