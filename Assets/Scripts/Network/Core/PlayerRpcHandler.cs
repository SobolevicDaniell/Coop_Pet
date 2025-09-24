using System;
using System.Reflection;
using Fusion;
using UnityEngine;
using Zenject;
using System.Collections.Generic;


namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerRpcHandler : NetworkBehaviour
    {

        private InteractionController _ic;
        private ItemDatabaseSO _db;
        private InventoryService _inventory;

        [Inject(Optional = true)] private InventoryServerService _invServer;
        [Inject(Optional = true)] private InventoryRpcRouter _invRouter;

        [Networked] private TickTimer _fireCooldown { get; set; }

        public override void Spawned()
        {
            _invRouter = ResolveServerRouter();

            if (Object.HasStateAuthority)
            {
                var ic = GetComponent<InteractionController>();
                ic?.ServerSetSelectedQuickIndex(-1);
            }

            if (Object.HasInputAuthority && _invRouter != null)
            {
                StartCoroutine(_invRouter.RetryFullResync());
            }
        }

        // ─── DI ────────────────────────────────────────────────────────────────────────
        public void Construct(ItemDatabaseSO db, InteractionController ic, InventoryService inventory)
        {
            _db = db;
            _ic = ic;
            _inventory = inventory;
        }

        private InventoryRpcRouter ResolveServerRouter()
        {
            if (_invRouter != null) return _invRouter;
            var r = Runner != null && Runner.TryGetPlayerObject(Object.InputAuthority, out var po) && po != null
                ? (po.GetComponent<InventoryRpcRouter>() ?? po.GetComponentInChildren<InventoryRpcRouter>(true))
                : null;
            if (r != null) _invRouter = r;
            return r;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_EquipItem(string itemId, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority) return;

            // важный фолбэк: не доверяем кэшу _ic
            var ic = _ic ??= GetComponent<InteractionController>();
            if (ic == null) return;

            if (string.IsNullOrEmpty(itemId))
            {
                ic.handItemController?.UnEquipItemServer();
            }
            else
            {
                ic.handItemController?.EquipItemServer(itemId);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_UnEquipItem(RpcInfo info = default)
        {
            if (!Object.HasStateAuthority) return;

            // важный фолбэк: не доверяем кэшу _ic
            var ic = _ic ??= GetComponent<InteractionController>();
            if (ic == null) return;

            ic.handItemController?.UnEquipItemServer();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RequestReload(int clientSlotIndex, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || _invServer == null) return;

            var actor = info.Source;
            if (actor == PlayerRef.None) actor = Object.InputAuthority;
            if (actor != Object.InputAuthority) return;

            var ic = _ic ??= GetComponent<InteractionController>();
            if (ic == null) return;

            int selected = ic.SelectedQuickIndexNet;
            if (selected < 0) selected = Mathf.Max(-1, clientSlotIndex);
            if (selected < 0) return;

            if (_invServer.TryReloadWeapon(actor, selected, out var newAmmo, out var deltas, out var reason))
            {
                var router = ResolveServerRouter();
                if (router != null && deltas != null)
                {
                    for (int i = 0; i < deltas.Count; i++)
                        router.BroadcastDeltaFromServer(deltas[i]);
                }
                RPC_SetSlotAmmo(selected, newAmmo);
            }
        }

        // ─── Плейсмент мира ───────────────────────────────────────────────────────────
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestPlaceObject(string itemId, Vector3 position, Quaternion rotation, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || Runner == null || _db == null) return;
            var so = _db.Get(itemId);
            if (so == null) return;

            if (!TryGetNetworkPrefab(so, out var placeablePrefab,
                "PlaceableNetwork", "PlaceNetwork", "WorldPlaceable", "WorldPrefab",
                "PlaceablePrefab", "PlacePrefab", "WorldPlace", "Prefab"))
                return;

            Runner.Spawn(placeablePrefab, position, rotation, Object.InputAuthority);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RequestDrop(Vector3 pos, Vector3 fwd, int fromGlobalIndex, int count, RpcInfo info = default)
        {
            var actor = info.Source;
            if (actor == PlayerRef.None) actor = Object.InputAuthority;
            if (actor != Object.InputAuthority) return;

            if (!Object.HasStateAuthority || Runner == null || _invServer == null || _db == null)
                return;

            var ic = _ic ??= GetComponent<InteractionController>();
            if (ic == null) return;

            PlayerInventoryServer container = null;
            int slotIndex = -1;

            if (fromGlobalIndex >= 0)
            {
                if (!_invServer.TryResolveGlobalIndex(actor, fromGlobalIndex, out container, out slotIndex))
                    return;
            }
            else
            {
                int selected = ic.SelectedQuickIndexNet;
                if (selected < 0) return;
                if (!_invServer.TryResolveGlobalIndex(actor, selected, out container, out slotIndex))
                    return;
            }

            if (container == null || slotIndex < 0 || container.Slots == null || slotIndex >= container.Slots.Length)
                return;

            var slot = container.Slots[slotIndex];
            var itemId = InventorySlotStateAccessor.ReadId(slot);
            if (string.IsNullOrEmpty(itemId))
                return;

            var so = _db.Get(itemId);
            if (so == null)
                return;

            var st = InventorySlotStateAccessor.ReadState(slot);
            int ammo = st?.ammo ?? 0;

            int currentCount = InventorySlotStateAccessor.ReadCount(slot);
            int dropCount = Mathf.Clamp(count > 0 ? count : currentCount, 1, currentCount);

            if (!_invServer.TryRemove(actor, container.Id, slotIndex, dropCount, out var deltas))
                return;

            var router = ResolveServerRouter();
            if (router != null && deltas != null)
            {
                for (int i = 0; i < deltas.Count; i++)
                    router.BroadcastDeltaFromServer(deltas[i]);
            }

            ServerRefreshHandsFromSelectedQuick(actor);

            if (!TryGetNetworkPrefab(so, out var pickablePrefab,
                "PickableNetwork", "PickupNetwork", "WorldDrop", "WorldPrefab",
                "PickablePrefab", "PickupPrefab", "WorldPickable", "Prefab"))
                return;

            var dir = fwd.sqrMagnitude > 0f ? fwd.normalized : Vector3.forward;
            var rot = Quaternion.LookRotation(dir);

            var spawned = Runner.Spawn(
                pickablePrefab, pos, rot, PlayerRef.None,
                onBeforeSpawned: (runner, netObj) =>
                {
                    var pick = netObj.GetComponentInChildren<PickableItem>(true);
                    if (pick != null)
                    {
                        pick.ServerInit(itemId, dropCount, ammo);
                    }
                    else
                    {
                        TryInitWorldItemDeep(netObj.gameObject, itemId, dropCount, ammo);
                    }
                });

            if (spawned != null && spawned.TryGetComponent<Rigidbody>(out var rb))
            {
                float force = ic != null ? ic.ThrowForce : 5f;
                rb.AddForce(dir * force, ForceMode.VelocityChange);
            }
        }

        private void ServerSyncHandsWithQuickDelta(PlayerRef actor, List<ContainerDelta> deltas)
        {
            if (deltas == null || deltas.Count == 0 || Runner == null) return;
            if (!Runner.TryGetPlayerObject(actor, out var playerNO) || playerNO == null) return;

            var ic = playerNO.GetComponentInChildren<InteractionController>(true);
            if (ic == null) return;

            int sel = ic.SelectedQuickIndexNet;
            if (sel < 0) return;

            // Ищем изменение именно выбранного быстрого слота
            for (int i = 0; i < deltas.Count; i++)
            {
                var d = deltas[i];
                if (d == null || d.id.type != ContainerType.PlayerQuick || d.changes == null) continue;

                for (int j = 0; j < d.changes.Length; j++)
                {
                    var c = d.changes[j];
                    if (c.index != sel) continue;

                    var id = InventorySlotStateAccessor.ReadId(c.state);
                    var cnt = InventorySlotStateAccessor.ReadCount(c.state);

                    if (string.IsNullOrEmpty(id) || cnt <= 0)
                        ic.handItemController?.UnEquipItemServer();
                    else
                        ic.handItemController?.EquipItemServer(id);

                    return; // нашли нужный индекс — выходим
                }
            }
        }

        private void TryInitWorldItemDeep(GameObject root, string itemId, int count, int ammo)
        {
            // Если вдруг PickableItem есть где-то в детях — используем его
            var pick = root.GetComponentInChildren<PickableItem>(true);
            if (pick != null)
            {
                if (Runner != null && HasStateAuthority) pick.ServerInit(itemId, count, ammo);
                else pick.Initialize(itemId, count, ammo);
                return;
            }

            // Иначе — проставим совместимые поля любому MonoBehaviour в иерархии
            var comps = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i]; if (c == null) continue;
                var t = c.GetType();
                WriteString(t, c, itemId, "ItemId", "itemId", "Id", "id");
                WriteInt(t, c, count, "Count", "count", "Stack", "stack");
                WriteInt(t, c, ammo, "Ammo", "ammo", "Bullets", "bullets");
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestPick(NetworkObject target, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || _invServer == null || target == null) return;

            var root = target.transform.root != null
                ? target.transform.root.GetComponent<NetworkObject>()
                : target;
            target = root != null ? root : target;

            if (!TryReadWorldItem(target, out var itemId, out var amount, out var ammoFromPickup))
                return;

            if (_invServer.TryAddItemToPlayer(Object.InputAuthority, itemId, amount, ammoFromPickup, out var left, out var deltas, out var reason))
            {
                var extra = ServerPrioritizeSelectedQuick(Object.InputAuthority, itemId);
                var router = ResolveServerRouter();
                if (router != null)
                {
                    if (deltas != null) for (int i = 0; i < deltas.Count; i++) router.BroadcastDeltaFromServer(deltas[i]);
                    if (extra != null) for (int i = 0; i < extra.Count; i++) router.BroadcastDeltaFromServer(extra[i]);
                }

                ServerRefreshHandsFromSelectedQuick(Object.InputAuthority);

                if (left == 0)
                {
                    Runner.Despawn(target);
                }
                else if (left < amount)
                {
                    var pick = target.GetComponentInChildren<PickableItem>(true) ?? target.GetComponentInParent<PickableItem>(true);
                    if (pick != null) pick.SetCount(left);
                    else WriteInt(typeof(MonoBehaviour), target, left, "Count", "count", "Stack", "stack");
                }
                else
                {
                    ServerDropOverflow(itemId, amount, ammoFromPickup);
                    Runner.Despawn(target);
                }
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestPickById(NetworkId targetId, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || _invServer == null) return;
            if (!Runner.TryFindObject(targetId, out var target) || target == null) return;

            if (!TryReadWorldItem(target, out var itemId, out var amount, out var ammoFromPickup))
                return;

            if (_invServer.TryAddItemToPlayer(Object.InputAuthority, itemId, amount, ammoFromPickup, out var left, out var deltas, out var reason))
            {
                var extra = ServerPrioritizeSelectedQuick(Object.InputAuthority, itemId);
                var router = ResolveServerRouter();
                if (router != null)
                {
                    if (deltas != null) for (int i = 0; i < deltas.Count; i++) router.BroadcastDeltaFromServer(deltas[i]);
                    if (extra != null) for (int i = 0; i < extra.Count; i++) router.BroadcastDeltaFromServer(extra[i]);
                }

                ServerRefreshHandsFromSelectedQuick(Object.InputAuthority);

                if (left == 0)
                {
                    Runner.Despawn(target);
                }
                else if (left < amount)
                {
                    var pick = target.GetComponentInChildren<PickableItem>(true) ?? target.GetComponentInParent<PickableItem>(true);
                    if (pick != null) pick.SetCount(left);
                    else WriteInt(typeof(MonoBehaviour), target, left, "Count", "count", "Stack", "stack");
                }
                else
                {
                    ServerDropOverflow(itemId, amount, ammoFromPickup);
                    Runner.Despawn(target);
                }
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestPickAtRay(Vector3 origin, Vector3 dir, float maxDist, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || _invServer == null) return;

            if (!Physics.Raycast(origin, dir, out var hit, maxDist, ~0, QueryTriggerInteraction.Collide))
                return;

            var pick = hit.collider.GetComponentInParent<PickableItem>();
            if (pick == null) return;

            var no = pick.GetComponentInParent<NetworkObject>();
            if (no == null) return;

            if (!TryReadWorldItem(no, out var itemId, out var amount, out var ammo))
                return;

            if (_invServer.TryAddItemToPlayer(Object.InputAuthority, itemId, amount, ammo, out var left, out var deltas, out var reason))
            {
                var extra = ServerPrioritizeSelectedQuick(Object.InputAuthority, itemId);
                var router = ResolveServerRouter();
                if (router != null)
                {
                    if (deltas != null) for (int i = 0; i < deltas.Count; i++) router.BroadcastDeltaFromServer(deltas[i]);
                    if (extra != null) for (int i = 0; i < extra.Count; i++) router.BroadcastDeltaFromServer(extra[i]);
                }

                ServerRefreshHandsFromSelectedQuick(Object.InputAuthority);

                if (left == 0)
                {
                    Runner.Despawn(no);
                }
                else if (left < amount)
                {
                    pick.SetCount(left);
                }
                else
                {
                    ServerDropOverflow(itemId, amount, ammo);
                    Runner.Despawn(no);
                }
            }
        }

        private List<ContainerDelta> ServerPrioritizeSelectedQuick(PlayerRef actor, string itemId)
        {
            if (Runner == null || _invServer == null || string.IsNullOrEmpty(itemId)) return null;
            if (!Runner.TryGetPlayerObject(actor, out var playerNO) || playerNO == null) return null;

            var ic = playerNO.GetComponentInChildren<InteractionController>(true);
            if (ic == null) return null;

            int sel = ic.SelectedQuickIndexNet;
            if (sel < 0) return null;

            if (!_invServer.TryResolveGlobalIndex(actor, sel, out var quick, out var qIdx)) return null;
            if (quick == null || quick.Slots == null || qIdx < 0 || qIdx >= quick.Slots.Length) return null;

            var qState = quick.Slots[qIdx];
            var qId = InventorySlotStateAccessor.ReadId(qState);
            var qCnt = InventorySlotStateAccessor.ReadCount(qState);

            bool canAccept = string.IsNullOrEmpty(qId) || qId == itemId;
            if (!canAccept) return null;

            PlayerInventoryServer main = null;
            TryGetPlayerContainers(actor, out var quickC, out main);

            int srcIdx = -1;
            PlayerInventoryServer src = null;

            if (quickC != null && quickC.Slots != null)
            {
                for (int i = 0; i < quickC.Slots.Length; i++)
                {
                    if (i == qIdx) continue;
                    var s = quickC.Slots[i];
                    if (InventorySlotStateAccessor.ReadId(s) == itemId && InventorySlotStateAccessor.ReadCount(s) > 0)
                    {
                        src = quickC; srcIdx = i; break;
                    }
                }
            }

            if (src == null && main != null && main.Slots != null)
            {
                for (int i = 0; i < main.Slots.Length; i++)
                {
                    var s = main.Slots[i];
                    if (InventorySlotStateAccessor.ReadId(s) == itemId && InventorySlotStateAccessor.ReadCount(s) > 0)
                    {
                        src = main; srcIdx = i; break;
                    }
                }
            }

            if (src == null || srcIdx < 0) return null;

            int amount = InventorySlotStateAccessor.ReadCount(src.Slots[srcIdx]);
            if (amount <= 0) return null;

            if (_invServer.TryTransfer(
                    actor,
                    src.Id, srcIdx,
                    quick.Id, qIdx,
                    amount,
                    out var fromDelta, out var toDelta, out var swapped, out var reason))
            {
                var list = new List<ContainerDelta>(2);
                if (fromDelta != null) list.Add(fromDelta);
                if (toDelta != null) list.Add(toDelta);
                return list.Count > 0 ? list : null;
            }

            return null;
        }

        private void GetDropPoint(out Vector3 pos, out Vector3 fwd)
        {
            Transform t = FindChildRecursive(_ic.transform, "DropPoint");
            if (t != null)
            {
                pos = t.position;
                fwd = t.forward.sqrMagnitude > 0f ? t.forward.normalized : _ic.transform.forward;
                return;
            }
            // запасной вариант: перед игроком
            pos = _ic.transform.position + _ic.transform.forward * 0.5f + Vector3.up * 1.4f;
            fwd = _ic.transform.forward;
        }

        private Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                var r = FindChildRecursive(c, name);
                if (r != null) return r;
            }
            return null;
        }


        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RequestShoot(string itemId, int clientSlotIndex, Vector3 dir, int seed, bool isAuto, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || _invServer == null || Runner == null) return;

            var actor = info.Source;
            if (actor == PlayerRef.None) actor = Object.InputAuthority;
            if (actor != Object.InputAuthority) return;

            var ic = _ic ??= GetComponent<InteractionController>();
            if (ic == null) return;

            int selected = ic.SelectedQuickIndexNet;
            if (selected < 0) selected = Mathf.Max(-1, clientSlotIndex);
            if (selected < 0) return;

            if (!_fireCooldown.ExpiredOrNotRunning(Runner)) return;

            if (!_invServer.TryConsumeAmmoFromQuick(actor, selected, 1, out var newAmmo, out var quickDelta, out var weaponSO, out var reason))
                return;

            float rate = 0f;
            if (weaponSO != null)
                rate = isAuto ? weaponSO.fireRate : (weaponSO.fireRateSingle > 0f ? weaponSO.fireRateSingle : weaponSO.fireRate);
            if (rate <= 0f) rate = 10f;
            _fireCooldown = TickTimer.CreateFromSeconds(Runner, 1f / Mathf.Max(0.01f, rate));

            Vector3 forward = dir.sqrMagnitude > 1e-12f ? dir.normalized : (ic != null ? ic.transform.forward : Vector3.forward);
            Transform muzzle = FindMuzzlePointTransformSafe(ic);
            Vector3 origin = muzzle != null ? muzzle.position : (ic != null ? ic.transform.position + forward * 0.5f : transform.position + forward * 0.5f);

            if (weaponSO != null && weaponSO.bulletPrefab != null)
            {
                var prefabNo = weaponSO.bulletPrefab.GetComponent<NetworkObject>();
                if (prefabNo != null)
                {
                    Runner.Spawn(
                        prefabNo,
                        origin,
                        Quaternion.LookRotation(forward),
                        actor,
                        (runner, obj) =>
                        {
                            var b = obj.GetComponent<Bullet>();
                            if (b != null)
                            {
                                b.Initialize((int)weaponSO.bulletDamage);
                                b.InitializeVelocity(forward * weaponSO.bulletSpeed);
                                b.SetMass(Mathf.Max(0.01f, weaponSO.bulletMass));
                            }
                            if (obj.TryGetComponent<Rigidbody>(out var rb))
                                rb.linearVelocity = forward * weaponSO.bulletSpeed;
                        });
                }
            }
            else
            {
                if (Runner.LagCompensation.Raycast(origin, forward, 1000f, actor, out var hit))
                {
                    var damageable = hit.GameObject != null ? hit.GameObject.GetComponentInParent<IDamageable>() : null;
                    if (damageable != null)
                    {
                        int dmg = weaponSO != null ? (int)weaponSO.bulletDamage : 10;
                        var infoDmg = new DamageInfo(dmg, DamageKind.Bullet, hit.Point, forward, actor);
                        damageable.ApplyDamage(infoDmg);
                    }
                }
            }

            var router = ResolveServerRouter();
            if (router != null && quickDelta != null)
                router.BroadcastDeltaFromServer(quickDelta);

            RPC_SetSlotAmmo(selected, newAmmo);
        }

        private Transform FindMuzzlePointTransformSafe(InteractionController ic)
        {
            if (ic == null) return null;

            var handNetObj = ic.GetHandModelNetworkInstance();
            if (handNetObj != null)
            {
                var mp = handNetObj.GetComponentInChildren<MuzzlePoint>(true);
                if (mp != null) return mp.transform;
            }

            var localMp = ic.GetComponentInChildren<MuzzlePoint>(true);
            return localMp != null ? localMp.transform : null;
        }


        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_SetSlotAmmo(int slotIndex, int ammo, RpcInfo info = default)
        {
            if (_inventory == null) return;
            var qs = _inventory.GetQuickSlots();
            if (qs == null || slotIndex < 0 || slotIndex >= qs.Length) return;

            var s = qs[slotIndex];
            if (s == null) return;
            if (s.State == null) s.State = new ItemState();
            s.State.ammo = ammo;

            _inventory.RaiseQuickSlotsChanged();
        }



        private static bool TryGetNetworkPrefab(ScriptableObject so, out NetworkObject prefab, params string[] candidateNames)
        {
            prefab = null;
            if (so == null) return false;

            // прямое поле ItemSO.Prefab
            if (so is ItemSO iso && iso.Prefab != null)
            {
                prefab = iso.Prefab.GetComponent<NetworkObject>();
                if (prefab != null) return true;
            }

            var t = so.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (var name in candidateNames)
            {
                var f = t.GetField(name, flags);
                if (f != null)
                {
                    var v = f.GetValue(so);
                    if (v is NetworkObject no1) { prefab = no1; return true; }
                    if (v is GameObject go1) { prefab = go1.GetComponent<NetworkObject>(); if (prefab != null) return true; }
                }

                var p = t.GetProperty(name, flags);
                if (p != null && p.CanRead)
                {
                    var v = p.GetValue(so);
                    if (v is NetworkObject no2) { prefab = no2; return true; }
                    if (v is GameObject go2) { prefab = go2.GetComponent<NetworkObject>(); if (prefab != null) return true; }
                }
            }

            return false;
        }

        private void TryInitWorldItem(NetworkObject obj, string itemId, int count, int ammo)
        {
            if (obj == null) return;

            if (obj.TryGetComponent<PickableItem>(out var pick))
            {
                if (Runner != null && HasStateAuthority)
                    pick.ServerInit(itemId, count, ammo);
                else
                    pick.Initialize(itemId, count, ammo);
                return;
            }

            var comps = obj.GetComponents<MonoBehaviour>();
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c == null) continue;
                var t = c.GetType();

                WriteString(t, c, itemId, "ItemId", "itemId", "Id", "id");
                WriteInt(t, c, count, "Count", "count", "Stack", "stack");
                WriteInt(t, c, ammo, "Ammo", "ammo", "Bullets", "bullets");
                return;
            }
        }
        private static void WriteString(Type t, object obj, string value, params string[] names)
        {
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (int i = 0; i < names.Length; i++)
            {
                var f = t.GetField(names[i], BF);
                if (f != null && f.FieldType == typeof(string)) { f.SetValue(obj, value); return; }
                var p = t.GetProperty(names[i], BF);
                if (p != null && p.CanWrite && p.PropertyType == typeof(string)) { p.SetValue(obj, value); return; }
            }
        }

        private static void WriteInt(Type t, object obj, int value, params string[] names)
        {
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (int i = 0; i < names.Length; i++)
            {
                var f = t.GetField(names[i], BF);
                if (f != null && f.FieldType == typeof(int)) { f.SetValue(obj, value); return; }
                var p = t.GetProperty(names[i], BF);
                if (p != null && p.CanWrite && p.PropertyType == typeof(int)) { p.SetValue(obj, value); return; }
            }
        }

        private bool TryReadWorldItem(NetworkObject obj, out string itemId, out int count, out int ammo)
        {
            itemId = null; count = 0; ammo = 0;
            if (obj == null) return false;

            // Ищем в обе стороны: вверх и вниз
            var pick = obj.GetComponentInParent<PickableItem>()
                       ?? obj.GetComponentInChildren<PickableItem>(true);
            if (pick != null)
            {
                itemId = pick.GetItemId();
                count = pick.GetCount();
                ammo = pick.GetAmmo();
                return !string.IsNullOrEmpty(itemId) && count > 0;
            }

            // Глубокий фолбэк: сначала вниз (дети), потом вверх (родители)
            foreach (var c in obj.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (c == null) continue;
                var tt = c.GetType();
                itemId = ReadString(tt, c, "ItemId", "itemId", "Id", "id");
                count = ReadInt(tt, c, "Count", "count", "Stack", "stack");
                ammo = ReadInt(tt, c, "Ammo", "ammo", "Bullets", "bullets");
                if (!string.IsNullOrEmpty(itemId) && count > 0) return true;
            }

            var t = obj.transform.parent;
            while (t != null)
            {
                var comps = t.GetComponents<MonoBehaviour>();
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    var tt = c.GetType();
                    itemId = ReadString(tt, c, "ItemId", "itemId", "Id", "id");
                    count = ReadInt(tt, c, "Count", "count", "Stack", "stack");
                    ammo = ReadInt(tt, c, "Ammo", "ammo", "Bullets", "bullets");
                    if (!string.IsNullOrEmpty(itemId) && count > 0) return true;
                }
                t = t.parent;
            }

            return false;
        }

        private static string ReadString(Type t, object instance, params string[] names)
        {
            foreach (var n in names)
            {
                var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(string)) return (string)f.GetValue(instance);

                var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.CanRead && p.PropertyType == typeof(string)) return (string)p.GetValue(instance);
            }
            return null;
        }

        private static int ReadInt(Type t, object instance, params string[] names)
        {
            foreach (var n in names)
            {
                var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(instance);

                var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.CanRead && p.PropertyType == typeof(int)) return (int)p.GetValue(instance);
            }
            return 0;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestDamageSelf(int amount)
        {
            var hp = GetComponent<PlayerHealthServer>();
            if (hp != null) hp.ApplyDamage(Mathf.Max(0, amount));
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RequestEquipQuickSlot(int quickIndex, RpcInfo info = default)
        {
            var actor = info.Source;
            if (actor == PlayerRef.None) actor = Object.InputAuthority;
            if (actor != Object.InputAuthority) return;

            var ic = GetComponent<InteractionController>();
            if (ic == null || _invServer == null) return;

            int current = ic.SelectedQuickIndexNet;
            if (quickIndex >= 0 && current == quickIndex)
            {
                ic.ServerSetSelectedQuickIndex(-1);
                ic.handItemController?.UnEquipItemServer();
                return;
            }

            ic.ServerSetSelectedQuickIndex(Mathf.Max(-1, quickIndex));

            string itemId = null;

            if (quickIndex >= 0 && _invServer.TryResolveGlobalIndex(actor, quickIndex, out var container, out int slotIndex))
            {
                var slots = container.Slots;
                if (slots != null && slotIndex >= 0 && slotIndex < slots.Length)
                {
                    var s = slots[slotIndex];
                    itemId = InventorySlotStateAccessor.ReadId(s);
                }
            }

            if (string.IsNullOrEmpty(itemId))
                ic.handItemController?.UnEquipItemServer();
            else
                ic.handItemController?.EquipItemServer(itemId);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RefreshSelectedQuick(RpcInfo info = default)
        {
            var actor = info.Source;
            if (actor == PlayerRef.None) actor = Object.InputAuthority;
            if (actor != Object.InputAuthority) return;

            var ic = GetComponent<InteractionController>();
            if (ic == null || _invServer == null) return;

            int selected = ic.SelectedQuickIndexNet;

            string itemId = null;

            if (selected >= 0 && _invServer.TryResolveGlobalIndex(actor, selected, out var container, out int slotIndex))
            {
                var slots = container.Slots;
                if (slots != null && slotIndex >= 0 && slotIndex < slots.Length)
                {
                    var s = slots[slotIndex];
                    itemId = InventorySlotStateAccessor.ReadId(s);
                }
            }

            if (string.IsNullOrEmpty(itemId))
                ic.handItemController?.UnEquipItemServer();
            else
                ic.handItemController?.EquipItemServer(itemId);
        }


        public bool TryGetPlayerContainers(PlayerRef player, out PlayerInventoryServer quick, out PlayerInventoryServer main)
        {
            quick = null; main = null;
            var runner = Runner != null ? Runner : FindObjectOfType<NetworkRunner>();
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

        public void ServerRefreshHandsFromSelectedQuick(PlayerRef actor)
        {
            if (Runner == null) return;
            if (!Runner.TryGetPlayerObject(actor, out var playerNO) || playerNO == null) return;

            var ic = playerNO.GetComponentInChildren<InteractionController>(true);
            if (ic == null) return;

            int sel = ic.SelectedQuickIndexNet;
            if (sel < 0)
            {
                ic.handItemController?.UnEquipItemServer();
                return;
            }

            if (!_invServer.TryResolveGlobalIndex(actor, sel, out var container, out int slotIndex))
            {
                ic.handItemController?.UnEquipItemServer();
                return;
            }

            var slots = container.Slots;
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
            {
                ic.handItemController?.UnEquipItemServer();
                return;
            }

            var id = InventorySlotStateAccessor.ReadId(slots[slotIndex]);
            var cnt = InventorySlotStateAccessor.ReadCount(slots[slotIndex]);

            if (string.IsNullOrEmpty(id) || cnt <= 0)
                ic.handItemController?.UnEquipItemServer();
            else
                ic.handItemController?.EquipItemServer(id);
        }



        public void ServerDropOverflow(string itemId, int count, int ammo)
        {
            if (!Object.HasStateAuthority || Runner == null || _db == null) return;

            if (!TryGetNetworkPrefab(_db.Get(itemId), out var pickablePrefab,
                "PickableNetwork", "PickupNetwork", "WorldDrop", "WorldPrefab",
                "PickablePrefab", "PickupPrefab", "WorldPickable", "Prefab"))
                return;

            var pos = _ic != null ? _ic.GetDropPointPosition() : transform.position + transform.forward;
            var fwd = _ic != null ? _ic.GetDropForward() : transform.forward;
            var dir = fwd.sqrMagnitude > 0f ? fwd.normalized : Vector3.forward;
            var rot = Quaternion.LookRotation(dir);

            var spawned = Runner.Spawn(
                pickablePrefab, pos, rot, PlayerRef.None,
                onBeforeSpawned: (runner, netObj) =>
                {
                    var pick = netObj.GetComponentInChildren<PickableItem>(true);
                    if (pick != null)
                    {
                        pick.ServerInit(itemId, count, ammo);
                    }
                    else
                    {
                        TryInitWorldItemDeep(netObj.gameObject, itemId, count, ammo);
                    }
                });

            if (spawned != null && spawned.TryGetComponent<Rigidbody>(out var rb))
            {
                float force = _ic != null ? _ic.ThrowForce : 5f;
                rb.AddForce(dir * force, ForceMode.VelocityChange);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RequestDropFromContainer(Vector3 pos, Vector3 fwd, int slotIndex, int count, byte type, PlayerRef ownerRef, NetworkId objectId, RpcInfo info = default)
        {
            var actor = info.Source;
            if (actor == PlayerRef.None) actor = Object.InputAuthority;
            if (actor != Object.InputAuthority) return;

            if (!Object.HasStateAuthority || Runner == null || _invServer == null || _db == null) return;

            var ic = _ic ??= GetComponent<InteractionController>();
            if (ic == null) return;

            var cid = new ContainerId { type = (ContainerType)type, ownerRef = ownerRef, objectId = objectId };

            IInventoryContainer container = null;
            if (!_invServer.TryResolveContainerAny(cid, out container)) return;
            if (!container.CanPlayerAccess(actor)) return;

            var slots = container.Slots;
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;

            var slot = slots[slotIndex];
            var itemId = InventorySlotStateAccessor.ReadId(slot);
            if (string.IsNullOrEmpty(itemId)) return;

            var st = InventorySlotStateAccessor.ReadState(slot);
            int ammo = st?.ammo ?? 0;

            int currentCount = Mathf.Max(1, InventorySlotStateAccessor.ReadCount(slot));
            int dropCount = Mathf.Clamp(count > 0 ? count : currentCount, 1, currentCount);

            if (!_invServer.TryRemove(actor, cid, slotIndex, dropCount, out var deltas)) return;

            var router = ResolveServerRouter();
            if (router != null && deltas != null)
            {
                for (int i = 0; i < deltas.Count; i++)
                    router.BroadcastDeltaFromServer(deltas[i]);
            }

            var so = _db.Get(itemId);
            if (so == null) return;

            if (!TryGetNetworkPrefab(so, out var pickablePrefab,
                "PickableNetwork", "PickupNetwork", "WorldDrop", "WorldPrefab",
                "PickablePrefab", "PickupPrefab", "WorldPickable", "Prefab"))
                return;

            var dir = fwd.sqrMagnitude > 0f ? fwd.normalized : Vector3.forward;
            var rot = Quaternion.LookRotation(dir);

            var spawned = Runner.Spawn(
                pickablePrefab, pos, rot, PlayerRef.None,
                onBeforeSpawned: (runner, netObj) =>
                {
                    var pick = netObj.GetComponentInChildren<PickableItem>(true);
                    if (pick != null)
                        pick.ServerInit(itemId, dropCount, ammo);
                    else
                        TryInitWorldItemDeep(netObj.gameObject, itemId, dropCount, ammo);
                });

            if (spawned != null && spawned.TryGetComponent<Rigidbody>(out var rb))
            {
                float force = ic != null ? ic.ThrowForce : 5f;
                rb.AddForce(dir * force, ForceMode.VelocityChange);
            }
        }

    }
}