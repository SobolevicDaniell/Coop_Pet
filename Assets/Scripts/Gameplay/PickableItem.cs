using Fusion;
using Game;
using UnityEngine;
using Zenject;

public class PickableItem : NetworkBehaviour
{
    [SerializeField] private string itemId;
    private ItemSO itemDef;

    [Inject] private ItemDatabaseSO db;

    void Awake()
    {
        itemDef = db.Get(itemId);
    }

    public ItemSO Definition => itemDef;

    public void OnPicked(NetworkRunner runner)
    {
        runner.Despawn(Object);
    }
}