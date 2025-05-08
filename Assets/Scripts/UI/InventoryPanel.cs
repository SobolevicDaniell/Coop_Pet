// Assets/Scripts/UI/InventoryPanel.cs
using Game;
using UnityEngine;
using UnityEngine.UIElements;
using Zenject;

namespace Game.UI
{
    public class InventoryPanel : MonoBehaviour
    {
        [SerializeField] private InventorySlotUI[] _slotsUI;

        private InventoryService _inventory;
        private ItemDatabaseSO _database;

        [Inject]
        public void Construct(InventoryService inventory, ItemDatabaseSO database)
        {
            _inventory = inventory;
            _database = database;

            _inventory.OnInventoryChanged += Refresh;
            Refresh();
        }

        public void Start()
        {
            for (int i = 0; i < _slotsUI.Length; i++)
            {
                _slotsUI[i].SetActive(false);
            }
        }

        private void Refresh()
        {
            var slots = _inventory.GetInventorySlots();
            int length = Mathf.Min(slots.Length, _slotsUI.Length);

            for (int i = 0; i < length; i++)
            {
                var slot = slots[i];
                var item = slot.Id != null ? _database.Get(slot.Id) : null;
                _slotsUI[i].Set(item, slot.Count);
            }

            for (int i = length; i < _slotsUI.Length; i++)
                _slotsUI[i].Set(null, 0);
        }

        private void OnDestroy()
        {
            if (_inventory != null)
                _inventory.OnInventoryChanged -= Refresh;
        }
    }
}
