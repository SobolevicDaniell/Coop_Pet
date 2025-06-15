using UnityEngine;
using Fusion;
using Zenject;
using System;

namespace Game
{
    public class HandItemController : NetworkBehaviour
    {
        private ItemDatabaseSO _itemDatabase;
        private ServerRpcHandler _serverRpcHandler;
        private InteractionController _interactionController;

        [SerializeField] private Transform _handPoint;

        public void Initialize(
            ItemDatabaseSO itemDatabase,
            ServerRpcHandler serverRpcHandler,
            InteractionController interactionController)
        {
            _itemDatabase = itemDatabase;
            _serverRpcHandler = serverRpcHandler;
            _interactionController = interactionController;
        }

        [Networked] public NetworkId handModelNetId { get; set; }
        public NetworkObject handModelNetObj { get; private set; }

        public void RequestEquip(string itemId)
        {
            Debug.Log($"[HandItemController] InputAuthority={Object.InputAuthority}, Runner.LocalPlayer={Runner.LocalPlayer}");
            
            _serverRpcHandler.RPC_RequestEquipHandModel(itemId);
        }
        public void RequestUnEquip()
        {
            if (!Object.HasInputAuthority)
                return;

            _serverRpcHandler.RPC_RequestUnEquipHandModel();
        }

       
        public void OnItemEquipped(NetworkObject netObj)
        {
            handModelNetObj = netObj;
            handModelNetId = netObj != null ? netObj.Id : default;
            if (netObj != null)
            {
                netObj.transform.SetParent(_handPoint, false);
                netObj.transform.localPosition = Vector3.zero;
                netObj.transform.localRotation = Quaternion.identity;
            }
        }

        public void OnItemUnEquipped()
        {
            handModelNetObj = null;
            handModelNetId = default;
        }

        public NetworkObject GetHandModelNetworkInstance()
        {
            if (handModelNetObj != null) return handModelNetObj;
            if (handModelNetId != default && Runner != null)
                handModelNetObj = Runner.FindObject(handModelNetId);
            return handModelNetObj;
        }
    }
}