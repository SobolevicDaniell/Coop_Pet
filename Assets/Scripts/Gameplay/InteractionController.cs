// Assets/Scripts/Gameplay/InteractionController.cs
using Fusion;
using UnityEngine;
using Zenject;

namespace Game.Gameplay
{
    [RequireComponent(typeof(NetworkObject))]
    public class InteractionController : NetworkBehaviour
    {
        [Inject] private InteractionPromptView _promptView;
        [Inject] private InventoryService _inventory;

        [Header("Настройки взаимодействия")]
        [SerializeField] private float range = 2f;

        private Camera _camera;
        private bool _initialized;

        public override void Spawned()
        {
            if (_promptView == null)
                FindObjectOfType<SceneContext>().Container.InjectGameObject(gameObject);

            if (Object.HasInputAuthority)
                InitializeLocal();
        }

        public void InitializeLocal()
        {
            if (_initialized) return;

            _camera = Camera.main;
            if (_camera == null)
                Debug.LogError("[InteractionController] Camera.main not found", this);

            _promptView.Hide();
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized) return;

            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            bool canPick = Physics.Raycast(ray, out var hit, range)
                           && hit.collider.TryGetComponent<PickableItem>(out _);

            if (canPick)
                _promptView.Show();
            else
                _promptView.Hide();

            if (canPick && Input.GetKeyDown(KeyCode.E))
                TryRequestPick(hit);
        }

        private void TryRequestPick(RaycastHit hit)
        {
            var pickable = hit.collider.GetComponent<PickableItem>();
            var netObj = pickable.GetComponent<NetworkObject>();
            Debug.Log($"[InteractionController] Sending RPC_RequestPick for {pickable.ItemId}");
            RPC_RequestPick(netObj);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        void RPC_RequestPick(NetworkObject pickable, RpcInfo info = default)
        {
            if (pickable == null) return;
            Runner.Despawn(pickable);
            var pick = pickable.GetComponent<PickableItem>();
            if (pick != null)
                RPC_ConfirmPick(info.Source, pick.ItemId);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        void RPC_ConfirmPick(PlayerRef _, string itemId)
        {
            bool added = _inventory.AddToQuickSlot(itemId);
            Debug.Log($"[RPC_ConfirmPick] Added '{itemId}' to inventory? {added}");
            if (!added)
                Debug.LogWarning($"[InteractionController] Failed to add '{itemId}'");
        }
    }
}
