using BepInEx;
using BepInEx.Logging;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Protocol;
using UnityEngine;
using WobblyLifeHeadTracking.Camera;
using WobblyLifeHeadTracking.Config;
using WobblyLifeHeadTracking.State;

namespace WobblyLifeHeadTracking
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Wobbly Life.exe")]
    public class WobblyLifeHeadTrackingPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.cameraunlock.wobblylife.headtracking";
        public const string PluginName = "Wobbly Life Head Tracking";
        public const string PluginVersion = "0.0.0";

        internal static ManualLogSource Log { get; private set; }

        private WobblyLifeConfig _config;
        private WobblyLifeCameraController _cameraController;
        private WobblyLifeGameStateDetector _gameStateDetector;
        private bool _trackingEnabled;
        private bool _wasConnected;
        private bool _wasTrackingAllowed;
        private int _stabilizationFramesRemaining;
        private const int StabilizationFrameCount = 5;

        private enum TrackingMode { Normal, RotationOnly, PositionOnly }
        private TrackingMode _trackingMode = TrackingMode.Normal;

        private void Awake()
        {
            Log = Logger;

            _config = new WobblyLifeConfig(Config);
            _trackingEnabled = _config.EnableOnStartup;

            _cameraController = gameObject.AddComponent<WobblyLifeCameraController>();
            _cameraController.Initialize(_config);
            _cameraController.WorldSpaceYaw = _config.WorldSpaceYaw.Value;

            _gameStateDetector = new WobblyLifeGameStateDetector(_config, Logger.LogInfo);
            _gameStateDetector.OnGameplayStateChanged += OnGameplayStateChanged;
            _gameStateDetector.OnPauseStateChanged += OnPauseStateChanged;
            _wasTrackingAllowed = _gameStateDetector.IsInGameplay;

            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded");
            var ports = _config.PlayerPorts;
            Logger.LogInfo($"Multiplayer head tracking: Player 1=port {ports[0]}, Player 2={ports[1]}, Player 3={ports[2]}, Player 4={ports[3]}");
            Logger.LogInfo($"Head tracking is {(_trackingEnabled ? "enabled" : "disabled")} on startup");
            Logger.LogInfo($"Controls: Toggle=[{_config.ToggleKeyName}], Recenter=[{_config.RecenterKeyName}]");
        }

        private void Update()
        {
            HandleKeyBinds();
            HandleConnectionStateChange();
            HandleTrackingAllowedStateChange();
        }

        private void HandleKeyBinds()
        {
            bool chord = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

            if (HotkeyPressed(_config.RecenterKeyEntry.Value, chord, KeyCode.T)) RecenterView();
            if (HotkeyPressed(_config.ToggleKeyEntry.Value, chord, KeyCode.Y)) ToggleTracking();
            if (HotkeyPressed(_config.PositionToggleKeyEntry.Value, chord, KeyCode.G)) CycleTrackingMode();
            if (HotkeyPressed(_config.YawModeKeyEntry.Value, chord, KeyCode.H)) ToggleYawMode();
        }

        private static bool HotkeyPressed(KeyCode primary, bool chordActive, KeyCode chordKey)
        {
            return Input.GetKeyDown(primary) || (chordActive && Input.GetKeyDown(chordKey));
        }

        public void ToggleYawMode()
        {
            bool newMode = !_cameraController.WorldSpaceYaw;
            _cameraController.WorldSpaceYaw = newMode;
            Logger.LogInfo($"Yaw mode: {(newMode ? "world-space (horizon-locked)" : "camera-local")}");
        }

        private void CycleTrackingMode()
        {
            switch (_trackingMode)
            {
                case TrackingMode.Normal:
                    _trackingMode = TrackingMode.RotationOnly;
                    _cameraController.PositionEnabled = false;
                    _cameraController.RotationEnabled = true;
                    Logger.LogInfo("Tracking mode: rotation only (position disabled)");
                    break;
                case TrackingMode.RotationOnly:
                    _trackingMode = TrackingMode.PositionOnly;
                    _cameraController.PositionEnabled = true;
                    _cameraController.RotationEnabled = false;
                    Logger.LogInfo("Tracking mode: position only (rotation disabled)");
                    break;
                case TrackingMode.PositionOnly:
                default:
                    _trackingMode = TrackingMode.Normal;
                    _cameraController.PositionEnabled = true;
                    _cameraController.RotationEnabled = true;
                    Logger.LogInfo("Tracking mode: normal (rotation and position enabled)");
                    break;
            }
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
                _stabilizationFramesRemaining = StabilizationFrameCount;
            }
            else if (!isConnected && _wasConnected)
            {
                Logger.LogInfo("Head tracking disconnected - holding last pose");
            }

            if (isConnected && _stabilizationFramesRemaining > 0)
            {
                _stabilizationFramesRemaining--;
                if (_stabilizationFramesRemaining == 0)
                {
                    _cameraController.RecenterAll();
                    Logger.LogInfo("Auto-recentered after stabilization");
                }
            }

            _wasConnected = isConnected;
        }

        private void HandleTrackingAllowedStateChange()
        {
            bool isTrackingAllowed = _gameStateDetector.IsInGameplay;

            if (!isTrackingAllowed && _wasTrackingAllowed)
            {
                _cameraController.ResetRotation();
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
                _cameraController.ResetRotation();
                _cameraController.InvalidateCamera();
            }
        }

        private void OnPauseStateChanged(bool isPaused)
        {
            if (isPaused && _config.DisableWhenPaused.Value)
            {
                _cameraController.ResetRotation();
            }
        }

        private void OnDestroy()
        {
            if (_gameStateDetector != null)
            {
                _gameStateDetector.OnGameplayStateChanged -= OnGameplayStateChanged;
                _gameStateDetector.OnPauseStateChanged -= OnPauseStateChanged;
                _gameStateDetector.Dispose();
            }

            if (_cameraController != null)
            {
                _cameraController.ResetRotation();
            }

            Logger.LogInfo($"{PluginName} unloaded");
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
                _cameraController.ResetRotation();
            }

            Logger.LogInfo($"Head tracking {(enabled ? "enabled" : "disabled")}");
        }

        public void ToggleTracking()
        {
            SetTrackingEnabled(!_trackingEnabled);
        }

        public void RecenterView()
        {
            if (_cameraController != null && _cameraController.IsAnyPlayerReceiving())
            {
                _cameraController.RecenterAll();
            }
            else
            {
                Logger.LogWarning("Cannot recenter - no tracking data available");
            }
        }
    }
}
