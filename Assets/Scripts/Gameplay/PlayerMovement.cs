// Assets/Scripts/Game/PlayerMovement.cs
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private CharacterController _controller;
        [SerializeField] private Transform _cameraRoot;
        [Header("Settings")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _mouseSensitivity = 2f;
        [SerializeField] private float _gravity = -9.81f;
        [SerializeField] private float _jumpForce = 5f;

        private Vector3 _velocity;
        private float _xRotation;

        public void HandleInput(InputData input, float deltaTime)
        {
            // Перемещение
            Vector3 move = transform.right * input.movement.x + transform.forward * input.movement.y;
            _controller.Move(move * _moveSpeed * deltaTime);

            // Прыжок и гравитация
            if (_controller.isGrounded && _velocity.y < 0) _velocity.y = -2f;
            if (input.jump && _controller.isGrounded) _velocity.y = Mathf.Sqrt(_jumpForce * -2f * _gravity);
            _velocity.y += _gravity * deltaTime;
            _controller.Move(_velocity * deltaTime);

            // Поворот камеры и персонажа
            _xRotation -= input.mouseY * _mouseSensitivity * deltaTime;
            _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);
            _cameraRoot.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * input.mouseX * _mouseSensitivity * deltaTime);
        }
    }
}
