// Scripts/Gameplay/PickDropController.cs
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public class PickDropController : MonoBehaviour
    {
        [Inject] public InventoryService Inventory { get; private set; }

        private InteractionController _ic;
        private ServerRpcHandler _rpc;
        private PickableItem _focusedItem;

        public void Initialize(InteractionController controller)
        {
            _ic = controller;
            _rpc = controller.RpcHandler;
        }

        public void TryPick()
        {
            if (_focusedItem == null)
            {
                Debug.Log("TryPick: No pickable under crosshair!");
                return;
            }
            Debug.Log("TryPick: Picking " + _focusedItem.name);
            _rpc.RPC_RequestPick(_focusedItem.Object);
        }

        public void TryDrop()
        {
            var selected = _ic.NetSelectedQuickSlot;
            if (selected < 0) return;

            var slots = Inventory.GetQuickSlots();
            if (selected >= slots.Length) return;

            var slot = slots[selected];
            if (slot.Id == null || slot.Count <= 0) return;

            int ammo = slot.State != null ? slot.State.Ammo : 0;

            _rpc.RPC_RequestDrop(
                _ic.DropPoint.position,
                _ic.Camera.transform.forward,
                slot.Id,
                slot.Count,
                ammo
            );

            slot.Id = null;
            slot.Count = 0;

            if (slot.State != null)
                slot.State.Ammo = 0;

            Inventory.RaiseQuickSlotsChanged();

            if (_ic.NetSelectedQuickSlot == selected)
            {
                _rpc.RPC_SelectQuickSlot(-1);
                _rpc.RPC_RequestDespawnHandModel();
            }
        }

        public void UpdateRaycast()
        {
            if (_ic == null) return;
            var camera = _ic.Camera;
            var range = _ic.Range;
            var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            if (Physics.Raycast(ray, out var hit, range))
            {
                Debug.Log($"RAY HIT: {hit.collider.gameObject.name}");
                var pickable = hit.collider.GetComponentInParent<PickableItem>();
                if (pickable != null)
                {
                    Debug.Log("Pickable FOUND: " + pickable.name);
                    _focusedItem = pickable;
                    _ic.Prompt.Show();
                    return;
                }
                else
                {
                    Debug.Log("Raycast hit, but NOT pickable: " + hit.collider.gameObject.name);
                }
            }
            else
            {
                Debug.Log("Raycast: NOTHING");
            }

            _focusedItem = null;
            _ic.Prompt.Hide();
        }
    }
}
