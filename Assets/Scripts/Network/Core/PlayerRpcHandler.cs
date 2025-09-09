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
        // [SerializeField] private NetworkObject defaultBulletPrefab;

        private InteractionController _ic;
        private ItemDatabaseSO _db;
        private InventoryService _inventory;

        [Inject(Optional = true)] private InventoryServerService _invServer;
        [Inject(Optional = true)] private InventoryRpcRouter _invRouter;

        [Networked] private TickTimer _fireCooldown { get; set; }

        public override void Spawned()
        {
            _invRouter ??= GetComponent<InventoryRpcRouter>();
        }

        // ─── DI ────────────────────────────────────────────────────────────────────────
        public void Construct(ItemDatabaseSO db, InteractionController ic, InventoryService inventory)
        {
            _db = db;
            _ic = ic;
            _inventory = inventory;
        }

        // ─── Экип / снятие предмета в руке ────────────────────────────────────────────
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_EquipItem(string itemId, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || _ic == null) return;
            if (string.IsNullOrEmpty(itemId)) { RPC_UnEquipItem(); return; }
            _ic.handItemController?.EquipItemServer(itemId);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_UnEquipItem(RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || _ic == null) return;
            _ic.handItemController?.UnEquipItemServer();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestReload(int slotIndex, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || _ic == null || _invServer == null) return;

            if (!TryGetPlayerContainers(Object.InputAuthority, out var quick, out var main))
                return;

            var slots = quick?.Slots;
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
                return;

            var sid = InventorySlotStateAccessor.ReadId(slots[slotIndex]);
            if (string.IsNullOrEmpty(sid))
                return;

            // Перезаряжаем ИМЕННО ЭТОТ слот
            if (_invServer.TryReloadWeapon(Object.InputAuthority, slotIndex,
                                           out var newAmmo,
                                           out var deltas,
                                           out var reason))
            {
                if (_invRouter != null && deltas != null)
                {
                    foreach (var d in deltas)
                        _invRouter.BroadcastDeltaFromServer(d);
                }
                RPC_SetSlotAmmo(slotIndex, newAmmo);
            }
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[RELOAD] denied: {reason}");
#endif
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

        // В PlayerRpcHandler.cs
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RequestDrop(Vector3 pos, Vector3 fwd, int fromGlobalIndex, int count, RpcInfo info = default)
        {
            // Нормализуем актёра (важно для Host)
            var actor = info.Source;
            if (actor == PlayerRef.None)
                actor = Object.InputAuthority;

            // Доп. страховка: RPC должно приходить от своего владельца
            if (actor != Object.InputAuthority)
                return;

            if (!Object.HasStateAuthority || Runner == null || _invServer == null || _db == null)
                return;

            // Разрешаем глобальный индекс в контейнер и локальный индекс
            if (!_invServer.TryResolveGlobalIndex(actor, fromGlobalIndex, out var container, out int slotIndex))
                return;

            var slot = container.Slots[slotIndex];
            var itemId = InventorySlotStateAccessor.ReadId(slot);
            if (string.IsNullOrEmpty(itemId))
                return;

            var so = _db.Get(itemId);
            if (so == null)
                return;

            // Снимем состояние ДО удаления (для ammo и т.п.)
            var st = InventorySlotStateAccessor.ReadState(slot);
            int ammo = st?.ammo ?? 0;

            int currentCount = InventorySlotStateAccessor.ReadCount(slot);
            int dropCount = Mathf.Clamp(count, 1, currentCount);

            // Удаляем из инвентаря на сервере
            if (!_invServer.TryRemove(actor, container.Id, slotIndex, dropCount, out var deltas))
                return;

            // Рассылаем дельты наблюдателям
            if (_invRouter != null && deltas != null)
            {
                foreach (var d in deltas)
                    _invRouter.BroadcastDeltaFromServer(d);
            }

            // Спавним предмет в мире
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
                        // 2) Фолбэк: проставим поля по всей иерархии (дети тоже)
                        TryInitWorldItemDeep(netObj.gameObject, itemId, dropCount, ammo);
                    }
                });

            if (spawned != null && spawned.TryGetComponent<Rigidbody>(out var rb))
            {
                float force = _ic != null ? _ic.ThrowForce : 5f;
                rb.AddForce(dir * force, ForceMode.VelocityChange);
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

            // safety: работаем с корневым NO
            var root = target.transform.root != null
                ? target.transform.root.GetComponent<NetworkObject>()
                : target;
            target = root != null ? root : target;

            if (!TryReadWorldItem(target, out var itemId, out var amount, out var ammoFromPickup))
                return;

            if (_invServer.TryAddItemToPlayer(
                    Object.InputAuthority, itemId, amount, ammoFromPickup,
                    out var left, out var deltas, out var reason))
            {
                if (deltas != null && _invRouter != null)
                    for (int i = 0; i < deltas.Count; i++)
                        _invRouter.BroadcastDeltaFromServer(deltas[i]);

                Runner.Despawn(target);
            }
        }
        // using Fusion;  // уже есть

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestPickById(NetworkId targetId, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || _invServer == null) return;
            if (!Runner.TryFindObject(targetId, out var target) || target == null) return;

            if (!TryReadWorldItem(target, out var itemId, out var amount, out var ammoFromPickup))
                return;

            if (_invServer.TryAddItemToPlayer(
                    Object.InputAuthority,
                    itemId,
                    amount,
                    ammoFromPickup,
                    out var left,
                    out var deltas,
                    out var reason))
            {
                if (deltas != null && _invRouter != null)
                {
                    for (int i = 0; i < deltas.Count; i++)
                        _invRouter.BroadcastDeltaFromServer(deltas[i]);
                }
                Runner.Despawn(target);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                Debug.Log($"[PICK] denied: {reason}");
            }
#endif
        }
        
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestPickAtRay(Vector3 origin, Vector3 dir, float maxDist, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || _invServer == null) return;

            // Серверный рейкаст в authoritative-физике
            if (!Physics.Raycast(origin, dir, out var hit, maxDist, ~0, QueryTriggerInteraction.Collide))
                return;

            // Берём то, что реально под прицелом
            var pick = hit.collider.GetComponentInParent<PickableItem>();
            if (pick == null) return;

            var no = pick.GetComponentInParent<NetworkObject>();
            if (no == null) return;

            if (!TryReadWorldItem(no, out var itemId, out var amount, out var ammo))
                return;

            if (_invServer.TryAddItemToPlayer(
                    Object.InputAuthority, itemId, amount, ammo,
                    out var left, out var deltas, out var reason))
            {
                if (deltas != null && _invRouter != null)
                    for (int i = 0; i < deltas.Count; i++)
                        _invRouter.BroadcastDeltaFromServer(deltas[i]);

                Runner.Despawn(no);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                Debug.Log($"[PICK] denied: {reason}");
            }
#endif
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
        public void RPC_RequestShoot(string itemId, int clientSlotIndex, Vector3 dir, int seed, bool isAuto)
        {
            if (!Object.HasStateAuthority || _invServer == null || Runner == null)
                return;

            if (!TryGetPlayerContainers(Object.InputAuthority, out var quick, out _))
                return;

            var slots = quick?.Slots;
            if (slots == null || clientSlotIndex < 0 || clientSlotIndex >= slots.Length)
                return;

            // Валидация содержимого именно того слота, который указан клиентом
            var sid = InventorySlotStateAccessor.ReadId(slots[clientSlotIndex]);
            if (string.IsNullOrEmpty(sid))
                return;

            // Если клиент прислал itemId — убеждаемся, что в этом слоте именно тот же предмет
            if (!string.IsNullOrEmpty(itemId) && sid != itemId)
                return;

            // КД
            if (!_fireCooldown.ExpiredOrNotRunning(Runner))
                return;

            // Списание 1 патрона из ЭТОГО же clientSlotIndex
            if (!_invServer.TryConsumeAmmoFromQuick(
                    Object.InputAuthority,
                    clientSlotIndex,
                    1,
                    out var newAmmo,
                    out var quickDelta,
                    out var weaponSO,
                    out var reason))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[SHOOT] denied: {reason}");
#endif
                return;
            }

            float rate = 0f;
            if (weaponSO != null)
                rate = isAuto ? weaponSO.fireRate : (weaponSO.fireRateSingle > 0f ? weaponSO.fireRateSingle : weaponSO.fireRate);
            if (rate <= 0f) rate = 10f;
            _fireCooldown = TickTimer.CreateFromSeconds(Runner, 1f / Mathf.Max(0.01f, rate));

            Vector3 forward = dir.sqrMagnitude > 1e-12f ? dir.normalized : Vector3.forward;
            Transform muzzle = FindMuzzlePointTransform();
            Vector3 muzzlePos = muzzle != null ? muzzle.position : _ic.transform.position + forward * 0.5f;

            if (weaponSO != null && weaponSO.bulletPrefab != null)
            {
                var prefabNo = weaponSO.bulletPrefab.GetComponent<NetworkObject>();
                if (prefabNo != null)
                {
                    Runner.Spawn(
                        prefabNo,
                        muzzlePos,
                        Quaternion.LookRotation(forward),
                        Object.InputAuthority,
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
                if (Runner.LagCompensation.Raycast(muzzlePos, forward, 1000f, Object.InputAuthority, out var hit))
                {
                    var damageable = hit.GameObject != null ? hit.GameObject.GetComponentInParent<IDamageable>() : null;
                    if (damageable != null)
                    {
                        int dmg = weaponSO != null ? (int)weaponSO.bulletDamage : 10;
                        var info = new DamageInfo(dmg, DamageKind.Bullet, hit.Point, forward, Object.InputAuthority);
                        damageable.ApplyDamage(info);
                    }
                }
            }

            if (_invRouter != null && quickDelta != null)
                _invRouter.BroadcastDeltaFromServer(quickDelta);

            RPC_SetSlotAmmo(clientSlotIndex, newAmmo);
        }
        private Transform FindMuzzlePointTransform()
        {
            if (_ic == null) return null;

            // 1) Есть ли сетевая ручная модель? (InteractionController уже хранит/ищет её)
            var handNetObj = _ic.GetHandModelNetworkInstance();
            if (handNetObj != null)
            {
                var mp = handNetObj.GetComponentInChildren<MuzzlePoint>(true);
                if (mp != null) return mp.transform;
            }

            // 2) Можно поискать и на локальной иерархии контроллера
            var localMp = _ic.GetComponentInChildren<MuzzlePoint>(true);
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

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestEquipQuickSlot(int quickIndex, RpcInfo info = default)
        {
            if (Object.InputAuthority != info.Source) return;

            var ic = GetComponent<InteractionController>();
            if (ic == null || _invServer == null) return;

            ic.ServerSetSelectedQuickIndex(Mathf.Max(-1, quickIndex));

            string itemId = null;

            if (quickIndex >= 0 && TryGetPlayerContainers(Object.InputAuthority, out var quick, out _))
            {
                var slots = quick.Slots;
                if (slots != null && quickIndex < slots.Length)
                {
                    var s = slots[quickIndex];
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
      


    }
}