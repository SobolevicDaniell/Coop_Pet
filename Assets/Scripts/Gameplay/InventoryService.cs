using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Game
{
    public class InventoryService : IInventory, IInitializable, IDisposable
    {
        public event Action OnQuickSlotsChanged;
        public event Action OnInventoryChanged;
        public event Action<int> OnQuickSlotSelectionChanged;

        public int SelectedQuickSlot { get; private set; } = -1;

        private InventorySlot[] _quickSlots;
        private InventorySlot[] _inventorySlots;

        [Inject] private ItemDatabaseSO _db;
        [Inject(Optional = true)] private InventoryClientFacade _facade;
        [Inject(Optional = true)] private ContainerViewSessionClient _view;

        private bool _quickSizedFromServer;
        private bool _mainSizedFromServer;

        public void Initialize()
        {
            // Стартуем с минимального валидного размера (избегаем хардкодов)
            int quickCap = Math.Max(_facade?.GetLocalQuickCapacity() ?? 0, 1);
            int mainCap  = Math.Max(_facade?.GetLocalMainCapacity()  ?? 0, 1);

            _quickSlots     = new InventorySlot[quickCap];
            _inventorySlots = new InventorySlot[mainCap];

            for (int i = 0; i < _quickSlots.Length; i++)     _quickSlots[i]     = new InventorySlot(null, 0);
            for (int i = 0; i < _inventorySlots.Length; i++) _inventorySlots[i] = new InventorySlot(null, 0);

            // Подписываемся ВСЕГДА и один раз подгоняем, если снапшоты уже есть
            if (_view != null && _facade != null)
            {
                _view.OnContainerChanged += OnContainerChanged_SizeOncePerContainer;
                TrySizeNow();
            }
        }

        public void Dispose()
        {
            if (_view != null)
                _view.OnContainerChanged -= OnContainerChanged_SizeOncePerContainer;
        }

        public InventorySlot[] GetQuickSlots() => _quickSlots;
        public InventorySlot[] GetInventorySlots() => _inventorySlots;

        private void OnContainerChanged_SizeOncePerContainer(ContainerId id)
        {
            if (_facade == null) return;

            if (!_quickSizedFromServer && id.Equals(_facade.localQuick))
            {
                int q = _facade.GetLocalQuickCapacity();
                if (q > 0 && _quickSlots.Length != q)
                {
                    ResizeQuick(q);
                    _quickSizedFromServer = true;
                }
                else if (q > 0) _quickSizedFromServer = true;
            }

            if (!_mainSizedFromServer && id.Equals(_facade.localMain))
            {
                int m = _facade.GetLocalMainCapacity();
                if (m > 0 && _inventorySlots.Length != m)
                {
                    ResizeMain(m);
                    _mainSizedFromServer = true;
                }
                else if (m > 0) _mainSizedFromServer = true;
            }

            if (_quickSizedFromServer && _mainSizedFromServer && _view != null)
                _view.OnContainerChanged -= OnContainerChanged_SizeOncePerContainer;
        }

        private void TrySizeNow()
        {
            int q = _facade.GetLocalQuickCapacity();
            if (q > 0)
            {
                if (_quickSlots.Length != q) ResizeQuick(q);
                _quickSizedFromServer = true;
            }

            int m = _facade.GetLocalMainCapacity();
            if (m > 0)
            {
                if (_inventorySlots.Length != m) ResizeMain(m);
                _mainSizedFromServer = true;
            }

            if (_quickSizedFromServer && _mainSizedFromServer && _view != null)
                _view.OnContainerChanged -= OnContainerChanged_SizeOncePerContainer;
        }

        private void ResizeQuick(int q)
        {
            var newQ = new InventorySlot[q];
            for (int i = 0; i < q; i++)
                newQ[i] = i < _quickSlots.Length ? _quickSlots[i] : new InventorySlot(null, 0);
            _quickSlots = newQ;
            OnQuickSlotsChanged?.Invoke();
            // Debug.Log($"[InvClient] Quick resized to {q}");
        }

        private void ResizeMain(int m)
        {
            var newM = new InventorySlot[m];
            for (int i = 0; i < m; i++)
                newM[i] = i < _inventorySlots.Length ? _inventorySlots[i] : new InventorySlot(null, 0);
            _inventorySlots = newM;
            OnInventoryChanged?.Invoke();
            // Debug.Log($"[InvClient] Main resized to {m}");
        }

        public void ToggleQuickSlot(int idx)
        {
            if (idx < -1 || idx >= _quickSlots.Length) return;
            SelectedQuickSlot = (SelectedQuickSlot == idx) ? -1 : idx;
            OnQuickSlotSelectionChanged?.Invoke(SelectedQuickSlot);
        }

        public bool MoveQuickSlot(int from, int to)
        {
            if (from == to) return false;
            if (from < 0 || from >= _quickSlots.Length) return false;
            if (to   < 0 || to   >= _quickSlots.Length) return false;

            (_quickSlots[from], _quickSlots[to]) = (_quickSlots[to], _quickSlots[from]);

            if (SelectedQuickSlot == from || SelectedQuickSlot == to)
                SelectedQuickSlot = -1;

            OnQuickSlotsChanged?.Invoke();
            OnQuickSlotSelectionChanged?.Invoke(SelectedQuickSlot);
            return true;
        }

        public int HandlePick(string itemId, int count, int ammo)
        {
            var item = _db.Get(itemId);
            if (item == null) return count;

            int rem = count;
            rem = (item.priority == 1) ? TryQuick(item, rem, ammo) : TryInventory(item, rem, ammo);
            if (rem > 0) rem = (item.priority == 1) ? TryInventory(item, rem, ammo) : TryQuick(item, rem, ammo);
            return rem;
        }

        private int TryQuick(ItemSO item, int rem, int ammo = 0)
        {
            if (item.MaxStack > 1)
            {
                foreach (var slot in _quickSlots)
                {
                    if (rem == 0) break;
                    if (slot.Id == item.Id && slot.Count < item.MaxStack)
                    {
                        int can = Mathf.Min(rem, item.MaxStack - slot.Count);
                        slot.Count += can;
                        rem -= can;
                        OnQuickSlotsChanged?.Invoke();
                    }
                }
            }
            foreach (var slot in _quickSlots)
            {
                if (rem == 0) break;
                if (slot.Id == null)
                {
                    slot.Id = item.Id;
                    int toPut = Mathf.Min(rem, item.MaxStack);
                    slot.Count = toPut;
                    slot.State = new ItemState(ammo);
                    rem -= toPut;
                    OnQuickSlotsChanged?.Invoke();
                }
            }
            return rem;
        }

        private int TryInventory(ItemSO item, int rem, int ammo = 0)
        {
            if (item.MaxStack > 1)
            {
                foreach (var slot in _inventorySlots)
                {
                    if (rem == 0) break;
                    if (slot.Id == item.Id && slot.Count < item.MaxStack)
                    {
                        int can = Mathf.Min(rem, item.MaxStack - slot.Count);
                        slot.Count += can;
                        rem -= can;
                        OnInventoryChanged?.Invoke();
                    }
                }
            }
            foreach (var slot in _inventorySlots)
            {
                if (rem == 0) break;
                if (slot.Id == null)
                {
                    slot.Id = item.Id;
                    int toPut = Mathf.Min(rem, item.MaxStack);
                    slot.Count = toPut;
                    slot.State = new ItemState(ammo);
                    rem -= toPut;
                    OnInventoryChanged?.Invoke();
                }
            }
            return rem;
        }

        public void RaiseQuickSlotsChanged() => OnQuickSlotsChanged?.Invoke();
        public void RaiseQuickSlotSelectionChanged(int sel) => OnQuickSlotSelectionChanged?.Invoke(sel);
        public void RaiseInventoryChanged() => OnInventoryChanged?.Invoke();

        public int GetResourceCount(string resourceId)
        {
            int count = 0;
            foreach (var slot in _quickSlots)
                if (slot.Id == resourceId) count += slot.Count;
            foreach (var slot in _inventorySlots)
                if (slot.Id == resourceId) count += slot.Count;
            return count;
        }

        public bool SpendResource(string resourceId, int amount)
        {
            var slots = new List<InventorySlot>(_quickSlots.Length + _inventorySlots.Length);
            slots.AddRange(_quickSlots);
            slots.AddRange(_inventorySlots);

            int toSpend = amount;
            foreach (var slot in slots)
            {
                if (toSpend <= 0) break;
                if (slot.Id == resourceId && slot.Count > 0)
                {
                    int take = Mathf.Min(slot.Count, toSpend);
                    slot.Count -= take;
                    toSpend -= take;
                    if (slot.Count <= 0)
                        slot.Id = null;
                }
            }
            OnQuickSlotsChanged?.Invoke();
            OnInventoryChanged?.Invoke();
            return toSpend <= 0;
        }

        public InventorySlot FindResourceSlot(string resourceId)
        {
            foreach (var slot in _quickSlots)
                if (slot.Id == resourceId && slot.Count > 0)
                    return slot;
            foreach (var slot in _inventorySlots)
                if (slot.Id == resourceId && slot.Count > 0)
                    return slot;
            return null;
        }

        public IEnumerable<InventorySlot> FindAllResourceSlots(string resourceId)
        {
            foreach (var slot in _quickSlots)
                if (slot.Id == resourceId && slot.Count > 0)
                    yield return slot;
            foreach (var slot in _inventorySlots)
                if (slot.Id == resourceId && slot.Count > 0)
                    yield return slot;
        }

        public void ForceSetQuickSlot(int idx)
        {
            if (idx < -1 || idx >= _quickSlots.Length) return;
            if (SelectedQuickSlot == idx) return;
            SelectedQuickSlot = idx;
            OnQuickSlotSelectionChanged?.Invoke(SelectedQuickSlot);
        }

        public bool TryAddItem(string itemId, int count)
        {
            var itemSo = _db.Get(itemId);
            if (itemSo == null) return false;
            int left = count;

            foreach (var slot in _quickSlots)
            {
                if (slot.Id == itemId && slot.Count < itemSo.MaxStack)
                {
                    int canPut = Mathf.Min(left, itemSo.MaxStack - slot.Count);
                    slot.Count += canPut;
                    left -= canPut;
                    if (left <= 0) { OnQuickSlotsChanged?.Invoke(); return true; }
                }
            }
            foreach (var slot in _inventorySlots)
            {
                if (slot.Id == itemId && slot.Count < itemSo.MaxStack)
                {
                    int canPut = Mathf.Min(left, itemSo.MaxStack - slot.Count);
                    slot.Count += canPut;
                    left -= canPut;
                    if (left <= 0) { OnInventoryChanged?.Invoke(); return true; }
                }
            }
            foreach (var slot in _quickSlots)
            {
                if (slot.Id == null)
                {
                    int canPut = Mathf.Min(left, itemSo.MaxStack);
                    slot.Id = itemId;
                    slot.Count = canPut;
                    left -= canPut;
                    if (left <= 0) { OnQuickSlotsChanged?.Invoke(); return true; }
                }
            }
            foreach (var slot in _inventorySlots)
            {
                if (slot.Id == null)
                {
                    int canPut = Mathf.Min(left, itemSo.MaxStack);
                    slot.Id = itemId;
                    slot.Count = canPut;
                    left -= canPut;
                    if (left <= 0) { OnInventoryChanged?.Invoke(); return true; }
                }
            }
            OnQuickSlotsChanged?.Invoke();
            OnInventoryChanged?.Invoke();
            return left == 0;
        }

        public bool TryMoveToSlot(string itemId, int count, int targetSlot, ItemState state = null)
        {
            InventorySlot[] allSlots;
            int slotCount;
            bool isQuick;

            if (targetSlot < _quickSlots.Length)
            {
                allSlots = _quickSlots;
                slotCount = _quickSlots.Length;
                isQuick = true;
            }
            else
            {
                int invIndex = targetSlot - _quickSlots.Length;
                if (invIndex < 0 || invIndex >= _inventorySlots.Length) return false;
                allSlots = _inventorySlots;
                slotCount = _inventorySlots.Length;
                targetSlot = invIndex;
                isQuick = false;
            }

            if (targetSlot < 0 || targetSlot >= slotCount)
                return false;

            var itemSo = _db.Get(itemId);
            if (itemSo == null) return false;

            var slot = allSlots[targetSlot];

            if (slot.Id == null)
            {
                slot.Id = itemId;
                slot.Count = count;
                slot.State = state != null ? new ItemState(state) : null;
                if (isQuick) OnQuickSlotsChanged?.Invoke();
                else OnInventoryChanged?.Invoke();
                return true;
            }
            if (slot.Id == itemId && slot.Count < itemSo.MaxStack)
            {
                int canPut = Mathf.Min(count, itemSo.MaxStack - slot.Count);
                slot.Count += canPut;
                if (slot.State == null && state != null)
                    slot.State = new ItemState(state);
                if (isQuick) OnQuickSlotsChanged?.Invoke();
                else OnInventoryChanged?.Invoke();
                return canPut == count;
            }

            return false;
        }

        public bool TryRemoveItem(int slotIndex, int amount)
        {
            InventorySlot[] allSlots;
            int slotCount;
            bool isQuick;

            if (slotIndex < _quickSlots.Length)
            {
                allSlots = _quickSlots;
                slotCount = _quickSlots.Length;
                isQuick = true;
            }
            else
            {
                int invIndex = slotIndex - _quickSlots.Length;
                if (invIndex < 0 || invIndex >= _inventorySlots.Length) return false;
                allSlots = _inventorySlots;
                slotCount = _inventorySlots.Length;
                slotIndex = invIndex;
                isQuick = false;
            }

            if (slotIndex < 0 || slotIndex >= slotCount) return false;

            var slot = allSlots[slotIndex];
            if (slot.Id == null || slot.Count < amount) return false;

            slot.Count -= amount;
            if (slot.Count <= 0)
            {
                slot.Id = null;
                slot.Count = 0;
                slot.State = null;
            }

            if (isQuick) OnQuickSlotsChanged?.Invoke();
            else OnInventoryChanged?.Invoke();
            return true;
        }
    }
}
