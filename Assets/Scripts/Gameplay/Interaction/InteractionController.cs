using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(ServerRpcHandler), typeof(NetworkObject))]
    public class InteractionController : NetworkBehaviour
    {
        
        [Inject] public HandItemBehaviorFactory Factory { get; private set; }
        [Inject] public ItemDatabaseSO Db { get; private set; }
        [Inject] public UIHealthView UI { get; private set; }

        public InputHandler Input;
        public InteractionPromptView Prompt;
        public InventoryService Inventory;

        // --- Локальные контроллеры — находим через GetComponent ---
        public InteractionInputHandler InputHandler { get; private set; }
        public ItemEquipController ItemEquip { get; private set; }
        public PickDropController PickDrop { get; private set; }
        public PlaceItemController PlaceItem { get; private set; }
        public QuickSlotController QuickSlot { get; private set; }
        public HealthComponent HealthComponent { get; private set; }

        public ServerRpcHandler RpcHandler { get; private set; }
        public IHandItemBehavior CurrentBehavior { get; private set; }

        public Transform HandPoint => _handPoint;
        public Transform DropPoint => _dropPoint;
        public Camera Camera => Camera.main;
        public float Range => _rangePick;
        public float ThrowForce => _throwForce;

        [SerializeField] private Transform _handPoint;
        [SerializeField] private float _rangePick = 4f;
        [SerializeField] public float _rangePlace = 8f;
        [SerializeField] private Transform _dropPoint;
        [SerializeField] private float _throwForce = 5f;

        [Networked] public int NetSelectedQuickSlot { get; set; } = -1;
        [Networked] public string HandModelId { get; set; }

        private NetworkObject _handModelNetObj;

        public void SetHandModelNetworkInstance(NetworkObject netObj) => _handModelNetObj = netObj;
        public NetworkObject GetHandModelNetworkInstance() => _handModelNetObj;

        public void ManualInit(InputHandler input, InteractionPromptView prompt, InventoryService inventory)
        {
            Input = input;
            Prompt = prompt;
            Inventory = inventory;

            // Найди все локальные контроллеры здесь!
            InputHandler = GetComponent<InteractionInputHandler>();
            ItemEquip = GetComponent<ItemEquipController>();
            PickDrop = GetComponent<PickDropController>();
            PlaceItem = GetComponent<PlaceItemController>();
            QuickSlot = GetComponent<QuickSlotController>();
            RpcHandler = GetComponent<ServerRpcHandler>();
            HealthComponent = GetComponent<HealthComponent>();

            if (InputHandler != null)
                InputHandler.Initialize(this, Input, Inventory, Prompt);

            if (ItemEquip != null)
                ItemEquip.Initialize(Factory, Db, this);

            if (PickDrop != null)
                PickDrop.Initialize(this);

            if (PlaceItem != null)
                PlaceItem.Initialize(this);

            if (QuickSlot != null)
                QuickSlot.Initialize(this);

            if (HealthComponent != null)
                HealthComponent.Initialize(UI, Object.HasStateAuthority, Object.HasInputAuthority);
        }

        public void Update()
        {
            PickDrop.UpdateRaycast();
            
        }
        private int _lastQuickSlot = -2;

        public override void FixedUpdateNetwork()
        {
            // ... другая логика

            if (_lastQuickSlot != NetSelectedQuickSlot)
            {
                if (QuickSlot != null)
                    QuickSlot.OnNetworkSlotChanged(NetSelectedQuickSlot);

                _lastQuickSlot = NetSelectedQuickSlot;
            }
        }


        public override void Spawned()
        {
            Debug.Log("Spawned");


            // Сначала локально ищем все контроллеры (MonoBehaviours)
            InputHandler = GetComponent<InteractionInputHandler>();
            ItemEquip = GetComponent<ItemEquipController>();
            PickDrop = GetComponent<PickDropController>();
            PlaceItem = GetComponent<PlaceItemController>();
            QuickSlot = GetComponent<QuickSlotController>();
            RpcHandler = GetComponent<ServerRpcHandler>();
            HealthComponent = GetComponent<HealthComponent>();

            if (!InputHandler || !ItemEquip || !PickDrop || !PlaceItem || !QuickSlot)
            {
                Debug.LogError("[InteractionController] Не все контроллеры найдены на объекте игрока! Проверь prefab.");
            }
            if (InputHandler == null) 
            {
                Debug.LogError("InputHandler = null");
            }

        }

        public void SetCurrentBehavior(IHandItemBehavior behavior)
        {
            CurrentBehavior = behavior;
        }

        public void ClearBehavior()
        {
            CurrentBehavior?.OnUnequip();
            CurrentBehavior = null;
        }
    }
}
