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

        [Header("Настройки взаимодействия")]
        [SerializeField] private float range = 4f;
        [SerializeField] private Transform _dropPoint; // Только для клиента

        private Camera _camera;
        private bool _initialized;

        // Параметры отложенного дропа
        private bool _needDrop;
        private Vector3 _toDropPos;
        private string _toDropItem;
        private int _toDropCount;

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
            _camera = Camera.main;
            _promptView.Hide();
            _inputHandler.OnInteractPressed += OnInteractPressed;
            _initialized = true;
        }

        private void OnDestroy()
        {
            if (_initialized)
                _inputHandler.OnInteractPressed -= OnInteractPressed;
        }

        private void Update()
        {
            if (!_initialized) return;

            // 1) Показ/скрытие подсказки
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            bool canPick = Physics.Raycast(ray, out var hit, range)
                           && hit.collider.TryGetComponent<PickableItem>(out _);
            if (canPick) _promptView.Show();
            else _promptView.Hide();

            // 2) Отправка отложенного дропа (если нужно)
            if (_needDrop)
            {
                _needDrop = false;
                RPC_RequestDrop(_toDropPos, _toDropItem, _toDropCount);
            }
        }

        private void OnInteractPressed()
        {
            if (!_initialized) return;

            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, range)
                && hit.collider.TryGetComponent<PickableItem>(out var pickable))
            {
                var netObj = pickable.GetComponent<NetworkObject>();
                var itemId = pickable.ItemId;
                var count = pickable.Count;

                RPC_RequestPick(netObj, itemId, count);
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
                _toDropItem = itemId;
                _toDropCount = leftover;
                _needDrop = true;
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        void RPC_RequestDrop(Vector3 dropPos, string itemId, int count, RpcInfo info = default)
        {
            Debug.Log($"[Server] RPC_RequestDrop from {info.Source} at {dropPos} for {itemId} x{count}");
            if (!Object.HasStateAuthority) return;

            var itemDef = _itemDatabase.Get(itemId);
            var prefabNo = itemDef.Prefab.GetComponent<NetworkObject>();

            Runner.Spawn(
                prefabNo,
                dropPos,
                Quaternion.identity,
                PlayerRef.None,
                onBeforeSpawned: (r, so) =>
                {
                    var pi = so.GetComponent<PickableItem>();
                    pi.Initialize(itemId, count);
                }
            );
        }
    }
}
