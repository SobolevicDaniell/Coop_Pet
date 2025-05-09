using UnityEngine;

namespace Game
{
    public class HandItemBehaviorFactory
    {
        public IHandItemBehavior Create(ItemSO so, Transform handParent, InteractionController ic)
        {
            if (so is WeaponSO wso)
            {
                var go = new GameObject("WeaponBehavior");
                var beh = go.AddComponent<WeaponBehavior>();
                return beh.Construct(wso, handParent, ic);
            }
            if (so is ToolSO tso)
            {
                var go = new GameObject("ToolBehavior");
                var beh = go.AddComponent<ToolBehavior>();
                return beh.Construct(tso, handParent);
            }
            var defaultGo = new GameObject("DefaultHand");
            var defaultBeh = defaultGo.AddComponent<DefaultHandBehavior>();
            return defaultBeh.Construct(so, handParent);
        }
    }
}
