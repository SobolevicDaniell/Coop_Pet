using Fusion;
using UnityEngine;
using Zenject;
using Game.UI;
using Game.Network;

namespace Game
{
    [RequireComponent(typeof(PlayerRpcHandler), typeof(NetworkObject))]
    public class InteractionController : NetworkBehaviour
    {
        [Header("Points")]
        [SerializeField] private Transform _handPoint;
        [SerializeField] private Transform _dropPoint;

        [Header("Ranges / Forces")]
        [SerializeField] private float _rangePick = 4f;
        [SerializeField] private float _rangePlace = 8f;
        [SerializeField] private float _throwForce = 5f;

        // сценовые сервисы / данные
        [Inject(Optional = true)] private InputHandler _input;
        [Inject] private InteractionPromptView _prompt;
        [Inject] private InventoryService _inventory;
        [Inject] private HandItemBehaviorFactory _factory;
        [Inject] private ItemDatabaseSO _db;
        [Inject] private UIHealthView _uiHealth;
        [Inject] private UIController _uiController;
        [Inject(Id = "PlayerInventoryPanel")] private InventoryPanel _playerPanel;
        [Inject(Id = "OtherInventoryPanel")] private OtherInventoryPanel _otherPanel;
        [Inject(Optional = true)] private QuickSlotPanel _quickSlotPanel;
        [Inject(Optional = true)] private PlayerCameraController _playerCam;

        // компоненты на игроке
        [Inject] private PlayerRpcHandler _playerRpc;
        [Inject] private HandItemController _handItemController;
        [Inject] private ItemEquipController _itemEquip;
        [Inject] private PickDropController _pickDrop;
        [Inject] private PlaceItemController _placeItem;
        [Inject] private QuickSlotController _quickSlot;
        [Inject] private HealthComponent _health;
        [Inject] private InventoryTransferController _transfer;

        // текущее поведение
        public IHandItemBehavior currentBehavior { get; private set; }

        [Networked] public int netSelectedQuickSlot { get; set; } = -1;

        [Networked] public NetworkId handModelNetId { get; set; }
        private NetworkObject _handModelNetObj;

        // API доступа
        public Transform handPoint => _handPoint;
        public Transform dropPoint => _dropPoint;
        public float range => _rangePick;
        public float PlaceRange => _rangePlace;
        public float ThrowForce => _throwForce;

        public Camera camera
        {
            get
            {
                if (_playerCam != null)
                {
                    var cam = _playerCam.GetComponentInChildren<Camera>(true);
                    if (cam != null) return cam;
                }
                return Object.HasInputAuthority ? Camera.main : null;
            }
        }

        public ItemDatabaseSO db => _db;
        public InventoryService inventory => _inventory;
        public UIController uiController => _uiController;
        public PlayerRpcHandler playerRpcHandler => _playerRpc;
        public HandItemController handItemController => _handItemController;
        public QuickSlotController quickSlot => _quickSlot;
        public PickDropController pickDrop => _pickDrop;
        public PlaceItemController placeItem => _placeItem;
        public ItemEquipController itemEquip => _itemEquip;

        // lifecycle
        private bool _localInitialized;
        private string _lastEquipKey; // index:itemId:stateHash

        public override void Spawned()
        {
            _playerRpc ??= GetComponent<PlayerRpcHandler>();
            _handItemController ??= GetComponent<HandItemController>();
            _itemEquip ??= GetComponent<ItemEquipController>();
            _pickDrop ??= GetComponent<PickDropController>();
            _placeItem ??= GetComponent<PlaceItemController>();
            _quickSlot ??= GetComponent<QuickSlotController>();
            _health ??= GetComponent<HealthComponent>();

            _quickSlot?.Construct(this, _inventory);
            _handItemController?.Construct(_db, _playerRpc, this);
            _placeItem?.Construct(this);
            _itemEquip?.Initialize(_factory, _db, this);
            // _playerPanel?.Construct(_inventory);
            _pickDrop?.Construct(this, _playerRpc, _inventory, _playerPanel, _otherPanel, _db, _uiController, _prompt, null);

            var inputHandler = GetComponent<InteractionInputHandler>();
            inputHandler?.Construct(this, _input, _inventory, _prompt);

            // ИЗМЕНЕНИЕ: конструируем RPC на обеих сторонах
            _playerRpc?.Construct(_db, this, _inventory);

            if (Object.HasInputAuthority)
            {
                _playerPanel?.Construct(_inventory);
                _quickSlot?.EnableForLocal();
                _health?.Initialize(_uiHealth, Object.HasStateAuthority, true);
                _quickSlotPanel?.InitializeIfLocal(this);
                _transfer?.Initialize(_inventory, _playerPanel, _quickSlotPanel, _otherPanel);

                if (_input != null)
                {
                    _input.OnInventoryToggle += ToggleInventory;
                    _input.OnGlobalUiCloseRequested += CloseInventory;
                }

                if (_inventory != null)
                {
                    _inventory.OnQuickSlotsChanged += OnQuickSlotsChanged;
                    OnQuickSlotsChanged();
                }

                _localInitialized = true;
            }
        }

        private void Update()
        {
            if (Object.HasInputAuthority)
                _pickDrop?.UpdateRaycast();
        }

        private void OnDestroy()
        {
            if (_inventory != null)
                _inventory.OnQuickSlotsChanged -= OnQuickSlotsChanged;

            if (_localInitialized && _input != null)
            {
                _input.OnInventoryToggle -= ToggleInventory;
                _input.OnGlobalUiCloseRequested -= CloseInventory;
            }
            _quickSlot?.DisableForLocal();
        }

        // API для поведения в руках
        public void SetCurrentBehavior(IHandItemBehavior behavior)
        {
            currentBehavior = behavior;
        }

        public void ClearBehavior()
        {
            currentBehavior?.OnUnequip();
            currentBehavior = null;
        }

        public void SetHandModelNetworkInstance(NetworkObject netObj)
        {
            _handModelNetObj = netObj;
            handModelNetId = (netObj != null) ? netObj.Id : default;
        }

        public NetworkObject GetHandModelNetworkInstance()
        {
            if (_handModelNetObj != null) return _handModelNetObj;
            if (handModelNetId != default && Runner != null)
                _handModelNetObj = Runner.FindObject(handModelNetId);
            return _handModelNetObj;
        }

        public void InvokeOnQuickSlotsChanged() => OnQuickSlotsChanged();

        // UI
        private void ToggleInventory()
        {
            if (!Object.HasInputAuthority) return;

            if (_uiController.InventoryOpened)
            {
                _uiController.ShowGameUI();
                _pickDrop.CloseOpenedInventories();
            }
            else
            {
                _uiController.ShowInventory();
                _pickDrop.OpenPlayerInventory();
            }
        }

        private void CloseInventory()
        {
            if (!Object.HasInputAuthority) return;

            _uiController.ShowGameUI();
            _pickDrop.CloseOpenedInventories();
        }

        // быстрые слоты → экип
        private void OnQuickSlotsChanged()
        {
            if (_itemEquip == null || _inventory == null)
                return;

            int idx = _inventory.SelectedQuickSlot;
            var slots = _inventory.GetQuickSlots();

            if (slots == null || idx < 0 || idx >= slots.Length)
            {
                _itemEquip.Equip(-1, slots);
                _lastEquipKey = null;
                return;
            }

            var slot = slots[idx];
            string id = slot?.Id;

            string stateHash = slot?.State != null ? $"{slot.State.Ammo}" : "null";
            string key = $"{idx}:{id}:{stateHash}";

            if (_lastEquipKey != key)
            {
                if (string.IsNullOrEmpty(id))
                    _itemEquip.Equip(-1, slots);
                else
                    _itemEquip.Equip(idx, slots);

                _lastEquipKey = key;
            }
        }
    }
}
