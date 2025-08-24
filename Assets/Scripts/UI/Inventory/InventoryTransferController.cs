// Assets/Scripts/UI/Inventory/InventoryTransferController.cs
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Game.UI
{
    public sealed class InventoryTransferController : MonoBehaviour
    {
        [SerializeField] private Image _dragIcon;

        private RectTransform _dragIconRT;

        [Inject(Optional = true)] private ItemDatabaseSO _db;

        private InventoryPanel _playerPanel;
        private QuickSlotPanel _quickPanel;
        private OtherInventoryPanel _chestPanel;

        private InventorySlotUI _dragSource;
        private InventorySlotUI _hoverTarget;
        private bool _dragActive;

        public void Initialize(Game.InventoryService playerInventory, InventoryPanel player, QuickSlotPanel quick, OtherInventoryPanel chest)
        {
            _playerPanel = player;
            _quickPanel  = quick;
            _chestPanel  = chest;

            Subscribe(_playerPanel);
            Subscribe(_quickPanel);
            Subscribe(_chestPanel);

            SetupIcon();
        }

        private void Awake() => SetupIcon();

        private void SetupIcon()
        {
            if (_dragIcon == null) return;
            _dragIconRT = _dragIcon.rectTransform;
            _dragIcon.raycastTarget = false;
            _dragIcon.enabled = false;
        }

        private void Subscribe(IInventoryPanelUI panel)
        {
            if (panel == null) return;
            panel.OnSlotBeginDrag += OnSlotBeginDrag;
            panel.OnSlotEndDrag   += OnSlotEndDrag;
            panel.OnSlotEnter     += OnSlotEnter;
            panel.OnSlotExit      += OnSlotExit;
        }

        private void Unsubscribe(IInventoryPanelUI panel)
        {
            if (panel == null) return;
            panel.OnSlotBeginDrag -= OnSlotBeginDrag;
            panel.OnSlotEndDrag   -= OnSlotEndDrag;
            panel.OnSlotEnter     -= OnSlotEnter;
            panel.OnSlotExit      -= OnSlotExit;
        }

        private void OnDestroy()
        {
            Unsubscribe(_playerPanel);
            Unsubscribe(_quickPanel);
            Unsubscribe(_chestPanel);
        }

        private void Update()
        {
            if (_dragActive && _dragIconRT != null)
                _dragIconRT.position = Input.mousePosition;
        }

        private void OnSlotBeginDrag(InventorySlotUI slot)
        {
            if (slot == null) return;
            if (!SlotHasItem(slot)) return;

            _dragSource = slot;
            _hoverTarget = null;
            _dragActive = true;

            if (_dragIcon != null)
            {
                var sprite = slot.Item != null ? slot.Item.Icon : ResolveIconFromBackend(slot);
                _dragIcon.sprite = sprite;
                _dragIcon.enabled = sprite != null;
                if (_dragIconRT != null) _dragIconRT.position = Input.mousePosition;
            }
        }

        private void OnSlotEnter(InventorySlotUI slot)
        {
            if (!_dragActive) return;
            _hoverTarget = slot;
        }

        private void OnSlotExit(InventorySlotUI slot)
        {
            if (!_dragActive) return;
            if (_hoverTarget == slot) _hoverTarget = null;
        }

        private void OnSlotEndDrag(InventorySlotUI slot)
        {
            if (_dragActive && _dragSource != null)
            {
                var src = _dragSource;
                var dst = _hoverTarget;

                // не переносим в тот же слот
                if (dst != null &&
                    !(ReferenceEquals(src.ParentPanel, dst.ParentPanel) && src.SlotIndex == dst.SlotIndex))
                {
                    PerformTransfer(src, dst);
                }
            }

            _dragActive = false;
            _dragSource = null;
            _hoverTarget = null;

            if (_dragIcon != null)
            {
                _dragIcon.enabled = false;
                _dragIcon.sprite = null;
            }
        }

        private void PerformTransfer(InventorySlotUI src, InventorySlotUI dst)
        {
            if (src == null || dst == null) return;

            var fromKind = src.ParentPanel.Kind;
            var toKind   = dst.ParentPanel.Kind;

            var fromInv  = src.ParentInventory;
            var toInv    = dst.ParentInventory;

            var fromIdx  = src.SlotIndex;
            var toIdx    = dst.SlotIndex;

            // Quick ⇄ Quick (только у InventoryService есть спец-метод)
            if (fromKind == PanelKind.Quick && toKind == PanelKind.Quick &&
                ReferenceEquals(fromInv, toInv) && fromInv is Game.InventoryService pis)
            {
                pis.MoveQuickSlot(fromIdx, toIdx);
                RaiseChanged(fromInv, fromKind);
                RefreshAll();
                return;
            }

            // Все остальные комбинации — универсальный перенос/свап
            GenericSwapOrMove(fromInv, fromKind, fromIdx, toInv, toKind, toIdx);

            RaiseChanged(fromInv, fromKind);
            if (!ReferenceEquals(fromInv, toInv))
                RaiseChanged(toInv, toKind);

            RefreshAll();
        }

        private void GenericSwapOrMove(IInventory fromInv, PanelKind fromKind, int fromIdx,
                                       IInventory toInv,   PanelKind toKind,   int toIdx)
        {
            var srcArr = GetArray(fromInv, fromKind);
            var dstArr = GetArray(toInv,   toKind);
            if (srcArr == null || dstArr == null) return;
            if (fromIdx < 0 || fromIdx >= srcArr.Length || toIdx < 0 || toIdx >= dstArr.Length) return;

            var s = srcArr[fromIdx];
            var d = dstArr[toIdx];

            bool sEmpty = s == null || string.IsNullOrEmpty(s.Id) || s.Count <= 0;
            if (sEmpty) return;

            bool dEmpty = d == null || string.IsNullOrEmpty(d.Id) || d.Count <= 0;

            if (dEmpty)
            {
                EnsureSlot(ref d, dstArr, toIdx);
                d.Id = s.Id; d.Count = s.Count; d.State = s.State;

                s.Id = null; s.Count = 0; s.State = null;
            }
            else
            {
                EnsureSlot(ref s, srcArr, fromIdx);
                EnsureSlot(ref d, dstArr, toIdx);

                var tmpId = s.Id; var tmpCnt = s.Count; var tmpSt = s.State;
                s.Id = d.Id; s.Count = d.Count; s.State = d.State;
                d.Id = tmpId; d.Count = tmpCnt; d.State = tmpSt;
            }
        }

        private InventorySlot[] GetArray(IInventory inv, PanelKind kind)
        {
            if (kind == PanelKind.Quick)
            {
                // Quick доступен только у InventoryService
                if (inv is Game.InventoryService svc) return svc.GetQuickSlots();
                return null;
            }
            // Player/Chest используют общий контракт IInventory
            return inv?.GetInventorySlots();
        }

        private void EnsureSlot(ref InventorySlot slot, InventorySlot[] arr, int idx)
        {
            if (slot != null) return;
            slot = new InventorySlot();
            arr[idx] = slot;
        }

        private void RaiseChanged(IInventory inv, PanelKind kind)
        {
            if (inv is Game.InventoryService svc && kind == PanelKind.Quick)
                svc.RaiseQuickSlotsChanged();
            else
                inv.RaiseInventoryChanged();
        }

        private bool SlotHasItem(InventorySlotUI ui)
        {
            // быстрее всего: полагаемся на уже выставленный Item
            if (ui.Item != null) return true;

            // бэкенд-проверка
            var arr = GetArray(ui.ParentInventory, ui.ParentPanel.Kind);
            if (arr == null) return false;
            int i = ui.SlotIndex;
            if (i < 0 || i >= arr.Length) return false;
            var s = arr[i];
            return s != null && !string.IsNullOrEmpty(s.Id) && s.Count > 0;
        }

        private Sprite ResolveIconFromBackend(InventorySlotUI ui)
        {
            if (_db == null) return null;
            var arr = GetArray(ui.ParentInventory, ui.ParentPanel.Kind);
            if (arr == null) return null;
            int i = ui.SlotIndex;
            if (i < 0 || i >= arr.Length) return null;
            var s = arr[i];
            if (s == null || string.IsNullOrEmpty(s.Id)) return null;
            var so = _db.Get(s.Id);
            return so != null ? so.Icon : null;
        }

        private void RefreshAll()
        {
            _playerPanel?.RefreshPanel();
            _quickPanel?.RefreshPanel();
            _chestPanel?.RefreshPanel();
        }
    }
}
