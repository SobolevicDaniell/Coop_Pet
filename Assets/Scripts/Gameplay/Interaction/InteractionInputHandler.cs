// Scripts/Gameplay/Interaction/InteractionInputHandler.cs
using UnityEngine;
using Zenject;

namespace Game
{
    public class InteractionInputHandler : MonoBehaviour
    {
        private InputHandler _input;
        private InventoryService _inventory;
        private InteractionPromptView _prompt;
        private InteractionController _controller;
        private ItemEquipController _itemEquip;
        private PickDropController _pickDrop;
        private PlaceItemController _placeItem;
        private QuickSlotController _quickSlot;

        private bool _isFiring;
        private int _lastSlot = -1;
        private bool _init;

        public void Initialize(
            InteractionController controller,
            InputHandler input,
            InventoryService inventory,
            InteractionPromptView prompt)
        {
            if (_init) return;
            _init = true;
            _controller = controller;
            _itemEquip = controller.itemEquip;
            _pickDrop = controller.pickDrop;
            _placeItem = controller.placeItem;
            _quickSlot = controller.quickSlot;

            _input = input;
            _inventory = inventory;
            _prompt = prompt;

            if (_input == null) Debug.LogError("[InteractionInputHandler] _input is NULL!");
            if (_quickSlot == null) Debug.LogError("[InteractionInputHandler] _quickSlot is NULL!");
            if (_prompt == null) Debug.LogError("[InteractionInputHandler] _prompt is NULL!");

            // Debug.Log("Hide");
            _prompt.Hide();

            _input.OnQuickSlotPressed += _quickSlot.ChangeSlotAbsolute;
            _input.OnQuickSlotScrollDelta += _quickSlot.ChangeSlotRelative;
            _input.OnInteractPressed += _pickDrop.TryPick;
            _input.OnQuickDropPressed += _pickDrop.TryDrop;
            _input.OnPlacePressed += _placeItem.TryPlace;
            _input.OnReloadPressed += () =>
            {
                if (_controller.currentBehavior is WeaponBehavior)
                    _controller.rpcHandler.RPC_RequestReload();
            };

            _input.OnUseDown += () =>
            {
                _isFiring = true;
                _controller.currentBehavior?.OnUsePressed();
            };
            _input.OnUseUp += () =>
            {
                _isFiring = false;
                _controller.currentBehavior?.OnUseReleased();
            };
        }


        void Update()
        {
            if (!_init) return;
            //_pickDrop.UpdateRaycast();

            if (_isFiring && _controller.currentBehavior != null)
                _controller.currentBehavior.OnUseHeld(Time.deltaTime);
        }



        public void FixedUpdateNetwork()
        {
            if (!_init || !_controller.Object.HasInputAuthority) return;

            if (_lastSlot == _controller.netSelectedQuickSlot) return;
            _lastSlot = _controller.netSelectedQuickSlot;

            // _quickSlot.OnNetworkSlotChanged(_lastSlot);

            _controller.currentBehavior?.OnUnequip();

            var slots = _inventory.GetQuickSlots();
            if (_lastSlot >= 0 && slots[_lastSlot]?.Id != null)
            {
                _itemEquip.Equip(_lastSlot, slots);
            }
            else
            {
                _controller.ClearBehavior();
                // _controller.rpcHandler.RPC_RequestDespawnHandModel();
                _itemEquip.Equip(-1, slots);
            }
        }
        
         private void OnDestroy()
        {
            _input.OnQuickSlotPressed -= _quickSlot.ChangeSlotAbsolute;
            _input.OnQuickSlotScrollDelta -= _quickSlot.ChangeSlotRelative;
            _input.OnInteractPressed -= _pickDrop.TryPick;
            _input.OnQuickDropPressed -= _pickDrop.TryDrop;
            _input.OnPlacePressed -= _placeItem.TryPlace;
        }
    }
}
