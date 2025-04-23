using Fusion;
using UnityEngine;
using Zenject;

namespace Game.Network
{
    /// <summary>
    /// Реализация IPlayerFactory: спавнит игрока через NetworkRunner
    /// и прогоняет GO через Zenject для инъекций в его компоненты.
    /// </summary>
    public class PlayerFactory : IPlayerFactory
    {
        readonly DiContainer _container;
        readonly NetworkRunner _runner;
        readonly GameObject _playerPrefab;

        [Inject]
        public PlayerFactory(
            DiContainer container,
            NetworkRunner runner,
            [Inject(Id = "PlayerPrefab")] GameObject playerPrefab
        )
        {
            _container = container;
            _runner = runner;
            _playerPrefab = playerPrefab;
        }

        public NetworkObject Spawn(PlayerRef playerRef)
        {
            // Спавним игрока, передаём ему право InputAuthority
            var netObj = _runner.Spawn(
                _playerPrefab,
                Vector3.zero,        // здесь можно подставить нужную позицию/ротацию
                Quaternion.identity,
                playerRef
            );

            if (netObj == null)
                return null;

            // Прогоним вновь созданный объект через контейнер,
            // чтобы все [Inject] поля и свойства заполнились
            _container.InjectGameObject(netObj.gameObject);

            return netObj;
        }
    }
}
