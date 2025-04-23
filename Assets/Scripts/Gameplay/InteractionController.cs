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

        [Inject] private InventoryService _inventory;
        [Inject] private NetworkRunner _networkRunner;
        [Inject] private InteractionPromptView _promptView;

        private Camera _camera;

        private void Start()
        {
            // Получаем основную камеру раз и навсегда
            _camera = Camera.main;
            _promptView.Hide();
        }

        private void Update()
        {
            if (!Object.HasInputAuthority)
                return;

            if (_camera == null)
                return;

            // 1. Рейкаст от позиции и направления камеры
            if (Physics.Raycast(_camera.transform.position, _camera.transform.forward, out var hit, range))
            {
                // 2. Проверяем, есть ли у попадания PickableItem
                if (hit.collider.TryGetComponent<PickableItem>(out var pickable))
                {
                    _promptView.Show();

                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        // 3. Шлём запрос на сервер
                        RPC_RequestPick(pickable.Object);
                        Debug.Log("Picked: " + pickable.ItemId);
                    }

                    return; // выходим, чтобы не скрыть подсказку
                }
            }

            // Если ничего не попало — скрываем подсказку
            _promptView.Hide();
        }

        // --- SERVER SIDE ---
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        void RPC_RequestPick(NetworkObject pickableNetObj, RpcInfo info = default)
        {
            if (pickableNetObj == null) return;

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
