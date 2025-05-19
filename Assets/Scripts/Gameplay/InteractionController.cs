using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(ServerRpcHandler), typeof(NetworkObject))]
    public class InteractionController : NetworkBehaviour
    {
        [Inject] public HandItemBehaviorFactory Factory { get; private set; }
        [Inject] public InteractionPromptView Prompt { get; private set; }
        [Inject] public InventoryService Inventory { get; private set; }
        [Inject] public InputHandler Input { get; private set; }
        [Inject] public ItemDatabaseSO Db { get; private set; }

        public ServerRpcHandler RpcHandler { get; private set; }

        QuickSlotController Quick;
        HandItemController Hand;
        PickDropController PickDrop;

        [Networked] public int NetSelectedQuickSlot { get; set; } = -1;
        [Networked] public string HandModelId { get; set; }

        [Header("Hand Point")]
        [SerializeField] Transform _handPoint;
        [Header("Pick/Drop")]
        [SerializeField] float _rangePick = 4f;
        [SerializeField] float _rangePlace = 8f;
        [SerializeField] Transform _dropPoint;
        [SerializeField] float _throwForce = 5f;

        bool _init;
        IHandItemBehavior _currentBehavior;
        int _lastSlot = -1;
        bool _isFiring;

        // Ссылка на текущий сетевой объект модели
        NetworkObject _handModelNetObj;
        public void SetHandModelNetworkInstance(NetworkObject netObj) => _handModelNetObj = netObj;
        public NetworkObject GetHandModelNetworkInstance() => _handModelNetObj;

        public Transform HandPoint => _handPoint;
        public Transform DropPoint => _dropPoint;
        public Camera Camera => Camera.main;
        public float Range => _rangePick;
        public float ThrowForce => _throwForce;
        public IHandItemBehavior CurrentBehavior => _currentBehavior;

        public override void Spawned()
        {
            // Zenject-inject
            if (Prompt == null)
                FindObjectOfType<SceneContext>()
                  .Container.InjectGameObject(gameObject);

            RpcHandler = GetComponent<ServerRpcHandler>();

            if (Object.HasInputAuthority)
                InitializeLocal();
        }

        public void InitializeLocal()
        {
            if (_init) return;
            _init = true;

            Prompt.Hide();

            Quick = new QuickSlotController(this, RpcHandler, Inventory);
            PickDrop = new PickDropController(this, RpcHandler);
            Hand = new HandItemController(Factory, _handPoint, Db);
            Hand.Initialize(this);

            Input.OnQuickSlotPressed += Quick.ChangeSlotAbsolute;
            Input.OnQuickSlotScrollDelta += Quick.ChangeSlotRelative;
            Input.OnInteractPressed += PickDrop.TryPick;
            Input.OnQuickDropPressed += DropQuickSlotItem;
            Input.OnPlacePressed += TryPlaceItem;
            Input.OnReloadPressed += () =>
            {
                if (_currentBehavior is WeaponBehavior wb)
                {
                    RpcHandler.RPC_RequestReload();
                }
            };

            Input.OnUseDown += () =>
            {
                _isFiring = true;
                if (_currentBehavior != null)
                    _currentBehavior.OnUsePressed();
            };
            Input.OnUseUp += () =>
            {
                _isFiring = false;
                if (_currentBehavior != null)
                    _currentBehavior.OnUseReleased();
            };
        }

        void Update()
        {
            if (!_init) return;
            PickDrop.UpdatePrompt();

            if (_isFiring && _currentBehavior != null)
                _currentBehavior.OnUseHeld(Time.deltaTime);
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasInputAuthority || !_init) return;

            if (_lastSlot == NetSelectedQuickSlot) return;

            _lastSlot = NetSelectedQuickSlot;

            Quick.OnNetworkSlotChanged(_lastSlot);

            _currentBehavior?.OnUnequip();

            var slots = Inventory.GetQuickSlots();

            if (_lastSlot >= 0 && slots[_lastSlot]?.Id != null)
            {
                var slot = slots[_lastSlot];
                Hand.Equip(_lastSlot, slots);

                var so = Db.Get(slot.Id);
                // !!! Передаём не копию состояния, а ссылку на весь слот!
                var behavior = Factory.Create(so, HandPoint, this, slot);
                SetCurrentBehavior(behavior);

                RpcHandler.RPC_RequestSpawnHandModel(slot.Id);
            }
            else
            {
                _currentBehavior = null;
                RpcHandler.RPC_RequestDespawnHandModel();
                Hand.Equip(-1, slots);
            }
        }

        private void TryPlaceItem()
        {
            // 1. Проверить выбран ли слот
            var selected = NetSelectedQuickSlot;
            if (selected < 0) return;

            var slots = Inventory.GetQuickSlots();
            if (selected >= slots.Length) return;
            var slot = slots[selected];

            if (string.IsNullOrEmpty(slot.Id) || slot.Count <= 0) return;

            // 2. Проверить, что предмет — PlaceableItemSO
            var so = Db.Get(slot.Id);
            if (!(so is PlaceableItemSO placeable)) return;

            // 3. Найти позицию для размещения (луч в экран из центра камеры)
            var camera = Camera;
            Vector3 placePos = Vector3.zero;
            Vector3 placeNormal = Vector3.up;
            bool canPlace = false;
            Ray ray = camera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
            if (Physics.Raycast(ray, out var hit, _rangePlace))
            {
                placePos = hit.point;
                placeNormal = hit.normal;
                canPlace = true;
            }
            else
            {
                // Если не попали — размещаем в воздухе на _rangePlace по направлению взгляда
                placePos = ray.origin + ray.direction * _rangePlace;
                canPlace = true;
            }

            // Можно добавить свою логику валидности точки (например, не ставить в игрока и т.д.)

            if (canPlace)
            {
                var rotation = Quaternion.LookRotation(placeNormal) * Quaternion.Euler(90, 0, 0);
                RpcHandler.RPC_RequestPlaceObject(placeable.Id, placePos, rotation);

                // Минусуем предмет из слота
                slot.Count -= 1;
                if (slot.Count <= 0)
                {
                    slot.Id = null;
                    slot.State = new ItemState();
                }
                Inventory.RaiseQuickSlotsChanged();
            }
        }


        private void DropQuickSlotItem()
        {
            // Только если выбран быстрый слот и он не пустой!
            var selected = NetSelectedQuickSlot;
            if (selected < 0) return;

            var slots = Inventory.GetQuickSlots();
            if (selected >= slots.Length) return;

            var slot = slots[selected];
            if (slot.Id == null || slot.Count <= 0) return;

            int ammo = slot.State != null ? slot.State.Ammo : 0;

            // Запрос дропа (выброса) — выкидываем весь стак
            RpcHandler.RPC_RequestDrop(
                DropPoint.position,
                Camera.transform.forward,
                slot.Id,
                slot.Count,
                ammo
            );

            // Очищаем слот
            slot.Id = null;
            slot.Count = 0;

            // Без .Reset() — сбрасываем руками
            if (slot.State != null)
            {
                slot.State.Ammo = 0; // Если у оружия были патроны — сбросить
                // Если у тебя есть другие поля, сбрасывай их тут
                // slot.State.ДругоеПоле = default;
            }
            Inventory.RaiseQuickSlotsChanged();

            // Если этот слот был выбран — сбросить выбор и убрать оружие из рук
            if (NetSelectedQuickSlot == selected)
            {
                RpcHandler.RPC_SelectQuickSlot(-1);
                RpcHandler.RPC_RequestDespawnHandModel();
            }
        }

        public void SetCurrentBehavior(IHandItemBehavior behavior)
        {
            _currentBehavior = behavior;
        }

        public void ClearBehavior()
        {
            _currentBehavior?.OnUnequip();
            _currentBehavior = null;
        }
    }
}
