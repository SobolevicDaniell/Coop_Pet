using Game;
using UnityEngine;

public class HandItemBehaviorFactory
{
    public IHandItemBehavior Create(ItemSO so, Transform handParent, InteractionController ic, InventorySlot slot)
    {
        GameObject go = new GameObject($"[Behavior] {so.Id}");
        go.transform.SetParent(handParent, false);
        go.transform.localPosition = Vector3.zero;

        if (so is WeaponSO wso)
        {
            var wb = go.AddComponent<WeaponBehavior>().Construct(wso, handParent, ic, slot);
            return wb;
        }

        if (so is ToolSO tso)
        {
            var beh = go.AddComponent<ToolBehavior>().Construct(tso, handParent, ic);
            return beh;
        }

        if (so is PlaceableItemSO piso)
        {
            var beh = go.AddComponent<PlaceableBehavior>().Construct(piso, handParent, ic, slot);
            return beh;
        }

        var defaultBeh = go.AddComponent<DefaultHandBehavior>().Construct(so, handParent);
        return defaultBeh;
    }
}
