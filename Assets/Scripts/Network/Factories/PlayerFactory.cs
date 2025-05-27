using Fusion;
using UnityEngine;
using Zenject;
using Game.UI;

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
        private readonly InventoryPanel _playerInventoryPanel;
        private readonly InventoryPanel _otherInventoryPanel;
        private readonly ItemDatabaseSO _itemDatabase;

        [Inject]
        public PlayerFactory(
            DiContainer container,
            [Inject(Id = "PlayerPrefab")] GameObject playerPrefab,
            NetworkRunner runner,
            InteractionPromptView prompt,
            InputHandler input,
            InventoryService inventory,
            [Inject(Id = "PlayerInventoryPanel")] InventoryPanel playerInventoryPanel,
            [Inject(Id = "OtherInventoryPanel")] InventoryPanel otherInventoryPanel,
            ItemDatabaseSO itemDatabase)
        {
            _container = container;
            _playerPrefab = playerPrefab;
            _runner = runner;
            _prompt = prompt;
            _input = input;
            _inventory = inventory;
            _playerInventoryPanel = playerInventoryPanel;
            _otherInventoryPanel = otherInventoryPanel;
            _itemDatabase = itemDatabase;
        }

        public NetworkObject Spawn(PlayerRef playerRef)
        {
            var prefabNetObj = _playerPrefab.GetComponent<NetworkObject>();
            var netObj = _runner.Spawn(prefabNetObj, Vector3.zero, Quaternion.identity, playerRef);

            _container.InjectGameObject(netObj.gameObject);

            var interactionController = netObj.GetComponent<InteractionController>();
            if (interactionController != null)
            {
                // Передай все нужные зависимости!
                interactionController.ManualInit(
                    _input,
                    _prompt,
                    _inventory,
                    _playerInventoryPanel,
                    _otherInventoryPanel,
                    _itemDatabase
                );
            }

            return netObj;
        }
    }
}
