using Fusion;
using UnityEngine;
using Zenject;
using Game.UI;

namespace Game
{
    [RequireComponent(typeof(ServerRpcHandler), typeof(NetworkObject))]
    public class InteractionController : NetworkBehaviour
    {
        public InputHandler input;
        public InteractionPromptView prompt;
        public InventoryService inventory;

        public HandItemBehaviorFactory factory { get; private set; }
        public ItemDatabaseSO db { get; private set; }
        public UIHealthView ui { get; private set; }
        public InteractionInputHandler inputHandler { get; private set; }
        public ItemEquipController itemEquip { get; private set; }
        public PickDropController pickDrop { get; private set; }
        public PlaceItemController placeItem { get; private set; }
        public QuickSlotController quickSlot { get; private set; }
        public HealthComponent healthComponent { get; private set; }
        public UIController uiController { get; private set; }
        public HandItemController handItemController { get; private set; }
        public QuickSlotPanel quickSlotPanel { get; private set; }
        public ServerRpcHandler rpcHandler { get; private set; }
        public IHandItemBehavior currentBehavior { get; private set; }

        [SerializeField] private Transform _handPoint;
        [SerializeField] private Transform _dropPoint;
        [SerializeField] private float _rangePick = 4f;
        [SerializeField] public float _rangePlace = 8f;
        [SerializeField] private float _throwForce = 5f;

        [Networked] public int netSelectedQuickSlot { get; set; } = -1;


        public Transform handPoint => _handPoint;
        public Transform dropPoint => _dropPoint;
        public Camera camera => Camera.main;
        public float range => _rangePick;
        public float ThrowForce => _throwForce;

        private SceneContext sceneContext;


        [Networked] public NetworkId handModelNetId { get; set; }
        public NetworkObject handModelNetObj;

        public void SetHandModelNetworkInstance(NetworkObject netObj)
        {
            handModelNetObj = netObj;
            handModelNetId = (netObj != null) ? netObj.Id : default;
        }
        public NetworkObject GetHandModelNetworkInstance()
        {
            if (handModelNetObj != null) return handModelNetObj;
            if (handModelNetId != default && Runner != null)
                handModelNetObj = Runner.FindObject(handModelNetId);
            return handModelNetObj;
        }
        public override void Spawned()
        {
            sceneContext = FindObjectOfType<SceneContext>();
            var container = sceneContext.Container;

            input = container.Resolve<InputHandler>();
            prompt = container.Resolve<InteractionPromptView>();
            inventory = container.Resolve<InventoryService>();
            uiController = container.Resolve<UIController>();
            var playerPanel = container.ResolveId<InventoryPanel>("PlayerInventoryPanel");
            var otherPanel = container.ResolveId<OtherInventoryPanel>("OtherInventoryPanel");
            // var db = container.Resolve<ItemDatabaseSO>();
            factory = container.Resolve<HandItemBehaviorFactory>();
            db = container.Resolve<ItemDatabaseSO>();
            ui = container.Resolve<UIHealthView>();
            handItemController = container.Resolve<HandItemController>();
            quickSlotPanel = container.Resolve<QuickSlotPanel>();
            // Debug.Log($"handItemController = {handItemController}");
            ManualInit(input, prompt, inventory, playerPanel, otherPanel, db, uiController, handItemController, quickSlotPanel);
        }

        public void ManualInit(
            InputHandler input,
            InteractionPromptView prompt,
            InventoryService inventory,
            InventoryPanel playerPanel,
            OtherInventoryPanel otherPanel,
            ItemDatabaseSO itemDatabase,
            UIController uiController,
            HandItemController handItemController,
            QuickSlotPanel quickSlotPanel
            )
        {
            rpcHandler = GetComponent<ServerRpcHandler>();
            inputHandler = GetComponent<InteractionInputHandler>();
            itemEquip = GetComponent<ItemEquipController>();
            pickDrop = GetComponent<PickDropController>();
            placeItem = GetComponent<PlaceItemController>();
            quickSlot = GetComponent<QuickSlotController>();
            healthComponent = GetComponent<HealthComponent>();
            handItemController = GetComponent<HandItemController>();

            if (Object.HasInputAuthority)
            {
                rpcHandler?.Construct(itemDatabase, handItemController, this, sceneContext);
                inputHandler?.Initialize(this, input, inventory, prompt);
                itemEquip?.Initialize(factory, db, this);
                pickDrop?.Initialize(this, inventory, playerPanel, otherPanel, itemDatabase, uiController, camera);
                placeItem?.Initialize(this);
                quickSlot?.Initialize(this, inventory);
                healthComponent?.Initialize(ui, Object.HasStateAuthority, true);
                handItemController?.Initialize(itemDatabase, rpcHandler, this);
                quickSlotPanel?.InitializeIfLocal(this);

                input.OnInventoryToggle += ToggleInventory;
                input.OnGlobalUiCloseRequested += CloseInventory;
                input.OnInteractPressed += pickDrop.TryPick;
                inventory.OnQuickSlotsChanged += OnQuickSlotsChanged;
            }
        }

        private void ToggleInventory()
        {
            if (!Object.HasInputAuthority) return;

            if (uiController.InventoryOpened)
            {
                uiController.ShowGameUI();
                pickDrop.CloseOpenedInventories();
            }
            else
            {
                uiController.ShowInventory();
                pickDrop.OpenPlayerInventory();
            }
        }

        private void CloseInventory()
        {
            if (!Object.HasInputAuthority) return;

            uiController.ShowGameUI();
            pickDrop.CloseOpenedInventories();
        }

        public void Update()
        {
            if (Object.HasInputAuthority)
                pickDrop?.UpdateRaycast();
        }

        public void SetCurrentBehavior(IHandItemBehavior behavior)
        {
            currentBehavior = behavior;
        }

        public void ClearBehavior()
        {
            currentBehavior?.OnUnequip();
            currentBehavior = null;
        }

        private void OnQuickSlotsChanged()
        {
            if (itemEquip != null && inventory != null)
            {
                var idx = inventory.SelectedQuickSlot;
                var slots = inventory.GetQuickSlots();

                if (slots == null || slots.Length == 0)
                {
                    itemEquip.Equip(-1, slots);
                    return;
                }
                if (idx < 0 || idx >= slots.Length)
                {
                    itemEquip.Equip(-1, slots);
                    return;
                }

                var slot = slots[idx];
                if (slot == null || string.IsNullOrEmpty(slot.Id))
                {
                    itemEquip.Equip(-1, slots);
                    return;
                }

                itemEquip.Equip(idx, slots);
            }
        }


        private void OnDestroy()
        {
            if (inventory != null && itemEquip != null)
                inventory.OnQuickSlotsChanged -= OnQuickSlotsChanged;

            if (Object.HasInputAuthority && input != null)
            {
                input.OnInventoryToggle -= ToggleInventory;
                input.OnGlobalUiCloseRequested -= CloseInventory;
                input.OnInteractPressed -= pickDrop.TryPick;
            }
        }
    }
}