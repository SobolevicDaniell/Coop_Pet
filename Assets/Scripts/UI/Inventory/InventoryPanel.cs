using Game;
using UnityEngine;
using Zenject;

namespace Game.UI
{
    public class InventoryPanel : MonoBehaviour
    {
        [SerializeField] private InventorySlotUI[] _slotsUI;

        private IInventory _inventory;
        private ItemDatabaseSO _database;

        public event System.Action<InventorySlotUI> OnSlotBeginDrag;
        public event System.Action<InventorySlotUI> OnSlotEndDrag;
        public event System.Action<InventorySlotUI> OnSlotPointerEnter;

        [Inject]
        public void Construct(InventoryService inventory, ItemDatabaseSO database)
        {
            SetInventory(inventory, database);
        }

        public void Start()
        {
            for (int i = 0; i < _slotsUI.Length; i++)
                _slotsUI[i].SetActive(false);
        }

        public void SetInventory(IInventory inventory, ItemDatabaseSO database)
        {
            if (_inventory != null)
                _inventory.OnInventoryChanged -= Refresh;

            _inventory = inventory;
            _database = database;

            if (_inventory != null)
                _inventory.OnInventoryChanged += Refresh;

            Refresh();
        }

        private void Refresh()
        {
            if (_inventory == null || _database == null)
            {
                foreach (var slot in _slotsUI)
                    slot.Set(null, 0);
                return;
            }

            var slots = _inventory.GetInventorySlots();
            int length = Mathf.Min(slots.Length, _slotsUI.Length);


            for (int i = 0; i < length; i++)
            {
                var slot = _slotsUI[i];
                var slotData = slots[i];
                var item = slotData.Id != null ? _database.Get(slotData.Id) : null;
                slot.Set(item, slotData.Count);
                slot.Init(i, _inventory);

                slot.OnBeginDrag -= HandleSlotBeginDrag;
                slot.OnBeginDrag += HandleSlotBeginDrag;

                slot.OnEndDrag -= HandleSlotEndDrag;
                slot.OnEndDrag += HandleSlotEndDrag;
            }

            for (int i = length; i < _slotsUI.Length; i++)
                _slotsUI[i].Set(null, 0);
        }

        private void HandleSlotBeginDrag(InventorySlotUI slot) => OnSlotBeginDrag?.Invoke(slot);
        private void HandleSlotEndDrag(InventorySlotUI slot) => OnSlotEndDrag?.Invoke(slot);

        public IInventory GetInventory()
        {
            return _inventory;
        }

        private void OnDestroy()
        {
            if (_inventory != null)
                _inventory.OnInventoryChanged -= Refresh;
        }
    }
}
