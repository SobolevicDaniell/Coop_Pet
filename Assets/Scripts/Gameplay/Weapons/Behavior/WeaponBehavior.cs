using System;
using System.Reflection;
using Fusion;
using UnityEngine;

namespace Game
{
    public sealed class WeaponBehavior : IHandItemBehavior
    {
        private InteractionController _ic;
        private PlayerRpcHandler _rpc;
        private ItemDatabaseSO _db;

        private string _itemId;
        private int _quickSlotIndex;
        private bool _isAutomatic = true;
        private float _fireRate = 8f;
        private float _spreadDeg = 0f;

        private bool _triggerHeld;
        private Transform _cachedMuzzle;
        private int _randSeed;

        private float _shotInterval;
        private float _nextShotAt;

        public void Construct(InteractionController ic, PlayerRpcHandler rpc, ItemDatabaseSO db, string itemId, int quickSlotIndex)
        {
            _ic = ic;
            _rpc = rpc;
            _db = db;
            _itemId = itemId;
            _quickSlotIndex = quickSlotIndex;

            _triggerHeld = false;
            _randSeed = Environment.TickCount;

            TryReadWeaponParamsFromSO();
            _shotInterval = _fireRate > 0f ? 1f / _fireRate : 0f;
            _cachedMuzzle = null;
            _nextShotAt = 0f;
        }

        public void OnEquip()
        {
            _cachedMuzzle = ResolveMuzzle();
            _nextShotAt = 0f;
            if (_ic != null && _ic.inventory != null) _quickSlotIndex = _ic.inventory.SelectedQuickSlot; // обновляем индекс слота
        }

        public void OnUnequip()
        {
            _triggerHeld = false;
            _cachedMuzzle = null;
            _nextShotAt = 0f;
        }

        public void OnUsePressed()
        {
            _triggerHeld = true;
            if (_isAutomatic)
            {
                if (Time.time + 0.0001f >= _nextShotAt) FireOnceAndSchedule();
            }
            else
            {
                FireOnceAndSchedule();
            }
        }

        public void OnUseHeld(float dt)
        {
            if (!_isAutomatic || !_triggerHeld) return;
            var now = Time.time;
            int safety = 4;
            while (_shotInterval > 0f && now + 0.0001f >= _nextShotAt && safety-- > 0)
                FireOnceAndSchedule();
        }

        public void OnUseReleased()
        {
            _triggerHeld = false;
        }

        public bool IsValid()
        {
            if (_ic == null || _rpc == null || string.IsNullOrEmpty(_itemId)) return false;
            if (_cachedMuzzle == null) _cachedMuzzle = ResolveMuzzle();
            return _cachedMuzzle != null || _ic.handPoint != null;
        }

        public void ServerReload()
        {
            if (_ic == null) return;
            var inv = _ic.inventory;
            if (inv == null) return;

            int slotIdx = _quickSlotIndex >= 0 ? _quickSlotIndex : inv.SelectedQuickSlot;
            var qs = inv.GetQuickSlots();
            if (qs == null || slotIdx < 0 || slotIdx >= qs.Length) return;

            var wSlot = qs[slotIdx];
            if (wSlot == null || string.IsNullOrEmpty(wSlot.Id)) return;
            if (wSlot.State == null) wSlot.State = new ItemState();

            int magSize = TryReadIntFromSO("MagazineSize", "MagSize", "ClipSize", "MaxAmmo", "maxAmmo") ?? 30;
            string ammoId = TryReadStringFromSO("AmmoId", "AmmoItemId", "AmmoType", "Ammo", "ammoId", "ammo");

            if (string.IsNullOrEmpty(ammoId))
            {
                var so = _db != null ? _db.Get(_itemId) : null;
                if (so != null)
                {
                    var t = so.GetType();
                    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                    object obj = null;
                    var f = t.GetField("ammoResource", flags) ?? t.GetField("AmmoResource", flags) ?? t.GetField("AmmoSO", flags) ?? t.GetField("AmmoSo", flags);
                    if (f != null) obj = f.GetValue(so);
                    if (obj == null)
                    {
                        var p = t.GetProperty("ammoResource", flags) ?? t.GetProperty("AmmoResource", flags) ?? t.GetProperty("AmmoSO", flags) ?? t.GetProperty("AmmoSo", flags);
                        if (p != null && p.CanRead) obj = p.GetValue(so);
                    }

                    if (obj is ItemSO iso && !string.IsNullOrEmpty(iso.Id))
                        ammoId = iso.Id;
                }
            }

            if (string.IsNullOrEmpty(ammoId)) return;

            int need = Mathf.Max(0, magSize - wSlot.State.Ammo);
            if (need <= 0) return;

            int available = inv.GetResourceCount(ammoId);
            if (available <= 0) return;

            int take = Mathf.Min(need, available);
            if (take <= 0) return;

            inv.SpendResource(ammoId, take);
            wSlot.State.Ammo += take;

            inv.RaiseQuickSlotsChanged();
        }

        private void FireOnceAndSchedule()
        {
            if (!IsValid() || _shotInterval <= 0f) return;

            var muzzle = _cachedMuzzle != null ? _cachedMuzzle : ResolveMuzzle();
            if (muzzle == null) muzzle = _ic.handPoint;

            Vector3 dir = muzzle.forward;
            if (_ic.camera != null)
            {
                var ray = _ic.camera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));
                dir = (ray.origin + ray.direction * 1000f - muzzle.position).normalized;
            }
            dir = ApplySpread(dir, _spreadDeg, ref _randSeed);

            int slotIndexToSend = (_ic != null && _ic.inventory != null) ? _ic.inventory.SelectedQuickSlot : _quickSlotIndex; // берём актуальный индекс
            _rpc.RPC_RequestShoot(_itemId, slotIndexToSend, muzzle.position, dir, _randSeed); // шлём текущий слот
            _randSeed++;

            _nextShotAt = Time.time + _shotInterval;
        }
        private Transform ResolveMuzzle()
        {
            var netHand = _ic.GetHandModelNetworkInstance();
            if (netHand != null)
            {
                var t = FindChildRecursiveByName(netHand.transform, "MuzzlePoint");
                if (t != null) return t;
            }
            return _ic.handPoint;
        }

        private static Transform FindChildRecursiveByName(Transform root, string nameNoCase)
        {
            if (root == null) return null;
            var target = nameNoCase.ToLowerInvariant();
            return DFS(root, target);
            Transform DFS(Transform tr, string targetLower)
            {
                if (tr.name.ToLowerInvariant() == targetLower) return tr;
                for (int i = 0; i < tr.childCount; i++)
                {
                    var got = DFS(tr.GetChild(i), targetLower);
                    if (got != null) return got;
                }
                return null;
            }
        }

        private static Vector3 ApplySpread(Vector3 dir, float spreadDegrees, ref int seed)
        {
            if (spreadDegrees <= 0f) return dir.normalized;
            var rng = new System.Random(seed);
            seed = rng.Next();
            float yaw = (float)rng.NextDouble() * 360f;
            float pitch = ((float)rng.NextDouble() * 2f - 1f) * spreadDegrees;
            dir.Normalize();
            Vector3 right = Vector3.Cross(dir, Vector3.up);
            if (right.sqrMagnitude < 1e-6f) right = Vector3.Cross(dir, Vector3.forward);
            right.Normalize();
            Vector3 up = Vector3.Cross(right, dir);
            Quaternion qYaw = Quaternion.AngleAxis(yaw, dir);
            Quaternion qPitch = Quaternion.AngleAxis(pitch, right);
            Vector3 sp = qYaw * (qPitch * dir);
            return sp.normalized;
        }

        private void TryReadWeaponParamsFromSO()
        {
            var so = _db != null ? _db.Get(_itemId) : null;
            if (so == null) return;
            var t = so.GetType();

            _isAutomatic = ReadBool(t, so, "IsAutomatic", "Automatic", "isAutomatic") ?? _isAutomatic;

            var fr = ReadFloat(t, so, "FireRate", "fireRate", "RoundsPerSecond", "ShotsPerSecond")
                     ?? ConvertRpmToSps(ReadFloat(t, so, "Rpm", "RPM", "ShotsPerMinute"));
            if (fr.HasValue && fr.Value > 0f) _fireRate = fr.Value;

            _spreadDeg = ReadFloat(t, so, "Spread", "spread", "SpreadDegrees", "SpreadDeg", "MaxSpread") ?? _spreadDeg;
        }

        private static float? ConvertRpmToSps(float? rpm)
        {
            if (!rpm.HasValue) return null;
            return Mathf.Max(0.01f, rpm.Value / 60f);
        }

        private int? TryReadIntFromSO(params string[] names)
        {
            var so = _db != null ? _db.Get(_itemId) : null;
            if (so == null) return null;
            return ReadInt(so.GetType(), so, names);
        }

        private string TryReadStringFromSO(params string[] names)
        {
            var so = _db != null ? _db.Get(_itemId) : null;
            if (so == null) return null;
            return ReadString(so.GetType(), so, names);
        }

        private static bool? ReadBool(Type t, object inst, params string[] names)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var n in names)
            {
                var f = t.GetField(n, flags);
                if (f != null && f.FieldType == typeof(bool)) return (bool)f.GetValue(inst);
                var p = t.GetProperty(n, flags);
                if (p != null && p.PropertyType == typeof(bool) && p.CanRead) return (bool)p.GetValue(inst);
            }
            return null;
        }

        private static float? ReadFloat(Type t, object inst, params string[] names)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var n in names)
            {
                var f = t.GetField(n, flags);
                if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(inst);
                if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(inst);
                var p = t.GetProperty(n, flags);
                if (p != null && p.PropertyType == typeof(float) && p.CanRead) return (float)p.GetValue(inst);
                if (p != null && p.PropertyType == typeof(int) && p.CanRead) return (int)p.GetValue(inst);
            }
            return null;
        }

        private static int? ReadInt(Type t, object inst, params string[] names)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var n in names)
            {
                var f = t.GetField(n, flags);
                if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(inst);
                var p = t.GetProperty(n, flags);
                if (p != null && p.PropertyType == typeof(int) && p.CanRead) return (int)p.GetValue(inst);
            }
            return null;
        }

        private static string ReadString(Type t, object inst, params string[] names)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var n in names)
            {
                var f = t.GetField(n, flags);
                if (f != null && f.FieldType == typeof(string)) return (string)f.GetValue(inst);
                var p = t.GetProperty(n, flags);
                if (p != null && p.PropertyType == typeof(string) && p.CanRead) return (string)p.GetValue(inst);
            }
            return null;
        }

        public void OnMuzzleFlash() { }
    }
}
