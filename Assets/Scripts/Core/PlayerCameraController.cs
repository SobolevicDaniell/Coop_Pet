// Assets/Scripts/Network/PlayerCameraController.cs
using Fusion;
using UnityEngine;

namespace Game.Network
{
    /// <summary>
    /// Управляет включением камеры и аудио для локального игрока.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerCameraController : NetworkBehaviour
    {
        [Header("Local Player Camera & Audio")]
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private AudioListener _audioListener;

        /// <summary>
        /// Вызывается из NetworkPlayer.Spawned() для настройки локальной копии.
        /// </summary>
        /// <param name="isLocal">true для игрока, которым управляют на этой машине</param>
        public void SetLocal(bool isLocal)
        {
            // Блокируем или разблокируем курсор
            Cursor.lockState = isLocal ? CursorLockMode.Locked : CursorLockMode.None;

            // Включаем или отключаем камеру и аудио-листенер на этом объекте
            if (_playerCamera != null) _playerCamera.enabled = isLocal;
            if (_audioListener != null) _audioListener.enabled = isLocal;

            // Если это локальный игрок, отключаем все другие камеры и аудио-листенеры в сцене
            if (isLocal)
            {
                foreach (var cam in FindObjectsOfType<Camera>())
                {
                    if (cam != _playerCamera)
                    {
                        cam.enabled = false;
                        var listener = cam.GetComponent<AudioListener>();
                        if (listener != null)
                            listener.enabled = false;
                    }
                }
            }
        }
    }
}
