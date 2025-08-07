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

            if (IsPointerOverDropZone())
            {
                DropFromSlot(_draggedSlot);
            }
            else
            {
                if (_hoveredSlot == null)
                    _hoveredSlot = GetSlotUnderMouse();

                if (_hoveredSlot != null && _hoveredSlot != _draggedSlot)
                {
                    var fromInv = _draggedSlot.ParentInventory;
                    var toInv = _hoveredSlot.ParentInventory;

                    int fromAbsIndex = GetInventoryServiceIndex(_draggedSlot);
                    int toAbsIndex = GetInventoryServiceIndex(_hoveredSlot);

                    InventorySlot fromSlotData;

                    if (_draggedSlot.ParentPanel is QuickSlotPanel && fromInv is InventoryService serviceFrom)
                        fromSlotData = serviceFrom.GetQuickSlots()[_draggedSlot.SlotIndex];
                    else
                        fromSlotData = fromInv.GetInventorySlots()[_draggedSlot.SlotIndex];

                    if (string.IsNullOrEmpty(fromSlotData.Id))
                    {
                        Debug.LogWarning("Попытка переместить пустой слот.");
                        ResetDrag();
                        return;
                    }

                    bool moved = toInv.TryMoveToSlot(fromSlotData.Id, fromSlotData.Count, toAbsIndex, fromSlotData.State);

                    if (moved)
                        fromInv.TryRemoveItem(fromAbsIndex, fromSlotData.Count);
                    else
                        Debug.LogWarning("Перенос предмета не удался.");
                }
            }

            foreach (var panel in _allPanels)
                panel?.RefreshPanel();

            ResetDrag();
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