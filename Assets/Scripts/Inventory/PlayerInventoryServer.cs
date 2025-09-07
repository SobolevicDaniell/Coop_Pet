// Scripts/Inventory/PlayerInventoryServer.cs
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerInventoryServer : NetworkBehaviour, IInventoryContainer
    {
        [SerializeField] private ContainerType _kind = ContainerType.PlayerMain;

        // РЕЗЕРВ через инспектор (тот же SO, что использует UI)
        [SerializeField] private PlayerStatsSO _statsSerialized;

        // DI-источник (как и было)
        [Inject(Optional = true)] private PlayerStatsSO _statsDI;

        private InventorySlotState[] _slots;
        private int _version;

        [Inject(Optional = true)] private InventoryContainerRegistry _registry;

        public ContainerId Id { get; private set; }
        public int Version => _version;
        public int Capacity => _slots?.Length ?? 0;
        public InventorySlotState[] Slots => _slots;

        public override void Spawned()
        {
            if (!Object.HasStateAuthority) return;

            if (_kind != ContainerType.PlayerMain && _kind != ContainerType.PlayerQuick)
            {
                Debug.LogError($"[PlayerInventoryServer] Unsupported kind '{_kind}'. Use PlayerMain or PlayerQuick.");
                _kind = ContainerType.PlayerMain;
            }

            var so = _statsSerialized != null ? _statsSerialized : _statsDI;
            var cap = ResolveCapacityFromSO(so, _kind);
            if (cap <= 0) cap = 1;

            _slots = new InventorySlotState[cap];

            Id = _kind == ContainerType.PlayerQuick
                ? ContainerId.PlayerQuickOf(Object.InputAuthority)
                : ContainerId.PlayerMainOf(Object.InputAuthority);

            _registry?.Register(this);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var soName = so ? $"{so.name}#{so.GetInstanceID()}" : "null";
            Debug.Log($"[INV][Server] Spawned {Id.type} for {Id.ownerRef}, capacity={_slots.Length}, SO={soName}");
#endif
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (!Object.HasStateAuthority) return;
            _registry?.Unregister(Id);
        }

        private static int ResolveCapacityFromSO(PlayerStatsSO so, ContainerType kind)
        {
            if (so == null) return 1;
            return kind == ContainerType.PlayerQuick ? so.quickSlotsCount : so.inventorySlotsCount;
        }

        public bool CanPlayerAccess(PlayerRef player) => player == Object.InputAuthority;
        public bool CanAccept(int slotIndex, InventorySlotState incoming) => true;

        public void SetSlot(int index, InventorySlotState state)
        {
            if (_slots == null || index < 0 || index >= _slots.Length) return;
            // Храним клон с itemState (Clone() уже глубокий)
            _slots[index] = state?.Clone();
        }

        public void IncrementVersion() => _version++;
    }
}