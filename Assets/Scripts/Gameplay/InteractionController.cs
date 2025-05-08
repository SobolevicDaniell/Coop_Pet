// Assets/Scripts/Gameplay/InteractionController.cs
using Fusion;
using UnityEngine;
using Zenject;
using Game;

namespace Game.Gameplay
{
    [RequireComponent(typeof(NetworkObject))]
    public class InteractionController : NetworkBehaviour
    {
        [Inject] private InteractionPromptView _promptView;
        [Inject] private InventoryService _inventory;
        [Inject] private InputHandler _inputHandler;
        [Inject] private ItemDatabaseSO _itemDatabase;

        [Header("Настройки")]
        [SerializeField] private float range = 4f;
        [SerializeField] private Transform _dropPoint;
        [SerializeField] private float _throwForce = 5f;

        private Camera _camera;
        private bool _initialized;

        // отложенный дроп
        private bool _needDrop;
        private Vector3 _toDropPos;
        private Vector3 _toDropDir;
        private string _toDropItem;
        private int _toDropCount;

        public override void Spawned()
        {
            // Инжект зависимостей на клиенте
            if (_promptView == null)
                FindObjectOfType<SceneContext>().Container.InjectGameObject(gameObject);

            if (Object.HasInputAuthority)
                InitializeLocal();
        }

        public void InitializeLocal()
        {
            if (_initialized) return;
            _camera = Camera.main;
            _promptView.Hide();

            _inputHandler.OnInteractPressed += OnInteractPressed;
            _inputHandler.OnQuickSlotPressed += OnQuickSlotPressed;
            _inputHandler.OnQuickSlotScrollDelta += OnQuickSlotScrolled;

            _initialized = true;
        }

        private void OnDestroy()
        {
            if (!_initialized) return;
            _inputHandler.OnInteractPressed -= OnInteractPressed;
            _inputHandler.OnQuickSlotPressed -= OnQuickSlotPressed;
            _inputHandler.OnQuickSlotScrollDelta -= OnQuickSlotScrolled;
        }

        void Update()
        {
            if (!_initialized) return;

            // 1) покажи или скрой "E" подсказку
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            bool can = Physics.Raycast(ray, out var hit, range)
                       && hit.collider.TryGetComponent<PickableItem>(out _);
            if (can) _promptView.Show(); else _promptView.Hide();

            // 2) отложенный дроп
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
                RPC_RequestPick(pickable.GetComponent<NetworkObject>(), pickable.ItemId, pickable.Count);
            }
        }

        private void OnQuickSlotPressed(int index)
        {
            _inventory.SelectQuickSlot(index);
        }

        private void OnQuickSlotScrolled(int delta)
        {
            int curr = _inventory.SelectedQuickSlot < 0 ? 0 : _inventory.SelectedQuickSlot;
            int next = (curr + delta + 10) % 10;
            _inventory.SelectQuickSlot(next);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        void RPC_RequestPick(NetworkObject no, string itemId, int count, RpcInfo info = default)
        {
            if (no == null) return;
            Runner.Despawn(no);
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
            var def = _itemDatabase.Get(itemId);
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
    }
}
