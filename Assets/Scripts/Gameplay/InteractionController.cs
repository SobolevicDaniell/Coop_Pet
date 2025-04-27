using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public class InteractionController : NetworkBehaviour
    {
        [Header("Настройки взаимодействия")]
        [SerializeField] private float range = 2f;

        private InventoryService _inventory;
        private NetworkRunner _networkRunner;
        private InteractionPromptView _promptView;
        private Camera _camera;

        private bool _isInitialized;
        private bool _isSpawned;

        public void Init(InventoryService inventory, NetworkRunner runner, InteractionPromptView promptView)
        {
            _inventory = inventory;
            _networkRunner = runner;
            _promptView = promptView;

            _isInitialized = true;
            TryInitialize();
        }

        public override void Spawned()
        {
            _isSpawned = true;
            TryInitialize();
        }

        private void TryInitialize()
        {
            if (!_isInitialized || !_isSpawned)
                return;

            if (HasInputAuthority)
            {
                _camera = Camera.main;

                if (_camera == null)
                    Debug.LogError("[InteractionController] Camera.main не найдена!", this);

                if (_promptView == null)
                    _promptView = FindObjectOfType<InteractionPromptView>();

                if (_promptView == null)
                    Debug.LogError("[InteractionController] InteractionPromptView не найден!", this);
                else
                    _promptView.Hide();
            }
        }

        private void Update()
        {
            if (!_isInitialized || !_isSpawned || !HasInputAuthority || _camera == null || _promptView == null)
                return;

            if (Physics.Raycast(_camera.transform.position, _camera.transform.forward, out var hit, range)
                && hit.collider.TryGetComponent<PickableItem>(out var pickable))
            {
                _promptView.Show();
                if (Input.GetKeyDown(KeyCode.E))
                {
                    RPC_RequestPick(pickable.Object);
                    Debug.Log($"[InteractionController] Request pick: {pickable.ItemId}");
                }
                return;
            }

            _promptView.Hide();
        }

        // --- SERVER SIDE ---
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        void RPC_RequestPick(NetworkObject pickableNetObj, RpcInfo info = default)
        {
            if (!_networkRunner.IsServer || pickableNetObj == null)
                return;

            var pick = pickableNetObj.GetComponent<PickableItem>();
            if (pick == null) return;

            _networkRunner.Despawn(pickableNetObj);
            RPC_ConfirmPick(info.Source, pick.ItemId);
        }

        // --- CLIENT SIDE ---
        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        void RPC_ConfirmPick(PlayerRef target, string itemId, RpcInfo info = default)
        {
            if (_inventory == null)
            {
                Debug.LogError("[InteractionController] InventoryService не проинжектирован!", this);
                return;
            }

            bool added = _inventory.AddToQuickSlot(itemId);
            if (!added)
                Debug.LogWarning($"[Inventory] Не удалось добавить предмет с id='{itemId}'");
        }
    }
}
