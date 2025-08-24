using System.Collections;
using System.Linq;
using Fusion;
using UnityEngine;
using Zenject;

namespace Game.Gameplay
{
    public class PickableSpawner : MonoBehaviour
    {
        [Inject] private NetworkRunner _runner;
        [Inject] private ItemDatabaseSO _database;

        [Header("Какой предмет спавним")]
        [SerializeField] private string _itemId;

        [Header("Желаемое количество (≤ MaxStack)")]
        [SerializeField] private int _requestedCount = 1;

        private bool _spawned;
        private Coroutine _routine;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            _routine = StartCoroutine(CoSpawnWhenReady());
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
        }

        private IEnumerator CoSpawnWhenReady()
        {
            while (_runner == null)
            {
                _runner = FindObjectOfType<NetworkRunner>();
                yield return null;
            }

            while (!_runner.IsRunning)
                yield return null;

            if (!HasAuthority(_runner))
                yield break;

            if (_spawned)
                yield break;

            SpawnPickable();
            _spawned = true;
        }

        private bool HasAuthority(NetworkRunner runner)
        {
            return runner.IsServer || runner.IsSharedModeMasterClient;
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
            var itemDef = _database.Get(_itemId);
            if (itemDef == null) return;

            int count = Mathf.Clamp(_requestedCount, 1, itemDef.MaxStack);

            var prefabNetObj = itemDef.Prefab.GetComponent<NetworkObject>();
            if (prefabNetObj == null) return;

            _runner.Spawn(
                prefabNetObj,
                transform.position,
                transform.rotation,
                PlayerRef.None,
                onBeforeSpawned: (runner, spawnedObj) =>
                {
                    var pickable = spawnedObj.GetComponent<PickableItem>();
                    if (pickable != null)
                        pickable.ServerInit(_itemId, count, 0);
                }
            );
        }
    }
}