using BepInEx.Logging;
using CameraUnlock.Core.Input;
using CameraUnlock.Core.Tracking;
using CameraUnlock.Core.Unity.Extensions;
using CameraUnlock.Core.Unity.State;
using UnityEngine;
using WobblyLifeHeadTracking.Camera;
using WobblyLifeHeadTracking.Config;

namespace WobblyLifeHeadTracking
{
    public sealed class WobblyLifeHeadTrackingRuntime : MonoBehaviour
    {
        private static ManualLogSource Logger => WobblyLifeHeadTrackingPlugin.Log;

        private WobblyLifeSettings _config;
        private KeyBinding[] _toggleKeys;
        private KeyBinding[] _cycleTrackingModeKeys;
        private KeyBinding[] _yawModeKeys;
        private WobblyLifeCameraController _cameraController;
        private SceneGameStateDetector _gameStateDetector;
        private bool _trackingEnabled;
        private bool _wasConnected;
        private bool _wasTrackingAllowed;

        private void Awake()
        {
            _config = SettingsStore.Load();
            _trackingEnabled = _config.EnableOnStartup;
            _toggleKeys = SettingsStore.Hotkeys("ToggleKey", _config.ToggleKeyName);
            _cycleTrackingModeKeys = SettingsStore.Hotkeys("CycleTrackingModeKey", _config.CycleTrackingModeKeyName);
            _yawModeKeys = SettingsStore.Hotkeys("YawModeKey", _config.YawModeKeyName);

            _cameraController = gameObject.AddComponent<WobblyLifeCameraController>();
            _cameraController.Initialize(_config);
            _cameraController.WorldSpaceYaw = _config.WorldSpaceYaw;
            // The table reads a pair that names no mode (both off) as its defaults, so the pair
            // always decodes.
            _cameraController.Tracking.Mode = TrackingModeChannels.Decode(_config.RotationEnabled, _config.PositionEnabled).Value;

            _gameStateDetector = new SceneGameStateDetector(log: Logger.LogInfo)
            {
                DisableInMenuScenes = _config.DisableInMenus,
                DisableWhenPaused = _config.DisableWhenPaused
            };
            _gameStateDetector.GameplayStateChanged += OnGameplayStateChanged;
            _wasTrackingAllowed = _gameStateDetector.IsInGameplay;

            Logger.LogInfo($"{WobblyLifeHeadTrackingPlugin.PluginName} v{WobblyLifeHeadTrackingPlugin.PluginVersion} loaded");
            var ports = _config.PlayerPorts;
            Logger.LogInfo($"Multiplayer head tracking: Player 1=port {ports[0]}, Player 2={ports[1]}, Player 3={ports[2]}, Player 4={ports[3]}");
            Logger.LogInfo($"Head tracking is {(_trackingEnabled ? "enabled" : "disabled")} on startup");
            Logger.LogInfo($"Controls: Toggle=[{_config.ToggleKeyName}], CycleMode=[{_config.CycleTrackingModeKeyName}], YawMode=[{_config.YawModeKeyName}]");
        }

        private void Update()
        {
            HandleKeyBinds();
            HandleConnectionStateChange();
            HandleTrackingAllowedStateChange();
        }

        private void HandleKeyBinds()
        {
            if (KeyBindingInput.IsTriggered(_toggleKeys)) ToggleTracking();
            if (KeyBindingInput.IsTriggered(_cycleTrackingModeKeys)) CycleTrackingMode();
            if (KeyBindingInput.IsTriggered(_yawModeKeys)) ToggleYawMode();
        }

        public void ToggleYawMode()
        {
            bool newMode = !_cameraController.WorldSpaceYaw;
            _cameraController.WorldSpaceYaw = newMode;
            Logger.LogInfo($"Yaw mode: {(newMode ? "world-space (horizon-locked)" : "camera-local")}");
            SettingsStore.Save(c => c.WorldSpaceYaw = newMode);
        }

        private void CycleTrackingMode()
        {
            TrackingMode mode = _cameraController.Tracking.CycleMode();
            Logger.LogInfo($"Tracking mode: {mode.Description()}");
            bool rotation, position;
            TrackingModeChannels.Encode(mode, out rotation, out position);
            SettingsStore.Save(c =>
            {
                c.RotationEnabled = rotation;
                c.PositionEnabled = position;
            });
        }

        private void LateUpdate()
        {
            if (!_trackingEnabled) return;
            if (!_wasTrackingAllowed) return;

            _cameraController.UpdateTracking();
        }

        private void HandleConnectionStateChange()
        {
            bool isConnected = _cameraController.IsAnyPlayerReceiving();

            if (isConnected && !_wasConnected)
            {
                Logger.LogInfo($"Head tracking connected - {_cameraController.GetConnectionStatus()}");
            }
            else if (!isConnected && _wasConnected)
            {
                Logger.LogInfo("Head tracking disconnected - holding last pose");
            }

            _wasConnected = isConnected;
        }

        private void HandleTrackingAllowedStateChange()
        {
            bool isTrackingAllowed = _gameStateDetector.IsInGameplay;

            if (!isTrackingAllowed && _wasTrackingAllowed)
            {
                _cameraController.ResetTracking();
            }
            else if (isTrackingAllowed && !_wasTrackingAllowed)
            {
                _cameraController.InvalidateCamera();
            }

            _wasTrackingAllowed = isTrackingAllowed;
        }

        private void OnGameplayStateChanged(bool isGameplay)
        {
            if (!isGameplay && _config.DisableInMenus)
            {
                _cameraController.ResetTracking();
                _cameraController.InvalidateCamera();
            }
        }

        private void OnDestroy()
        {
            if (_gameStateDetector != null)
            {
                _gameStateDetector.GameplayStateChanged -= OnGameplayStateChanged;
                _gameStateDetector.Dispose();
            }

            if (_cameraController != null)
            {
                _cameraController.ResetTracking();
            }

            Logger.LogInfo($"{WobblyLifeHeadTrackingPlugin.PluginName} unloaded");
        }

        public bool IsTrackingEnabled => _trackingEnabled;

        public bool IsConnected => _cameraController?.IsAnyPlayerReceiving() ?? false;

        public bool IsTrackingAllowed => _gameStateDetector?.IsInGameplay ?? true;

        /// <summary>Turns head tracking on or off for this session. Never saved.</summary>
        public void SetTrackingEnabled(bool enabled)
        {
            if (_trackingEnabled == enabled) return;

            _trackingEnabled = enabled;

            if (!enabled)
            {
                _cameraController.ResetTracking();
            }

            Logger.LogInfo($"Head tracking {(enabled ? "enabled" : "disabled")}");
        }

        public void ToggleTracking()
        {
            SetTrackingEnabled(!_trackingEnabled);
        }
    }
}
