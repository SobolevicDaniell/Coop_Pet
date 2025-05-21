using Fusion;
using UnityEngine;

namespace Game
{
    public class PlaceItemController : MonoBehaviour
    {
        private InteractionController _controller;

        // Вызывается из InteractionController.Spawned()
        public void Initialize(InteractionController controller)
        {
            _controller = controller;
        }

        public void TryPlace()
        {
            // 1. Проверить выбран ли слот
            var selected = _controller.NetSelectedQuickSlot;
            if (selected < 0) return;

            var slots = _controller.Inventory.GetQuickSlots();
            if (selected >= slots.Length) return;
            var slot = slots[selected];

            if (string.IsNullOrEmpty(slot.Id) || slot.Count <= 0) return;

            // 2. Проверить, что предмет — PlaceableItemSO
            var so = _controller.Db.Get(slot.Id);
            if (!(so is PlaceableItemSO placeable)) return;

            // 3. Найти позицию для размещения (луч в экран из центра камеры)
            var camera = _controller.Camera;
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
                // Если не попали — размещаем в воздухе на _rangePlace по направлению взгляда
                placePos = ray.origin + ray.direction * _controller._rangePlace;
                canPlace = true;
            }

            // Можно добавить свою логику валидности точки (например, не ставить в игрока и т.д.)

            if (canPlace)
            {
                var rotation = Quaternion.LookRotation(placeNormal) * Quaternion.Euler(90, 0, 0);
                _controller.RpcHandler.RPC_RequestPlaceObject(placeable.Id, placePos, rotation);

                // Минусуем предмет из слота
                slot.Count -= 1;
                if (slot.Count <= 0)
                {
                    slot.Id = null;
                    slot.State = new ItemState();
                }
                _controller.Inventory.RaiseQuickSlotsChanged();
            }
        }
    }
}
