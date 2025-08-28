using System;
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    /// <summary>
    /// Серверный сервис инвентаря: open/close + подписки, переносы, подбор с приоритетом.
    /// Хранит карту подписчиков контейнеров и рассылает снапшоты через внешний роутер.
    /// </summary>
    public sealed class InventoryServerService
    {
        private readonly ItemDatabaseSO _db;

        [Inject(Optional = true)] private InventorySnapshotBuilder _snapshots;
        [Inject(Optional = true)] private NetworkRunner _runner;
        [Inject(Optional = true)] private NetworkRunnerProvider _runnerProvider;

        // кто сейчас «смотрит» на контейнер (открыт у клиента)
        private readonly Dictionary<ContainerId, HashSet<PlayerRef>> _watchers =
            new Dictionary<ContainerId, HashSet<PlayerRef>>(new ContainerIdComparer());

        public InventoryServerService(ItemDatabaseSO db)
        {
            _db = db;
        }

        // ---------- OPEN / CLOSE (регистрируем watcher'ов) ----------

        public bool TryOpenContainer(PlayerRef requester, ContainerId id, out ContainerSnapshot snap, out string reason)
        {
            snap = default;
            reason = null;

            if (!TryResolveContainer(id, out var container)) { reason = "Container not found"; return false; }
            if (!container.CanPlayerAccess(requester))        { reason = "Access denied";      return false; }
            if (_snapshots == null)                           { reason = "SnapshotBuilder missing"; return false; }

            // регистрируем подписчика
            AddWatcher(id, requester);

            snap = _snapshots.Build(id);
            return true;
        }

        public bool TryCloseContainer(PlayerRef requester, ContainerId id, out string reason)
        {
            reason = null;

            if (!TryResolveContainer(id, out var container)) { reason = "Container not found"; return false; }
            if (!container.CanPlayerAccess(requester))        { reason = "Access denied";      return false; }

            RemoveWatcher(id, requester);
            return true;
        }

        // Для InventoryRpcRouter: вернуть, кому слать снапшот по этому контейнеру
        public IEnumerable<PlayerRef> Watchers(ContainerId id)
        {
            if (_watchers.TryGetValue(id, out var set) && set != null && set.Count > 0)
                return set;

            // безопасный дефолт — владелец контейнера
            return new [] { id.ownerRef };
        }

        // ---------- TRANSFER (сервер-авторитетный перенос/стек/свап) ----------

        public bool TryTransfer(
            PlayerRef actor,
            ContainerId fromId, int fromIdx,
            ContainerId toId,   int toIdx,
            int amount,
            out ContainerDelta fromDelta, out ContainerDelta toDelta, out bool swapped)
        {
            swapped   = false;
            fromDelta = new ContainerDelta(fromId);
            toDelta   = new ContainerDelta(toId);

            if (!TryResolveContainer(fromId, out var from)) return false;
            if (!TryResolveContainer(toId,   out var to))   return false;

            if (!from.CanPlayerAccess(actor) || !to.CanPlayerAccess(actor))
                return false;

            var src = SafeGet(from, fromIdx);
            if (src == null) return false;

            var srcId  = InventorySlotStateAccessor.ReadId(src);
            var srcCnt = InventorySlotStateAccessor.ReadCount(src);
            var srcSt  = InventorySlotStateAccessor.ReadState(src);
            if (string.IsNullOrEmpty(srcId) || srcCnt <= 0) return false;

            var item = _db.Get(srcId);
            if (item == null) return false;

            var dst    = SafeGet(to, toIdx);
            var dstId  = InventorySlotStateAccessor.ReadId(dst);
            var dstCnt = InventorySlotStateAccessor.ReadCount(dst);
            var dstSt  = InventorySlotStateAccessor.ReadState(dst);

            int move = Mathf.Clamp(amount <= 0 ? srcCnt : amount, 1, srcCnt);

            // 1) Стекование (тот же предмет)
            if (!string.IsNullOrEmpty(dstId) && dstId == srcId)
            {
                int can = Mathf.Min(move, item.MaxStack - dstCnt);
                if (can <= 0) return false;

                var newSrc = src.Clone();
                InventorySlotStateAccessor.WriteCount(newSrc, srcCnt - can);
                if (InventorySlotStateAccessor.ReadCount(newSrc) <= 0)
                {
                    InventorySlotStateAccessor.WriteId(newSrc, null);
                    InventorySlotStateAccessor.WriteState(newSrc, null);
                }

                var newDst = dst.Clone();
                InventorySlotStateAccessor.WriteCount(newDst, dstCnt + can);
                if (dstSt == null && srcSt != null)
                    InventorySlotStateAccessor.WriteState(newDst, new ItemState(srcSt));

                from.SetSlot(fromIdx, newSrc);
                to.SetSlot(toIdx, newDst);
                from.IncrementVersion();
                to.IncrementVersion();
                return true;
            }

            // 2) Перемещение в пустую ячейку
            if (string.IsNullOrEmpty(dstId))
            {
                var newSrc = src.Clone();
                var newDst = dst?.Clone() ?? new InventorySlotState();

                int put = move;
                InventorySlotStateAccessor.WriteId(newDst, srcId);
                InventorySlotStateAccessor.WriteCount(newDst, put);
                InventorySlotStateAccessor.WriteState(newDst, srcSt != null ? new ItemState(srcSt) : null);

                if (!to.CanAccept(toIdx, newDst)) return false;

                InventorySlotStateAccessor.WriteCount(newSrc, srcCnt - put);
                if (InventorySlotStateAccessor.ReadCount(newSrc) <= 0)
                {
                    InventorySlotStateAccessor.WriteId(newSrc, null);
                    InventorySlotStateAccessor.WriteState(newSrc, null);
                }

                from.SetSlot(fromIdx, newSrc);
                to.SetSlot(toIdx, newDst);
                from.IncrementVersion();
                to.IncrementVersion();
                return true;
            }

            // 3) Свап (разные предметы, переносится весь стак)
            if (move == srcCnt)
            {
                var newSrc = dst.Clone() ?? new InventorySlotState();
                var newDst = src.Clone() ?? new InventorySlotState();

                if (!from.CanAccept(fromIdx, newSrc)) return false;
                if (!to.CanAccept(toIdx,   newDst)) return false;

                from.SetSlot(fromIdx, newSrc);
                to.SetSlot(toIdx,   newDst);
                from.IncrementVersion();
                to.IncrementVersion();
                swapped = true;
                return true;
            }

            return false;
        }

        // ---------- PICKUP (добавление при подборе, с приоритетом SO) ----------

        public bool TryAddItemToPlayer(
            PlayerRef player,
            string itemId,
            int amount,
            int ammo,
            out int left,
            out ContainerId firstTouched,
            out ContainerId secondTouched,
            out string reason)
        {
            left = amount;
            firstTouched = default;
            secondTouched = default;
            reason = null;

            // защита: если модель слота не содержит id/count — не «проглатываем» предмет
            if (!InventorySlotStateAccessor.HasId || !InventorySlotStateAccessor.HasCnt)
            {
                reason = "Slot model incompatible";
                return false;
            }

            var so = _db.Get(itemId);
            if (so == null) { reason = "Item not found"; return false; }
            if (left <= 0) return true;

            if (!TryGetPlayerContainers(player, out var quick, out var main))
            {
                reason = "Player containers missing";
                return false;
            }

            var first  = (so.priority == 1) ? quick : main;
            var second = (so.priority == 1) ? main  : quick;

            bool t1 = false, t2 = false;

            left = TryAddToContainer(first, so, left, ammo, ref t1);
            if (left > 0) left = TryAddToContainer(second, so, left, ammo, ref t2);

            if (t1) firstTouched = first.Id;
            if (t2) secondTouched = second.Id;

            if (left > 0) reason = "Not enough space";
            return left == 0;
        }

        private static int TryAddToContainer(PlayerInventoryServer container, ItemSO so, int left, int ammo, ref bool touched)
        {
            if (left <= 0) return 0;

            var slots = container.Slots;
            var max   = so.MaxStack;

            // достаковать (уменьшаем left только на реально применённое количество)
            for (int i = 0; i < slots.Length && left > 0; i++)
            {
                var s   = slots[i];
                var sid = InventorySlotStateAccessor.ReadId(s);
                if (sid != null && sid == so.Id)
                {
                    var cnt = InventorySlotStateAccessor.ReadCount(s);
                    if (cnt < max)
                    {
                        int want = Mathf.Min(left, max - cnt);

                        var ns = s?.Clone() ?? new InventorySlotState();
                        InventorySlotStateAccessor.WriteId(ns, so.Id);
                        InventorySlotStateAccessor.WriteCount(ns, cnt + want);
                        container.SetSlot(i, ns);
                        container.IncrementVersion();

                        // верификация применения
                        var after = container.Slots[i];
                        var aid   = InventorySlotStateAccessor.ReadId(after);
                        var acnt  = InventorySlotStateAccessor.ReadCount(after);

                        int applied = (aid == so.Id) ? Mathf.Max(0, acnt - cnt) : 0;
                        if (applied > 0)
                        {
                            left -= applied;
                            touched = true;
                        }
                    }
                }
            }

            // заполнить пустые (аналогично — считаем применённое)
            for (int i = 0; i < slots.Length && left > 0; i++)
            {
                var s   = slots[i];
                var sid = InventorySlotStateAccessor.ReadId(s);
                if (sid == null)
                {
                    int want = Mathf.Min(left, max);

                    var ns = s?.Clone() ?? new InventorySlotState();
                    InventorySlotStateAccessor.WriteId(ns, so.Id);
                    InventorySlotStateAccessor.WriteCount(ns, want);
                    InventorySlotStateAccessor.WriteState(ns, new ItemState(ammo));
                    container.SetSlot(i, ns);
                    container.IncrementVersion();

                    // верификация применения
                    var after = container.Slots[i];
                    var aid   = InventorySlotStateAccessor.ReadId(after);
                    var acnt  = InventorySlotStateAccessor.ReadCount(after);

                    int applied = (aid == so.Id) ? Mathf.Min(want, acnt) : 0;
                    if (applied > 0)
                    {
                        left -= applied;
                        touched = true;
                    }
                }
            }

            return left;
        }

        // ---------- HELPERS ----------

        private static InventorySlotState SafeGet(PlayerInventoryServer c, int idx)
        {
            var slots = c.Slots;
            if (slots == null || idx < 0 || idx >= slots.Length) return null;
            return slots[idx];
        }

        private bool TryGetPlayerContainers(PlayerRef player, out PlayerInventoryServer quick, out PlayerInventoryServer main)
        {
            quick = null; main = null;
            var runner = ResolveRunner();
            if (runner == null) return false;

            if (!runner.TryGetPlayerObject(player, out var playerNO) || playerNO == null)
                return false;

            var all = playerNO.GetComponentsInChildren<PlayerInventoryServer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null) continue;
                if (c.Id.type == ContainerType.PlayerQuick) quick = c;
                else if (c.Id.type == ContainerType.PlayerMain) main = c;
            }
            return quick != null && main != null;
        }

        private bool TryResolveContainer(ContainerId id, out PlayerInventoryServer container)
        {
            container = null;
            var runner = ResolveRunner();
            if (runner == null) return false;

            if (!runner.TryGetPlayerObject(id.ownerRef, out var ownerNO) || ownerNO == null)
                return false;

            var all = ownerNO.GetComponentsInChildren<PlayerInventoryServer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c != null && c.Id.Equals(id)) { container = c; return true; }
            }
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c != null && c.Id.type == id.type) { container = c; return true; }
            }
            return false;
        }

        private NetworkRunner ResolveRunner()
        {
            if (_runner != null) return _runner;

            if (_runnerProvider != null)
            {
                var t = _runnerProvider.GetType();

                foreach (var propName in new[] { "Runner", "Current", "Instance", "Value" })
                {
                    var p = t.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
                    if (p != null && typeof(NetworkRunner).IsAssignableFrom(p.PropertyType))
                    {
                        var val = p.GetValue(_runnerProvider) as NetworkRunner;
                        if (val != null) return val;
                    }
                }
                foreach (var methodName in new[] { "Get", "GetRunner", "GetCurrent", "Resolve", "GetOrCreate" })
                {
                    var m = t.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                    if (m != null && typeof(NetworkRunner).IsAssignableFrom(m.ReturnType))
                    {
                        var val = m.Invoke(_runnerProvider, null) as NetworkRunner;
                        if (val != null) return val;
                    }
                }
            }

            return UnityEngine.Object.FindObjectOfType<NetworkRunner>();
        }

        private void AddWatcher(ContainerId id, PlayerRef viewer)
        {
            if (!_watchers.TryGetValue(id, out var set))
            {
                set = new HashSet<PlayerRef>();
                _watchers[id] = set;
            }
            set.Add(viewer);
        }

        private void RemoveWatcher(ContainerId id, PlayerRef viewer)
        {
            if (_watchers.TryGetValue(id, out var set))
            {
                set.Remove(viewer);
                if (set.Count == 0) _watchers.Remove(id);
            }
        }

        private sealed class ContainerIdComparer : IEqualityComparer<ContainerId>
        {
            public bool Equals(ContainerId a, ContainerId b)
            {
                return a.type == b.type && a.ownerRef == b.ownerRef && a.objectId.Equals(b.objectId);
            }

            public int GetHashCode(ContainerId x)
            {
                unchecked
                {
                    int h = 17;
                    h = h * 31 + (int)x.type;
                    h = h * 31 + x.ownerRef.GetHashCode();
                    h = h * 31 + x.objectId.GetHashCode();
                    return h;
                }
            }
        }
    }
}
