using Fusion;
using UnityEngine;
using System;
using Zenject;
using Game.UI;

public sealed class InputHandler : MonoBehaviour
{
    [Inject] private Game.PlayerStatsSO _stats;

    private bool _lmbHeld;
    private InputData _lastInput;

    private float _yawAbsLocal;
    private float _pitchAbsLocal;
    private bool _anglesInitialized;

    public bool IsBlok { get; private set; }

    public event Action OnInteractPressed;
    public event Action OnReloadPressed;
    public event Action OnQuickDropPressed;
    public event Action OnPlacePressed;

    public event Action OnUseDown;
    public event Action OnUseUp;
    public event Action OnInventoryToggle;
    public event Action OnGlobalUiToggleMenu;

    void Update()
    {
        if (!Application.isPlaying) return;

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

        float x = 0f;
        float y = 0f;
        if (Input.GetKey(KeyCode.A)) x -= 1f;
        if (Input.GetKey(KeyCode.D)) x += 1f;
        if (Input.GetKey(KeyCode.W)) y += 1f;
        if (Input.GetKey(KeyCode.S)) y -= 1f;
        data.movement = new Vector2(x, y);
        if (data.movement.sqrMagnitude > 1f) data.movement = data.movement.normalized;

        data.jump = Input.GetKey(KeyCode.Space);

        float mouseDeltaX = 0f;
        float mouseDeltaY = 0f;
        float keyDeltaX = 0f;
        float keyDeltaY = 0f;

        if (!IsBlok)
        {
            mouseDeltaX = Input.GetAxisRaw("Mouse X") * _stats.mouseLookSensitivity;
            mouseDeltaY = Input.GetAxisRaw("Mouse Y") * _stats.mouseLookSensitivity;

            float dt = runner != null ? runner.DeltaTime : Time.deltaTime;
            if (Input.GetKey(KeyCode.RightArrow)) keyDeltaX += _stats.keyboardLookSensitivity * dt;
            if (Input.GetKey(KeyCode.LeftArrow))  keyDeltaX -= _stats.keyboardLookSensitivity * dt;
            if (Input.GetKey(KeyCode.UpArrow))    keyDeltaY += _stats.keyboardLookSensitivity * dt;
            if (Input.GetKey(KeyCode.DownArrow))  keyDeltaY -= _stats.keyboardLookSensitivity * dt;
        }

        data.mouseX = mouseDeltaX + keyDeltaX;
        data.mouseY = mouseDeltaY + keyDeltaY;

        if (!_anglesInitialized)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                var e = cam.transform.rotation.eulerAngles;
                float p = e.x; if (p > 180f) p -= 360f;
                _yawAbsLocal = Mathf.Repeat(e.y, 360f);
                _pitchAbsLocal = Mathf.Clamp(p, -89f, 89f);
                _anglesInitialized = true;
            }
        }

        _yawAbsLocal = Mathf.Repeat(_yawAbsLocal + data.mouseX, 360f);
        _pitchAbsLocal = Mathf.Clamp(_pitchAbsLocal - data.mouseY, -89f, 89f);

        data.yawAbs = _yawAbsLocal;
        data.pitchAbs = _pitchAbsLocal;
        data.hasAngles = 1;

        _lastInput = data;
        input.Set(data);
    }
}
