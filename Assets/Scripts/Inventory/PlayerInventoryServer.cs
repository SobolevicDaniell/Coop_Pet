using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerInventoryServer : NetworkBehaviour, IInventoryContainer
    {
        [SerializeField] private ContainerType _kind = ContainerType.PlayerMain;

        [SerializeField, Min(0)] private int _capacityOverride = 0;

        [Inject(Optional = true)] private InventoryContainerRegistry _registry;
        [Inject(Optional = true)] private PlayerStatsSO _stats; // источник конфигурации (UI уже его использует)

        private InventorySlotState[] _slots;
        private int _version;

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

            var cap = ResolveCapacity(); // ЕДИНЫЙ источник: SO (+ опциональный оверрайд)
            if (cap < 1) cap = 1;

            _slots = new InventorySlotState[cap];

            Id = _kind == ContainerType.PlayerQuick
                ? ContainerId.PlayerQuickOf(Object.InputAuthority)
                : ContainerId.PlayerMainOf(Object.InputAuthority);

            _registry?.Register(this);

            // Диагностика (можно удалить позже)
            Debug.Log($"[InvServer] {Object.InputAuthority} {_kind} capacity={cap} (SO={GetSoCapacityOrMinus1()}, Override={_capacityOverride})");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (!Object.HasStateAuthority) return;
            _registry?.Unregister(Id);
        }

        public bool CanPlayerAccess(PlayerRef player) => player == Object.InputAuthority;
        public bool CanAccept(int slotIndex, InventorySlotState incoming) => true;

        public void SetSlot(int slotIndex, InventorySlotState state) => _slots[slotIndex] = state?.Clone();
        public void IncrementVersion() => _version++; 

        private int ResolveCapacity()
        {
            if (_capacityOverride > 0) return _capacityOverride;
            var soCap = GetSoCapacityOrMinus1();
            if (soCap > 0) return soCap;

            return _kind == ContainerType.PlayerQuick ? 10 : 24;
        }

        private int GetSoCapacityOrMinus1()
        {
            if (_stats == null) return -1;
            return _kind == ContainerType.PlayerQuick ? _stats.quickSlotsCount : _stats.inventorySlotsCount;
        }
    }
}
