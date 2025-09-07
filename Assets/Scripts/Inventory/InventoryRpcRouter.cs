using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class InventoryRpcRouter : NetworkBehaviour
    {
        private static readonly Dictionary<PlayerRef, InventoryRpcRouter> _byPlayer = new();

        // На сервере (StateAuthority)
        [Inject(Optional = true)] private InventoryServerService _server;
        [Inject(Optional = true)] private InventorySessionServer _session;
        [Inject(Optional = true)] private InventorySnapshotBuilder _snapshots;

        // На клиенте
        [Inject(Optional = true)] private InventoryClientModel _clientModel;
        [Inject(Optional = true)] private InventoryService _clientService;

        public override void Spawned()
        {
            _byPlayer[Object.InputAuthority] = this;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _byPlayer.Remove(Object.InputAuthority);
        }

        // RPC_RequestOpenContainer
[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
public void RPC_RequestOpenContainer(int type, PlayerRef owner, NetworkId objectId, RpcInfo info = default)
{
    if (_server == null) return;

    var id = DecodeId(type, owner, objectId);

    // 👇 берём viewer с фолбэком на InputAuthority (важно для Host)
    var viewer = info.Source;
    if (viewer == PlayerRef.None)
        viewer = Object.InputAuthority;

    id = NormalizeOwnedId(id, viewer);

    if (_server.TryOpenContainer(viewer, id, out var snapshot, out var reason))
    {
        BuildSnapshotArrays(snapshot, out var capacity, out var itemIds, out var counts, out var ammo, out var durability);
        RPC_PushSnapshot(type, id.ownerRef, objectId, snapshot.version, capacity, itemIds, counts, ammo, durability);
        return;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.LogWarning($"[INV] OpenContainer pending, will retry: reason='{reason}', type={id.type}, owner={id.ownerRef}");
#endif

    // 👇 передаём в ретрай именно viewer, а не owner из параметров
    StartCoroutine(RetryOpenContainer(type, id.ownerRef, objectId, viewer));
}

private IEnumerator RetryOpenContainer(int type, PlayerRef owner, NetworkId objectId, PlayerRef viewer, int attempts = 20, float delay = 0.05f)
{
    for (int i = 0; i < attempts; i++)
    {
        yield return new WaitForSeconds(delay);

        if (_server != null)
        {
            var id = DecodeId(type, owner, objectId);
            id = NormalizeOwnedId(id, viewer);

            if (_server.TryOpenContainer(viewer, id, out var snapshot, out var _))
            {
                BuildSnapshotArrays(snapshot, out var capacity, out var itemIds, out var counts, out var ammo, out var durability);
                RPC_PushSnapshot(type, id.ownerRef, objectId, snapshot.version, capacity, itemIds, counts, ammo, durability);
                yield break;
            }
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.LogWarning($"[INV] RetryOpenContainer timeout: type={(ContainerType)type}, owner={owner}, viewer={viewer}");
#endif
}



        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestCloseContainer(int type, PlayerRef owner, NetworkId objectId, RpcInfo info = default)
        {
            var id = DecodeId(type, owner, objectId);
            _session?.Close(info.Source, id);
        }

      [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
public void RPC_RequestTransfer(
    int fromType, PlayerRef fromOwner, NetworkId fromObjectId, int fromIdx,
    int toType,   PlayerRef toOwner,   NetworkId toObjectId,   int toIdx,
    int amount,   int clientReqId, RpcInfo info = default)
{
    if (_server == null) { RPC_OpAck(clientReqId, false, "no_server"); return; }

    // 👇 безопасно определяем актёра
    var actor = info.Source;
    if (actor == PlayerRef.None)
        actor = Object.InputAuthority;

    var fromId = NormalizeOwnedId(DecodeId(fromType, fromOwner, fromObjectId), actor);
    var toId   = NormalizeOwnedId(DecodeId(toType,   toOwner,   toObjectId),   actor);

    if (!_server.TryTransfer(actor, fromId, fromIdx, toId, toIdx, amount,
                             out var fromDelta, out var toDelta, out var swapped, out var reason))
    {
        RPC_OpAck(clientReqId, false, reason ?? "denied");
        return;
    }

    if (fromDelta != null) BroadcastDeltaFromServer(fromDelta);
    if (toDelta   != null) BroadcastDeltaFromServer(toDelta);
    RPC_OpAck(clientReqId, true, "ok");
}
        private ContainerId NormalizeOwnedId(ContainerId id, PlayerRef actor)
        {
            if (id.type == ContainerType.PlayerQuick || id.type == ContainerType.PlayerMain)
                id.ownerRef = actor;
            return id;
        }
        

        

       

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestPickup(string itemId, int amount, int ammo, int clientReqId, RpcInfo info = default)
        {
            if (_server == null) return;

            var ok = _server.TryAddItemToPlayer(
                info.Source, itemId, amount, ammo,
                out var left,
                out var deltas,
                out var reason);

            if (deltas != null)
            {
                foreach (var d in deltas)
                    BroadcastDeltaFromServer(d);
            }

            RPC_OpAck(clientReqId, ok && left == 0, reason ?? ((ok && left == 0) ? "ok" : "not_enough_space"));
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_PushSnapshot(
    int type, PlayerRef owner, NetworkId objectId, int version,
    int capacity, string[] itemIds, int[] counts, int[] ammo, int[] durability, RpcInfo info = default)
        {
            var id = DecodeId(type, owner, objectId);

            var slots = new InventorySlotState[capacity];
            for (int i = 0; i < capacity; i++)
            {
                var idStr = (itemIds != null && i < itemIds.Length) ? itemIds[i] : null;
                var cnt = (counts != null && i < counts.Length) ? counts[i] : 0;
                var amm = (ammo != null && i < ammo.Length) ? ammo[i] : 0;
                var dur = (durability != null && i < durability.Length) ? durability[i] : 0;

                slots[i] = (string.IsNullOrEmpty(idStr) || cnt <= 0)
                    ? null
                    : new InventorySlotState { itemId = idStr, count = cnt, itemState = new ItemState(amm, dur) };
            }

            var snap = new ContainerSnapshot { id = id, version = version, slots = slots };

            if (_clientService != null) _clientService.ApplySnapshot(snap);

            if (_clientModel != null) _clientModel.ApplySnapshot(snap);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_PushDelta(
    int type, PlayerRef owner, NetworkId objectId,
    int fromVersion, int toVersion,
    int[] indices, string[] itemIds, int[] counts, int[] ammo, int[] durability, RpcInfo info = default)
        {
            var id = DecodeId(type, owner, objectId);

            int n = indices != null ? indices.Length : 0;
            var changes = new SlotChange[n];

            for (int k = 0; k < n; k++)
            {
                int idx = indices[k];

                string idStr = (itemIds != null && k < itemIds.Length) ? itemIds[k] : null;
                int cnt = (counts != null && k < counts.Length) ? counts[k] : 0;
                int amm = (ammo != null && k < ammo.Length) ? ammo[k] : 0;
                int dur = (durability != null && k < durability.Length) ? durability[k] : 0;

                InventorySlotState state = (string.IsNullOrEmpty(idStr) || cnt <= 0)
                    ? null
                    : new InventorySlotState { itemId = idStr, count = cnt, itemState = new ItemState(amm, dur) };

                changes[k] = new SlotChange { index = idx, state = state };
            }

            var delta = new ContainerDelta
            {
                id = id,
                fromVersion = fromVersion,
                toVersion = toVersion,
                changes = changes
            };

            if (_clientService != null) _clientService.ApplyDelta(delta);

            if (_clientModel != null) _clientModel.ApplyDelta(delta);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_OpAck(int clientReqId, bool ok, string message, RpcInfo info = default)
        {
            _clientModel?.AckOperation(clientReqId, ok, message);
        }        private bool BroadcastDelta(ContainerDelta delta)
        {
            if (delta == null || _server == null) return false;

            var (t, o, n) = EncodeId(delta.id);
            BuildDeltaArrays(delta,
                out var indices, out var itemIds, out var counts, out var ammo, out var durability);

            bool sent = false;

            foreach (var watcher in _server.Watchers(delta.id))
            {
                if (_byPlayer.TryGetValue(watcher, out var router))
                {
                    router.RPC_PushDelta(
                        t, o, n,
                        delta.fromVersion, delta.toVersion,
                        indices, itemIds, counts, ammo, durability
                    );
                    sent = true;
                }
            }

            return sent;
        }


        private void BroadcastSnapshot(ContainerId id)
        {
            if (_snapshots == null || _server == null) return;

            var snap = _snapshots.Build(id);
            if (snap.slots == null) return;

            BuildSnapshotArrays(snap,
                out var capacity, out var itemIds, out var counts, out var ammo, out var durability);

            var (t, o, n) = EncodeId(id);
            foreach (var watcher in _server.Watchers(id))
            {
                if (_byPlayer.TryGetValue(watcher, out var router))
                {
                    router.RPC_PushSnapshot(t, o, n, snap.version,
                                            capacity, itemIds, counts, ammo, durability);
                }
            }
        }

        private static void BuildSnapshotArrays(ContainerSnapshot snap,
                                                out int capacity, out string[] itemIds, out int[] counts, out int[] ammo, out int[] durability)
        {
            var slots = snap.slots ?? System.Array.Empty<InventorySlotState>();
            capacity = slots.Length;

            itemIds = new string[capacity];
            counts = new int[capacity];
            ammo = new int[capacity];
            durability = new int[capacity];

            for (int i = 0; i < capacity; i++)
            {
                var s = slots[i];
                if (s == null || s.IsEmpty)
                {
                    itemIds[i] = string.Empty;
                    counts[i] = 0;
                    ammo[i] = 0;
                    durability[i] = 0;
                }
                else
                {
                    itemIds[i] = s.itemId ?? string.Empty;
                    counts[i] = s.count;
                    ammo[i] = s.itemState != null ? s.itemState.ammo : 0;
                    durability[i] = s.itemState != null ? s.itemState.durability : 0;
                }
            }
        }

        private static void BuildDeltaArrays(ContainerDelta delta,
                                             out int[] indices, out string[] itemIds, out int[] counts, out int[] ammo, out int[] durability)
        {
            var ch = delta.changes ?? System.Array.Empty<SlotChange>();
            int n = ch.Length;

            indices = new int[n];
            itemIds = new string[n];
            counts = new int[n];
            ammo = new int[n];
            durability = new int[n];

            for (int k = 0; k < n; k++)
            {
                indices[k] = ch[k].index;

                var s = ch[k].state;
                if (s == null || s.IsEmpty)
                {
                    itemIds[k] = string.Empty;
                    counts[k] = 0;
                    ammo[k] = 0;
                    durability[k] = 0;
                }
                else
                {
                    itemIds[k] = s.itemId ?? string.Empty;
                    counts[k] = s.count;
                    ammo[k] = s.itemState != null ? s.itemState.ammo : 0;
                    durability[k] = s.itemState != null ? s.itemState.durability : 0;
                }
            }
        }

        private static (int, PlayerRef, NetworkId) EncodeId(ContainerId id)
        {
            return ((int)id.type, id.ownerRef, id.objectId);
        }

        private static ContainerId DecodeId(int type, PlayerRef owner, NetworkId objectId)
        {
            return new ContainerId { type = (ContainerType)type, ownerRef = owner, objectId = objectId };
        }
        // Внутри InventoryRpcRouter
        // InventoryRpcRouter.cs
        public void BroadcastDeltaFromServer(ContainerDelta delta)
        {
            if (delta == null) return;

            // Подготовим массивы для RPC
            BuildDeltaArrays(delta,
                out var indices, out var itemIds, out var counts, out var ammo, out var durability);

            var (t, o, n) = EncodeId(delta.id);

            // 1) Если серверный сервис заинжектился — используем список watcher'ов
            if (_server != null)
            {
                foreach (var watcher in _server.Watchers(delta.id))
                {
                    if (_byPlayer.TryGetValue(watcher, out var router))
                    {
                        router.RPC_PushDelta(
                            t, o, n,
                            delta.fromVersion, delta.toVersion,
                            indices, itemIds, counts, ammo, durability
                        );
                    }
                }
                return;
            }

            // 2) Фолбэк: шлём хотя бы владельцу контейнера (ownerRef),
            // даже если _server не заинжектился на этой стороне.
            var owner = delta.id.ownerRef;
            if (_byPlayer.TryGetValue(owner, out var ownerRouter))
            {
                ownerRouter.RPC_PushDelta(
                    t, o, n,
                    delta.fromVersion, delta.toVersion,
                    indices, itemIds, counts, ammo, durability
                );
            }
        }



    }
}