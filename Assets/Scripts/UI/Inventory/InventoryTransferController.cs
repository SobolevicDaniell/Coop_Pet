using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace Game.UI
{
    public sealed class InventoryTransferController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Image _dragIcon;
        [SerializeField] private Canvas _canvas;

        private InventoryPanel _playerPanel;
        private QuickSlotPanel _quickPanel;
        private OtherInventoryPanel _otherPanel;

        [Inject] private InventoryService _inv;
        [Inject] private ItemDatabaseSO _db;
        [Inject(Optional = true)] private InventoryClientFacade _facade;


        private Game.InteractionController _ic;
        private Game.PlayerRpcHandler _rpc => _ic != null ? _ic.playerRpcHandler : null;

        private InventorySlotUI _srcSlot;
        private IInventoryPanelUI _srcPanel;
        private InventorySlotUI _hoverSlot;
        private IInventoryPanelUI _hoverPanel;

        private bool _subscribed;
        private int _uiLayer = -1;

        public void Initialize(InventoryService inv,
                       InventoryPanel playerPanel,
                       QuickSlotPanel quickPanel,
                       OtherInventoryPanel otherPanel,
                       InteractionController ic,
                       InventoryClientFacade facade)
        {
            _inv = inv ?? _inv;
            _playerPanel = playerPanel;
            _quickPanel = quickPanel;
            _otherPanel = otherPanel;
            _ic = ic ?? FindLocalInteractionController();
            _facade = facade ?? _facade;

            if (_subscribed) UnsubscribeAll();
            SubscribeAll();
            _subscribed = true;

            if (_dragIcon != null)
            {
                _dragIcon.enabled = false;
                _dragIcon.raycastTarget = false;
            }

            _uiLayer = LayerMask.NameToLayer("UI");
        }

        private Game.InteractionController FindLocalInteractionController()
        {
            var all = FindObjectsOfType<Game.InteractionController>(true);
            foreach (var ic in all)
            {
                try { if (ic.Object != null && ic.Object.HasInputAuthority) return ic; }
                catch { }
            }
            return null;
        }

        private void SubscribeAll()
        {
            if (_playerPanel != null)
            {
                _playerPanel.OnSlotBeginDrag += OnBeginDrag;
                _playerPanel.OnSlotEndDrag += OnEndDrag;
                _playerPanel.OnSlotEnter += OnEnterSlot;
                _playerPanel.OnSlotExit += OnExitSlot;
            }
            if (_quickPanel != null)
            {
                _quickPanel.OnSlotBeginDrag += OnBeginDrag;
                _quickPanel.OnSlotEndDrag += OnEndDrag;
                _quickPanel.OnSlotEnter += OnEnterSlot;
                _quickPanel.OnSlotExit += OnExitSlot;
            }
            if (_otherPanel != null)
            {
                _otherPanel.OnSlotBeginDrag += OnBeginDrag;
                _otherPanel.OnSlotEndDrag += OnEndDrag;
                _otherPanel.OnSlotEnter += OnEnterSlot;
                _otherPanel.OnSlotExit += OnExitSlot;
            }
        }

        private void UnsubscribeAll()
        {
            if (_playerPanel != null)
            {
                _playerPanel.OnSlotBeginDrag -= OnBeginDrag;
                _playerPanel.OnSlotEndDrag -= OnEndDrag;
                _playerPanel.OnSlotEnter -= OnEnterSlot;
                _playerPanel.OnSlotExit -= OnExitSlot;
            }
            if (_quickPanel != null)
            {
                _quickPanel.OnSlotBeginDrag -= OnBeginDrag;
                _quickPanel.OnSlotEndDrag -= OnEndDrag;
                _quickPanel.OnSlotEnter -= OnEnterSlot;
                _quickPanel.OnSlotExit -= OnExitSlot;
            }
            if (_otherPanel != null)
            {
                _otherPanel.OnSlotBeginDrag -= OnBeginDrag;
                _otherPanel.OnSlotEndDrag -= OnEndDrag;
                _otherPanel.OnSlotEnter -= OnEnterSlot;
                _otherPanel.OnSlotExit -= OnExitSlot;
            }
        }

        private void Update()
        {
            if (_dragIcon == null || !_dragIcon.enabled) return;

            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvas.transform as RectTransform,
                    Input.mousePosition,
                    _canvas.worldCamera,
                    out var local);
                (_dragIcon.transform as RectTransform).anchoredPosition = local;
            }
            else
            {
                (_dragIcon.transform as RectTransform).position = Input.mousePosition;
            }
        }

        private void OnBeginDrag(InventorySlotUI slot)
        {
            if (slot == null || slot.Item == null) return;

            _srcSlot = slot;
            _srcPanel = slot.ParentPanel;

            if (_dragIcon != null)
            {
                _dragIcon.sprite = slot.Item.Icon;
                _dragIcon.enabled = true;
                _dragIcon.raycastTarget = false;
            }
        }

        private void OnEnterSlot(InventorySlotUI slot)
        {
            _hoverSlot = slot;
            _hoverPanel = slot?.ParentPanel;
        }

        private void OnExitSlot(InventorySlotUI slot)
        {
            if (_hoverSlot == slot)
            {
                _hoverSlot = null;
                _hoverPanel = null;
            }
        }

        private void OnEndDrag(InventorySlotUI _)
        {
            if (_dragIcon != null) _dragIcon.enabled = false;

            if (_srcSlot == null || _srcSlot.Item == null) { ResetDrag(); return; }

            if (_hoverSlot == null || _hoverPanel == null)
                TryPickSlotUnderPointer(out _hoverSlot, out _hoverPanel);

            if (!IsPointerOverUILayer())
            {
                TryWorldDropFromSource();
                ResetDrag();
                return;
            }

            if (_hoverSlot == null || _hoverPanel == null) { ResetDrag(); return; }

            if (!TryDecodePanelIndex(_srcPanel, _srcSlot.SlotIndex, out var fromId, out int fromIdx) ||
                !TryDecodePanelIndex(_hoverPanel, _hoverSlot.SlotIndex, out var toId, out int toIdx))
            {
                ResetDrag(); return;
            }

            if (fromId.Equals(toId) && fromIdx == toIdx) { ResetDrag(); return; }

            var srcItemId = GetItemIdByPanelIndex(_srcPanel, _srcSlot.SlotIndex);
            var srcCount = GetCountByPanelIndex(_srcPanel, _srcSlot.SlotIndex);
            if (string.IsNullOrEmpty(srcItemId) || srcCount <= 0) { ResetDrag(); return; }

            if (_facade != null)
            {
                _facade.Transfer(fromId, fromIdx, toId, toIdx, srcCount);
            }
            else
            {
                Debug.LogWarning("[InventoryTransferController] _facade is null -> transfer not sent. Inject InventoryClientFacade.");
            }

            ResetDrag();
        }

        private void ResetDrag()
        {
            _srcSlot = null;
            _srcPanel = null;
            _hoverSlot = null;
            _hoverPanel = null;
        }


        // InventoryTransferController.cs
        private void TryWorldDropFromSource()
        {
            if (_ic == null || _rpc == null || _inv == null) return;

            if (!TryCalcGlobalIndex(_srcPanel, _srcSlot.SlotIndex, out int fromG)) return;

            var itemId = GetItemIdByGlobalIndex(fromG);
            if (string.IsNullOrEmpty(itemId)) return;

            int count = GetCountByGlobalIndex(fromG);
            if (count <= 0) return;

            GetDropPoint(out var pos, out var fwd);

            // сервер ждёт именно «глобальный индекс»
            _rpc.RPC_RequestDrop(pos, fwd, fromG, count);

            // Никаких локальных правок — ждём дельту
        }

        private void GetDropPoint(out Vector3 pos, out Vector3 fwd)
        {
            pos = Vector3.zero; fwd = Vector3.forward;
            if (_ic == null) return;

            var t = _ic.dropPoint != null ? _ic.dropPoint : null;
            if (t != null)
            {
                pos = t.position;
                fwd = t.forward.sqrMagnitude > 0f ? t.forward.normalized : _ic.transform.forward;
            }
            else
            {
                var cam = _ic.camera != null ? _ic.camera.transform : null;
                if (cam != null)
                {
                    pos = cam.position + cam.forward * 0.2f;
                    fwd = cam.forward;
                }
                else
                {
                    pos = _ic.transform.position + _ic.transform.forward * 0.5f + Vector3.up * 1.4f;
                    fwd = _ic.transform.forward;
                }
            }
        }


        private bool TryCalcGlobalIndex(IInventoryPanelUI panel, int localIndex, out int global)
        {
            global = -1;
            if (panel == null) return false;

            int quickLen = _inv?.GetQuickSlots()?.Length ?? 0;

            switch (panel.Kind)
            {
                case PanelKind.Quick:
                    global = localIndex;
                    return true;

                case PanelKind.Player:
                    global = quickLen + localIndex;   // ВАЖНО: раньше было захардкожено 10
                    return true;

                case PanelKind.Chest:
                default:
                    return false;
            }
        }

        private string GetItemIdByGlobalIndex(int gidx)
        {
            int quickLen = _inv?.GetQuickSlots()?.Length ?? 0;
            if (gidx < quickLen)
            {
                var s = _inv.GetQuickSlots();
                return (s != null && gidx >= 0 && gidx < s.Length) ? s[gidx]?.Id : null;
            }
            else
            {
                int i = gidx - quickLen;
                var s = _inv.GetInventorySlots();
                return (s != null && i >= 0 && i < s.Length) ? s[i]?.Id : null;
            }
        }

        private int GetCountByGlobalIndex(int gidx)
        {
            int quickLen = _inv?.GetQuickSlots()?.Length ?? 0;
            if (gidx < quickLen)
            {
                var s = _inv.GetQuickSlots();
                return (s != null && gidx >= 0 && gidx < s.Length && s[gidx] != null) ? s[gidx].Count : 0;
            }
            else
            {
                int i = gidx - quickLen;
                var s = _inv.GetInventorySlots();
                return (s != null && i >= 0 && i < s.Length && s[i] != null) ? s[i].Count : 0;
            }
        }

        private Game.ItemState GetStateByGlobalIndex(int gidx)
        {
            int quickLen = _inv?.GetQuickSlots()?.Length ?? 0;
            if (gidx < quickLen)
            {
                var s = _inv.GetQuickSlots();
                return (s != null && gidx >= 0 && gidx < s.Length) ? s[gidx]?.State : null;
            }
            else
            {
                int i = gidx - quickLen;
                var s = _inv.GetInventorySlots();
                return (s != null && i >= 0 && i < s.Length) ? s[i]?.State : null;
            }
        }

        private static readonly List<RaycastResult> _rayResults = new List<RaycastResult>(16);
        private bool TryPickSlotUnderPointer(out InventorySlotUI slot, out IInventoryPanelUI panel)
        {
            slot = null; panel = null;

            var es = EventSystem.current;
            if (es == null) return false;

            var ev = new PointerEventData(es) { position = Input.mousePosition };
            _rayResults.Clear();
            es.RaycastAll(ev, _rayResults);

            for (int i = 0; i < _rayResults.Count; i++)
            {
                var go = _rayResults[i].gameObject;
                if (!go) continue;

                slot = go.GetComponentInParent<InventorySlotUI>();
                if (slot != null)
                {
                    panel = slot.ParentPanel ?? go.GetComponentInParent<IInventoryPanelUI>();
                    return true;
                }
            }
            return false;
        }

        private bool IsPointerOverUILayer()
        {
            var es = EventSystem.current;
            if (es == null) return false;

            var ev = new PointerEventData(es) { position = Input.mousePosition };
            _rayResults.Clear();
            es.RaycastAll(ev, _rayResults);

            if (_uiLayer >= 0)
            {
                for (int i = 0; i < _rayResults.Count; i++)
                {
                    var go = _rayResults[i].gameObject;
                    if (go != null && go.layer == _uiLayer)
                        return true;
                }
                return false;
            }

            return _rayResults.Count > 0;
        }
        private bool TryCalcContainerAndIndex(IInventoryPanelUI panel, int localIndex, out ContainerId id, out int idx)
        {
            id = default; idx = -1;
            if (panel == null || _facade == null || _inv == null) return false;

            switch (panel.Kind)
            {
                case PanelKind.Quick:
                    {
                        var q = _inv.GetQuickSlots();
                        int qLen = (q != null) ? q.Length : 0;
                        if (localIndex < 0 || localIndex >= qLen) return false;
                        id = _facade.localQuick;
                        idx = localIndex;
                        return true;
                    }
                case PanelKind.Player:
                    {
                        var m = _inv.GetInventorySlots();
                        int mLen = (m != null) ? m.Length : 0;
                        if (localIndex < 0 || localIndex >= mLen) return false;
                        id = _facade.localMain;
                        idx = localIndex;
                        return true;
                    }
                case PanelKind.Chest:
                    // (когда добавишь поддержку сундуков, нужно будет получить ContainerId сундука)
                    return false;
                default:
                    return false;
            }
        }
        // Преобразуем панель/локальный индекс в ContainerId + локальный индекс
        private bool TryDecodePanelIndex(IInventoryPanelUI panel, int localIndex, out ContainerId id, out int idx)
        {
            id = default;
            idx = -1;
            if (panel == null) return false;

            // Берём фасад — только он знает локальные ID контейнеров игрока
            if (_facade == null) return false;

            switch (panel.Kind)
            {
                case PanelKind.Quick:
                    id = _facade.localQuick;
                    idx = localIndex;
                    return idx >= 0;

                case PanelKind.Player:
                    id = _facade.localMain;
                    idx = localIndex;
                    return idx >= 0;

                default:
                    return false;
            }
        }

        // Эти функции больше не вычисляют «глобальный индекс»,
        // они читают прямо из правильного массива.
        private string GetItemIdByPanelIndex(IInventoryPanelUI panel, int localIndex)
        {
            if (panel == null || localIndex < 0) return null;
            if (panel.Kind == PanelKind.Quick)
            {
                var s = _inv?.GetQuickSlots();
                return (s != null && localIndex < s.Length) ? s[localIndex]?.Id : null;
            }
            if (panel.Kind == PanelKind.Player)
            {
                var s = _inv?.GetInventorySlots();
                return (s != null && localIndex < s.Length) ? s[localIndex]?.Id : null;
            }
            return null;
        }

        private int GetCountByPanelIndex(IInventoryPanelUI panel, int localIndex)
        {
            if (panel == null || localIndex < 0) return 0;
            if (panel.Kind == PanelKind.Quick)
            {
                var s = _inv?.GetQuickSlots();
                return (s != null && localIndex < s.Length && s[localIndex] != null) ? s[localIndex].Count : 0;
            }
            if (panel.Kind == PanelKind.Player)
            {
                var s = _inv?.GetInventorySlots();
                return (s != null && localIndex < s.Length && s[localIndex] != null) ? s[localIndex].Count : 0;
            }
            return 0;
        }


    }
}