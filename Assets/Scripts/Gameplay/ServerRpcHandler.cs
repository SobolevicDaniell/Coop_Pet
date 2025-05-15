// Assets/Scripts/Gameplay/ServerRpcHandler.cs
using System.Linq;
using Fusion;
using TMPro;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public class ServerRpcHandler : NetworkBehaviour
    {
        private InteractionController _ic;

        public override void Spawned()
        {
            _ic = GetComponent<InteractionController>();
        }

        // Быстрая смена слота
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SelectQuickSlot(int slot, RpcInfo _ = default)
        {
            _ic.NetSelectedQuickSlot = (_ic.NetSelectedQuickSlot == slot) ? -1 : slot;
        }

        // PICK
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestPick(NetworkObject pickable, RpcInfo info = default)
        {
            if (pickable == null) return;
            var pi = pickable.GetComponent<PickableItem>();
            if (pi == null) return;

            var itemId = pi.ItemId;
            var count = pi.Count;

            Runner.Despawn(pickable);
            RPC_ConfirmPick(info.Source, itemId, count);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        void RPC_ConfirmPick(PlayerRef _, string itemId, int count)
        {
            int leftover = _ic.Inventory.HandlePick(itemId, count);
            if (leftover > 0)
            {
                RPC_RequestDrop(
                    _ic.DropPoint.position,
                    _ic.Camera.transform.forward,
                    itemId,
                    leftover);
            }
        }

        // DROP
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestDrop(
            Vector3 pos, Vector3 dir, string itemId, int count, RpcInfo _ = default)
        {
            if (!Object.HasStateAuthority) return;
            var def = _ic.Db.Get(itemId);
            var prefab = def.Prefab.GetComponent<NetworkObject>();

            Runner.Spawn(
                prefab,
                pos,
                Quaternion.LookRotation(dir),
                PlayerRef.None,
                onBeforeSpawned: (runner, spawned) =>
                {
                    spawned.GetComponent<PickableItem>()
                           .Initialize(itemId, count);
                    if (spawned.TryGetComponent<Rigidbody>(out var rb))
                        rb.linearVelocity = dir * _ic.ThrowForce;
                });
        }

        // Запрос стрельбы
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestShoot(RpcInfo _ = default)
        {
            if (_ic.CurrentBehavior is WeaponBehavior wb && wb.TryUseAmmo())
            {
                //Runner.Spawn(
                //  wb.GetBulletNetworkObject(),
                //  wb.MuzzlePosition,
                //  wb.MuzzleRotation,
                //  Object.InputAuthority,
                //  onBeforeSpawned: (runner, spawned) => {
                //      if (spawned.TryGetComponent<Bullet>(out var b))
                //          b.InitializeVelocity(wb.MuzzleForward * wb.BulletSpeed);
                //  }
                //);
                Runner.Spawn(
                    wb.GetBulletNetworkObject(),
                    wb.MuzzlePosition,
                    wb.MuzzleRotation,
                    Object.InputAuthority,
                    onBeforeSpawned: (runner, spawned) => {
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
            _ic.CurrentBehavior?.OnMuzzleFlash();
        }





        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestSpawnHandModel(string itemId, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority) return;

            var old = _ic.GetHandModelNetworkInstance();
            if (old != null)
            {
                Runner.Despawn(old);
                _ic.SetHandModelNetworkInstance(null);
                RPC_OnHandModelDespawned(Object.InputAuthority);
                _ic.ClearBehavior();
            }

            var so = _ic.Db.Get(itemId);
            var prefab = so is WeaponSO w ? w._handModelNetwork
                       : so is ToolSO t ? t._handModelNetwork
                       : null;
            if (prefab == null) return;

            // ВАЖНО: используем StateAuthority и спавним объект сразу с правильной привязкой.
            var spawned = Runner.Spawn(prefab,
                                       inputAuthority: Object.InputAuthority,
                                       onBeforeSpawned: (runner, obj) =>
                                       {
                                           var ic = FindObjectsOfType<InteractionController>()
                                                        .FirstOrDefault(x => x.Object.InputAuthority == Object.InputAuthority);
                                           if (ic != null)
                                           {
                                               obj.transform.SetParent(ic.HandPoint, false);
                                               obj.transform.localPosition = Vector3.zero;
                                               obj.transform.localRotation = Quaternion.identity;
                                           }
                                       });

            _ic.SetHandModelNetworkInstance(spawned);
            RPC_OnHandModelSpawned(spawned, Object.InputAuthority);

            int selectedSlot = _ic.NetSelectedQuickSlot;
            ItemState state = selectedSlot >= 0
                ? _ic.Inventory.GetQuickSlots()[selectedSlot].State
                : new ItemState();

            var behavior = _ic.Factory.Create(so, _ic.HandPoint, _ic, state);
            _ic.SetCurrentBehavior(behavior);
        }





        // 2) Сервер → Все: приклеить модель к руке нужного игрока
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        void RPC_OnHandModelSpawned(NetworkObject spawnedModel, PlayerRef owner, RpcInfo _ = default)
        {
            if (spawnedModel == null) return;
            foreach (var ic in FindObjectsOfType<InteractionController>())
            {
                if (ic.Object.InputAuthority == owner)
                {
                    var go = spawnedModel.gameObject;
                    go.transform.SetParent(ic.HandPoint, worldPositionStays: false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    break;
                }
            }
        }

        // 3) Клиент → Сервер: попросить убрать модель
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestDespawnHandModel(RpcInfo _ = default)
        {
            if (!Object.HasStateAuthority) return;

            var old = _ic.GetHandModelNetworkInstance();
            if (old != null)
            {
                Runner.Despawn(old);
                _ic.SetHandModelNetworkInstance(null);
                // и разослать всем комманду забыть ссылку
                RPC_OnHandModelDespawned(Object.InputAuthority);
            }
        }

        // 4) Сервер → Все: очистить локальную ссылку (но GameObject уже уничтожен)
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        void RPC_OnHandModelDespawned(PlayerRef owner, RpcInfo _ = default)
        {
            foreach (var ic in FindObjectsOfType<InteractionController>())
            {
                if (ic.Object.InputAuthority == owner)
                {
                    ic.SetHandModelNetworkInstance(null);
                    break;
                }
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestReload(RpcInfo info = default)
        {
            if (_ic.CurrentBehavior is WeaponBehavior wb)
            {
                wb.Reload();
                RPC_OnReload(info.Source);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        void RPC_OnReload(PlayerRef owner)
        {
            foreach (var ic in FindObjectsOfType<InteractionController>())
            {
                if (ic.Object.InputAuthority == owner && ic.CurrentBehavior is WeaponBehavior wb)
                {
                    wb.Reload(); // синхронизируем перезарядку на клиентах
                }
            }
        }
    }
}

