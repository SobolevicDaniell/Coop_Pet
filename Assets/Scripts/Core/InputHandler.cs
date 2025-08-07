using Fusion;
using UnityEngine;
using System;

public class InputHandler : MonoBehaviour
{
    [SerializeField] private float keyboardLookSensitivity = 700f;
    [SerializeField] private float mouseLookSensitivity = 5f;
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

            float mouseX = Input.GetAxis("Mouse X") * mouseLookSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseLookSensitivity;

            float keyboardX = 0f;
            float keyboardY = 0f;

            if (Input.GetKey(KeyCode.RightArrow)) keyboardX += keyboardLookSensitivity * Time.deltaTime;
            if (Input.GetKey(KeyCode.LeftArrow)) keyboardX -= keyboardLookSensitivity * Time.deltaTime;
            if (Input.GetKey(KeyCode.UpArrow)) keyboardY += keyboardLookSensitivity * Time.deltaTime;
            if (Input.GetKey(KeyCode.DownArrow)) keyboardY -= keyboardLookSensitivity * Time.deltaTime;

            _networkInput.mouseX = mouseX + keyboardX;
            _networkInput.mouseY = mouseY + keyboardY;

            _networkInput.jump = Input.GetKey(KeyCode.Space);

            if (Input.GetKeyDown(KeyCode.E)) OnInteractPressed?.Invoke();
            if (Input.GetKeyDown(KeyCode.R)) OnReloadPressed?.Invoke();

            for (int i = 1; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                {
                    // Debug.Log($"Pressed {i}, slot {i - 1}");
                    OnQuickSlotPressed?.Invoke(i - 1);
                }
            }
            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                // Debug.Log("Pressed 0, slot 9");
                OnQuickSlotPressed?.Invoke(9);
            }

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
    }
}
