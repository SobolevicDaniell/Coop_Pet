using UnityEngine;

namespace Game
{
    public class ToolBehavior : MonoBehaviour, IHandItemBehavior
    {
        private ToolSO _so;

        public ToolBehavior Construct(ToolSO so)
        {
            _so = so;
            return this;
        }

        public void OnEquip()
        {
            // play equip anim
        }

        public void OnUnequip()
        {
            Destroy(gameObject);
        }

        public void OnUsePressed()
        {
            // например, удар киркой: raycast по объекту, наносим урон
        }

        public void OnUseHeld(float delta) { }

        public void OnUseReleased() { }
    }
}
