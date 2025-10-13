using Fusion;
using UnityEngine;
using System;
using Zenject;
using Game.UI;
using Game.Settings;

namespace Game
{
    public sealed class InputHandler : MonoBehaviour
    {
        [Inject] private PlayerStatsSO _stats;
        [Inject] private ISettingsService _settings;

        private bool _lmbHeld;
        private InputData _lastInput;

        private Transform _localRotationRoot;
        private Transform _localCameraRoot;

        public float YawLocal { get; private set; }
        public float PitchLocal { get; private set; }

        public bool IsBlok { get; private set; }

        public event Action OnInteractPressed;
        public event Action OnReloadPressed;
        public event Action OnQuickDropPressed;
        public event Action OnPlacePressed;

        public event Action OnUseDown;
        public event Action OnUseUp;
        public event Action OnInventoryToggle;
        public event Action OnGlobalUiToggleMenu;

        public void BindLocalAvatar(Transform rotationRoot, Transform cameraRoot)
        {
            _localRotationRoot = rotationRoot;
            _localCameraRoot = cameraRoot;

            YawLocal = _localRotationRoot != null ? _localRotationRoot.eulerAngles.y : 0f;

            float p = 0f;
            if (_localCameraRoot != null)
            {
                p = _localCameraRoot.localEulerAngles.x;
                if (p > 180f) p -= 360f;
            }
            PitchLocal = Mathf.Clamp(p, -89f, 89f);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                OnInventoryToggle?.Invoke();
                if (_lmbHeld) { _lmbHeld = false; OnUseUp?.Invoke(); }
            }

            if (Input.GetKeyDown(KeyCode.Escape))
                OnGlobalUiToggleMenu?.Invoke();

            if (!IsBlok)
            {
                if (Input.GetKeyDown(KeyCode.E)) OnInteractPressed?.Invoke();
                if (Input.GetKeyDown(KeyCode.R)) OnReloadPressed?.Invoke();

                if (Input.GetMouseButtonDown(0))
                {
                    if (!_lmbHeld) { _lmbHeld = true; OnUseDown?.Invoke(); }
                }
                if (Input.GetMouseButtonUp(0))
                {
                    if (_lmbHeld) { _lmbHeld = false; OnUseUp?.Invoke(); }
                }

                if (Input.GetKeyDown(KeyCode.Q)) OnQuickDropPressed?.Invoke();
                if (Input.GetKeyDown(KeyCode.F)) OnPlacePressed?.Invoke();
            }
        }

        public void BlockLook(UiPhase phase)
        {
            bool block = phase == UiPhase.Inventory || phase == UiPhase.OtherInventory || phase == UiPhase.Menu || phase == UiPhase.Exit;
            if (block == IsBlok) return;
            IsBlok = block;
            if (IsBlok && _lmbHeld)
            {
                _lmbHeld = false;
                OnUseUp?.Invoke();
            }
        }

        public InputData GetLastInputData() => _lastInput;

        public void ProvideNetworkInput(NetworkRunner runner, NetworkInput input)
        {
            var data = new InputData();

            float x = 0f, y = 0f;
            if (Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.W)) y += 1f;
            if (Input.GetKey(KeyCode.S)) y -= 1f;
            data.movement = new Vector2(x, y);
            if (data.movement.sqrMagnitude > 1f) data.movement.Normalize();

            data.jump = Input.GetKey(KeyCode.Space);

            float mouseDeltaX = 0f, mouseDeltaY = 0f;
            if (!IsBlok)
            {
                mouseDeltaX = Input.GetAxisRaw("Mouse X") * _settings.MouseSensitivity;
                mouseDeltaY = Input.GetAxisRaw("Mouse Y") * _settings.MouseSensitivity;

                float dt = runner != null ? runner.DeltaTime : Time.deltaTime;
                if (Input.GetKey(KeyCode.RightArrow)) mouseDeltaX += _stats.keyboardLookSensitivity * dt;
                if (Input.GetKey(KeyCode.LeftArrow)) mouseDeltaX -= _stats.keyboardLookSensitivity * dt;
                if (Input.GetKey(KeyCode.UpArrow)) mouseDeltaY += _stats.keyboardLookSensitivity * dt;
                if (Input.GetKey(KeyCode.DownArrow)) mouseDeltaY -= _stats.keyboardLookSensitivity * dt;

                YawLocal = Mathf.Repeat(YawLocal + mouseDeltaX, 360f);
                PitchLocal = Mathf.Clamp(PitchLocal - mouseDeltaY, -89f, 89f);
            }

            data.mouseX = mouseDeltaX;
            data.mouseY = mouseDeltaY;

            data.yawAbs = YawLocal;
            data.pitchAbs = PitchLocal;
            data.hasAngles = 1;

            _lastInput = data;
            input.Set(data);
        }
    }
}