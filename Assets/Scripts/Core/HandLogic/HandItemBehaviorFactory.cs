using UnityEngine;

namespace Game
{
    public class HandItemBehaviorFactory
    {
        public IHandItemBehavior Create(ItemSO so, Transform handParent)
        {
            // 1) ������� ��� ���������� ������ � ����
            var go = Object.Instantiate(so.Prefab, handParent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            // 2) � ����������� �� ���� SO ���������� ������ Behavior
            if (so is WeaponSO wso)
                return go.AddComponent<WeaponBehavior>().Construct(wso);
            if (so is ToolSO tso)
                return go.AddComponent<ToolBehavior>().Construct(tso);
            // ������ � ������ ���������
            return go.AddComponent<DefaultHandBehavior>().Construct(so);
        }
    }
}
