using UnityEngine;

namespace Game
{
    public class ToolBehavior : MonoBehaviour, IHandItemBehavior
    {
        private ToolSO _so;
        //private GameObject _instance;
        private Transform _handPoint;
        private InteractionController _ic;

        public ToolBehavior Construct(ToolSO so, Transform handParent, InteractionController ic)
        {
            _so = so;
            _handPoint = handParent;
            _ic = ic;
            return this;
        }

        public void OnEquip()
        {
            _ic.RpcHandler.RPC_RequestSpawnHandModel(_so.Id);
        }


        public void OnUnequip()
        {
            //Destroy(_instance);
            _ic.RpcHandler.RPC_RequestDespawnHandModel();
        }

        public void OnUsePressed()
        {
            // например, удар киркой: raycast по объекту, наносим урон
        }

        public void OnUseHeld(float delta) { }

        public void OnUseReleased() { }

        public void OnMuzzleFlash()
        {
        }

        public void OnUseHeld()
        {
            throw new System.NotImplementedException();
        }
    }
}
