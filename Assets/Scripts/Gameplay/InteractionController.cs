// Assets/Scripts/Game/Gameplay/InteractionController.cs
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
                        // 1) ������ �� ������: "���� ��������� ���� ������"
                        RPC_RequestPick(pick.Object, Object.InputAuthority);
                    }
                }
            }
        }

        // 1) ��������� �����: ��������� ������� � ��� ������� itemId
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        void RPC_RequestPick(NetworkObject pickableNetObj, PlayerRef requester)
        {
            if (pickableNetObj == null) return;

            var pickable = pickableNetObj.GetComponent<PickableItem>();
            if (pickable == null) return;

            pickable.OnPicked(runner);

            // ���������� ����� �� ������ ��������� �������������
            RPC_ConfirmPick(requester, pickable.ItemId);
        }

        // 2) ���������� �����: �������� itemId � ��������� � ������
        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        void RPC_ConfirmPick(PlayerRef target, string itemId)
        {
            // Fusion ��� �������� ��� ������ ������� �������
            inventory.AddToQuickSlot(itemId);
        }
    }
}
