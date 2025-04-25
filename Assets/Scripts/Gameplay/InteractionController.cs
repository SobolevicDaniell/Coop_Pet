// Assets/Scripts/Gameplay/InteractionController.cs
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

        // Zenject вызовет этот метод сразу после биндинга зависимостей.
        // PromptView помечен опциональным:
        [Inject]
        public void Construct(
            InventoryService inventory,
            NetworkRunner runner,
            [InjectOptional] InteractionPromptView promptView)
        {
            _inventory = inventory;
            _networkRunner = runner;
            _promptView = promptView;
        }

        private void Start()
        {
            if (!Object.HasInputAuthority)
                return;

            // Если Zenject не заинжектил promptView — ищем сами:
            if (_promptView == null)
            {
                _promptView = FindObjectOfType<InteractionPromptView>();
                if (_promptView == null)
                    Debug.LogError("[InteractionController] InteractionPromptView не найден!", this);
            }

            _camera = Camera.main;
            if (_camera == null)
                Debug.LogError("[InteractionController] Camera.main не найдена!", this);

            // Прячем подсказку, если она есть
            _promptView?.Hide();
        }

        private void Update()
        {
            if (!Object.HasInputAuthority || _camera == null || _promptView == null)
                return;

            if (Physics.Raycast(_camera.transform.position, _camera.transform.forward, out var hit, range)
                && hit.collider.TryGetComponent<PickableItem>(out var pickable))
            {
                _promptView.Show();
                if (Input.GetKeyDown(KeyCode.E))
                    RPC_RequestPick(pickable.Object);

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
            bool added = _inventory.AddToQuickSlot(itemId);
            if (!added)
                Debug.LogWarning($"[Inventory] Не удалось добавить предмет с id='{itemId}'");
        }
    }
}
