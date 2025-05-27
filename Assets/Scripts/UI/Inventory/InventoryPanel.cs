using Game;
using UnityEngine;
using Zenject;

namespace Game.UI
{
    public class InventoryPanel : MonoBehaviour
    {
        [SerializeField] private InventorySlotUI[] _slotsUI;

        private IInventory _inventory;        // Теперь поддерживает любой IInventory!
        private ItemDatabaseSO _database;

        // Для DI — можно оставить, если нужно инъецировать по умолчанию
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

        /// <summary>
        /// Назначить новый инвентарь и базу предметов, подписаться на обновления
        /// </summary>
        public void SetInventory(IInventory inventory, ItemDatabaseSO database)
        {
            // Отписываемся от предыдущего инвентаря (если был)
            if (_inventory != null)
                _inventory.OnInventoryChanged -= Refresh;

            _inventory = inventory;
            _database = database;

            // Подписываемся на новый
            if (_inventory != null)
                _inventory.OnInventoryChanged += Refresh;

            Refresh();
        }

        private void Refresh()
        {
            if (_inventory == null || _database == null)
            {
                // Очистить UI, если нет данных
                foreach (var slot in _slotsUI)
                    slot.Set(null, 0);
                return;
            }

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
