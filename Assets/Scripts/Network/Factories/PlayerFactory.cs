using Fusion;
using UnityEngine;
using Zenject;

namespace Game.Network
{
    public class PlayerFactory : IPlayerFactory
    {
        private readonly DiContainer _container;
        private readonly NetworkRunner _runner;
        private readonly GameObject _playerPrefab;
        private readonly InteractionPromptView _prompt;
        private readonly InputHandler _input;
        private readonly InventoryService _inventory;

        [Inject]
        public PlayerFactory(
            DiContainer container,
            [Inject(Id = "PlayerPrefab")] GameObject playerPrefab,
            NetworkRunner runner,
            InteractionPromptView prompt,
            InputHandler input,
            InventoryService inventory)
        {
            _container = container;
            _playerPrefab = playerPrefab;
            _runner = runner;
            _prompt = prompt;
            _input = input;
            _inventory = inventory;
        }

        public NetworkObject Spawn(PlayerRef playerRef)
        {
            var prefabNetObj = _playerPrefab.GetComponent<NetworkObject>();
            var netObj = _runner.Spawn(prefabNetObj, Vector3.zero, Quaternion.identity, playerRef);

            // Инъекция через Zenject (если что-то ещё нужно)
            _container.InjectGameObject(netObj.gameObject);

            // Далее ищем контроллеры и инициализируем вручную!
            var interactionController = netObj.GetComponent<InteractionController>();
            if (interactionController != null)
            {
                interactionController.ManualInit(_input, _prompt, _inventory);
            }

            // ... если ещё нужны ручные зависимости для других компонентов, делай аналогично

            return netObj;
        }
    }

}
