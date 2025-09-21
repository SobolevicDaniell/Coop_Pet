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

        private bool  _isAutomatic   = false;
        private float _fireRate      = 8f;   // выстр/сек при авто
        private float _fireRateSingle= 12f;  // выстр/сек при одиночных
        private float _spreadDeg     = 0f;

        private bool _triggerHeld;
        private Transform _cachedMuzzle;
        private int _randSeed;

        private float _intervalAuto;
        private float _intervalSingle;
        private float _nextAutoAt;
        private float _nextSingleAt;

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

            _intervalAuto   = _fireRate       > 0f ? 1f / _fireRate       : 0f;
            _intervalSingle = _fireRateSingle > 0f ? 1f / _fireRateSingle : _intervalAuto;

            _cachedMuzzle = null;
            _nextAutoAt = 0f;
            _nextSingleAt = 0f;
        }

        public void OnEquip()
        {
            _cachedMuzzle = ResolveMuzzle();
            _nextAutoAt = 0f;
            _nextSingleAt = 0f;
            if (_ic != null && _ic.inventory != null)
                _quickSlotIndex = _ic.inventory.SelectedQuickSlot;
        }

        public void OnUnequip()
        {
            _triggerHeld = false;
            _cachedMuzzle = null;
            _nextAutoAt = 0f;
            _nextSingleAt = 0f;
        }

        public void OnUsePressed()
        {
            _triggerHeld = true;

            if (Time.time + 0.0001f >= _nextSingleAt)
            {
                FireOnce(isAuto: false);
                _nextSingleAt = Time.time + _intervalSingle;

                if (_isAutomatic)
                    _nextAutoAt = Time.time + _intervalAuto;
            }
        }

        public void OnUseHeld(float dt)
        {
            if (!_triggerHeld || !_isAutomatic) return;

            if (Time.time + 0.0001f >= _nextAutoAt)
            {
                FireOnce(isAuto: true);
                _nextAutoAt = Time.time + _intervalAuto;
            }
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
            _rpc?.RPC_RequestReload(-1);
        }

        // WeaponBehavior.cs — внутри класса
        private void FireOnce(bool isAuto)
        {
            if (!IsValid()) return;
            if (isAuto && _intervalAuto <= 0f) return;
            if (!isAuto && _intervalSingle <= 0f) return;

            var muzzle = _cachedMuzzle != null ? _cachedMuzzle : ResolveMuzzle();
            if (muzzle == null) muzzle = _ic.handPoint;

            Vector3 dir = muzzle.forward;
            if (_ic.camera != null)
            {
                var ray = _ic.camera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));
                dir = (ray.origin + ray.direction * 1000f - muzzle.position).normalized;
            }
            dir = ApplySpread(dir, _spreadDeg, ref _randSeed);

            // индекс шлём для совместимости, сервер использует SelectedQuickIndexNet
            int slotIndexToSend =
                (_ic != null && _ic.inventory != null && _ic.inventory.SelectedQuickSlot >= 0)
                ? _ic.inventory.SelectedQuickSlot
                : _quickSlotIndex;

            _rpc.RPC_RequestShoot(_itemId, slotIndexToSend, dir, _randSeed, isAuto);
            _randSeed++;
        }

        private Transform ResolveMuzzle()
        {
            // 1) сначала — сетевой hand-модель
            var netHand = _ic != null ? _ic.GetHandModelNetworkInstance() : null;
            if (netHand != null)
            {
                var mp = netHand.GetComponentInChildren<MuzzlePoint>(true);
                if (mp != null) return mp.transform;
            }

            // 2) затем — локальная иерархия контроллера
            if (_ic != null)
            {
                var localMp = _ic.GetComponentInChildren<MuzzlePoint>(true);
                if (localMp != null) return localMp.transform;
            }

            // 3) фоллбэк
            return _ic != null ? _ic.handPoint : null;
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

            // Быстрый путь: явный WeaponSO
            if (so is WeaponSO w)
            {
                _isAutomatic = w.isAutomatic;
                if (w.fireRate       > 0f) _fireRate       = w.fireRate;
                if (w.fireRateSingle > 0f) _fireRateSingle = w.fireRateSingle;
                _spreadDeg = w.spread;
                return;
            }

            // Рефлексия на совместимые поля
            var t = so.GetType();

            var auto = ReadBool(t, so, "IsAutomatic", "Automatic", "isAutomatic");
            if (auto.HasValue) _isAutomatic = auto.Value;

            var fr = ReadFloat(t, so, "FireRate", "fireRate", "RoundsPerSecond", "ShotsPerSecond")
                     ?? ConvertRpmToSps(ReadFloat(t, so, "Rpm", "RPM", "ShotsPerMinute"));
            if (fr.HasValue && fr.Value > 0f) _fireRate = fr.Value;

            var frs = ReadFloat(t, so, "FireRateSingle", "fireRateSingle", "SingleFireRate", "singleFireRate");
            if (frs.HasValue && frs.Value > 0f) _fireRateSingle = frs.Value;

            var spr = ReadFloat(t, so, "Spread", "spread", "SpreadDegrees", "SpreadDeg", "MaxSpread");
            if (spr.HasValue) _spreadDeg = spr.Value;
        }

        private static float? ConvertRpmToSps(float? rpm)
        {
            if (!rpm.HasValue) return null;
            return Mathf.Max(0.01f, rpm.Value / 60f);
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
                if (f != null && f.FieldType == typeof(int))   return (int)f.GetValue(inst);
                var p = t.GetProperty(n, flags);
                if (p != null && p.PropertyType == typeof(float) && p.CanRead) return (float)p.GetValue(inst);
                if (p != null && p.PropertyType == typeof(int)   && p.CanRead) return (int)p.GetValue(inst);
            }
            return null;
        }

        public void OnMuzzleFlash() { }
    }
}
