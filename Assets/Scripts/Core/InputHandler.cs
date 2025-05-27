using Fusion;
using UnityEngine;
using System;

public class InputHandler : MonoBehaviour
{

    private InputData _networkInput;
    public bool InventoryOpen { get; private set; }

    public event Action OnInteractPressed;
    public event Action<int> OnQuickSlotPressed;
    public event Action<int> OnQuickSlotScrollDelta;
    public event Action OnReloadPressed;
    public event Action OnQuickDropPressed;
    public event Action OnPlacePressed;

    public event Action OnUseDown;
    public event Action OnUseUp;
    public event Action OnInventoryToggle;
    public event Action OnGlobalUiCloseRequested;



    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            OnInventoryToggle?.Invoke();
            OnUseUp?.Invoke();
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnGlobalUiCloseRequested?.Invoke();
        }

        if (InventoryOpen)
        {
            _networkInput = new InputData();
        }
        else
        {
            _networkInput.movement = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            _networkInput.mouseX = Input.GetAxis("Mouse X");
            _networkInput.mouseY = Input.GetAxis("Mouse Y");
            _networkInput.jump = Input.GetKey(KeyCode.Space);

            if (Input.GetKeyDown(KeyCode.E)) OnInteractPressed?.Invoke();
            if (Input.GetKeyDown(KeyCode.R)) OnReloadPressed?.Invoke();

            for (int i = 1; i <= 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                    OnQuickSlotPressed?.Invoke(i - 1);
            if (Input.GetKeyDown(KeyCode.Alpha0))
                OnQuickSlotPressed?.Invoke(9);

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                OnQuickSlotScrollDelta?.Invoke(scroll > 0 ? +1 : -1);

            if (Input.GetMouseButtonDown(0)) OnUseDown?.Invoke();
            if (Input.GetMouseButtonUp(0)) OnUseUp?.Invoke();

            if (Input.GetKeyDown(KeyCode.Q)) OnQuickDropPressed?.Invoke();
            if (Input.GetKeyDown(KeyCode.F)) OnPlacePressed?.Invoke();
        }
    }

    public void ProvideNetworkInput(NetworkRunner runner, NetworkInput input)
    {
        input.Set(_networkInput);
    }

    public void SetInventoryOpen(bool open)
    {
        InventoryOpen = open;
        // Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        // Cursor.visible = open;
    }

}
