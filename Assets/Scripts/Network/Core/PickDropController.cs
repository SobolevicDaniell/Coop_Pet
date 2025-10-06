using Fusion;
using UnityEngine;
using Game.UI;

namespace Game
{
    public class PickDropController : MonoBehaviour
    {
        private InteractionController _ic;
        private PlayerRpcHandler _rpc;
        private InventoryService _inventory;
        private ItemDatabaseSO _db;
        private UIController _ui;
        private InventoryPanel _playerPanel;
        private OtherInventoryPanel _otherPanel;
        private InteractionPromptView _prompt;
        private Camera _overrideCamera;

        public void Construct(
            InteractionController ic,
            PlayerRpcHandler rpc,
            InventoryService inventory,
            InventoryPanel playerPanel,
            OtherInventoryPanel otherPanel,
            ItemDatabaseSO db,
            UIController ui,
            InteractionPromptView prompt,
            Camera camOverride)
        {
            _ic = ic;
            _rpc = rpc;
            _inventory = inventory;
            _playerPanel = playerPanel;
            _otherPanel = otherPanel;
            _db = db;
            _ui = ui;
            _prompt = prompt;
            _overrideCamera = camOverride;
        }

        private Camera Cam => _overrideCamera != null ? _overrideCamera : _ic?.camera;

        public void UpdateRaycast()
        {
            if (_ic == null || !_ic.Object.HasInputAuthority) return;
            var cam = Cam;
            if (cam == null) { _prompt?.Hide(); return; }
            var center = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f);
            var ray = cam.ScreenPointToRay(center);
            if (Physics.Raycast(ray, out var hit, _ic.range, ~0, QueryTriggerInteraction.Collide)) { }
            else { _prompt?.Hide(); }


        }

        public void TryPickAtCrosshair()
        {
            if (_ic == null || !_ic.Object.HasInputAuthority || _rpc == null) return;
            var cam = Cam;
            if (cam == null) return;

            var origin = cam.transform.position;
            var dir = cam.transform.forward;
            var range = _ic.range;

            _rpc.RPC_RequestPickAtRay(origin, dir, range);
        }

        

        public void TryPick()
        {
            TryPickAtCrosshair();
        }

        public void TryDrop()
        {
            if (_ic == null || !_ic.Object.HasInputAuthority || _rpc == null) return;

            Vector3 origin;
            Vector3 forward;
            var cam = Cam;

            if (_ic.dropPoint != null)
            {
                origin = _ic.dropPoint.position;
                forward = _ic.dropPoint.forward;
            }
            else if (cam != null)
            {
                origin = cam.transform.position + cam.transform.forward * 0.2f;
                forward = cam.transform.forward;
            }
            else
            {
                origin = _ic.transform.position + _ic.transform.forward * 0.5f;
                forward = _ic.transform.forward;
            }

            TryDropFromQuickSlot(origin, forward, true);
        }

        public void TryDropFromQuickSlot(Vector3 origin, Vector3 forward, bool dropAll)
        {
            if (_ic == null || !_ic.Object.HasInputAuthority || _rpc == null || _inventory == null || _db == null) return;

            int idx = _inventory.SelectedQuickSlot;
            var slots = _inventory.GetQuickSlots();
            if (slots == null || idx < 0 || idx >= slots.Length) return;

            var slot = slots[idx];
            if (slot == null || string.IsNullOrEmpty(slot.Id) || slot.Count <= 0) return;

            var item = _db.Get(slot.Id);
            if (item == null) return;

            int dropCount = dropAll ? slot.Count : 1;

            _rpc.RPC_RequestDrop(origin, forward, idx, dropCount);
        }


        public void TryPlaceFromQuickSlot(Vector3 pos, Quaternion rot)
        {
            if (_ic == null || !_ic.Object.HasInputAuthority || _rpc == null || _inventory == null) return;
            int idx = _inventory.SelectedQuickSlot;
            var slots = _inventory.GetQuickSlots();
            if (slots == null || idx < 0 || idx >= slots.Length) return;
            var slot = slots[idx];
            if (slot == null || string.IsNullOrEmpty(slot.Id)) return;
            _rpc.RPC_RequestPlaceObject(slot.Id, pos, rot);
        }


        public void OpenPlayerInventory()
        {
        }

        public void CloseOpenedInventories()
        {
        }
    }
}
