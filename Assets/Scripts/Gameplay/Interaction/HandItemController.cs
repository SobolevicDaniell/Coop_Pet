using UnityEngine;
using Fusion;
using System.Reflection;

namespace Game
{
    public class HandItemController : NetworkBehaviour
    {
        [SerializeField] private Transform _handPoint;

        [Networked] public NetworkId HandModelNetId { get; private set; }
        private NetworkObject _handModel;

        private ItemDatabaseSO _itemDatabase;
        private PlayerRpcHandler _playerRpc;
        private InteractionController _ic;

        public void Construct(ItemDatabaseSO db, PlayerRpcHandler rpc, InteractionController ic)
        {
            _itemDatabase = db;
            _playerRpc = rpc;
            _ic = ic;
        }

        public void RequestEquip(string itemId)
        {
            if (_playerRpc == null) { Debug.LogError("[HandItemController] _playerRpc == null"); return; }
            _playerRpc.RPC_EquipItem(itemId);
        }

        public void RequestUnEquip()
        {
            if (_playerRpc == null) { Debug.LogError("[HandItemController] _playerRpc == null"); return; }
            _playerRpc.RPC_UnEquipItem();
        }

        public void EquipItemServer(string itemId)
        {
            if (!Object.HasStateAuthority) return;
            DespawnCurrent();

            if (string.IsNullOrEmpty(itemId))
            {
                NotifyHandModelChanged(null);
                return;
            }

            if (_itemDatabase == null)
            {
                Debug.LogError("[HandItemController] _itemDatabase == null. Вызовите Construct(...)");
                return;
            }

            var item = _itemDatabase.Get(itemId);
            if (item == null)
            {
                Debug.LogError($"[HandItemController] Item '{itemId}' not found");
                return;
            }

            var handPrefab = ResolveHandPrefab(item);
            if (handPrefab == null)
            {
                NotifyHandModelChanged(null);
                return;
            }

            var spawned = Runner.Spawn(handPrefab, Vector3.zero, Quaternion.identity, Object.InputAuthority);
            _handModel = spawned;
            HandModelNetId = spawned != null ? spawned.Id : default;

            SanitizeHandModel(_handModel != null ? _handModel.gameObject : null);
            NotifyHandModelChanged(_handModel);
            AttachToHand(_handModel != null ? _handModel.transform : null);
        }

        public void UnEquipItemServer()
        {
            if (!Object.HasStateAuthority) return;
            DespawnCurrent();
            HandModelNetId = default;
            NotifyHandModelChanged(null);
        }

        private void DespawnCurrent()
        {
            if (_handModel != null)
            {
                var toDespawn = _handModel;
                _handModel = null;
                if (toDespawn != null && toDespawn.Runner) Runner.Despawn(toDespawn);
            }
        }

        private void NotifyHandModelChanged(NetworkObject model) => _ic?.SetHandModelNetworkInstance(model);

        private void AttachToHand(Transform t)
        {
            if (t == null || _handPoint == null) return;
            var originalLocalScale = t.localScale;
            t.SetParent(_handPoint, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = originalLocalScale;
        }

        private void SanitizeHandModel(GameObject go)
        {
            if (go == null) return;
            var rbs = go.GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in rbs) { rb.isKinematic = true; rb.useGravity = false; rb.detectCollisions = false; }
            var cols = go.GetComponentsInChildren<Collider>(true);
            foreach (var c in cols) c.enabled = false;
        }

        public override void Render()
        {
            if (_handModel == null && HandModelNetId != default && Runner != null)
            {
                _handModel = Runner.FindObject(HandModelNetId);
                if (_handModel != null)
                {
                    SanitizeHandModel(_handModel.gameObject);
                    AttachToHand(_handModel.transform);
                }
            }
            if (_handModel != null && _handPoint != null && _handModel.transform.parent != _handPoint)
                AttachToHand(_handModel.transform);
        }

        private static NetworkObject ResolveHandPrefab(ItemSO item)
        {
            if (item is IHandModelProvider p && p.HandModelNetwork != null)
                return p.HandModelNetwork;

            var t = item.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            var f = t.GetField("HandModelNetwork", flags);
            if (f != null)
            {
                var v = f.GetValue(item);
                if (v is NetworkObject no1) return no1;
                if (v is GameObject go1) { var no = go1.GetComponent<NetworkObject>(); if (no != null) return no; }
            }

            var pinfo = t.GetProperty("HandModelNetwork", flags);
            if (pinfo != null && pinfo.CanRead)
            {
                var v = pinfo.GetValue(item);
                if (v is NetworkObject no2) return no2;
                if (v is GameObject go2) { var no = go2.GetComponent<NetworkObject>(); if (no != null) return no; }
            }

            return null;
        }
    }
}
