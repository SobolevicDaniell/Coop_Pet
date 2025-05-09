// Assets/Scripts/Gameplay/InteractionController.cs
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public class InteractionController : NetworkBehaviour
    {
        [Inject] private HandItemBehaviorFactory _factory;
        [Inject] private InteractionPromptView _promptView;
        [Inject] private InventoryService _inventory;
        [Inject] private InputHandler _input;
        [Inject] private ItemDatabaseSO _db;

        [Networked] public int NetSelectedQuickSlot { get; set; } = -1;

        [Header("Hand Point")]
        [SerializeField] private Transform _handPoint;

        [Header("Pick/Drop Settings")]
        [SerializeField] private float range = 4f;
        [SerializeField] private Transform _dropPoint;
        [SerializeField] private float _throwForce = 5f;

        private Camera _camera;
        private bool _initialized;

        private bool _needDrop;
        private Vector3 _toDropPos;
        private Vector3 _toDropDir;
        private string _toDropItem;
        private int _toDropCount;

        private IHandItemBehavior _currentBehavior;
        private int _lastSyncedSlot = -1;

        public override void Spawned()
        {
            if (_promptView == null)
                FindObjectOfType<SceneContext>()
                    .Container.InjectGameObject(gameObject);

            if (Object.HasInputAuthority)
                InitializeLocal();
        }

        public void InitializeLocal()
        {
            if (_initialized) return;
            _initialized = true;

            _camera = Camera.main;
            _promptView.Hide();

            _input.OnInteractPressed += OnInteractPressed;
            _input.OnQuickSlotPressed += OnQuickSlotPressed;
            _input.OnQuickSlotScrollDelta += OnQuickSlotScrolled;
            _input.SingleShot += OnSingleShot;
        }

        private void OnDestroy()
        {
            if (!_initialized) return;
            _input.OnInteractPressed -= OnInteractPressed;
            _input.OnQuickSlotPressed -= OnQuickSlotPressed;
            _input.OnQuickSlotScrollDelta -= OnQuickSlotScrolled;
            _input.SingleShot -= OnSingleShot;
        }

        public override void FixedUpdateNetwork()
        {
            // Отслеживаем смену слота вручную
            if (_lastSyncedSlot != NetSelectedQuickSlot)
            {
                _lastSyncedSlot = NetSelectedQuickSlot;
                HandleSlotChanged();
            }
        }

        private void Update()
        {
            if (!_initialized) return;

            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            bool canPick = Physics.Raycast(ray, out var hit, range)
                           && hit.collider.TryGetComponent<PickableItem>(out _);
            if (canPick) _promptView.Show();
            else _promptView.Hide();

            if (_needDrop)
            {
                _needDrop = false;
                RPC_RequestDrop(_toDropPos, _toDropDir, _toDropItem, _toDropCount);
            }
        }

        private void OnInteractPressed()
        {
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, range)
             && hit.collider.TryGetComponent<PickableItem>(out var pickable))
            {
                RPC_RequestPick(
                    pickable.GetComponent<NetworkObject>(),
                    pickable.ItemId,
                    pickable.Count
                );
            }
        }

        private void OnSingleShot()
        {
            if (!HasInputAuthority) return;
            _currentBehavior?.OnUsePressed();
        }

        private void OnQuickSlotPressed(int slotIndex)
        {
            if (!HasInputAuthority) return;
            RPC_SelectQuickSlot(slotIndex);
        }

        private void OnQuickSlotScrolled(int delta)
        {
            if (!HasInputAuthority) return;
            int curr = NetSelectedQuickSlot < 0 ? 0 : NetSelectedQuickSlot;
            int next = (curr + delta + 10) % 10;
            RPC_SelectQuickSlot(next);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        void RPC_SelectQuickSlot(int slot, RpcInfo info = default)
        {
            NetSelectedQuickSlot = (NetSelectedQuickSlot == slot) ? -1 : slot;
        }

        private void HandleSlotChanged()
        {
            _currentBehavior?.OnUnequip();
            _currentBehavior = null;

            int slotIdx = NetSelectedQuickSlot;
            if (slotIdx >= 0)
            {
                var slot = _inventory.GetQuickSlots()[slotIdx];
                if (slot.Id != null)
                {
                    var so = _db.Get(slot.Id);
                    _currentBehavior = _factory.Create(so, _handPoint, this);
                    _currentBehavior.OnEquip();
                }
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        void RPC_RequestPick(NetworkObject pickable, string itemId, int count, RpcInfo info = default)
        {
            if (pickable == null) return;
            Runner.Despawn(pickable);
            RPC_ConfirmPick(info.Source, itemId, count);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        void RPC_ConfirmPick(PlayerRef _, string itemId, int count)
        {
            int leftover = _inventory.HandlePick(itemId, count);
            if (leftover > 0)
            {
                _toDropPos = _dropPoint.position;
                _toDropDir = _camera.transform.forward.normalized;
                _toDropItem = itemId;
                _toDropCount = leftover;
                _needDrop = true;
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        void RPC_RequestDrop(Vector3 pos, Vector3 dir, string itemId, int count, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority) return;
            var def = _db.Get(itemId);
            var prefab = def.Prefab.GetComponent<NetworkObject>();
            Runner.Spawn(
                prefab,
                pos,
                Quaternion.LookRotation(dir),
                PlayerRef.None,
                onBeforeSpawned: (r, spawned) =>
                {
                    var pi = spawned.GetComponent<PickableItem>();
                    pi.Initialize(itemId, count);
                    if (spawned.TryGetComponent<Rigidbody>(out var rb))
                        rb.linearVelocity = dir * _throwForce;
                }
            );
        }

        // Сделали public, чтобы из WeaponBehavior можно было вызывать
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestShoot(RpcInfo info = default)
        {
            if (_currentBehavior is WeaponBehavior wb && wb.TryUseAmmo())
            {
                var netObj = wb.GetBulletNetworkObject();
                Runner.Spawn(
                    netObj,
                    wb.MuzzlePosition,
                    wb.MuzzleRotation,
                    Object.InputAuthority,
                    onBeforeSpawned: (runner, spawned) =>
                    {
                        if (spawned.TryGetComponent<Rigidbody>(out var rb))
                            rb.linearVelocity = wb.MuzzleForward * wb.BulletSpeed;
                    }
                );

                RPC_OnMuzzleFlash();
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        void RPC_OnMuzzleFlash(RpcInfo info = default)
        {
            _currentBehavior?.OnMuzzleFlash();
        }
    }
}
