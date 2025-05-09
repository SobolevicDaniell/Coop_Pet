using UnityEngine;

namespace Game
{
    public class ToolBehavior : MonoBehaviour, IHandItemBehavior
    {
        private ToolSO _so;
        private GameObject _instance;
        private Transform _handPoint;

        public ToolBehavior Construct(ToolSO so, Transform handParent)
        {
            _so = so;
            _handPoint = handParent;
            return this;
        }

        public void OnEquip()
        {
            _instance = Instantiate(_so._handModel, _handPoint);
            _instance.transform.localPosition = Vector3.zero;
            _instance.transform.localRotation = Quaternion.identity;
        }

        public void OnUnequip()
        {
            Destroy(_instance);
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
    }
}
