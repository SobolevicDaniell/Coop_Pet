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

        // Инъекции Zenject
        [Inject] private InventoryService _inventory;
        [Inject] private NetworkRunner _networkRunner;
        [Inject] private InteractionPromptView _injectedPromptView;

        private InteractionPromptView _promptView;
        private Camera _camera;

        private void Start()
        {
            if (!Object.HasInputAuthority)
                return;

            // 1) Сохраняем ссылку на камеру
            _camera = Camera.main;
            if (_camera == null)
                Debug.LogError("[InteractionController] Camera.main не найдена!", this);

            // 2) Сначала пытаемся использовать Zenject-инъекцию
            _promptView = _injectedPromptView;

            // 3) Если Zenject не вбил, ищем в сцене
            if (_promptView == null)
            {
                _promptView = FindObjectOfType<InteractionPromptView>();
                if (_promptView == null)
                    Debug.LogError("[InteractionController] InteractionPromptView не найден в сцене!", this);
            }

            // 4) Скрываем подсказку в начале
            _promptView?.Hide();
        }

        private void Update()
        {
            if (!Object.HasInputAuthority || _camera == null || _promptView == null)
                return;

            // Рейкаст от камеры
            if (Physics.Raycast(_camera.transform.position, _camera.transform.forward, out var hit, range)
                && hit.collider.TryGetComponent<PickableItem>(out var pickable))
            {
                // Показываем подсказку
                _promptView.Show();

                // Обрабатываем нажатие E
                if (Input.GetKeyDown(KeyCode.E))
                {
                    RPC_RequestPick(pickable.Object);
                    Debug.Log($"[InteractionController] Request pick: {pickable.ItemId}");
                }

                return; // не скрывать подсказку сразу после
            }

            // Скрываем подсказку, если не смотрим на предмет
            _promptView.Hide();
        }

        // --- SERVER SIDE ---
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        void RPC_RequestPick(NetworkObject pickableNetObj, RpcInfo info = default)
        {
            if (pickableNetObj == null) return;
            if (!_networkRunner.IsServer) return;

            var pick = pickableNetObj.GetComponent<PickableItem>();
            if (pick == null) return;

            // Деспавним на сервере
            _networkRunner.Despawn(pickableNetObj);

            // Шлём подтверждение обратно тому же клиенту
            RPC_ConfirmPick(info.Source, pick.ItemId);
        }

        // --- CLIENT SIDE ---
        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        void RPC_ConfirmPick(PlayerRef target, string itemId, RpcInfo info = default)
        {
            // Проверяем, что это наш клиент
            //if (info.Target != target && info.Source != target) return;

            bool added = _inventory.AddToQuickSlot(itemId);
            if (!added)
                Debug.LogWarning($"[Inventory] Не удалось добавить предмет с id='{itemId}'");
        }
    }
}
