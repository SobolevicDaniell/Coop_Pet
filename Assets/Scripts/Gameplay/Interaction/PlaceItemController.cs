using Fusion;
using UnityEngine;

namespace Game
{
    public class PlaceItemController : MonoBehaviour
    {
        private InteractionController _controller;
        private Camera _camera;

        public void Initialize(InteractionController controller)
        {
            _controller = controller;
            // Берём камеру из контроллера (он возвращает Camera.main), либо запасной Camera.main
            _camera = controller != null ? controller.camera : Camera.main;
        }

        public void TryPlace()
        {
            if (_controller == null || !_controller.Object.HasInputAuthority) return;

            int selected = _controller.netSelectedQuickSlot;
            if (selected < 0) return;

            var slots = _controller.inventory.GetQuickSlots();
            if (selected >= slots.Length) return;
            var slot = slots[selected];

            if (string.IsNullOrEmpty(slot.Id) || slot.Count <= 0) return;

            var so = _controller.db.Get(slot.Id);
            if (so is not PlaceableItemSO placeable) return;

            // НЕ Ищем камеру на игроке! Берём из кэша/Camera.main
            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null)
            {
                Debug.LogError("[PlaceItemController] Camera is NULL. Проставь тег MainCamera на игровой камере.");
                return;
            }

            // Луч из центра экрана (удобнее для клавиши F)
            Vector3 screenPoint = new(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            Ray ray = cam.ScreenPointToRay(screenPoint);

            Vector3 placePos;
            Vector3 up;

            if (Physics.Raycast(ray, out var hit, _controller._rangePlace, ~0, QueryTriggerInteraction.Ignore))
            {
                placePos = hit.point;
                up = hit.normal;
            }
            else
            {
                // Фоллбек — ставим по направлению камеры на дальности
                placePos = cam.transform.position + cam.transform.forward * _controller._rangePlace;
                up = Vector3.up;
            }

            // Ориентация: up = нормаль поверхности, forward = проекция взгляда на плоскость
            Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, up);
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.Cross(up, Vector3.right);
            forward.Normalize();
            Quaternion rotation = Quaternion.LookRotation(forward, up);

            _controller.playerRpcHandler.RPC_RequestPlaceObject(placeable.Id, placePos, rotation);

            // Списываем предмет из слота
            slot.Count -= 1;
            if (slot.Count <= 0)
            {
                slot.Id = null;
                slot.State = null; // именно null, чтобы слот считался пустым
            }
            _controller.inventory.RaiseQuickSlotsChanged();
        }
    }
}
