using Zenject;
using UnityEngine;
using Game.Settings;

namespace Game
{
    public class ProjectInstaller : MonoInstaller
    {
        [SerializeField] private PlayerStatsSO _playerStats;
        private ISettingsService _settings;

        public override void InstallBindings()
        {
            Container.BindInstance(_playerStats).AsSingle();
            Container.BindInterfacesTo<SettingsService>().AsSingle().NonLazy();
        }
    }
}
