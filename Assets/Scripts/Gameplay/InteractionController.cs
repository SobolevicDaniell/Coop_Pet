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
        private string _lastHandModelId;
        private NetworkObject _handModelInstance;



        [Header("Hand Point")]
        [SerializeField] Transform _handPoint;
        [Header("Pick/Drop")]
        [SerializeField] float _range = 4f;
        [SerializeField] Transform _dropPoint;
        [SerializeField] float _throwForce = 5f;

        bool _init;
        IHandItemBehavior _currentBehavior;
        int _lastSlot = -1;

        // Ссылка на текущий сетевой объект модели
        NetworkObject _handModelNetObj;
        public void SetHandModelNetworkInstance(NetworkObject netObj) => _handModelNetObj = netObj;
        public NetworkObject GetHandModelNetworkInstance() => _handModelNetObj;

        public Transform HandPoint => _handPoint;
        public Transform DropPoint => _dropPoint;
        public Camera Camera => Camera.main;
        public float Range => _range;
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
            Input.SingleShot += () => RpcHandler.RPC_RequestShoot();

            Input.OnReloadPressed += () =>
            {
                if (_currentBehavior is WeaponBehavior wb)
                {
                    RpcHandler.RPC_RequestReload();
                }
            };
        }

        void Update()
        {
            if (!_init) return;
            PickDrop.UpdatePrompt();
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
                var behavior = Factory.Create(so, HandPoint, this, slot.State);
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

