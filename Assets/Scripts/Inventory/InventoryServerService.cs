using System;
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public sealed class InventoryServerService
    {
        private readonly ItemDatabaseSO _db;

        [Inject(Optional = true)] private InventorySnapshotBuilder _snapshots;
        [Inject(Optional = true)] private NetworkRunner _runner;
        [Inject(Optional = true)] private NetworkRunnerProvider _runnerProvider;
        [Inject(Optional = true)] private InventoryContainerRegistry _registry;


        private readonly Dictionary<ContainerId, HashSet<PlayerRef>> _watchers =
            new Dictionary<ContainerId, HashSet<PlayerRef>>(new ContainerIdComparer());

        public InventoryServerService(ItemDatabaseSO db) { _db = db; }


        public bool TryOpenContainer(PlayerRef requester, ContainerId id,
                             out ContainerSnapshot snap, out string reason)
        {
            snap = default; reason = null;

            if (!TryResolveContainer(id, out var container))
            { reason = "Container not found"; return false; }

            if (!container.CanPlayerAccess(requester))
            { reason = "Access denied"; return false; }

            if (_snapshots == null)
            { reason = "SnapshotBuilder missing"; return false; }

            // ВАЖНО: работаем с реальным ID контейнера
            var realId = container.Id;

            AddWatcher(realId, requester);
            snap = _snapshots.Build(realId); // ← не по исходному id
            return true;
        }

        public bool TryCloseContainer(PlayerRef requester, ContainerId id, out string reason)
        {
            reason = null;
            if (!TryResolveContainer(id, out var container))
            { reason = "Container not found"; return false; }

            if (!container.CanPlayerAccess(requester))
            { reason = "Access denied"; return false; }

            RemoveWatcher(container.Id, requester); // ← тоже реальный ID
            return true;
        }

        public IEnumerable<PlayerRef> Watchers(ContainerId id)
        {
            if (_watchers.TryGetValue(id, out var set) && set != null && set.Count > 0)
                return set;
            return new[] { id.ownerRef };
        }

        public bool TryTransfer(
    PlayerRef actor,
    ContainerId fromId, int fromIdx,
    ContainerId toId,   int toIdx,
    int amount,
    out ContainerDelta fromDelta, out ContainerDelta toDelta, out bool swapped,
    out string reason)
{
    reason = null; swapped = false; fromDelta = null; toDelta = null;

    // 👇 страховка: даже если пришёл owner=None, поправим на сервере
    if ((fromId.type == ContainerType.PlayerQuick || fromId.type == ContainerType.PlayerMain) && fromId.ownerRef == PlayerRef.None)
        fromId.ownerRef = actor;
    if ((toId.type == ContainerType.PlayerQuick || toId.type == ContainerType.PlayerMain) && toId.ownerRef == PlayerRef.None)
        toId.ownerRef = actor;

    if (!TryResolveContainer(fromId, out var from))
    { reason = "no_from_container"; return false; }

    if (!TryResolveContainer(toId, out var to))
    { reason = "no_to_container"; return false; }

    // дальше — как было...


            bool HasAccess(IInventoryContainer c)
            {
                // Владелец своих контейнеров всегда имеет доступ
                if ((c.Id.type == ContainerType.PlayerQuick || c.Id.type == ContainerType.PlayerMain) &&
                    c.Id.ownerRef == actor)
                    return true;

                // Для прочих — штатная проверка (сундуки/чужие)
                return c.CanPlayerAccess(actor);
            }

            if (!HasAccess(from))
            {
                reason = "no_access_from";
                return false;
            }
            if (!HasAccess(to))
            {
                reason = "no_access_to";
                return false;
            }

            var fSlots = from.Slots;
            var tSlots = to.Slots;
            if (fSlots == null || tSlots == null) { reason = "null_slots"; return false; }
            if (fromIdx < 0 || fromIdx >= fSlots.Length) { reason = "from_index_oob"; return false; }
            if (toIdx   < 0 || toIdx   >= tSlots.Length)  { reason = "to_index_oob";   return false; }

            var src = fSlots[fromIdx];
            var dst = tSlots[toIdx];

            var srcId = InventorySlotStateAccessor.ReadId(src);
            var dstId = InventorySlotStateAccessor.ReadId(dst);
            var srcCnt = InventorySlotStateAccessor.ReadCount(src);
            var dstCnt = InventorySlotStateAccessor.ReadCount(dst);

            if (string.IsNullOrEmpty(srcId) || srcCnt <= 0)
            {
                reason = "empty_src";
                return false;
            }

            int move = Mathf.Clamp(amount <= 0 ? srcCnt : amount, 1, srcCnt);

            // Цель занята
            if (!string.IsNullOrEmpty(dstId) && dstCnt > 0)
            {
                // Стек к такому же
                if (dstId == srcId)
                {
                    var so = _db != null ? _db.Get(srcId) : null;
                    int maxStack = Mathf.Max(1, so != null ? so.MaxStack : 999);

                    int free = Mathf.Max(0, maxStack - dstCnt);
                    if (free <= 0) { reason = "stack_full"; return false; }

                    int put = Mathf.Min(move, free);
                    if (put <= 0) { reason = "nothing_to_put"; return false; }

                    var newDst = dst?.Clone() ?? new InventorySlotState();
                    InventorySlotStateAccessor.WriteId(newDst, dstId);
                    InventorySlotStateAccessor.WriteCount(newDst, dstCnt + put);
                    if (InventorySlotStateAccessor.ReadState(newDst) == null)
                        InventorySlotStateAccessor.WriteState(newDst, InventorySlotStateAccessor.ReadState(src)?.Clone());

                    var newSrc = src?.Clone() ?? new InventorySlotState();
                    InventorySlotStateAccessor.WriteCount(newSrc, srcCnt - put);
                    if (InventorySlotStateAccessor.ReadCount(newSrc) <= 0)
                    {
                        InventorySlotStateAccessor.WriteId(newSrc, null);
                        InventorySlotStateAccessor.WriteState(newSrc, null);
                    }

                    int fBefore = from.Version;
                    int tBefore = to.Version;

                    to.SetSlot(toIdx, newDst);   to.IncrementVersion();
                    from.SetSlot(fromIdx, newSrc); from.IncrementVersion();

                    fromDelta = new ContainerDelta
                    {
                        id = from.Id, fromVersion = fBefore, toVersion = from.Version,
                        changes = new[] { new SlotChange { index = fromIdx, state = newSrc?.Clone() } }
                    };
                    toDelta = new ContainerDelta
                    {
                        id = to.Id, fromVersion = tBefore, toVersion = to.Version,
                        changes = new[] { new SlotChange { index = toIdx, state = newDst?.Clone() } }
                    };
                    reason = "ok";
                    return true;
                }

                // Свап с другим — только если переносим всё
                if (move < srcCnt) { reason = "partial_swap_blocked"; return false; }
                if (!to.CanAccept(toIdx, src))   { reason = "to_cannot_accept_src";   return false; }
                if (!from.CanAccept(fromIdx, dst)) { reason = "from_cannot_accept_dst"; return false; }

                int fBeforeSwap = from.Version;
                int tBeforeSwap = to.Version;

                var srcCopy = src?.Clone();
                var dstCopy = dst?.Clone();

                to.SetSlot(toIdx, srcCopy);     to.IncrementVersion();
                from.SetSlot(fromIdx, dstCopy); from.IncrementVersion();

                swapped = true;
                fromDelta = new ContainerDelta
                {
                    id = from.Id, fromVersion = fBeforeSwap, toVersion = from.Version,
                    changes = new[] { new SlotChange { index = fromIdx, state = dstCopy?.Clone() } }
                };
                toDelta = new ContainerDelta
                {
                    id = to.Id, fromVersion = tBeforeSwap, toVersion = to.Version,
                    changes = new[] { new SlotChange { index = toIdx, state = srcCopy?.Clone() } }
                };
                reason = "ok";
                return true;
            }

            // Цель пустая
            if (!to.CanAccept(toIdx, src)) { reason = "to_cannot_accept_src"; return false; }

            var moved = src?.Clone();  InventorySlotStateAccessor.WriteCount(moved, move);
            var left  = src?.Clone();  InventorySlotStateAccessor.WriteCount(left,  srcCnt - move);
            if (InventorySlotStateAccessor.ReadCount(left) <= 0)
            {
                InventorySlotStateAccessor.WriteId(left, null);
                InventorySlotStateAccessor.WriteState(left, null);
            }

            int fBefore2 = from.Version;
            int tBefore2 = to.Version;

            to.SetSlot(toIdx, moved);   to.IncrementVersion();
            from.SetSlot(fromIdx, left); from.IncrementVersion();

            fromDelta = new ContainerDelta
            {
                id = from.Id, fromVersion = fBefore2, toVersion = from.Version,
                changes = new[] { new SlotChange { index = fromIdx, state = left?.Clone() } }
            };
            toDelta = new ContainerDelta
            {
                id = to.Id, fromVersion = tBefore2, toVersion = to.Version,
                changes = new[] { new SlotChange { index = toIdx, state = moved?.Clone() } }
            };
            reason = "ok";
            return true;
        }
    


        // ─────────────────────────────────────────────────────────────────────────────
        // PICKUP: server-authoritative, возвращает список дельт
        // ─────────────────────────────────────────────────────────────────────────────

        public bool TryAddItemToPlayer(
            PlayerRef player,
            string itemId,
            int amount,
            int ammo,
            out int left,
            out List<ContainerDelta> deltas,
            out string reason)
        {
            left = amount; deltas = null; reason = null;
            if (left <= 0) return true;

            var so = _db != null ? _db.Get(itemId) : null;
            if (so == null) { reason = "Item not found"; return false; }

            if (!TryGetPlayerContainers(player, out var quick, out var main))
            {
                reason = "Player containers missing";
                return false;
            }

            var first = (so.priority == 1) ? quick : main;
            var second = (so.priority == 1) ? main : quick;

            var list = new List<ContainerDelta>(2);

            left = TryAddToContainerForDeltas(first, so, left, ammo, out var d1);
            if (d1 != null) list.Add(d1);

            if (left > 0)
            {
                left = TryAddToContainerForDeltas(second, so, left, ammo, out var d2);
                if (d2 != null) list.Add(d2);
            }

            if (list.Count > 0) deltas = list;
            if (left > 0) reason = "Not enough space";
            return true;
        }

        // InventoryServerService.cs
        private static int TryAddToContainerForDeltas(
    PlayerInventoryServer container,
    ItemSO so,
    int left,
    int ammo,
    out ContainerDelta delta)
        {
            delta = null;
            if (container == null || left <= 0) return left;

            var slots = container.Slots;
            if (slots == null || slots.Length == 0) return left;

            int beforeVersion = container.Version;
            var changes = new List<SlotChange>(4);

            int max = Mathf.Max(1, so.MaxStack);

            // 1) достаковываем
            for (int i = 0; i < slots.Length && left > 0; i++)
            {
                var s = slots[i];
                var sid = InventorySlotStateAccessor.ReadId(s);
                if (!string.IsNullOrEmpty(sid) && sid == so.Id)
                {
                    int cnt = InventorySlotStateAccessor.ReadCount(s);
                    if (cnt < max)
                    {
                        int put = Mathf.Min(left, max - cnt);
                        var ns = s?.Clone() ?? new InventorySlotState();
                        InventorySlotStateAccessor.WriteId(ns, so.Id);
                        InventorySlotStateAccessor.WriteCount(ns, cnt + put);

                        container.SetSlot(i, ns);
                        container.IncrementVersion();

                        left -= put;
                        changes.Add(new SlotChange { index = i, state = ns?.Clone() });
                    }
                }
            }

            // 2) в пустые (ИМЕННО IsNullOrEmpty)
            for (int i = 0; i < slots.Length && left > 0; i++)
            {
                var s = slots[i];
                var sid = InventorySlotStateAccessor.ReadId(s);
                if (string.IsNullOrEmpty(sid))
                {
                    int put = Mathf.Min(left, max);
                    var ns = s?.Clone() ?? new InventorySlotState();
                    InventorySlotStateAccessor.WriteId(ns, so.Id);
                    InventorySlotStateAccessor.WriteCount(ns, put);
                    InventorySlotStateAccessor.WriteState(ns, new ItemState(ammo));

                    container.SetSlot(i, ns);
                    container.IncrementVersion();

                    left -= put;
                    changes.Add(new SlotChange { index = i, state = ns?.Clone() });
                }
            }

            if (changes.Count > 0)
            {
                delta = new ContainerDelta
                {
                    id = container.Id,
                    fromVersion = beforeVersion,
                    toVersion = container.Version,
                    changes = changes.ToArray()
                };
            }
            return left;
        }
        // ─────────────────────────────────────────────────────────────────────────────
        // SHOOT: расход ammo из быстрого слота
        // ─────────────────────────────────────────────────────────────────────────────

        // Внутри InventoryServerService
        public bool TryConsumeAmmoFromQuick(
            PlayerRef player,
            int quickIndex,
            int amount,
            out int newAmmo,
            out ContainerDelta delta,
            out WeaponSO weaponSO,
            out string reason)
        {
            newAmmo = 0;
            delta = null;
            weaponSO = null;
            reason = null;

            if (!TryGetPlayerContainers(player, out var quick, out _))
            {
                reason = "no_containers";
                return false;
            }

            var slots = quick?.Slots;
            if (slots == null || quickIndex < 0 || quickIndex >= slots.Length)
            {
                reason = "bad_index";
                return false;
            }

            var slot = slots[quickIndex];
            var itemId = InventorySlotStateAccessor.ReadId(slot);
            if (string.IsNullOrEmpty(itemId))
            {
                reason = "empty_slot";
                return false;
            }

            // строго читаем оружие ТОЛЬКО из этого слота
            var so = _db != null ? _db.Get(itemId) as WeaponSO : null;
            if (so == null)
            {
                reason = "not_a_weapon";
                return false;
            }

            var st = InventorySlotStateAccessor.ReadState(slot);
            int ammo = st?.ammo ?? 0;
            if (ammo < amount)
            {
                reason = "no ammo";
                return false;
            }

            // списываем из ЭТОГО ЖЕ quickIndex
            var ns = slot?.Clone() ?? new InventorySlotState();
            var newState = (st != null) ? new ItemState(st) : new ItemState();
            newState.ammo = ammo - amount;
            InventorySlotStateAccessor.WriteId(ns, itemId);
            InventorySlotStateAccessor.WriteCount(ns, Mathf.Max(1, InventorySlotStateAccessor.ReadCount(slot))); // count не меняем
            InventorySlotStateAccessor.WriteState(ns, newState);

            int before = quick.Version;
            quick.SetSlot(quickIndex, ns);
            quick.IncrementVersion();

            newAmmo = newState.ammo;
            weaponSO = so;

            delta = new ContainerDelta
            {
                id = quick.Id,
                fromVersion = before,
                toVersion = quick.Version,
                changes = new[] { new SlotChange { index = quickIndex, state = ns?.Clone() } }
            };

            return true;
        }


        public bool TryReloadWeapon(
    PlayerRef player,
    int quickIndex,
    out int newAmmo,
    out List<ContainerDelta> deltas,
    out string reason)
        {
            newAmmo = 0; deltas = null; reason = null;

            if (!TryGetPlayerContainers(player, out var quick, out var main))
            { reason = "Player containers missing"; return false; }

            var qs = quick.Slots;
            if (qs == null || quickIndex < 0 || quickIndex >= qs.Length)
            { reason = "Bad quick index"; return false; }

            var s = qs[quickIndex];
            var wid = InventorySlotStateAccessor.ReadId(s);
            var wst = InventorySlotStateAccessor.ReadState(s);
            if (string.IsNullOrEmpty(wid)) { reason = "no weapon"; return false; }

            var wSo = _db.Get(wid) as WeaponSO;
            if (wSo == null) { reason = "not a weapon"; return false; }

            ReadWeaponAmmoSpec(wSo, out int magSize, out string ammoItemId);
            if (magSize <= 0) magSize = 30;
            if (string.IsNullOrEmpty(ammoItemId))
            { reason = "weapon has no ammo type"; return false; }

            int curAmmo = wst?.ammo ?? 0;
            int need = magSize - curAmmo;
            if (need <= 0) { newAmmo = curAmmo; deltas = new List<ContainerDelta>(0); return true; }

            var list = new List<ContainerDelta>(2);

            int taken = ConsumeFromContainerById(main, ammoItemId, need, out var mainDelta);
            if (mainDelta != null) list.Add(mainDelta);

            if (taken < need)
            {
                int rest = need - taken;
                int takenQ = ConsumeFromContainerById(quick, ammoItemId, rest, out var quickAmmoDelta, skipIndex: quickIndex);
                taken += takenQ;
                if (quickAmmoDelta != null) list.Add(quickAmmoDelta);
            }

            if (taken <= 0) { reason = "no ammo items"; return false; }

            int before = quick.Version;
            int toPut = Mathf.Min(need, taken);

            var ns = s?.Clone() ?? new InventorySlotState();
            var newState = wst != null ? new ItemState(wst) : new ItemState();
            newState.ammo = curAmmo + toPut;
            InventorySlotStateAccessor.WriteState(ns, newState);

            quick.SetSlot(quickIndex, ns);
            quick.IncrementVersion();

            list.Add(new ContainerDelta
            {
                id = quick.Id,
                fromVersion = before,
                toVersion = quick.Version,
                changes = new[] { new SlotChange { index = quickIndex, state = ns?.Clone() } }
            });

            deltas = list;
            newAmmo = newState.ammo;
            return true;
        }

        private static int ConsumeFromContainerById(
            PlayerInventoryServer container,
            string itemId,
            int need,
            out ContainerDelta delta,
            int skipIndex = -1)
        {
            delta = null;
            if (container == null || need <= 0) return 0;

            var slots = container.Slots;
            if (slots == null || slots.Length == 0) return 0;

            int before = container.Version;
            var changes = new List<SlotChange>(4);
            int taken = 0;

            for (int i = 0; i < slots.Length && taken < need; i++)
            {
                if (i == skipIndex) continue;

                var s = slots[i];
                var sid = InventorySlotStateAccessor.ReadId(s);
                var cnt = InventorySlotStateAccessor.ReadCount(s);

                if (sid == itemId && cnt > 0)
                {
                    int take = Mathf.Min(cnt, need - taken);

                    var ns = s?.Clone() ?? new InventorySlotState();
                    InventorySlotStateAccessor.WriteCount(ns, cnt - take);
                    if (InventorySlotStateAccessor.ReadCount(ns) <= 0)
                    {
                        InventorySlotStateAccessor.WriteId(ns, null);
                        InventorySlotStateAccessor.WriteState(ns, null);
                    }

                    container.SetSlot(i, ns);
                    container.IncrementVersion();
                    taken += take;
                    changes.Add(new SlotChange { index = i, state = ns?.Clone() });
                }
            }

            if (changes.Count > 0)
            {
                delta = new ContainerDelta
                {
                    id = container.Id,
                    fromVersion = before,
                    toVersion = container.Version,
                    changes = changes.ToArray()
                };
            }

            return taken;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────────

        private static InventorySlotState SafeGet(PlayerInventoryServer c, int idx)
        {
            var slots = c.Slots;
            if (slots == null || idx < 0 || idx >= slots.Length) return null;
            return slots[idx];
        }

        private bool TryGetPlayerContainers(PlayerRef player, out PlayerInventoryServer quick, out PlayerInventoryServer main)
        {
            quick = null;
            main = null;

            // 1) Сначала пробуем через реестр (надежно)
            if (_registry != null)
            {
                var qId = ContainerId.PlayerQuickOf(player);
                var mId = ContainerId.PlayerMainOf(player);

                if (_registry.TryGet(qId, out var q) && q is PlayerInventoryServer qs) quick = qs;
                if (_registry.TryGet(mId, out var m) && m is PlayerInventoryServer ms) main = ms;

                if (quick != null && main != null)
                    return true;
            }

            // 2) Фолбэк через Runner
            var runner = ResolveRunner();
            if (runner == null)
                return false;

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

            // 0) Быстрый путь — реестр
            if (_registry != null && _registry.TryGet(id, out var ic) && ic is PlayerInventoryServer ps0)
            {
                container = ps0;
                return true;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[INV][Server] Registry miss for {id.type}, owner={id.ownerRef}. Trying runner fallback...");
#endif

            // 1) Runner → PlayerObject → компоненты
            var runner = ResolveRunner();
            NetworkObject ownerNO = null;                   // ← объявляем заранее

            if (runner != null)
            {
                runner.TryGetPlayerObject(id.ownerRef, out ownerNO);
                if (ownerNO != null)
                {
                    var all = ownerNO.GetComponentsInChildren<PlayerInventoryServer>(true);
                    for (int i = 0; i < all.Length; i++)
                    {
                        var c = all[i];
                        if (c == null) continue;

                        // Совпадение по типу и владельцу
                        if (c.Id.type == id.type && c.Object != null && c.Object.InputAuthority == id.ownerRef)
                        {
                            container = c;
                            return true;
                        }
                    }
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (runner == null) Debug.LogWarning("[INV][Server] ResolveRunner() returned null.");
            else if (ownerNO == null) Debug.LogWarning($"[INV][Server] Runner has no PlayerObject for {id.ownerRef}. Trying global scan...");
#endif

            // 2) Последний шанс — глобальный скан по сцене
            var allGlobal = UnityEngine.Object.FindObjectsOfType<PlayerInventoryServer>(true);
            for (int i = 0; i < allGlobal.Length; i++)
            {
                var c = allGlobal[i];
                if (c == null) continue;

                if (c.Id.type == id.type && c.Id.ownerRef == id.ownerRef)
                {
                    container = c;
                    return true;
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[INV][Server] Container not found: {id.type}, owner={id.ownerRef}.");
#endif
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
                        if (p.GetValue(_runnerProvider) is NetworkRunner val1) return val1;
                    }
                }
                foreach (var methodName in new[] { "Get", "GetRunner", "GetCurrent", "Resolve", "GetOrCreate" })
                {
                    var m = t.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                    if (m != null && typeof(NetworkRunner).IsAssignableFrom(m.ReturnType))
                    {
                        if (m.Invoke(_runnerProvider, null) is NetworkRunner val2) return val2;
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

        private static void ReadWeaponAmmoSpec(WeaponSO so, out int magSize, out string ammoItemId)
        {
            magSize = 0;
            ammoItemId = null;
            if (so == null) return;

            if (so.maxAmmo > 0) magSize = so.maxAmmo;

            if (so.ammoResource != null)
                ammoItemId = so.ammoResource.Id;

            if (magSize <= 0)
                magSize = ReadIntViaReflection(so, "clipSize", "magSize", "magazineSize", "MagSize", "ClipSize");

            if (string.IsNullOrEmpty(ammoItemId))
            {
                ammoItemId = ReadStringViaReflection(so, "ammoItemId", "ammoId", "AmmoId", "ammo");
                if (string.IsNullOrEmpty(ammoItemId))
                {
                    var itemSO = ReadObjectViaReflection(so, "ammoItem", "AmmoItem", "AmmoSO") as ItemSO;
                    if (itemSO != null) ammoItemId = itemSO.Id;

                    if (string.IsNullOrEmpty(ammoItemId))
                    {
                        var resSO = ReadObjectViaReflection(so, "ammoResource", "AmmoResource") as ResourceSO;
                        if (resSO != null) ammoItemId = resSO.Id;
                    }
                }
            }
        }

        private static int ReadIntViaReflection(object obj, params string[] names)
        {
            var t = obj.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var n in names)
            {
                var f = t.GetField(n, flags);
                if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(obj);
                var p = t.GetProperty(n, flags);
                if (p != null && p.CanRead && p.PropertyType == typeof(int)) return (int)p.GetValue(obj);
            }
            return 0;
        }

        private static string ReadStringViaReflection(object obj, params string[] names)
        {
            var t = obj.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var n in names)
            {
                var f = t.GetField(n, flags);
                if (f != null && f.FieldType == typeof(string)) return (string)f.GetValue(obj);
                var p = t.GetProperty(n, flags);
                if (p != null && p.CanRead && p.PropertyType == typeof(string)) return (string)p.GetValue(obj);
            }
            return null;
        }

        private static object ReadObjectViaReflection(object obj, params string[] names)
        {
            var t = obj.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var n in names)
            {
                var f = t.GetField(n, flags);
                if (f != null) return f.GetValue(obj);
                var p = t.GetProperty(n, flags);
                if (p != null && p.CanRead) return p.GetValue(obj);
            }
            return null;
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


        public bool TryResolveGlobalIndex(PlayerRef player, int globalIndex, out PlayerInventoryServer container, out int slotIndex)
        {
            container = null;
            slotIndex = -1;

            if (!TryGetPlayerContainers(player, out var quick, out var main))
                return false;

            int qcap = quick?.Slots?.Length ?? 0;
            int mcap = main?.Slots?.Length ?? 0;
            int total = qcap + mcap;

            if (globalIndex < 0 || globalIndex >= total) return false;

            if (globalIndex < qcap)
            {
                container = quick;
                slotIndex = globalIndex;
            }
            else
            {
                container = main;
                slotIndex = globalIndex - qcap;
            }

            return container != null && slotIndex >= 0;
        }
        public bool TryRemove(PlayerRef player, ContainerId containerId, int slotIndex, int count, out List<ContainerDelta> deltas)
        {
            deltas = null;

            if (!TryResolveContainer(containerId, out var container)) return false;
            if (!container.CanPlayerAccess(player)) return false;

            var slots = container.Slots;
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return false;

            var s = slots[slotIndex];
            var id = InventorySlotStateAccessor.ReadId(s);
            var cur = InventorySlotStateAccessor.ReadCount(s);
            if (string.IsNullOrEmpty(id) || cur <= 0) return false;

            int remove = Mathf.Clamp(count, 1, cur);
            int before = container.Version;

            var ns = s?.Clone() ?? new InventorySlotState();
            InventorySlotStateAccessor.WriteCount(ns, cur - remove);
            if (InventorySlotStateAccessor.ReadCount(ns) <= 0)
            {
                InventorySlotStateAccessor.WriteId(ns, null);
                InventorySlotStateAccessor.WriteState(ns, null);
            }

            container.SetSlot(slotIndex, ns);
            container.IncrementVersion();

            var delta = new ContainerDelta
            {
                id = container.Id,
                fromVersion = before,
                toVersion = container.Version,
                changes = new[] { new SlotChange { index = slotIndex, state = ns?.Clone() } }
            };

            deltas = new List<ContainerDelta>(1) { delta };
            return true;
        }


    }
}