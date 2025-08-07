using UnityEngine;
using Fusion;
using Zenject;
using System;

namespace Game
{
    public class HandItemController : NetworkBehaviour
    {
        [SerializeField] private Transform _handPoint;

        [Networked] public NetworkId HandModelNetId { get; private set; }
        private NetworkObject _handModel;

        private ItemDatabaseSO       _itemDatabase;
        private PlayerRpcHandler     _playerRpc;
        private InteractionController _ic;

        // ⟶ глобальные singletons берём напрямую из ProjectContext
        public override void Spawned()
        {
            var container = ProjectContext.Instance.Container;

            _playerRpc    = GetComponent<PlayerRpcHandler>();
            _ic           = GetComponent<InteractionController>();
        }
        public void Initialize(ItemDatabaseSO itemDatabase)
        {
            _itemDatabase = itemDatabase;
        }

        
        void TryAttach()
        {
            if (_handModel == null && HandModelNetId.IsValid)
                _handModel = Runner.FindObject(HandModelNetId);

            if (_handModel != null)
                AttachToHand(_handModel.transform);
        }
        public void RequestEquip(string itemId)
        {
            if (!Object.HasInputAuthority || _playerRpc == null) return;

            _playerRpc.RPC_EquipItem(itemId);
        }

        public void RequestUnEquip()
        {
            if (!Object.HasInputAuthority || _playerRpc == null) return;

            _playerRpc.RPC_UnEquipItem();
        }


        public void EquipItemServer(string itemId)
        {
            if (!Object.HasStateAuthority) return;

            var item = _itemDatabase.Get(itemId);
            if (item == null) return;

            var field = item.GetType().GetField("HandModelNetwork");
            var prefab = field?.GetValue(item) as NetworkObject;
            if (prefab == null) return;

            DespawnCurrent();

            _handModel = Runner.Spawn(
                prefab,
                _handPoint.position,
                _handPoint.rotation,
                Object.InputAuthority,
                (runner, spawned) =>
                {
                    spawned.transform.SetParent(_handPoint, false);
                    spawned.transform.localPosition = Vector3.zero;
                    spawned.transform.localRotation = Quaternion.identity;
                });

            HandModelNetId = _handModel.Id;
        }



        public void UnEquipItemServer()
        {
            if (!Object.HasStateAuthority) return;
            DespawnCurrent();
        }

        // ─── вспомогательные ───────────────────────────────────────────────────────────
        private void DespawnCurrent()
        {
            if (_handModel != null && _handModel.IsValid)
                Runner.Despawn(_handModel);

            _handModel = null;
            HandModelNetId = default;
        }

        private void AttachToHand(Transform t)
        {
            t.SetParent(_handPoint, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
        }

        // ─── восстановление для late-join ──────────────────────────────────────────────
        public override void Render()
        {
            // При первом получении ссылки на объект
            if (_handModel == null && HandModelNetId.IsValid)
            {
                _handModel = Runner.FindObject(HandModelNetId);
                if (_handModel != null)
                    AttachToHand(_handModel.transform);
            }

            // Если объект уже есть, но случайно потерял родителя
            if (_handModel != null && _handModel.transform.parent != _handPoint)
            {
                AttachToHand(_handModel.transform);
            }
        }

    }
}
