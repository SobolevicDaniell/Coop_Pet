using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public sealed class PlayerInputRouter : NetworkBehaviour
    {
        [Inject] private InputHandler _input;
        [Inject(Optional = true)] private InventoryClientFacade _inventoryFacade;
        [Inject(Optional = true)] private InventoryService _inventory;

        [SerializeField] private float _wheelCooldown = 0.12f;
        [SerializeField] private float _placeMaxDistance = 4f;
        [SerializeField] private float _dropCooldown = 0.12f;

        private PlayerRpcHandler _playerRpc;
        private QuickSlotController _quick;
        private PickDropController _pickDrop;
        private InteractionController _ic;

        private float _nextWheelTime;
        private float _nextDropTime;
        private bool _isFiring;

        public override void Spawned()
        {
            _playerRpc ??= GetComponent<PlayerRpcHandler>();
            _quick ??= GetComponent<QuickSlotController>();
            _pickDrop ??= GetComponent<PickDropController>();
            _ic ??= GetComponent<InteractionController>();

            InventoryRpcRouter router = null;
            NetworkId poId = default;

            if (Runner != null && Runner.TryGetPlayerObject(Object.InputAuthority, out var po) && po != null)
            {
                router = po.GetComponentInChildren<InventoryRpcRouter>(true);
                poId = po.Id;
            }

            if (router == null)
            {
                var all = FindObjectsOfType<InventoryRpcRouter>(true);
                for (int i = 0; i < all.Length && router == null; i++)
                    if (all[i].Object != null && all[i].Object.HasInputAuthority)
                        router = all[i];
            }

            if (router == null) return;

            if (_inventoryFacade != null)
            {
                _inventoryFacade.SetLocal(Object.InputAuthority, router, poId);
                _inventoryFacade.OpenLocalQuick();
                _inventoryFacade.OpenLocalMain();
            }
            else
            {
                StartCoroutine(router.RetryOpenContainer((int)ContainerType.PlayerQuick, Object.InputAuthority, poId));
                StartCoroutine(router.RetryOpenContainer((int)ContainerType.PlayerMain, Object.InputAuthority, poId));
            }
        }

        private void OnEnable()
        {
            if (_input != null)
            {
                _input.OnReloadPressed += OnReload;
                _input.OnPlacePressed += OnPlace;
                _input.OnQuickDropPressed += OnQuickDrop;
                _input.OnInteractPressed += OnInteract;
                _input.OnUseDown += OnUseDown;
                _input.OnUseUp += OnUseUp;
            }
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.OnReloadPressed -= OnReload;
                _input.OnPlacePressed -= OnPlace;
                _input.OnQuickDropPressed -= OnQuickDrop;
                _input.OnInteractPressed -= OnInteract;
                _input.OnUseDown -= OnUseDown;
                _input.OnUseUp -= OnUseUp;
            }
        }

        private void Update()
        {
            if (!HasInputAuthority) return;
            if (_input != null && _input.InventoryOpen) return;

            HandleNumberKeys();
            HandleMouseWheel();

            if (Input.GetKeyDown(KeyCode.Q))
                _pickDrop?.TryDrop();

            var beh = _ic != null ? _ic.currentBehavior : null;
            var wb = beh as WeaponBehavior;

            bool held = Input.GetMouseButton(0);

            if (held)
            {
                if (!_isFiring)
                {
                    _isFiring = true;
                    if (wb != null) wb.OnUsePressed();
                    else beh?.OnUsePressed();
                }
                if (wb != null) wb.OnUseHeld(Time.deltaTime);
                else beh?.OnUseHeld(Time.deltaTime);
            }
            else
            {
                if (_isFiring)
                {
                    _isFiring = false;
                    beh?.OnUseReleased();
                }
            }
        }

        private void HandleNumberKeys()
        {
            int quickLen = _inventory?.GetQuickSlots()?.Length ?? 0;
            if (quickLen <= 0) return;

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { _quick?.ChangeSlotAbsolute(0); return; }
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { _quick?.ChangeSlotAbsolute(1); return; }
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) { _quick?.ChangeSlotAbsolute(2); return; }
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) { _quick?.ChangeSlotAbsolute(3); return; }
            if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) { _quick?.ChangeSlotAbsolute(4); return; }
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) { _quick?.ChangeSlotAbsolute(5); return; }
            if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) { _quick?.ChangeSlotAbsolute(6); return; }
            if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) { _quick?.ChangeSlotAbsolute(7); return; }
            if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) { _quick?.ChangeSlotAbsolute(8); return; }
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
            {
                int last = Mathf.Clamp(quickLen - 1, 0, quickLen - 1);
                _quick?.ChangeSlotAbsolute(last);
            }
        }

        private void HandleMouseWheel()
        {
            int quickLen = _inventory?.GetQuickSlots()?.Length ?? 0;
            if (quickLen <= 0) return;

            if (Time.unscaledTime < _nextWheelTime) return;

            float dy = Input.mouseScrollDelta.y;
            if (dy > 0.1f)
            {
                _quick?.ChangeSlotRelative(-1);
                _nextWheelTime = Time.unscaledTime + _wheelCooldown;
            }
            else if (dy < -0.1f)
            {
                _quick?.ChangeSlotRelative(1);
                _nextWheelTime = Time.unscaledTime + _wheelCooldown;
            }
        }

        private void OnUseDown()
        {
            if (!HasInputAuthority) return;
            if (_input != null && _input.InventoryOpen) return;
            if (_ic != null && _ic.currentBehavior is WeaponBehavior wb && wb.IsValid())
            {
                _isFiring = true;
                wb.OnUsePressed();
            }
        }

        private void OnUseUp()
        {
            if (!HasInputAuthority) return;
            _isFiring = false;
            _ic?.currentBehavior?.OnUseReleased();
        }

        private void OnReload()
        {
            if (!HasInputAuthority) return;
            int idx = _inventory != null ? _inventory.SelectedQuickSlot : -1;
            _playerRpc?.RPC_RequestReload(idx);
        }

        private void OnInteract()
        {
            if (!HasInputAuthority) return;
            if (_ic != null && _ic.TryOpenContainerAtCrosshair()) return;
            _pickDrop?.TryPickAtCrosshair();
        }

        private void OnQuickDrop()
        {
            if (!HasInputAuthority) return;
            if (Time.unscaledTime < _nextDropTime) return;

            _pickDrop?.TryDrop();
            _nextDropTime = Time.unscaledTime + _dropCooldown;
        }

        private void OnPlace()
        {
            if (!HasInputAuthority) return;

            Vector3 origin;
            Vector3 forward;
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam != null) { origin = cam.position; forward = cam.forward; }
            else
            {
                var t = (_ic != null) ? _ic.transform : transform;
                origin = t.position + Vector3.up * 1.6f;
                forward = t.forward;
            }

            Vector3 pos;
            Quaternion rot;
            if (Physics.Raycast(origin, forward, out var hit, _placeMaxDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                pos = hit.point;
                var flat = Vector3.ProjectOnPlane(forward, Vector3.up);
                rot = flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : Quaternion.identity;
            }
            else
            {
                pos = origin + forward.normalized * _placeMaxDistance;
                var flat = Vector3.ProjectOnPlane(forward, Vector3.up);
                rot = flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : Quaternion.identity;
            }

            _pickDrop?.TryPlaceFromQuickSlot(pos, rot);
        }
    }
}
