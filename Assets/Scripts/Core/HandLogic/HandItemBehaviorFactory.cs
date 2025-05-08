using UnityEngine;

namespace Game
{
    public class HandItemBehaviorFactory
    {
        public IHandItemBehavior Create(ItemSO so, Transform handParent)
        {
            // 1) Спавним или активируем модель в руке
            var go = Object.Instantiate(so.Prefab, handParent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            // 2) В зависимости от типа SO навешиваем нужный Behavior
            if (so is WeaponSO wso)
                return go.AddComponent<WeaponBehavior>().Construct(wso);
            if (so is ToolSO tso)
                return go.AddComponent<ToolBehavior>().Construct(tso);
            // ресурс — пустое поведение
            return go.AddComponent<DefaultHandBehavior>().Construct(so);
        }
    }
}
