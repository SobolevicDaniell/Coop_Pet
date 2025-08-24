using Fusion;
using UnityEngine;
using System;

public class InputHandler : MonoBehaviour
{
    [SerializeField] private float keyboardLookSensitivity;
    [SerializeField] private float mouseLookSensitivity;

    private InputData _networkInput;
    private bool _lmbHeld;                       // изменено: трекинг удержания ЛКМ

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
        if (!Application.isPlaying) return;      // изменено: защита в редакторе

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            OnInventoryToggle?.Invoke();
            if (_lmbHeld) { _lmbHeld = false; OnUseUp?.Invoke(); } // изменено: сброс удержания
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnGlobalUiCloseRequested?.Invoke();
            if (_lmbHeld) { _lmbHeld = false; OnUseUp?.Invoke(); } // изменено: сброс удержания
        }

        if (InventoryOpen)
        {
            _networkInput = new InputData();
            return;                                // изменено: не обрабатываем остальные нажатия
        }

        _networkInput.movement = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

        float mouseX = Input.GetAxis("Mouse X") * mouseLookSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseLookSensitivity;

        float keyboardX = 0f;
        float keyboardY = 0f;

        if (Input.GetKey(KeyCode.RightArrow)) keyboardX += keyboardLookSensitivity * Time.deltaTime;
        if (Input.GetKey(KeyCode.LeftArrow))  keyboardX -= keyboardLookSensitivity * Time.deltaTime;
        if (Input.GetKey(KeyCode.UpArrow))    keyboardY += keyboardLookSensitivity * Time.deltaTime;
        if (Input.GetKey(KeyCode.DownArrow))  keyboardY -= keyboardLookSensitivity * Time.deltaTime;

        _networkInput.mouseX = mouseX + keyboardX;
        _networkInput.mouseY = mouseY + keyboardY;

        _networkInput.jump = Input.GetKey(KeyCode.Space);

        if (Input.GetKeyDown(KeyCode.E)) OnInteractPressed?.Invoke();
        if (Input.GetKeyDown(KeyCode.R)) OnReloadPressed?.Invoke();

        if (Input.GetMouseButtonDown(0))
        {
            if (!_lmbHeld) { _lmbHeld = true; OnUseDown?.Invoke(); } // изменено
        }
        if (Input.GetMouseButtonUp(0))
        {
            if (_lmbHeld) { _lmbHeld = false; OnUseUp?.Invoke(); }   // изменено
        }

        if (Input.GetKeyDown(KeyCode.Q)) OnQuickDropPressed?.Invoke();
        if (Input.GetKeyDown(KeyCode.F)) OnPlacePressed?.Invoke();
    }

    public void ProvideNetworkInput(NetworkRunner runner, NetworkInput input)
    {
        input.Set(_networkInput);
    }

    public void SetInventoryOpen(bool open)
    {
        if (open == InventoryOpen) return;        // изменено: защита от лишних вызовов
        InventoryOpen = open;
        if (InventoryOpen && _lmbHeld)            // изменено: отпускание ЛКМ при открытии UI
        {
            _lmbHeld = false;
            OnUseUp?.Invoke();
        }
    }
}
