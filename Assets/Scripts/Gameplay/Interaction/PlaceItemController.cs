using Fusion;
using UnityEngine;

namespace Game
{
    public class PlaceItemController : MonoBehaviour
    {
        private InteractionController _controller;

        public void Initialize(InteractionController controller)
        {
            _controller = controller;
        }

        public void TryPlace()
        {
            var selected = _controller.netSelectedQuickSlot;
            if (selected < 0) return;

            var slots = _controller.inventory.GetQuickSlots();
            if (selected >= slots.Length) return;
            var slot = slots[selected];

            if (string.IsNullOrEmpty(slot.Id) || slot.Count <= 0) return;

            var so = _controller.db.Get(slot.Id);
            if (!(so is PlaceableItemSO placeable)) return;

            var camera = _controller.GetComponent<Camera>();
            Vector3 placePos = Vector3.zero;
            Vector3 placeNormal = Vector3.up;
            bool canPlace = false;
            Ray ray = camera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
            if (Physics.Raycast(ray, out var hit, _controller._rangePlace))
            {
                placePos = hit.point;
                placeNormal = hit.normal;
                canPlace = true;
            }
            else
            {
                placePos = ray.origin + ray.direction * _controller._rangePlace;
                canPlace = true;
            }


            if (canPlace)
            {
                var rotation = Quaternion.LookRotation(placeNormal) * Quaternion.Euler(90, 0, 0);
                _controller.playerRpcHandler.RPC_RequestPlaceObject(placeable.Id, placePos, rotation);

                slot.Count -= 1;
                if (slot.Count <= 0)
                {
                    slot.Id = null;
                    slot.State = new ItemState();
                }
                _controller.inventory.RaiseQuickSlotsChanged();
            }
        }
    }
}
