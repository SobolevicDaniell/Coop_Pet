using System;
using System.Reflection;
using Fusion;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerRpcHandler : NetworkBehaviour
    {
        [SerializeField] private NetworkObject defaultBulletPrefab;

        private InteractionController _ic;
        private ItemDatabaseSO _db;
        private InventoryService _inventory;

        // ─── DI ────────────────────────────────────────────────────────────────────────
        public void Construct(ItemDatabaseSO db, InteractionController ic, InventoryService inventory)
        {
            _db        = db;
            _ic        = ic;
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

        // ─── Перезарядка ───────────────────────────────────────────────────────────────
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestReload(RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || _ic == null) return;
            if (_ic.currentBehavior is WeaponBehavior wb && wb.IsValid())
                wb.ServerReload();
        }

        // ─── Плейсмент мира ───────────────────────────────────────────────────────────
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestPlaceObject(string itemId, Vector3 position, Quaternion rotation, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || Runner == null || _db == null) return;
            var so = _db.Get(itemId);
            if (so == null) return;

            if (!TryGetNetworkPrefab(so, out var placeablePrefab,
                "PlaceableNetwork","PlaceNetwork","WorldPlaceable","WorldPrefab",
                "PlaceablePrefab","PlacePrefab","WorldPlace","Prefab"))
                return;

            Runner.Spawn(placeablePrefab, position, rotation, Object.InputAuthority);
        }

        // ─── Дроп предмета ────────────────────────────────────────────────────────────
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestDrop(Vector3 position, Vector3 forward, string itemId, int count, int ammo, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || Runner == null || _db == null) return;

            var so = _db.Get(itemId);
            if (so == null) return;

            if (!TryGetNetworkPrefab(so, out var pickablePrefab,
                "PickableNetwork","PickupNetwork","WorldDrop","WorldPrefab",
                "PickablePrefab","PickupPrefab","WorldPickable","Prefab"))
                return;

            var dir = forward.sqrMagnitude > 0f ? forward.normalized : Vector3.forward;
            var rot = Quaternion.LookRotation(dir);

            var spawned = Runner.Spawn(
                pickablePrefab,
                position,
                rot,
                Object.InputAuthority,
                onBeforeSpawned: (runner, netObj) =>
                {
                    // init payload
                    TryInitWorldItem(netObj, itemId, count, ammo);
                });

            // толчок вперёд (если есть Rigidbody)
            if (spawned != null && spawned.TryGetComponent<Rigidbody>(out var rb))
            {
                float force = _ic != null ? _ic.ThrowForce : 5f;
                rb.AddForce(dir * force, ForceMode.VelocityChange);
            }
        }

        // ─── Подбор предмета ──────────────────────────────────────────────────────────
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestPick(NetworkObject worldObject, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || Runner == null) return;

            var pickable = worldObject ? worldObject.GetComponent<PickableItem>() : null;
            if (pickable == null) return;
            if (!pickable.TryConsumeServer()) return;

            if (!TryReadWorldItem(worldObject, out var itemId, out var count, out var ammo))
                return;

            Runner.Despawn(worldObject);
            RPC_ConfirmPicked(itemId, count, ammo);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_ConfirmPicked(string itemId, int count, int ammo, RpcInfo info = default)
        {
            if (_inventory == null) return;

            var ok = _inventory.TryAddItem(itemId, count);
            if (!ok) return;

            // ↓ добавлено: переносим боезапас в слот, если есть
            if (ammo > 0)
                ApplyAmmoToInventory(_inventory, itemId, ammo);

            _inventory.RaiseInventoryChanged();
            _inventory.RaiseQuickSlotsChanged();
        }

        // ─── Стрельба ────────────────────────────────────────────────────────────────
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RequestShoot(string itemId, int slotIndex, Vector3 muzzlePos, Vector3 dir, int seed)
        {
            if (!Object.HasStateAuthority) return;
            if (_inventory == null || _db == null || Runner == null) return;

            var qs = _inventory.GetQuickSlots();
            if (qs == null || slotIndex < 0 || slotIndex >= qs.Length) return;

            var wSlot = qs[slotIndex];
            if (wSlot == null || string.IsNullOrEmpty(wSlot.Id)) return;

            if (wSlot.State == null) wSlot.State = new ItemState();
            if (wSlot.State.ammo <= 0) return;

            var so = _db.Get(wSlot.Id) as WeaponSO;
            if (so == null) return;

            var prefab = (so.bulletPrefab ? so.bulletPrefab.GetComponent<NetworkObject>() : null) ?? defaultBulletPrefab;
            if (prefab == null) return; // префаб невалиден — отмена

            float damage = so.bulletDamage;
            float speed  = so.bulletSpeed;

            Runner.Spawn(
                prefab,
                muzzlePos,
                Quaternion.LookRotation(dir),
                Object.InputAuthority,
                (runner, netObj) =>
                {
                    var b = netObj.GetComponent<Bullet>();
                    if (b != null)
                    {
                        b.Initialize((int)damage);
                        b.InitializeVelocity(dir.normalized * speed);
                    }
                });

            wSlot.State.ammo -= 1;
            _inventory.RaiseQuickSlotsChanged();

            // ↓ добавлено: точная синхронизация боезапаса владельцу
            RPC_SetSlotAmmo(slotIndex, wSlot.State.ammo);
        }

        // ↓ добавлено: однонаправленная синхронизация боезапаса после выстрела
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

        // ─── Helpers ─────────────────────────────────────────────────────────────────
        private static void ApplyAmmoToInventory(InventoryService inv, string itemId, int ammo)
        {
            if (inv == null || string.IsNullOrEmpty(itemId) || ammo <= 0) return;

            var qs = inv.GetQuickSlots();
            if (qs != null)
            {
                for (int i = 0; i < qs.Length; i++)
                {
                    var s = qs[i];
                    if (s != null && s.Id == itemId)
                    {
                        if (s.State == null) s.State = new ItemState();
                        s.State.ammo = ammo;
                        return;
                    }
                }
            }

            var arr = inv.GetInventorySlots();
            if (arr != null)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    var s = arr[i];
                    if (s != null && s.Id == itemId)
                    {
                        if (s.State == null) s.State = new ItemState();
                        s.State.ammo = ammo;
                        return;
                    }
                }
            }
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
                    if (v is GameObject go1)   { prefab = go1.GetComponent<NetworkObject>(); if (prefab != null) return true; }
                }

                var p = t.GetProperty(name, flags);
                if (p != null && p.CanRead)
                {
                    var v = p.GetValue(so);
                    if (v is NetworkObject no2) { prefab = no2; return true; }
                    if (v is GameObject go2)    { prefab = go2.GetComponent<NetworkObject>(); if (prefab != null) return true; }
                }
            }

            return false;
        }

        private static void TryInitWorldItem(NetworkObject spawned, string itemId, int count, int ammo)
        {
            if (spawned == null) return;

            var pickable = spawned.GetComponent<PickableItem>();
            if (pickable != null)
            {
                pickable.ServerInit(itemId, count, ammo);
                return;
            }

            var go = spawned.gameObject;
            var comps = go.GetComponents<MonoBehaviour>();
            foreach (var c in comps)
            {
                if (c == null) continue;
                var t = c.GetType();
                if (TrySetField(t, c, "ItemId", itemId)) { }
                if (TrySetField(t, c, "Count",  count))  { }
                if (TrySetField(t, c, "Ammo",   ammo))   { }
            }
        }

        private bool TryReadWorldItem(NetworkObject obj, out string itemId, out int count, out int ammo)
        {
            itemId = null; count = 0; ammo = 0;
            if (obj == null) return false;

            var pickable = obj.GetComponent<PickableItem>();
            if (pickable != null)
            {
                itemId = pickable.GetItemId();
                count  = pickable.GetCount();
                ammo   = pickable.GetAmmo();
                return !string.IsNullOrEmpty(itemId) && count > 0;
            }

            var comps = obj.GetComponents<MonoBehaviour>();
            foreach (var c in comps)
            {
                if (c == null) continue;
                var t = c.GetType();
                itemId = ReadString(t, c, "ItemId","itemId","Id","id");
                count  = ReadInt(   t, c, "Count","count","Stack","stack");
                ammo   = ReadInt(   t, c, "Ammo","ammo","Bullets","bullets");
                if (!string.IsNullOrEmpty(itemId) && count > 0)
                    return true;
            }

            return false;
        }

        private static bool TrySetField(Type t, object instance, string fieldName, object value)
        {
            var f = t.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null && (value == null || f.FieldType.IsInstanceOfType(value)))
            {
                try { f.SetValue(instance, value); return true; } catch { }
            }

            var p = t.GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanWrite)
            {
                try { p.SetValue(instance, value); return true; } catch { }
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
    }
}
