using UnityEngine;
using System.Collections;
using UnityEngine.UI;

namespace Game.UI
{
    public class InventoryTransferController : MonoBehaviour
    {
        [SerializeField] private Canvas _cursorCanvas;
        [SerializeField] private Image _dragIcon; // пустой по умолчанию
        [SerializeField] private float transferTime = 1.0f;

        private InventorySlotUI _draggedSlot;
        private InventorySlotUI _hoverSlot;
        private Coroutine _transferCoroutine;

        public void Initialize(InventoryPanel[] panels)
        {
            foreach (var panel in panels)
            {
                panel.OnSlotBeginDrag += BeginDrag;
                panel.OnSlotEndDrag += EndDrag;
                panel.OnSlotPointerEnter += SlotPointerEnter;
            }
        }

        private void BeginDrag(InventorySlotUI slotUI)
        {
            if (_draggedSlot != null) EndDrag(_draggedSlot);
            _draggedSlot = slotUI;
            if (_draggedSlot.ItemIcon != null)
            {
                _dragIcon.sprite = _draggedSlot.ItemIcon.sprite;
                _dragIcon.enabled = true;
            }
            _transferCoroutine = StartCoroutine(DragAndDropCoroutine());
        }

        private void EndDrag(InventorySlotUI slotUI)
        {
            if (_transferCoroutine != null)
                StopCoroutine(_transferCoroutine);
            _dragIcon.enabled = false;
            _draggedSlot = null;
            _hoverSlot = null;
        }

        private void SlotPointerEnter(InventorySlotUI slotUI)
        {
            _hoverSlot = slotUI;
        }

        private IEnumerator DragAndDropCoroutine()
        {
            float timer = 0f;
            while (Input.GetMouseButton(0)) // LMB
            {
                timer += Time.deltaTime;
                // Update icon position
                _dragIcon.transform.position = Input.mousePosition;
                if (timer >= transferTime)
                {
                    TryTransfer();
                    yield break;
                }
                yield return null;
            }
            _dragIcon.enabled = false;
        }

        private void TryTransfer()
        {
            if (_draggedSlot == null || _hoverSlot == null) return;
            if (_draggedSlot == _hoverSlot) return; // Не переносим в ту же ячейку

            var fromInventory = _draggedSlot.ParentInventory;
            var toInventory = _hoverSlot.ParentInventory;

            var fromIndex = _draggedSlot.SlotIndex;
            var toIndex = _hoverSlot.SlotIndex;

            var slotData = fromInventory.GetInventorySlots()[fromIndex];

            if (slotData.Id == null || slotData.Count == 0) return;

            // Если один и тот же инвентарь - swap/slit/stack
            if (fromInventory == toInventory)
            {
                // Простой swap:
                var targetData = toInventory.GetInventorySlots()[toIndex];
                (slotData.Id, targetData.Id) = (targetData.Id, slotData.Id);
                (slotData.Count, targetData.Count) = (targetData.Count, slotData.Count);
            }
            else
            {
                // Перемещение между инвентарями:
                int amountToMove = 1; // по одному предмету (или весь стак - по дизайну)
                if (toInventory.TryAddItem(slotData.Id, amountToMove))
                {
                    fromInventory.TryRemoveItem(fromIndex, amountToMove);
                }
            }
        }
    }
}
