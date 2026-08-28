using BepInEx.Logging;
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

        private WobblyLifeConfig _config;
        private WobblyLifeCameraController _cameraController;
        private SceneGameStateDetector _gameStateDetector;
        private bool _trackingEnabled;
        private bool _wasConnected;
        private bool _wasTrackingAllowed;

        private void Awake()
        {
            _config = new WobblyLifeConfig(WobblyLifeHeadTrackingPlugin.ConfigFile);
            _trackingEnabled = _config.EnableOnStartup.Value;

            _cameraController = gameObject.AddComponent<WobblyLifeCameraController>();
            _cameraController.Initialize(_config);
            _cameraController.WorldSpaceYaw = _config.WorldSpaceYaw.Value;

            _gameStateDetector = new SceneGameStateDetector(log: Logger.LogInfo)
            {
                DisableInMenuScenes = _config.DisableInMenus.Value,
                DisableWhenPaused = _config.DisableWhenPaused.Value
            };
            _gameStateDetector.GameplayStateChanged += OnGameplayStateChanged;
            _config.File.SettingChanged += OnConfigSettingChanged;
            _wasTrackingAllowed = _gameStateDetector.IsInGameplay;

            Logger.LogInfo($"{WobblyLifeHeadTrackingPlugin.PluginName} v{WobblyLifeHeadTrackingPlugin.PluginVersion} loaded");
            var ports = _config.PlayerPorts;
            Logger.LogInfo($"Multiplayer head tracking: Player 1=port {ports[0]}, Player 2={ports[1]}, Player 3={ports[2]}, Player 4={ports[3]}");
            Logger.LogInfo($"Head tracking is {(_trackingEnabled ? "enabled" : "disabled")} on startup");
            Logger.LogInfo($"Controls: Toggle=[{_config.ToggleKey.Value}], CycleMode=[{_config.PositionToggleKey.Value}], YawMode=[{_config.YawModeKey.Value}]");
        }

        private void Update()
        {
            HandleKeyBinds();
            HandleConnectionStateChange();
            HandleTrackingAllowedStateChange();
        }

        private void HandleKeyBinds()
        {
            if (ChordHotkeys.IsActionPressed(_config.ToggleKey.Value, ChordHotkeys.ToggleLetter)) ToggleTracking();
            if (ChordHotkeys.IsActionPressed(_config.PositionToggleKey.Value, ChordHotkeys.PositionLetter)) CycleTrackingMode();
            if (ChordHotkeys.IsActionPressed(_config.YawModeKey.Value, ChordHotkeys.FourthToggleLetter)) ToggleYawMode();
        }

        public void ToggleYawMode()
        {
            bool newMode = !_cameraController.WorldSpaceYaw;
            _cameraController.WorldSpaceYaw = newMode;
            Logger.LogInfo($"Yaw mode: {(newMode ? "world-space (horizon-locked)" : "camera-local")}");
        }

        private void CycleTrackingMode()
        {
            TrackingMode mode = _cameraController.Tracking.CycleMode();
            Logger.LogInfo($"Tracking mode: {mode.Description()}");
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
            if (!isGameplay && _config.DisableInMenus.Value)
            {
                _cameraController.ResetTracking();
                _cameraController.InvalidateCamera();
            }
        }

        private void OnConfigSettingChanged(object sender, BepInEx.Configuration.SettingChangedEventArgs e)
        {
            _gameStateDetector.DisableInMenuScenes = _config.DisableInMenus.Value;
            _gameStateDetector.DisableWhenPaused = _config.DisableWhenPaused.Value;
        }

        private void OnDestroy()
        {
            if (_config != null)
            {
                _config.File.SettingChanged -= OnConfigSettingChanged;
            }

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
