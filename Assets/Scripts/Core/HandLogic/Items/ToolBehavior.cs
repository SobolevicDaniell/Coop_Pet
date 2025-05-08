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
            // ��������, ���� ������: raycast �� �������, ������� ����
        }

        public void OnUseHeld(float delta) { }

        public void OnUseReleased() { }
    }
}
