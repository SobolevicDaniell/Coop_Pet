using System.Collections.Generic;
using Fusion;
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
        [Inject(Optional = true)] private PlayerStatsSO _stats;


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

            EnsureFacadeReady();

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

        // Scripts/UI/Inventory/InventoryTransferController.cs
        void OnEndDrag(InventorySlotUI _)
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

            EnsureFacadeReady();
            if (_facade == null ||
                _facade.localQuick.ownerRef == PlayerRef.None ||
                _facade.localMain.ownerRef == PlayerRef.None)
            { ResetDrag(); return; }

            if (_hoverSlot == null || _hoverPanel == null) { ResetDrag(); return; }

            ContainerId fromId, toId;
            if (_srcPanel.Kind == PanelKind.Quick) fromId = _facade.localQuick;
            else if (_srcPanel.Kind == PanelKind.Player) fromId = _facade.localMain;
            else { ResetDrag(); return; }

            if (_hoverPanel.Kind == PanelKind.Quick) toId = _facade.localQuick;
            else if (_hoverPanel.Kind == PanelKind.Player) toId = _facade.localMain;
            else { ResetDrag(); return; }

            int fromIdx = _srcSlot.SlotIndex;
            int toIdx = _hoverSlot.SlotIndex;

            if (!IsIndexValid(_srcPanel.Kind, fromIdx) || !IsIndexValid(_hoverPanel.Kind, toIdx))
            {
                Debug.LogWarning($"[DnD] invalid index: from {fromId.type}:{fromIdx}, to {toId.type}:{toIdx}. caps quick={GetQuickLen()}, main={GetMainLen()}");
                ResetDrag();
                return;
            }

            if (fromIdx == toIdx && fromId.Equals(toId)) { ResetDrag(); return; }

            int amount;
            if (_srcPanel.Kind == PanelKind.Quick)
            {
                var qs = _inv.GetQuickSlots();
                amount = Mathf.Max(1, (qs != null && fromIdx < qs.Length && qs[fromIdx] != null) ? qs[fromIdx].Count : 1);
            }
            else
            {
                var inv = _inv.GetInventorySlots();
                amount = Mathf.Max(1, (inv != null && fromIdx < inv.Length && inv[fromIdx] != null) ? inv[fromIdx].Count : 1);
            }

            _facade.Transfer(fromId, fromIdx, toId, toIdx, amount, (ok, msg) =>
            {
                Debug.Log($"[DnD] transfer ack: ok={ok}, msg={msg}, from={fromId.type}:{fromIdx} -> to={toId.type}:{toIdx}, amount={amount}");
            });

            ResetDrag();
        }

        private void ResetDrag()
        {
            _srcSlot = null;
            _srcPanel = null;
            _hoverSlot = null;
            _hoverPanel = null;
        }


        void TryWorldDropFromSource()
        {
            if (_ic == null || _rpc == null || _inv == null) return;

            int localIdx = _srcSlot.SlotIndex;

            Game.InventorySlot slotRef = null;
            if (_srcPanel.Kind == PanelKind.Quick)
            {
                var qs = _inv.GetQuickSlots();
                if (qs != null && localIdx >= 0 && localIdx < qs.Length)
                    slotRef = qs[localIdx];
            }
            else if (_srcPanel.Kind == PanelKind.Player)
            {
                var inv = _inv.GetInventorySlots();
                if (inv != null && localIdx >= 0 && localIdx < inv.Length)
                    slotRef = inv[localIdx];
            }

            if (slotRef == null || string.IsNullOrEmpty(slotRef.Id) || slotRef.Count <= 0)
                return;

            int quickCap = GetQuickLen();
            int fromG = (_srcPanel.Kind == PanelKind.Quick) ? localIdx : quickCap + localIdx;

            GetDropPoint(out var pos, out var fwd);

            _rpc.RPC_RequestDrop(pos, fwd, fromG, slotRef.Count);
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

            int quickCap = 0;
            var qs = _inv?.GetQuickSlots();
            if (qs != null) quickCap = qs.Length;
            if (quickCap <= 0 && _facade != null)
                quickCap = _facade.GetLocalQuickCapacity();

            if (panel.Kind == PanelKind.Quick)
            {
                global = localIndex;
                return true;
            }

            if (panel.Kind == PanelKind.Player)
            {
                global = quickCap + localIndex;
                return true;
            }

            return false;
        }

        private string GetItemIdByGlobalIndex(int gidx)
        {
            int quickCap = _inv?.GetQuickSlots()?.Length ?? (_facade != null ? _facade.GetLocalQuickCapacity() : 0);
            if (gidx < quickCap)
            {
                var s = _inv.GetQuickSlots();
                return (s != null && gidx >= 0 && gidx < s.Length) ? s[gidx]?.Id : null;
            }
            else
            {
                int i = gidx - quickCap;
                var s = _inv.GetInventorySlots();
                return (s != null && i >= 0 && i < s.Length) ? s[i]?.Id : null;
            }
        }

        private int GetCountByGlobalIndex(int gidx)
        {
            int quickCap = _inv?.GetQuickSlots()?.Length ?? (_facade != null ? _facade.GetLocalQuickCapacity() : 0);
            if (gidx < quickCap)
            {
                var s = _inv.GetQuickSlots();
                return (s != null && gidx >= 0 && gidx < s.Length && s[gidx] != null) ? s[gidx].Count : 0;
            }
            else
            {
                int i = gidx - quickCap;
                var s = _inv.GetInventorySlots();
                return (s != null && i >= 0 && i < s.Length && s[i] != null) ? s[i].Count : 0;
            }
        }

        private Game.ItemState GetStateByGlobalIndex(int gidx)
        {
            int quickCap = _inv?.GetQuickSlots()?.Length ?? (_facade != null ? _facade.GetLocalQuickCapacity() : 0);
            if (gidx < quickCap)
            {
                var s = _inv.GetQuickSlots();
                return (s != null && gidx >= 0 && gidx < s.Length) ? s[gidx]?.State : null;
            }
            else
            {
                int i = gidx - quickCap;
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

        bool IsPointerOverUILayer()
        {
            var es = EventSystem.current;
            if (es == null) return false;

            var ev = new PointerEventData(es) { position = Input.mousePosition };
            _rayResults.Clear();
            es.RaycastAll(ev, _rayResults);

            if (_rayResults.Count == 0) return false;

            if (_uiLayer >= 0)
            {
                for (int i = 0; i < _rayResults.Count; i++)
                {
                    var rr = _rayResults[i];
                    var go = rr.gameObject;
                    if (go == null) continue;
                    if (go.layer == _uiLayer) return true;
                    if (rr.module is GraphicRaycaster) return true;
                }
                return false;
            }

            return true; // нет выделенного слоя — достаточно любого хита по UI
        }


        private void EnsureFacadeReady()
        {
            if (_facade == null) return;

            bool needBind =
                _facade.localQuick.ownerRef == PlayerRef.None ||
                _facade.localMain.ownerRef == PlayerRef.None;

            if (needBind)
            {
                InventoryRpcRouter router = null;

                if (_ic != null)
                {
                    router = _ic.GetComponent<InventoryRpcRouter>();
                    if (router == null) router = _ic.GetComponentInParent<InventoryRpcRouter>();

                    if (router == null && _ic.Runner != null && _ic.Object != null)
                    {
                        if (_ic.Runner.TryGetPlayerObject(_ic.Object.InputAuthority, out var po) && po != null)
                            router = po.GetComponentInChildren<InventoryRpcRouter>(true);
                    }
                }

                if (router == null)
                {
                    var all = FindObjectsOfType<InventoryRpcRouter>(true);
                    for (int i = 0; i < all.Length && router == null; i++)
                        if (all[i].Object != null && all[i].Object.HasInputAuthority) router = all[i];
                }

                if (router != null && _ic != null && _ic.Object != null)
                    _facade.SetLocal(_ic.Object.InputAuthority, router);
            }
        }

        private int GetQuickLen()
        {
            if (_facade != null)
            {
                var cap = _facade.GetLocalQuickCapacity();
                if (cap > 0) return cap;
            }
            var qs = _inv?.GetQuickSlots();
            if (qs != null && qs.Length > 0) return qs.Length;

            return _stats != null ? Mathf.Max(0, _stats.quickSlotsCount) : 0;
        }

        private int GetMainLen()
        {
            if (_facade != null)
            {
                var cap = _facade.GetLocalMainCapacity();
                if (cap > 0) return cap;
            }
            var inv = _inv?.GetInventorySlots();
            if (inv != null && inv.Length > 0) return inv.Length;

            return _stats != null ? Mathf.Max(0, _stats.inventorySlotsCount) : 0;
        }


        private bool IsIndexValid(PanelKind kind, int idx)
        {
            if (idx < 0) return false;
            if (kind == PanelKind.Quick) return idx < GetQuickLen();
            if (kind == PanelKind.Player) return idx < GetMainLen();
            return false;
        }
    }
}