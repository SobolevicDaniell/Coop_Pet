using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public class QuickSlotController : MonoBehaviour
    {
        private InteractionController _ic;
        private InventoryService _inventory;

        [Inject(Optional = true)] private InventoryClientFacade _facade;
        [Inject(Optional = true)] private ContainerViewSessionClient _view;

        private bool _constructed;
        private bool _enabledForLocal;

        private int _fixedQuickCapacity; // нов
        private bool _capacityFixed;     // нов

        public void Construct(InteractionController ic, InventoryService inventory)
        {
            _ic = ic;
            _inventory = inventory;
            _constructed = true;
        }

        public void EnableForLocal()
        {
            _enabledForLocal = true;

            // нов: фиксируем количество слотов один раз из снапшота
            if (_facade != null)
            {
                var cap = _facade.GetLocalQuickCapacity();
                if (cap > 0)
                {
                    _fixedQuickCapacity = cap;
                    _capacityFixed = true;
                }
                else if (_view != null)
                {
                    _view.OnContainerChanged += OnContainerChanged_OnceFixCapacity;
                }
            }
        }

        public void DisableForLocal()
        {
            _enabledForLocal = false;
            if (_view != null)
                _view.OnContainerChanged -= OnContainerChanged_OnceFixCapacity;
        }

        public void ChangeSlotAbsolute(int slot)
        {
            if (!_constructed || !_enabledForLocal) return;
            if (_ic == null || !_ic.Object.HasInputAuthority || _inventory == null) return;

            var cap = GetCapacity();
            if (cap <= 0) return;
            if (slot < 0 || slot >= cap) return; // нов: защита по диапазону

            _inventory.ToggleQuickSlot(slot);
            _ic.InvokeOnQuickSlotsChanged();
        }

        public void ChangeSlotRelative(int delta)
        {
            if (!_constructed || !_enabledForLocal) return;
            if (_ic == null || !_ic.Object.HasInputAuthority || _inventory == null) return;

            var cap = GetCapacity();
            if (cap <= 0) return;

            int cur = _inventory.SelectedQuickSlot < 0 ? 0 : Mathf.Min(_inventory.SelectedQuickSlot, cap - 1);
            int next = (cur + delta % cap + cap) % cap;

            ChangeSlotAbsolute(next);
        }

        private int GetCapacity()
        {
            if (_capacityFixed) return _fixedQuickCapacity;

            // fallback, пока снапшот не пришёл: локальный сервис (временно)
            var slots = _inventory?.GetQuickSlots();
            return (slots != null) ? slots.Length : 0;
        }

        private void OnContainerChanged_OnceFixCapacity(ContainerId id)
        {
            if (_capacityFixed || _facade == null) return;
            if (!id.Equals(_facade.localQuick)) return;

            var cap = _facade.GetLocalQuickCapacity();
            if (cap <= 0) return;

            _fixedQuickCapacity = cap;
            _capacityFixed = true;

            if (_view != null)
                _view.OnContainerChanged -= OnContainerChanged_OnceFixCapacity;
        }
    }
}
