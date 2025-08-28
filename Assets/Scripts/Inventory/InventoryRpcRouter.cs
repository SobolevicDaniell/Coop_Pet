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

        // На клиенте
        [Inject(Optional = true)] private InventoryClientModel _clientModel;

        public override void Spawned()
        {
            // Регистрируем роутер для игрока с данным InputAuthority на ВСЕХ сторонах
            _byPlayer[Object.InputAuthority] = this;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _byPlayer.Remove(Object.InputAuthority);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // CLIENT → SERVER
        // ─────────────────────────────────────────────────────────────────────────────

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestOpenContainer(int type, PlayerRef owner, NetworkId objectId, RpcInfo info = default)
        {
            if (_session == null || _server == null) return;

            var id = DecodeId(type, owner, objectId);
            if (_session.Open(info.Source, id, out var snapshot))
            {
                BuildSnapshotArrays(snapshot,
                    out var capacity, out var itemIds, out var counts, out var ammo, out var durability);

                RPC_PushSnapshot(type, owner, objectId, snapshot.version,
                                 capacity, itemIds, counts, ammo, durability);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestCloseContainer(int type, PlayerRef owner, NetworkId objectId, RpcInfo info = default)
        {
            var id = DecodeId(type, owner, objectId);
            _session?.Close(info.Source, id);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestTransfer(int fType, PlayerRef fOwner, NetworkId fObj, int fIdx,
                                        int tType, PlayerRef tOwner, NetworkId tObj, int tIdx,
                                        int amount, int clientReqId, RpcInfo info = default)
        {
            if (_server == null) return;

            var fromId = DecodeId(fType, fOwner, fObj);
            var toId   = DecodeId(tType, tOwner, tObj);

            if (_server.TryTransfer(info.Source, fromId, fIdx, toId, tIdx, amount,
                                    out var fromDelta, out var toDelta, out var swapped))
            {
                BroadcastDelta(fromDelta);
                if (!fromId.Equals(toId))
                    BroadcastDelta(toDelta);

                RPC_OpAck(clientReqId, true, swapped ? "swapped" : "moved");
            }
            else
            {
                RPC_OpAck(clientReqId, false, "denied");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // SERVER → CLIENT
        // ─────────────────────────────────────────────────────────────────────────────

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_PushSnapshot(int type, PlayerRef owner, NetworkId objectId, int version,
                                      int capacity, string[] itemIds, int[] counts, int[] ammo, int[] durability, RpcInfo info = default)
        {
            if (_clientModel == null) return;

            var id = DecodeId(type, owner, objectId);

            var slots = new InventorySlotState[capacity];
            for (int i = 0; i < capacity; i++)
            {
                var idStr = (itemIds != null && i < itemIds.Length) ? itemIds[i] : null;
                var cnt   = (counts  != null && i < counts.Length ) ? counts[i]  : 0;
                var amm   = (ammo    != null && i < ammo.Length   ) ? ammo[i]    : 0;
                var dur   = (durability != null && i < durability.Length) ? durability[i] : 0;

                if (string.IsNullOrEmpty(idStr) || cnt <= 0)
                {
                    slots[i] = null;
                }
                else
                {
                    slots[i] = new InventorySlotState
                    {
                        itemId = idStr,
                        count = cnt,
                        itemState = new ItemState(amm, dur)
                    };
                }
            }

            var snap = new ContainerSnapshot { id = id, version = version, slots = slots };
            _clientModel.ApplySnapshot(snap);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_PushDelta(int type, PlayerRef owner, NetworkId objectId,
                                   int fromVersion, int toVersion,
                                   int[] indices, string[] itemIds, int[] counts, int[] ammo, int[] durability, RpcInfo info = default)
        {
            if (_clientModel == null) return;

            var id = DecodeId(type, owner, objectId);

            int n = indices != null ? indices.Length : 0;
            var changes = new SlotChange[n];

            for (int k = 0; k < n; k++)
            {
                int idx = indices[k];

                string idStr = (itemIds != null && k < itemIds.Length) ? itemIds[k] : null;
                int cnt      = (counts  != null && k < counts.Length ) ? counts[k]  : 0;
                int amm      = (ammo    != null && k < ammo.Length   ) ? ammo[k]    : 0;
                int dur      = (durability != null && k < durability.Length) ? durability[k] : 0;

                InventorySlotState state;
                if (string.IsNullOrEmpty(idStr) || cnt <= 0)
                {
                    state = null;
                }
                else
                {
                    state = new InventorySlotState
                    {
                        itemId = idStr,
                        count = cnt,
                        itemState = new ItemState(amm, dur)
                    };
                }

                changes[k] = new SlotChange { index = idx, state = state };
            }

            var delta = new ContainerDelta
            {
                id = id,
                fromVersion = fromVersion,
                toVersion = toVersion,
                changes = changes
            };

            _clientModel.ApplyDelta(delta);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_OpAck(int clientReqId, bool ok, string message, RpcInfo info = default)
        {
            _clientModel?.AckOperation(clientReqId, ok, message);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Helpers (server side)
        // ─────────────────────────────────────────────────────────────────────────────

        private void BroadcastDelta(ContainerDelta delta)
        {
            if (delta == null || _server == null) return;

            var (t, o, n) = EncodeId(delta.id);
            BuildDeltaArrays(delta,
                out var indices, out var itemIds, out var counts, out var ammo, out var durability);

            foreach (var watcher in _server.Watchers(delta.id))
            {
                if (_byPlayer.TryGetValue(watcher, out var router))
                {
                    router.RPC_PushDelta(t, o, n, delta.fromVersion, delta.toVersion,
                                         indices, itemIds, counts, ammo, durability);
                }
            }
        }

        private static void BuildSnapshotArrays(ContainerSnapshot snap,
                                                out int capacity, out string[] itemIds, out int[] counts, out int[] ammo, out int[] durability)
        {
            var slots = snap.slots ?? System.Array.Empty<InventorySlotState>();
            capacity = slots.Length;

            itemIds    = new string[capacity];
            counts     = new int[capacity];
            ammo       = new int[capacity];
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

            indices    = new int[n];
            itemIds    = new string[n];
            counts     = new int[n];
            ammo       = new int[n];
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
    }
}
