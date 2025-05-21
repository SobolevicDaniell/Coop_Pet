using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public class QuickSlotController : MonoBehaviour
    {
        [Inject] public InventoryService Inventory { get; private set; }

        private InteractionController _ic;
        private ServerRpcHandler _rpc;

        // Важно: вызывай после спавна и Inject
        public void Initialize(InteractionController controller)
        {
            _ic = controller;
            _rpc = controller.RpcHandler;
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
                var id = Inventory.GetQuickSlots()[slot].Id;

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

            var slots = Inventory.GetQuickSlots();
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
                if (newSlot >= 0) Inventory.SetQuickSlot(newSlot);
                else Inventory.ClearQuickSlot();
            }
        }
    }
}
