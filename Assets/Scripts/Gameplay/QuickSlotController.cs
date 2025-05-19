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

            var currentSelectedSlot = _ic.NetSelectedQuickSlot;

            if (currentSelectedSlot == slot)
            {
                // Повторное нажатие на активный слот — отключаем его
                _rpc.RPC_SelectQuickSlot(-1);
                _rpc.RPC_RequestDespawnHandModel();
            }
            else
            {
                // Переключение на новый слот
                var id = _inv.GetQuickSlots()[slot].Id;

                if (!string.IsNullOrEmpty(id))
                {
                    _rpc.RPC_SelectQuickSlot(slot);
                    _rpc.RPC_RequestSpawnHandModel(id);
                }
                else
                {
                    // Если слот пустой — сбрасываем выбор и деспавним модель
                    _rpc.RPC_SelectQuickSlot(-1);
                    _rpc.RPC_RequestDespawnHandModel();
                }
            }
        }

        public void ChangeSlotRelative(int d)
        {
            if (!_ic.Object.HasInputAuthority) return;

            var slots = _inv.GetQuickSlots();
            int cnt = slots.Length;
            int cur = _ic.NetSelectedQuickSlot < 0 ? 0 : _ic.NetSelectedQuickSlot;
            int next = (cur + d + cnt) % cnt;

            int checkedSlots = 0;
            while (string.IsNullOrEmpty(slots[next].Id) && checkedSlots < cnt)
            {
                next = (next + d + cnt) % cnt;
                checkedSlots++;
            }
            if (string.IsNullOrEmpty(slots[next].Id))
                return;

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
