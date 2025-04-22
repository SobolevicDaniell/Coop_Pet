using UnityEngine;
using Fusion;

namespace Game.Network
{
    public class PlayerCameraController : NetworkBehaviour
    {
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private AudioListener _audioListener;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            if (Object.HasInputAuthority)
            {
                EnableLocalCamera();
            }
            else
            {
                DisableNonLocalCamera();
            }
        }

        private void EnableLocalCamera()
        {
            if (_playerCamera != null) _playerCamera.enabled = true;
            if (_audioListener != null) _audioListener.enabled = true;

            var allCameras = FindObjectsOfType<Camera>();
            foreach (var camera in allCameras)
            {
                if (camera != _playerCamera)
                {
                    camera.enabled = false;
                    var listener = camera.GetComponent<AudioListener>();
                    if (listener != null) listener.enabled = false;
                }
            }
        }

        private void DisableNonLocalCamera()
        {
            if (_playerCamera != null) _playerCamera.enabled = false;
            if (_audioListener != null) _audioListener.enabled = false;
        }
    }
}
