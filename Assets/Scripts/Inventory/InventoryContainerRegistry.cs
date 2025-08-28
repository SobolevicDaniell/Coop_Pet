using System.Collections.Generic;
using Fusion;

namespace Game
{
    public sealed class InventoryContainerRegistry
    {
        private readonly Dictionary<ContainerId, IInventoryContainer> _containers = new();
        private readonly Dictionary<ContainerId, HashSet<PlayerRef>> _watchers = new();

        public void Register(IInventoryContainer container)
        {
            _containers[container.Id] = container;
            if (!_watchers.ContainsKey(container.Id)) _watchers[container.Id] = new HashSet<PlayerRef>();
        }

        public void Unregister(ContainerId id)
        {
            _containers.Remove(id);
            _watchers.Remove(id);
        }

        public bool TryGet(ContainerId id, out IInventoryContainer container) => _containers.TryGetValue(id, out container);

        public void AddWatcher(ContainerId id, PlayerRef viewer)
        {
            if (!_watchers.TryGetValue(id, out var set)) { set = new HashSet<PlayerRef>(); _watchers[id] = set; }
            set.Add(viewer);
        }

        public void RemoveWatcher(ContainerId id, PlayerRef viewer)
        {
            if (_watchers.TryGetValue(id, out var set)) set.Remove(viewer);
        }

        public IEnumerable<PlayerRef> GetWatchers(ContainerId id)
        {
            if (_watchers.TryGetValue(id, out var set)) return set;
            return System.Array.Empty<PlayerRef>();
        }
    }
}
