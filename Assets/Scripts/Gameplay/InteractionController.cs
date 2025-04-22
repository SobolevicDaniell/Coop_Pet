using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public class InteractionController : NetworkBehaviour
    {
        [SerializeField] private float range = 2f;
        [Inject] private InventoryService inventory;
        [Inject] private NetworkRunner runner;

        void Update()
        {
            if (!Object.HasInputAuthority) return;

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (Physics.Raycast(transform.position, transform.forward, out var hit, range))
                {
                    var pick = hit.collider.GetComponent<PickableItem>();
                    if (pick != null)
                    {
                        RPC_PickItem(pick.Object, Object.InputAuthority);
                    }
                }
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        void RPC_PickItem(NetworkObject pickableNetObj, PlayerRef requester, RpcInfo info = default)
        {
            if (pickableNetObj == null) return;

            // получаем наш компонент и удаляем его
            var pickable = pickableNetObj.GetComponent<PickableItem>();
            pickable.OnPicked(runner);

            // на клиенте добавляем в слоты
            if (info.Source == requester)
                inventory.AddToQuickSlot(pickable.Definition);
        }
    }
}
