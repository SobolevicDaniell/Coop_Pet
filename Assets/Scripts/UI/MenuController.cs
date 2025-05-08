using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Fusion;
using System.Threading.Tasks;

namespace Game.Network
{
    public class MenuController : MonoBehaviour
    {
        [Inject] private Startup _startup;
        [SerializeField] private Button _hostButton;
        [SerializeField] private Button _clientButton;

        void Awake()
        {
            _hostButton.onClick.AddListener(async () => await Launch(GameMode.Host));
            _clientButton.onClick.AddListener(async () => await Launch(GameMode.Client));
        }

        private async Task Launch(GameMode mode)
        {
            gameObject.SetActive(false);

            await _startup.BeginSession(mode);
        }
    }
}
