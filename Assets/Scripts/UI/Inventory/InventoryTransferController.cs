using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace Game.UI
{
    public class InventoryTransferController : MonoBehaviour
    {
        [Header("UI Drag Icon")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Image _dragIcon;

        [Header("Panels")]
        [SerializeField] private InventoryPanel _playerInventoryPanel;
        [SerializeField] private OtherInventoryPanel _otherInventoryPanel;
        [SerializeField] private QuickSlotPanel _quickSlotPanel;

        private IInventoryPanelUI[] _allPanels;
        private InventorySlotUI _draggedSlot;
        private InventorySlotUI _hoveredSlot;

        public InventorySlotUI CurrentDraggedSlot => _draggedSlot;

        private void Awake()
        {
            _dragIcon.enabled = false;
            _allPanels = new IInventoryPanelUI[] {
                _playerInventoryPanel, _quickSlotPanel, _otherInventoryPanel
            };
            foreach (var panel in _allPanels)
                if (panel != null) SubscribePanel(panel);
        }

        private void OnDestroy()
        {
            foreach (var panel in _allPanels)
                if (panel != null) UnsubscribePanel(panel);
        }

        private void SubscribePanel(IInventoryPanelUI panel)
        {
            panel.OnSlotBeginDrag += OnSlotBeginDrag;
            panel.OnSlotEndDrag += OnSlotEndDrag;
            panel.OnSlotEnter += OnSlotEnter;
            panel.OnSlotExit += OnSlotExit;
        }
        private void UnsubscribePanel(IInventoryPanelUI panel)
        {
            panel.OnSlotBeginDrag -= OnSlotBeginDrag;
            panel.OnSlotEndDrag -= OnSlotEndDrag;
            panel.OnSlotEnter -= OnSlotEnter;
            panel.OnSlotExit -= OnSlotExit;
        }

        private void Update()
        {
            if (_draggedSlot != null && _dragIcon.enabled)
                _dragIcon.transform.position = Input.mousePosition;
        }

        private void OnSlotBeginDrag(InventorySlotUI slot)
        {
            if (_draggedSlot != null) return;
            if (slot.Item == null) return;
            _draggedSlot = slot;

            _dragIcon.sprite = slot.Item.Icon;
            _dragIcon.enabled = true;
            _dragIcon.transform.position = Input.mousePosition;
            _hoveredSlot = slot;
        }

        private void OnSlotEndDrag(InventorySlotUI slot)
        {
            if (_draggedSlot == null) return;

            // 1) если можно бросить в мир — бросаем
            if (CanDropToWorldHere())
            {
                DropFromSlot(_draggedSlot); // вызовет PickDropController.DropFromSlot(...)
                FinishAndRefresh();
                return;
            }

            // 2) иначе — ищем слот под мышью (если _hoveredSlot не пойман из enter/exit)
            if (_hoveredSlot == null)
                _hoveredSlot = GetSlotUnderMouse();

            // 2.1) если есть другой слот — пытаемся перенести / сложить стаки / иначе ОБМЕН
            if (_hoveredSlot != null && _hoveredSlot != _draggedSlot)
            {
                TryMoveOrSwap(_draggedSlot, _hoveredSlot);
                FinishAndRefresh();
                return;
            }

            // 3) иначе — просто закончить
            FinishAndRefresh();
        }

        private void FinishAndRefresh()
        {
            foreach (var panel in _allPanels)
                panel?.RefreshPanel();

            ResetDrag();
        }

        private void TryMoveOrSwap(InventorySlotUI fromUI, InventorySlotUI toUI)
        {
            var fromInv = fromUI.ParentInventory;
            var toInv = toUI.ParentInventory;

            int fromAbsIndex = GetInventoryServiceIndex(fromUI);
            int toAbsIndex = GetInventoryServiceIndex(toUI);

            var fromSlot = GetBackendSlotRef(fromUI);
            var toSlot = GetBackendSlotRef(toUI);

            if (fromSlot == null || string.IsNullOrEmpty(fromSlot.Id))
            {
                Debug.LogWarning("Попытка переместить пустой слот.");
                return;
            }

            // сначала пробуем обычный перенос (даст стаки, если можно)
            bool moved = toInv.TryMoveToSlot(fromSlot.Id, fromSlot.Count, toAbsIndex, fromSlot.State);
            if (moved)
            {
                fromInv.TryRemoveItem(fromAbsIndex, fromSlot.Count);
                return;
            }

            // если не получилось — делаем ОБМЕН, только если целевой слот занят
            bool toOccupied = (toSlot != null && !string.IsNullOrEmpty(toSlot.Id));
            if (!toOccupied)
                return;

            // один и тот же инвентарь → своп полей
            if (ReferenceEquals(fromInv, toInv))
            {
                (fromSlot.Id, toSlot.Id) = (toSlot.Id, fromSlot.Id);
                (fromSlot.Count, toSlot.Count) = (toSlot.Count, fromSlot.Count);
                (fromSlot.State, toSlot.State) = (toSlot.State, fromSlot.State);

                // Сигналы обновления
                fromInv.RaiseInventoryChanged();
                if (fromInv is InventoryService svc)
                    svc.RaiseQuickSlotsChanged();
                return;
            }

            // разные инвентари → «двойной мув» как обмен
            string keepId = toSlot.Id; int keepCount = toSlot.Count; var keepState = toSlot.State;

            // освободить целевой
            toInv.TryRemoveItem(toAbsIndex, keepCount);

            // from → to
            bool toOk = toInv.TryMoveToSlot(fromSlot.Id, fromSlot.Count, toAbsIndex, fromSlot.State);
            if (!toOk)
            {
                // откат
                toInv.TryMoveToSlot(keepId, keepCount, toAbsIndex, keepState);
                return;
            }

            // toSaved → from
            fromInv.TryRemoveItem(fromAbsIndex, fromSlot.Count);
            fromInv.TryMoveToSlot(keepId, keepCount, fromAbsIndex, keepState);
        }
        private InventorySlot GetBackendSlotRef(InventorySlotUI slotUI)
        {
            var parentInv = slotUI.ParentInventory;

            if (parentInv is InventoryService svc)
            {
                if (slotUI.ParentPanel is QuickSlotPanel)
                    return svc.GetQuickSlots()[slotUI.SlotIndex];
                else if (slotUI.ParentPanel is InventoryPanel)
                    return svc.GetInventorySlots()[slotUI.SlotIndex];
            }
            // другие инвентари (сундук и т.д.)
            return parentInv.GetInventorySlots()[slotUI.SlotIndex];
        }

        private bool CanDropToWorldHere()
        {
            // Нельзя, если курсор над панелями/слотами инвентаря
            if (GetSlotUnderMouse() != null)
                return false;

            // Если курсор над любым из UI-панелей инвентаря — запрещаем
            PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (var hit in results)
            {
                if (hit.gameObject.GetComponent<InventoryPanel>() != null ||
                    hit.gameObject.GetComponent<OtherInventoryPanel>() != null ||
                    hit.gameObject.GetComponent<QuickSlotPanel>() != null)
                    return false;
            }

            // Всё остальное (пустое пространство или любой не-инвентарный UI) — позволяем бросить
            return true;
        }

        private void OnSlotEnter(InventorySlotUI slot)
        {
            _hoveredSlot = slot;
        }
        private void OnSlotExit(InventorySlotUI slot)
        {
            if (_hoveredSlot == slot)
                _hoveredSlot = null;
        }

        private InventorySlotUI GetSlotUnderMouse()
        {
            foreach (var panel in _allPanels)
            {
                var mono = panel as MonoBehaviour;
                if (mono == null || !mono.isActiveAndEnabled) continue;
                foreach (var slot in mono.GetComponentsInChildren<InventorySlotUI>(true))
                {
                    if (IsPointerOverSlot(slot))
                        return slot;
                }
            }
            return null;
        }

        private bool IsPointerOverSlot(InventorySlotUI slot)
        {
            var rect = slot.GetComponent<RectTransform>();
            return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, _canvas.worldCamera);
        }

        // ВАЖНО: выброс через тег DropZone!
        private bool IsPointerOverDropZone()
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            bool overInventoryPanel = false;
            bool overDropZone = false;

            foreach (var hit in results)
            {
                // Проверяем панели (скрипты должны быть на объекте-панели)
                if (hit.gameObject.GetComponent<InventoryPanel>() != null ||
                    hit.gameObject.GetComponent<OtherInventoryPanel>() != null ||
                    hit.gameObject.GetComponent<QuickSlotPanel>() != null)
                {
                    overInventoryPanel = true;
                }
                // Проверяем DropZone по тегу
                if (hit.gameObject.CompareTag("InventoryDropZone"))
                {
                    overDropZone = true;
                }
            }

            // Разрешаем выброс только если есть DropZone и НЕТ панелей под мышью
            return overDropZone && !overInventoryPanel;
        }


        // Убираем предмет из инвентаря, вызываем выброс
        private void DropFromSlot(InventorySlotUI slotUI)
        {
            var pickDrop = FindObjectOfType<PickDropController>();
            if (pickDrop != null)
            {
                pickDrop.DropFromSlot(slotUI);
            }
        }

        private void ResetDrag()
        {
            _draggedSlot = null;
            _hoveredSlot = null;
            _dragIcon.enabled = false;
        }

        /// <summary>
        /// Универсальный абсолютный индекс для InventoryService, quick (0-9), inventory (10-39), для других — просто SlotIndex.
        /// </summary>
        private int GetInventoryServiceIndex(InventorySlotUI slotUI)
        {
            if (slotUI.ParentInventory is InventoryService)
            {
                if (slotUI.ParentPanel is QuickSlotPanel)
                    return slotUI.SlotIndex; // QuickSlotPanel индексы 0-9
                if (slotUI.ParentPanel is InventoryPanel)
                    return 10 + slotUI.SlotIndex; // InventoryPanel индексы с 10 и далее
            }
            // Для других инвентарей (например, ChestInventory)
            return slotUI.SlotIndex;
        }
    }
}