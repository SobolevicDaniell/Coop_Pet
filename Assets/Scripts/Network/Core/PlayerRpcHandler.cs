using System;
using Fusion;
using UnityEngine;
using System.Linq;
using Game.Network;
using Zenject;


namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerRpcHandler : NetworkBehaviour
    {
        private ItemDatabaseSO _itemDatabase;
        private HandItemController _handItemController;
        private InteractionController _ic;


        public void Construct(ItemDatabaseSO itemDatabase)
        {
            _itemDatabase = itemDatabase;
        }
        public override void Spawned()
        {
            _handItemController = GetComponent<HandItemController>();
            _ic = GetComponent<InteractionController>();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SelectQuickSlot(int slot, RpcInfo _ = default)
        {
            _ic.netSelectedQuickSlot = (_ic.netSelectedQuickSlot == slot) ? -1 : slot;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_EquipItem(string itemId, RpcInfo _ = default)
        {
            if (!Object.HasStateAuthority) return;
            _handItemController.EquipItemServer(itemId);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_UnEquipItem(RpcInfo _ = default)
        {
            if (!Object.HasStateAuthority) return;
            _handItemController.UnEquipItemServer();
        }


        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestPick(NetworkObject pickable, RpcInfo info = default)
        {
            if (pickable == null) return;
            var pi = pickable.GetComponent<PickableItem>();
            if (pi == null) return;

            var itemId = pi.ItemId;
            var count = pi.Count;
            var state = pi.State;

            Runner.Despawn(pickable);

            RPC_ConfirmPick(info.Source, itemId, count, state?.Ammo ?? 0);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        void RPC_ConfirmPick(PlayerRef _, string itemId, int count, int ammo)
        {
            int leftover = _ic.inventory.HandlePick(itemId, count, ammo);
            if (leftover > 0)
            {
                RPC_RequestDrop(
                    _ic.dropPoint.position,
                    _ic.camera.transform.forward,
                    itemId,
                    leftover);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestDrop(
            Vector3 pos, Vector3 dir, string itemId, int count, int ammo = 0, RpcInfo _ = default)
        {
            if (!Object.HasStateAuthority) return;
            var def = _ic.db.Get(itemId);
            var prefab = def.Prefab.GetComponent<NetworkObject>();

            Runner.Spawn(
                prefab,
                pos,
                Quaternion.LookRotation(dir),
                PlayerRef.None,
                onBeforeSpawned: (runner, spawned) =>
                {
                    spawned.GetComponent<PickableItem>()
                           .Initialize(itemId, count, new ItemState(ammo));
                    if (spawned.TryGetComponent<Rigidbody>(out var rb))
                        rb.linearVelocity = dir * _ic.ThrowForce;
                });
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestShoot(RpcInfo _ = default)
        {
            if (_ic.currentBehavior is WeaponBehavior wb)
            {
                Runner.Spawn(
                  wb.GetBulletNetworkObject(),
                  wb.MuzzlePosition,
                  wb.MuzzleRotation,
                  Object.InputAuthority,
                  onBeforeSpawned: (runner, spawned) =>
                  {
                      if (spawned.TryGetComponent<Bullet>(out var b))
                      {
                          b.Initialize(Mathf.FloorToInt(wb.BulletDamage));
                          b.InitializeVelocity(wb.MuzzleForward * wb.BulletSpeed);
                      }
                  }
                );
                RPC_OnMuzzleFlash();
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        void RPC_OnMuzzleFlash(RpcInfo _ = default)
        {
            _ic.currentBehavior?.OnMuzzleFlash();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestReload(RpcInfo info = default)
        {
            if (!Object.HasStateAuthority) return;

            if (_ic.currentBehavior is WeaponBehavior wb)
            {
                wb.Reload();
                RPC_OnReload(info.Source); // уведомление всех клиентов
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        void RPC_OnReload(PlayerRef owner)
        {
            foreach (var ic in FindObjectsOfType<InteractionController>())
            {
                if (ic.Object.InputAuthority == owner && ic.currentBehavior is WeaponBehavior wb)
                {
                    wb.Reload();
                }
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestPlaceObject(string itemId, Vector3 pos, Quaternion rot, RpcInfo _ = default)
        {
            if (!Object.HasStateAuthority) return;
            var so = _ic.db.Get(itemId) as PlaceableItemSO;
            if (so == null) return;

            var prefab = so.PlaceablePrefab.GetComponent<NetworkObject>();
            if (prefab == null) return;

            Runner.Spawn(
                prefab,
                pos,
                rot,
                PlayerRef.None,
                onBeforeSpawned: (runner, spawned) =>
                {
                }
            );
        }
    }
}