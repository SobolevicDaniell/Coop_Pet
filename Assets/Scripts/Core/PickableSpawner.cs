using Fusion;
using UnityEngine;
using Zenject;
using Game;
using System.Linq;

namespace Game.Gameplay
{
    [RequireComponent(typeof(NetworkRunnerProvider))]
    public class PickableSpawner : MonoBehaviour
    {
        [Inject] private NetworkRunner _runner;
        [Inject] private ItemDatabaseSO _database;

        [Header("Какой предмет спавним")]
        [SerializeField] private string _itemId;

        [Header("Желаемое количество (≤ MaxStack)")]
        [SerializeField] private int _requestedCount = 1;


        private void OnEnable()
        {
            Network.Startup.OnSessionStarted += SpawnPickable;
        }

        private void OnDisable()
        {
            Network.Startup.OnSessionStarted -= SpawnPickable;
        }

        private void OnValidate()
        {
            if (_database == null) return;

            var names = new string[_database.Items.Count];
            for (int i = 0; i < names.Length; i++)
                names[i] = _database.Items[i].Id;

            if (!names.Contains(_itemId) && names.Length > 0)
                _itemId = names[0];
        }

        private void SpawnPickable()
        {
            // Только на сервере
            if (!_runner.IsServer) return;

            var itemDef = _database.Get(_itemId);
            if (itemDef == null)
            {
                Debug.LogError($"[PickableSpawner] ItemSO '{_itemId}' не найден в базе!");
                return;
            }

            int count = Mathf.Clamp(_requestedCount, 1, itemDef.MaxStack);

            var prefabNetObj = itemDef.Prefab.GetComponent<NetworkObject>();
            if (prefabNetObj == null)
            {
                Debug.LogError($"[PickableSpawner] У Prefab предмета '{_itemId}' нет компонента NetworkObject!");
                return;
            }

            _runner.Spawn(
                prefabNetObj,
                transform.position,
                transform.rotation,
                PlayerRef.None,
                onBeforeSpawned: (runner, spawnedObj) =>
                {
                    var pickable = spawnedObj.GetComponent<PickableItem>();
                    pickable.Initialize(_itemId, count);
                }
            );
        }
    }
}
