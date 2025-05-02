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
        [Inject] private InputHandler _inputHandler;

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

            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            bool canPick = Physics.Raycast(ray, out var hit, range)
                           && hit.collider.TryGetComponent<PickableItem>(out _);

            if (canPick)
                _promptView.Show();
            else
                _promptView.Hide();
        }

        private void OnInteractPressed()
        {
            if (!_initialized) return;

            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, range)
                && hit.collider.TryGetComponent<PickableItem>(out var pickable))
            {
                var netObj = pickable.GetComponent<NetworkObject>();
                RPC_RequestPick(netObj);
            }
        }

        //private void TryRequestPick(RaycastHit hit)
        //{
        //    var pickable = hit.collider.GetComponent<PickableItem>();
        //    var netObj = pickable.GetComponent<NetworkObject>();
        //    //Debug.Log($"[InteractionController] Sending RPC_RequestPick for {pickable.ItemId}");
        //    RPC_RequestPick(netObj);
        //}

        // --- SERVER SIDE ---
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        void RPC_RequestPick(NetworkObject pickable, RpcInfo info = default)
        {
            if (pickable == null) return;
            var pick = pickable.GetComponent<PickableItem>();
            if (pick == null) return;

            // сохраняем до despawn
            var itemId = pick.ItemId;
            var count = pick.Count;

            Runner.Despawn(pickable);
            RPC_ConfirmPick(info.Source, itemId, count);
        }

        // --- CLIENT SIDE ---
        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        void RPC_ConfirmPick(PlayerRef _, string itemId, int count, RpcInfo info = default)
        {
            for (int i = 0; i < count; i++)
            {
                if (!_inventory.AddToQuickSlot(itemId))
                {
                    Debug.LogWarning($"[Inventory] Could not add '{itemId}' to quick slot.");
                    break;
                }
            }
        }


    }
}
