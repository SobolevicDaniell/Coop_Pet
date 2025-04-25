using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Network
{
    public class MenuStartup : MonoBehaviour
    {
        [SerializeField] private Button _createButton;
        [SerializeField] private Button _joinButton;

        private void Start()
        {
            _createButton.onClick.AddListener(() => LaunchGame(GameMode.Host));
            _joinButton.onClick.AddListener(() => LaunchGame(GameMode.Client));
        }

        private void LaunchGame(GameMode mode)
        {
            PlayerPrefs.SetString("GameMode", mode.ToString());
            SceneManager.LoadScene("GameScene");
        }
    }
}
