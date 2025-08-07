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

        [Networked] private Vector3 NetworkedPosition { get; set; }
        [Networked] private Quaternion NetworkedRotation { get; set; }

        private Vector3 _velocity;
        private float _xRotation;

        public override void Spawned()
        {
            NetworkedPosition = transform.position;
            NetworkedRotation = transform.rotation;
        }

        public override void FixedUpdateNetwork()
        {
            if (GetInput(out InputData input))
            {
                HandleInput(input, Runner.DeltaTime);
            }

            if (Object.HasStateAuthority)
            {
                NetworkedPosition = transform.position;
                NetworkedRotation = transform.rotation;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, NetworkedPosition, Runner.DeltaTime * 15);
                transform.rotation = Quaternion.Slerp(transform.rotation, NetworkedRotation, Runner.DeltaTime * 15);
            }
        }

        public void HandleInput(InputData input, float deltaTime)
        {
            Vector3 move = transform.right * input.movement.x + transform.forward * input.movement.y;
            _controller.Move(move * _moveSpeed * deltaTime);

            if (_controller.isGrounded && _velocity.y < 0)
                _velocity.y = -2f;
            if (input.jump && _controller.isGrounded)
                _velocity.y = Mathf.Sqrt(_jumpForce * -2f * _gravity);

            _velocity.y += _gravity * deltaTime;
            _controller.Move(_velocity * deltaTime);

            _xRotation -= input.mouseY * _mouseSensitivity * deltaTime;
            _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);
            _cameraRoot.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * input.mouseX * _mouseSensitivity * deltaTime);
        }
    }
}
