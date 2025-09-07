using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public sealed class PlayerInputRouter : NetworkBehaviour
    {
        [Inject] private InputHandler _input;
        [Inject] private PlayerRpcHandler _rpc;
        [Inject] private QuickSlotController _quick;
        [Inject] private PickDropController _pickDrop;
        [Inject(Optional = true)] private InteractionController _ic;

        // нов: берём количество слотов с сервера (через фасад), без хардкода
        [Inject(Optional = true)] private InventoryClientFacade _inventoryFacade;
        [Inject(Optional = true)] private ContainerViewSessionClient _view;
        [Inject(Optional = true)] private InventoryService _inventory;

        [SerializeField] private float _wheelCooldown = 0.12f;
        [SerializeField] private float _placeMaxDistance = 4f;
        [SerializeField] private float _dropCooldown = 0.12f;

        private float _nextWheelTime;
        private float _nextDropTime;
        private bool _isFiring;

        private int _slotCount;              // нов: фактическое число слотов хотбара
        private bool _quickCountFixed;       // нов: фиксируем число слотов один раз
        private bool _subscribedQuickCount;  // нов: чтобы отписаться

        public override void Spawned()
        {
            if (HasInputAuthority) _quick.EnableForLocal();
            else _quick.DisableForLocal();

            // нов: открываем локальные контейнеры и фиксируем число слотов один раз
            if (HasInputAuthority && _inventoryFacade != null)
            {
                _inventoryFacade.SetLocal(Object.InputAuthority, GetComponent<InventoryRpcRouter>());

                _inventoryFacade.OpenLocalQuick();
                _inventoryFacade.OpenLocalMain();

                var cap = _inventoryFacade.GetLocalQuickCapacity();
                if (cap > 0)
                {
                    _slotCount = cap;
                    _quickCountFixed = true;
                }
                else if (_view != null)
                {
                    _view.OnContainerChanged += OnContainerChanged_OnceForQuick;
                    _subscribedQuickCount = true;
                }
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_subscribedQuickCount && _view != null)
                _view.OnContainerChanged -= OnContainerChanged_OnceForQuick;
            _subscribedQuickCount = false;
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
            // если слотов ещё нет — просто выходим
            int quickLen = _inventory?.GetQuickSlots()?.Length ?? 0;
            if (quickLen <= 0) return;

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { _quick.ChangeSlotAbsolute(0); return; }
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { _quick.ChangeSlotAbsolute(1); return; }
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) { _quick.ChangeSlotAbsolute(2); return; }
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) { _quick.ChangeSlotAbsolute(3); return; }
            if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) { _quick.ChangeSlotAbsolute(4); return; }
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) { _quick.ChangeSlotAbsolute(5); return; }
            if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) { _quick.ChangeSlotAbsolute(6); return; }
            if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) { _quick.ChangeSlotAbsolute(7); return; }
            if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) { _quick.ChangeSlotAbsolute(8); return; }
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
            {
                int last = Mathf.Clamp(quickLen - 1, 0, quickLen - 1);
                _quick.ChangeSlotAbsolute(last);
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
                _quick.ChangeSlotRelative(-1);
                _nextWheelTime = Time.unscaledTime + _wheelCooldown;
            }
            else if (dy < -0.1f)
            {
                _quick.ChangeSlotRelative(1);
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
            _rpc.RPC_RequestReload(idx);
        }

        private void OnInteract()
        {
            if (!HasInputAuthority) return;
            _pickDrop.TryPickAtCrosshair();
        }

        private void OnQuickDrop()
        {
            if (!HasInputAuthority) return;
            if (Time.unscaledTime < _nextDropTime) return;

            _pickDrop.TryDrop();
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

            _pickDrop.TryPlaceFromQuickSlot(pos, rot);
        }

        private void OnContainerChanged_OnceForQuick(ContainerId id)
        {
            if (_quickCountFixed || _inventoryFacade == null) return;
            if (!id.Equals(_inventoryFacade.localQuick)) return;

            var cap = _inventoryFacade.GetLocalQuickCapacity();
            if (cap <= 0) return;

            _slotCount = cap;
            _quickCountFixed = true;

            if (_subscribedQuickCount && _view != null)
                _view.OnContainerChanged -= OnContainerChanged_OnceForQuick;

            _subscribedQuickCount = false;
        }

        private void GetDropPoint(out Vector3 pos, out Vector3 fwd)
        {
            Transform t = null;
            if (_ic != null)
            {
                var root = _ic.transform;
                t = FindChildRecursive(root, "DropPoint");
            }
            if (t == null)
            {
                var cam = Camera.main != null ? Camera.main.transform : null;
                if (cam != null) { pos = cam.position + cam.forward * 0.2f; fwd = cam.forward; return; }
                var fallback = (_ic != null) ? _ic.transform : transform;
                pos = fallback.position + fallback.forward * 0.5f + Vector3.up * 1.4f;
                fwd = fallback.forward;
                return;
            }
            pos = t.position;
            fwd = t.forward;
        }

        private Transform FindChildRecursive(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                var r = FindChildRecursive(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
