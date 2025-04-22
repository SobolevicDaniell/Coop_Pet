// Assets/Scripts/Game/Gameplay/PickableItem.cs
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public class PickableItem : NetworkBehaviour
    {
        [SerializeField] private string itemId;
        private ItemSO itemDef;
        [Inject] private ItemDatabaseSO db;

        // ��������� ������������� ��� RPC
        public string ItemId => itemId;
        // ��������� �����������
        public ItemSO Definition => itemDef;

        private void Awake()
        {
            // �� ������ ���������� ScriptableObject �� ����
            itemDef = db.Get(itemId);
        }

        public void OnPicked(NetworkRunner runner)
        {
            // ������ ��������� ���� �� �������
            runner.Despawn(Object);
        }
    }
}
