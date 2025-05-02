using Fusion;
using UnityEngine;

namespace Game.Network
{
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerCameraController : NetworkBehaviour
    {
        [Header("Local Player Camera & Audio")]
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private AudioListener _audioListener;

        private bool _isLocal;

        public void SetLocal(bool isLocal)
        {
            _isLocal = isLocal;
            ApplyCursorAndCameras();
        }

        private void ApplyCursorAndCameras()
        {
            Cursor.lockState = _isLocal ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !_isLocal;

            if (_playerCamera != null) _playerCamera.enabled = _isLocal;
            if (_audioListener != null) _audioListener.enabled = _isLocal;

            if (_isLocal)
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

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && _isLocal)
            {
                ApplyCursorAndCameras();
            }
        }
    }
}
