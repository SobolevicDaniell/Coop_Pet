using Fusion;
using UnityEngine;
using System;
using Zenject;

public sealed class InputHandler : MonoBehaviour
{
    [Inject] private Game.PlayerStatsSO _stats;

    private bool _lmbHeld;

    public bool InventoryOpen { get; private set; }

    public event Action OnInteractPressed;
    public event Action OnReloadPressed;
    public event Action OnQuickDropPressed;
    public event Action OnPlacePressed;

    public event Action OnUseDown;
    public event Action OnUseUp;
    public event Action OnInventoryToggle;
    public event Action OnGlobalUiCloseRequested;

    private void Update()
    {
        if (!Application.isPlaying) return;

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            OnInventoryToggle?.Invoke();
            if (_lmbHeld) { _lmbHeld = false; OnUseUp?.Invoke(); }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnGlobalUiCloseRequested?.Invoke();
            if (_lmbHeld) { _lmbHeld = false; OnUseUp?.Invoke(); }
        }

        if (!InventoryOpen)
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

    public void ProvideNetworkInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new InputData();

        if (!InventoryOpen)
        {
            float x = 0f;
            float y = 0f;

            if (Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.W)) y += 1f;
            if (Input.GetKey(KeyCode.S)) y -= 1f;

            data.movement = new Vector2(x, y);
            if (data.movement.sqrMagnitude > 1f)
                data.movement.Normalize();

            float mouseDeltaX = Input.GetAxisRaw("Mouse X") * _stats.mouseLookSensitivity;
            float mouseDeltaY = Input.GetAxisRaw("Mouse Y") * _stats.mouseLookSensitivity;

            float dt = runner != null ? runner.DeltaTime : Time.deltaTime;
            float keyDeltaX = 0f;
            float keyDeltaY = 0f;
            if (Input.GetKey(KeyCode.RightArrow)) keyDeltaX += _stats.keyboardLookSensitivity * dt;
            if (Input.GetKey(KeyCode.LeftArrow))  keyDeltaX -= _stats.keyboardLookSensitivity * dt;
            if (Input.GetKey(KeyCode.UpArrow))    keyDeltaY += _stats.keyboardLookSensitivity * dt;
            if (Input.GetKey(KeyCode.DownArrow))  keyDeltaY -= _stats.keyboardLookSensitivity * dt;

            data.mouseX = mouseDeltaX + keyDeltaX;
            data.mouseY = mouseDeltaY + keyDeltaY;
            data.jump   = Input.GetKey(KeyCode.Space);
        }

        input.Set(data);
    }

    public void SetInventoryOpen(bool open)
    {
        if (open == InventoryOpen) return;
        InventoryOpen = open;
        if (InventoryOpen && _lmbHeld)
        {
            _lmbHeld = false;
            OnUseUp?.Invoke();
        }
    }
}
