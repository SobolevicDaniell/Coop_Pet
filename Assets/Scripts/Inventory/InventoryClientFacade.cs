using System;
using System.Collections;
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public sealed partial class InventoryClientFacade : MonoBehaviour
    {

        [Inject(Optional = true)] private NetworkRunner _runner;
        public event Action<ContainerId> OnContainerChanged;

        private PlayerRef _local;
        private InventoryRpcRouter _router;
        private NetworkId _localPlayerObjectId;

        [Inject(Optional = true)] private InventoryClientModel _clientModel;

        private int _clientReqSeq;
        private int _req;

        private void OnEnable()
        {
            if (_clientModel != null)
                _clientModel.OnContainerChanged += HandleModelContainerChanged;
        }

        private void OnDisable()
        {
            if (_clientModel != null)
                _clientModel.OnContainerChanged -= HandleModelContainerChanged;
        }

        public void SetLocal(PlayerRef localPlayer, InventoryRpcRouter router)
        {
            _local = localPlayer;
            _router = router;
        }

        public void SetLocal(PlayerRef localPlayer, InventoryRpcRouter router, NetworkId localPlayerObjectId)
        {
            _local = localPlayer;
            _router = router;
            _localPlayerObjectId = localPlayerObjectId;
        }

        public void OpenLocalQuick()
        {
            if (_router == null) return;
            StartCoroutine(_router.RetryOpenContainer((int)ContainerType.PlayerQuick, _local, _localPlayerObjectId));
        }

        public void OpenLocalMain()
        {
            if (_router == null) return;
            StartCoroutine(_router.RetryOpenContainer((int)ContainerType.PlayerMain, _local, _localPlayerObjectId));
        }

        public void Open(ContainerId id)
        {
            if (_router == null) return;
            StartCoroutine(_router.RetryOpenContainer((int)id.type, id.ownerRef, id.objectId));
        }

        public void Close(ContainerId id)
        {
            if (_router == null) return;
            _router.RPC_RequestCloseContainer((int)id.type, id.ownerRef, id.objectId);
        }

        public bool TryGetSnapshot(ContainerId id, out int version, out InventorySlotState[] slots)
        {
            version = 0;
            slots = null;
            if (_clientModel == null) return false;

            var snap = _clientModel.Get(id);
            if (snap == null || snap.slots == null) return false;

            version = snap.version;
            slots = snap.slots;
            return true;
        }

        public bool TryGetSnapshotResolved(ContainerId probe, out ContainerId resolvedId, out int version, out InventorySlotState[] slots)
        {
            resolvedId = probe;
            if (TryGetSnapshot(probe, out version, out slots)) return true;

            version = 0;
            slots = null;
            if (_clientModel != null && _clientModel.TryResolveExistingId(probe, out var rId))
            {
                if (TryGetSnapshot(rId, out version, out slots))
                {
                    resolvedId = rId;
                    return true;
                }
            }
            return false;
        }

        public int GetCapacityImmediate(ContainerId id)
        {
            if (TryGetSnapshot(id, out _, out var slots) && slots != null)
                return slots.Length;

            if (_runner == null || id.objectId == default) return 0;

            var no = _runner.FindObject(id.objectId);
            if (no == null) return 0;

            var corpse = no.GetComponent<CorpseInventoryServer>();
            if (corpse != null) return Mathf.Max(0, corpse.SlotsCapacity);

            var chest = no.GetComponent<ChestInventoryServer>();
            if (chest != null) return Mathf.Max(0, chest.SlotsCapacity);

            return 0;
        }

        public int GetLocalQuickCapacity()
        {
            var id = new ContainerId { type = ContainerType.PlayerQuick, ownerRef = _local, objectId = default };
            return TryGetSnapshot(id, out _, out var slots) ? (slots?.Length ?? 0) : 0;
        }

        public int GetLocalMainCapacity()
        {
            var id = new ContainerId { type = ContainerType.PlayerMain, ownerRef = _local, objectId = default };
            return TryGetSnapshot(id, out _, out var slots) ? (slots?.Length ?? 0) : 0;
        }

        public ContainerId localQuick => new ContainerId { type = ContainerType.PlayerQuick, ownerRef = _local, objectId = default };
        public ContainerId localMain => new ContainerId { type = ContainerType.PlayerMain, ownerRef = _local, objectId = default };

        public void Transfer(ContainerId fromId, int fromIdx, ContainerId toId, int toIdx, int amount, Action<bool, string> onAck)
        {
            if (_router == null)
            {
                onAck?.Invoke(false, "no_router");
                return;
            }

            if (amount <= 0) amount = 1;

            int reqId = ++_clientReqSeq;

            _router.RPC_RequestTransfer(
                (int)fromId.type, fromId.ownerRef, fromId.objectId, fromIdx,
                (int)toId.type, toId.ownerRef, toId.objectId, toIdx,
                amount, reqId);

            onAck?.Invoke(true, "sent");
        }

        private void HandleModelContainerChanged(ContainerId id)
        {
            OnContainerChanged?.Invoke(id);
        }
        
        
    }
}
