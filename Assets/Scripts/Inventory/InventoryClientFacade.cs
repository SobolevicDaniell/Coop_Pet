using System;
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public sealed class InventoryClientFacade
    {
        private readonly InventoryClientModel _model;

        public ContainerId localQuick;
        public ContainerId localMain;

        private InventoryRpcRouter _router;

        // УБРАНО: зависимость на ContainerViewSessionClient, она и создавала цикл
        // [Inject] private ContainerViewSessionClient _view;

        public InventoryClientFacade(InventoryClientModel model)
        {
            _model = model;
        }

        public void SetLocal(PlayerRef pref, InventoryRpcRouter router)
        {
            localQuick = ContainerId.PlayerQuickOf(pref);
            localMain  = ContainerId.PlayerMainOf(pref);
            _router    = router;
        }

        public void ClearLocal()
        {
            _router    = null;
            localQuick = default;
            localMain  = default;
        }

        public void Open(ContainerId id)
        {
            if (_router == null) return;
            _router.RPC_RequestOpenContainer((int)id.type, id.ownerRef, id.objectId);
        }

        public void Close(ContainerId id)
        {
            if (_router == null) return;
            _router.RPC_RequestCloseContainer((int)id.type, id.ownerRef, id.objectId);
        }

        public void OpenLocalQuick()
        {
            if (IsValidId(localQuick)) Open(localQuick);
        }

        public void OpenLocalMain()
        {
            if (IsValidId(localMain)) Open(localMain);
        }

        public void CloseLocalQuick()
        {
            if (IsValidId(localQuick)) Close(localQuick);
        }

        public void CloseLocalMain()
        {
            if (IsValidId(localMain)) Close(localMain);
        }

        public void Transfer(
            ContainerId from, int fromIdx,
            ContainerId to,   int toIdx,
            int amount,
            Action<bool, string> onAck = null)
        {
            if (_router == null) return;

            var reqId = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            if (onAck != null) _model.TrackOperation(reqId, onAck);

            _router.RPC_RequestTransfer(
                (int)from.type, from.ownerRef, from.objectId, fromIdx,
                (int)to.type,   to.ownerRef,   to.objectId,   toIdx,
                amount, reqId
            );
        }

        public int GetCapacity(ContainerId id)
        {
            var snap = _model.Get(id);
            return snap?.slots?.Length ?? 0;
        }

        public int GetLocalQuickCapacity() => GetCapacity(localQuick);
        public int GetLocalMainCapacity()  => GetCapacity(localMain);

        private static bool IsValidId(in ContainerId id)
        {
            return id.type != 0 && id.ownerRef != PlayerRef.None;
        }
    }
}
