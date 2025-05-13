using Fusion;

namespace Game
{
    public class QuickSlotController
    {
        readonly InteractionController _ic;
        readonly ServerRpcHandler _rpc;
        readonly InventoryService _inv;

        public QuickSlotController(
          InteractionController ic,
          ServerRpcHandler rpc,
          InventoryService inv
        )
        {
            _ic = ic;
            _rpc = rpc;
            _inv = inv;
        }

        public void ChangeSlotAbsolute(int slot)
        {
            if (!_ic.Object.HasInputAuthority) return;

            // 1) обновляем property на сервере
            _rpc.RPC_SelectQuickSlot(slot);

            // 2) сразу просим спавн/деспавн
            var id = _inv.GetQuickSlots()[slot].Id;
            if (!string.IsNullOrEmpty(id))
                _rpc.RPC_RequestSpawnHandModel(id);
            else
                _rpc.RPC_RequestDespawnHandModel();

        }

        public void ChangeSlotRelative(int d)
        {
            if (!_ic.Object.HasInputAuthority) return;
            var slots = _inv.GetQuickSlots();
            int cnt = slots.Length;
            int cur = _ic.NetSelectedQuickSlot < 0 ? 0 : _ic.NetSelectedQuickSlot;
            int next = (cur + d + cnt) % cnt;
            ChangeSlotAbsolute(next);
        }

        public void OnNetworkSlotChanged(int newSlot)
        {
            if (_ic.Object.HasInputAuthority)
            {
                if (newSlot >= 0) _inv.SetQuickSlot(newSlot);
                else _inv.ClearQuickSlot();
            }
        }
    }
}
