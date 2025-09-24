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

        [SerializeField] private LayerMask _slotsLayers;


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

        void OnEndDrag(InventorySlotUI _)
        {
            if (_dragIcon != null) _dragIcon.enabled = false;
            if (_srcSlot == null || _srcSlot.Item == null) { ResetDrag(); return; }

            if (_hoverSlot == null || _hoverPanel == null)
                TryPickSlotUnderPointer(out _hoverSlot, out _hoverPanel);

            var overSlot = _hoverSlot != null && _hoverPanel != null;

            if (!overSlot)
            {
                TryWorldDropFromSource();
                ResetDrag();
                return;
            }

            EnsureFacadeReady();
            if (_facade == null)
            {
                ResetDrag();
                return;
            }

            ContainerId fromId, toId;

            if (_srcPanel.Kind == PanelKind.Quick)
            {
                if (!TryResolveLocal(ContainerType.PlayerQuick, out fromId)) { ResetDrag(); return; }
            }
            else if (_srcPanel.Kind == PanelKind.Player)
            {
                if (!TryResolveLocal(ContainerType.PlayerMain, out fromId)) { ResetDrag(); return; }
            }
            else if (_srcPanel.Kind == PanelKind.Chest)
            {
                if (_otherPanel == null || _otherPanel.CurrentId.Equals(default)) { ResetDrag(); return; }
                fromId = _otherPanel.CurrentId;
            }
            else { ResetDrag(); return; }

            if (_hoverPanel.Kind == PanelKind.Quick)
            {
                if (!TryResolveLocal(ContainerType.PlayerQuick, out toId)) { ResetDrag(); return; }
            }
            else if (_hoverPanel.Kind == PanelKind.Player)
            {
                if (!TryResolveLocal(ContainerType.PlayerMain, out toId)) { ResetDrag(); return; }
            }
            else if (_hoverPanel.Kind == PanelKind.Chest)
            {
                if (_otherPanel == null || _otherPanel.CurrentId.Equals(default)) { ResetDrag(); return; }
                toId = _otherPanel.CurrentId;
            }
            else { ResetDrag(); return; }

            int fromIdx = _srcSlot.SlotIndex;
            int toIdx = _hoverSlot.SlotIndex;

            if (!IsIndexValid(_srcPanel.Kind, fromIdx) || !IsIndexValid(_hoverPanel.Kind, toIdx))
            {
                ResetDrag();
                return;
            }

            if (fromIdx == toIdx && fromId.Equals(toId)) { ResetDrag(); return; }

            int amount;
            if (_srcPanel.Kind == PanelKind.Quick)
            {
                var qs = _inv.GetQuickSlots();
                amount = Mathf.Max(1, (qs != null && fromIdx >= 0 && fromIdx < qs.Length && qs[fromIdx] != null) ? qs[fromIdx].Count : 1);
            }
            else if (_srcPanel.Kind == PanelKind.Player)
            {
                var inv = _inv.GetInventorySlots();
                amount = Mathf.Max(1, (inv != null && fromIdx >= 0 && fromIdx < inv.Length && inv[fromIdx] != null) ? inv[fromIdx].Count : 1);
            }
            else
            {
                amount = 1;
                if (_otherPanel != null && !_otherPanel.CurrentId.Equals(default))
                {
                    if (_facade.TryGetSnapshotResolved(_otherPanel.CurrentId, out var _, out var _, out var chestSlots) &&
                        chestSlots != null && fromIdx >= 0 && fromIdx < chestSlots.Length && chestSlots[fromIdx] != null)
                    {
                        var cnt = chestSlots[fromIdx].count;
                        amount = Mathf.Max(1, cnt);
                    }
                }
            }

            _facade.Transfer(fromId, fromIdx, toId, toIdx, amount, (ok, msg) => { });
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
            if (_ic == null || _rpc == null) return;

            int localIdx = _srcSlot.SlotIndex;

            if (_srcPanel.Kind == PanelKind.Quick)
            {
                if (_inv == null) return;
                var qs = _inv.GetQuickSlots();
                if (qs == null || localIdx < 0 || localIdx >= qs.Length) return;
                var slotRef = qs[localIdx];
                if (slotRef == null || string.IsNullOrEmpty(slotRef.Id) || slotRef.Count <= 0) return;

                GetDropPoint(out var pos, out var fwd);
                _rpc.RPC_RequestDrop(pos, fwd, localIdx, slotRef.Count);
                return;
            }

            if (_srcPanel.Kind == PanelKind.Player)
            {
                if (_inv == null) return;
                var inv = _inv.GetInventorySlots();
                if (inv == null || localIdx < 0 || localIdx >= inv.Length) return;
                var slotRef = inv[localIdx];
                if (slotRef == null || string.IsNullOrEmpty(slotRef.Id) || slotRef.Count <= 0) return;

                GetDropPoint(out var pos, out var fwd);
                int quickCap = GetQuickLen();
                _rpc.RPC_RequestDrop(pos, fwd, quickCap + localIdx, slotRef.Count);
                return;
            }

            if (_srcPanel.Kind == PanelKind.Chest)
            {
                EnsureFacadeReady();
                if (_facade == null || _otherPanel == null) return;

                var probe = _otherPanel.CurrentId;
                if (probe.Equals(default)) return;

                if (!_facade.TryGetSnapshotResolved(probe, out var resolvedId, out var version, out var slots)) return;
                if (slots == null || localIdx < 0 || localIdx >= slots.Length) return;

                var s = slots[localIdx];
                if (s == null) return;

                var idStr = InventorySlotStateAccessor.ReadId(s);
                if (string.IsNullOrEmpty(idStr)) return;

                int amount = Mathf.Max(1, InventorySlotStateAccessor.ReadCount(s));

                GetDropPoint(out var pos, out var fwd);
                _rpc.RPC_RequestDropFromContainer(pos, fwd, localIdx, amount, (byte)resolvedId.type, resolvedId.ownerRef, resolvedId.objectId);
                return;
            }
        }

        private void GetDropPoint(out Vector3 pos, out Vector3 fwd)
        {
            if (_ic != null)
            {
                pos = _ic.GetDropPointPosition();
                fwd = _ic.transform.forward;
            }
            else
            {
                pos = Vector3.zero;
                fwd = Vector3.forward;
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

            bool maskConfigured = _slotsLayers.value != 0;

            for (int i = 0; i < _rayResults.Count; i++)
            {
                var rr = _rayResults[i];
                if (!(rr.module is GraphicRaycaster)) continue;

                var go = rr.gameObject;
                if (!go) continue;

                var s = go.GetComponentInParent<InventorySlotUI>();
                if (s == null) continue;

                if (maskConfigured)
                {
                    if (!AnyParentMatches(go.transform, _slotsLayers) && !AnyParentMatches(s.transform, _slotsLayers))
                        continue;
                }

                slot = s;
                panel = s.ParentPanel ?? go.GetComponentInParent<IInventoryPanelUI>();
                return true;
            }

            return false;
        }

        private static bool AnyParentMatches(Transform t, LayerMask mask)
        {
            while (t != null)
            {
                if ((mask.value & (1 << t.gameObject.layer)) != 0)
                    return true;
                t = t.parent;
            }
            return false;
        }

        private void EnsureFacadeReady()
        {
            if (_facade == null) return;

            bool needBind =
                _facade.localQuick.ownerRef == PlayerRef.None ||
                _facade.localMain.ownerRef == PlayerRef.None;

            if (!needBind) return;

            InventoryRpcRouter router = null;

            if (_ic != null)
            {
                router = _ic.GetComponent<InventoryRpcRouter>();
                if (router == null) router = _ic.GetComponentInParent<InventoryRpcRouter>();
                if (router == null && _ic.Object != null)
                {
                    if (router == null && _ic.Runner != null && _ic.Runner.TryGetPlayerObject(_ic.Object.InputAuthority, out var po) && po != null)
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
            {
                var poId = default(NetworkId);
                if (router.Runner != null && router.Runner.TryGetPlayerObject(_ic.Object.InputAuthority, out var po) && po != null)
                    poId = po.Id;

                _facade.SetLocal(_ic.Object.InputAuthority, router, poId);
            }
        }

        private bool TryResolveLocal(ContainerType type, out ContainerId id)
        {
            var owner = PlayerRef.None;
            if (_facade != null)
            {
                if (type == ContainerType.PlayerQuick) owner = _facade.localQuick.ownerRef;
                else if (type == ContainerType.PlayerMain) owner = _facade.localMain.ownerRef;
            }
            if (owner == PlayerRef.None && _ic != null && _ic.Object != null)
                owner = _ic.Object.InputAuthority;

            if (owner == PlayerRef.None)
            {
                id = default;
                return false;
            }

            id = new ContainerId { type = type, ownerRef = owner, objectId = default };
            return true;
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

        private int GetOtherLen()
        {
            if (_otherPanel == null) return 0;
            var id = _otherPanel.CurrentId;
            if (id.Equals(default)) return 0;
            return _facade != null ? _facade.GetCapacityImmediate(id) : 0;
        }



        private bool IsIndexValid(PanelKind kind, int idx)
        {
            if (idx < 0) return false;
            if (kind == PanelKind.Quick) return idx < GetQuickLen();
            if (kind == PanelKind.Player) return idx < GetMainLen();
            if (kind == PanelKind.Chest) return idx < GetOtherLen();
            return false;
        }

    }
}