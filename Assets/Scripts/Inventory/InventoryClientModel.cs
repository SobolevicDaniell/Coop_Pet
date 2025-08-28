using System;
using System.Collections.Generic;

namespace Game
{
    public sealed class InventoryClientModel
    {
        private readonly Dictionary<ContainerId, ContainerSnapshot> _snapshots = new();
        private readonly Dictionary<int, Action<bool,string>> _pending = new();

        public event Action<ContainerId> OnContainerChanged;

        public void ApplySnapshot(ContainerSnapshot snap)
        {
            _snapshots[snap.id] = snap;
            OnContainerChanged?.Invoke(snap.id);
        }

        public void ApplyDelta(ContainerDelta delta)
        {
            if (!_snapshots.TryGetValue(delta.id, out var s)) return;
            if (delta.toVersion <= s.version) return;

            foreach (var ch in delta.changes)
            {
                if (ch.index < 0 || ch.index >= s.slots.Length) continue;
                s.slots[ch.index] = ch.state?.Clone();
            }
            s.version = delta.toVersion;
            OnContainerChanged?.Invoke(delta.id);
        }

        public ContainerSnapshot Get(ContainerId id)
        {
            if (_snapshots.TryGetValue(id, out var s)) return s;
            return null;
        }

        public void TrackOperation(int clientReqId, Action<bool,string> onAck)
        {
            _pending[clientReqId] = onAck;
        }

        public void AckOperation(int clientReqId, bool ok, string message)
        {
            if (_pending.TryGetValue(clientReqId, out var cb))
            {
                _pending.Remove(clientReqId);
                cb?.Invoke(ok, message);
            }
        }
    }
}
