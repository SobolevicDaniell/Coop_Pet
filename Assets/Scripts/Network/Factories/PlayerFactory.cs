// Assets/Scripts/Game/Network/PlayerFactory.cs
using Fusion;
using UnityEngine;
using Zenject;
using System.Collections;

namespace Game.Network
{
    public class PlayerFactory : IPlayerFactory
    {
        readonly DiContainer _container;
        readonly NetworkRunner _runner;
        readonly GameObject _playerPrefab;
        readonly MonoBehaviour _coroutineRunner;

        [Inject]
        public PlayerFactory(
            DiContainer container,
            NetworkRunner runner,
            [Inject(Id = "PlayerPrefab")] GameObject playerPrefab,
            [InjectOptional] MonoBehaviour coroutineRunner = null
        )
        {
            _container = container;
            _runner = runner;
            _playerPrefab = playerPrefab;
            // ���� ����� MonoBehaviour �� ����������, ����� ��������� ��������.
            // ����� ������������ ��� �� NetworkRunner GameObject
            _coroutineRunner = coroutineRunner ?? runner.GetComponent<MonoBehaviour>();
        }

        public NetworkObject Spawn(PlayerRef playerRef)
        {
            var netObj = _runner.Spawn(
                _playerPrefab, Vector3.zero, Quaternion.identity, playerRef
            );

            if (netObj == null)
                return null;

            // ������ ����������� InjectGameObject �
            // ��������� ��������: ���� 1 ���� � ������ ����� ��������.
            _coroutineRunner.StartCoroutine(DelayedInject(netObj.gameObject));

            return netObj;
        }

        IEnumerator DelayedInject(GameObject go)
        {
            // ��� ���� ����, ����� Zenject ����� ��������� NetworkInstaller.InstallBindings()
            yield return null;
            _container.InjectGameObject(go);
        }
    }
}
