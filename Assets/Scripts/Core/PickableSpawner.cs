using Fusion;
using UnityEngine;
using Zenject;
using System.Collections.Generic;

namespace Game.Gameplay
{
    [RequireComponent(typeof(NetworkRunnerProvider))]
    public class PickableSpawner : MonoBehaviour
    {
        [Header("Fusion Pickable Prefab (NetworkObject)")]
        [SerializeField] private NetworkObject _pickablePrefab;

        [Header("Spawn Points")]
        [Tooltip("Список точек, в которых нужно заспавнить предметы")]
        [SerializeField] private List<Transform> _spawnPoints;

        private NetworkRunner _runner;

        [Inject]
        public void Construct(NetworkRunner runner)
        {
            _runner = runner;
        }
        
        public void SpawnAllPickables()
        {
            foreach (var point in _spawnPoints)
            {
                var no = _runner.Spawn(
                    _pickablePrefab,
                    point.position,
                    point.rotation,
                    PlayerRef.None
                );
            }
        }
    }
}
