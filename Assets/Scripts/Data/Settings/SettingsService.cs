using System;
using UnityEngine;
using Zenject;

namespace Game.Settings
{
    public interface ISettingsService
    {
        float MouseSensitivity { get; }
        void  SetMouseSensitivity(float value);
        event Action<float> OnMouseSensitivityChanged;
    }

    public sealed class SettingsService : ISettingsService, IInitializable, IDisposable
    {
        private readonly PlayerStatsSO _stats;
        private const string KEY_MOUSE = "settings.mouseSensitivity";

        public float MouseSensitivity { get; private set; }
        public event Action<float> OnMouseSensitivityChanged;

        public SettingsService(PlayerStatsSO stats) => _stats = stats;

        public void Initialize()
        {
            var d = _stats.defaultMouseLookSensitivity;
            var min = _stats.minMouseLookSensitivity;
            var max = _stats.maxMouseLookSensitivity;

            var loaded = PlayerPrefs.HasKey(KEY_MOUSE) ? PlayerPrefs.GetFloat(KEY_MOUSE, d) : d;
            MouseSensitivity = Mathf.Clamp(loaded, min, max);
        }

        public void SetMouseSensitivity(float value)
        {
            var min = _stats.minMouseLookSensitivity;
            var max = _stats.maxMouseLookSensitivity;
            var v = Mathf.Clamp(value, min, max);
            if (Mathf.Approximately(v, MouseSensitivity)) return;

            MouseSensitivity = v;
            PlayerPrefs.SetFloat(KEY_MOUSE, v);
            PlayerPrefs.Save();
            OnMouseSensitivityChanged?.Invoke(v);
        }

        public void Dispose() { }
    }
}
