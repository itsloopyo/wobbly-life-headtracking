using System;
using System.Collections.Generic;
using System.Reflection;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Tracking;
using CameraUnlock.Core.Unity.Rendering;
using CameraUnlock.Core.Unity.Tracking;
using UnityEngine;
using WobblyLifeHeadTracking.Config;

namespace WobblyLifeHeadTracking.Camera
{
    public sealed class WobblyLifeCameraController : MonoBehaviour
    {
        private WobblyLifeConfig _config;
        private MultiPlayerTrackingManager _tracking;

        public bool WorldSpaceYaw { get; set; } = true;

        public MultiPlayerTrackingManager Tracking => _tracking;

        private sealed class CameraState
        {
            public readonly TransformFrameState FrameState = new TransformFrameState();
            public int PlayerIndex = -1;
        }

        private readonly Dictionary<int, CameraState> _cameraStates = new Dictionary<int, CameraState>();

        // Shared sentinel stored in _cameraStates for cameras we've classified as non-gameplay.
        // Lets OnBeginCameraRendering bail with a single dictionary lookup per camera per render pass.
        private static readonly CameraState NonGameplaySentinel = new CameraState();

        private static Type _gameplayCameraType;
        private static MethodInfo _getLocalPlayerIdMethod;
        private static bool _reflectionInitialized;

        private bool _initialized;
        private bool _isHooked;

        public void Initialize(WobblyLifeConfig config)
        {
            _config = config;

            _tracking = new MultiPlayerTrackingManager(_config.PlayerPorts)
            {
                Log = msg => WobblyLifeHeadTrackingPlugin.Log?.LogInfo(msg)
            };
            ApplyTrackingSettings();
            _tracking.Start();

            InitializeReflection();

            // Sensitivity/smoothing/position settings are pushed into the tracking manager,
            // so re-push whenever any config value changes (changes are rare; re-applying
            // everything is cheaper than tracking which entry changed).
            _config.File.SettingChanged += OnConfigSettingChanged;

            _initialized = true;
        }

        private void OnConfigSettingChanged(object sender, BepInEx.Configuration.SettingChangedEventArgs e)
        {
            ApplyTrackingSettings();
        }

        private void ApplyTrackingSettings()
        {
            _tracking.ApplySensitivity(_config.Sensitivity);
            // Both values go to every player's processor; the manager selects between
            // them per player from that player's receiver connection locality.
            _tracking.ApplySmoothing(_config.LocalSmoothing.Value, _config.RemoteSmoothing.Value);
            _tracking.ApplyPositionSettings(_config.PositionSettingsFromConfig);
        }

        private void InitializeReflection()
        {
            if (_reflectionInitialized) return;
            _reflectionInitialized = true;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                _gameplayCameraType = assembly.GetType("GameplayCamera");
                if (_gameplayCameraType != null) break;
            }

            if (_gameplayCameraType != null)
            {
                _getLocalPlayerIdMethod = _gameplayCameraType.GetMethod("GetLocalPlayerid", BindingFlags.Public | BindingFlags.Instance);
                if (_getLocalPlayerIdMethod != null)
                {
                    WobblyLifeHeadTrackingPlugin.Log?.LogInfo("GameplayCamera reflection initialized - multiplayer support enabled");
                }
            }

            if (_getLocalPlayerIdMethod == null)
            {
                WobblyLifeHeadTrackingPlugin.Log?.LogWarning("Could not find GameplayCamera.GetLocalPlayerid - multiplayer assignment unavailable, will use player 1 for all gameplay cameras");
            }
        }

        private int GetPlayerIndexForCamera(UnityEngine.Camera cam)
        {
            if (_gameplayCameraType == null)
            {
                return cam.name.Contains("GameplayCamera") ? 0 : -1;
            }

            var gameplayCamera = cam.GetComponent(_gameplayCameraType);
            if (gameplayCamera == null)
                return -1;

            if (_getLocalPlayerIdMethod == null)
                return 0;

            object result = _getLocalPlayerIdMethod.Invoke(gameplayCamera, null);
            if (!(result is int playerId)) return 0;
            return Mathf.Clamp(playerId, 0, _tracking.PlayerCount - 1);
        }

        public void UpdateTracking()
        {
            if (!_initialized) return;

            _tracking.Update(Time.deltaTime);

            if (!_isHooked)
            {
                RenderPipelineHelper.RegisterCallbacks(OnBeginCameraRendering, OnEndCameraRendering);
                _isHooked = true;
                WobblyLifeHeadTrackingPlugin.Log?.LogInfo($"Registered render callbacks (Pipeline: {(RenderPipelineHelper.IsSRP ? "SRP" : "Legacy")})");
            }
        }

        private void OnBeginCameraRendering(UnityEngine.Camera cam)
        {
            if (!_initialized) return;
            int camId = cam.GetInstanceID();

            if (!_cameraStates.TryGetValue(camId, out var state))
            {
                int assignedIndex = GetPlayerIndexForCamera(cam);
                if (assignedIndex < 0)
                {
                    _cameraStates[camId] = NonGameplaySentinel;
                    return;
                }

                state = new CameraState { PlayerIndex = assignedIndex };
                _cameraStates[camId] = state;

                WobblyLifeHeadTrackingPlugin.Log?.LogInfo($"Camera {cam.name} assigned to player {assignedIndex + 1}");
            }

            int playerIndex = state.PlayerIndex;
            if (playerIndex < 0) return;
            if (!_tracking.HasPose(playerIndex)) return;

            var camTransform = cam.transform;
            if (!state.FrameState.BeginFrame(camTransform, Time.frameCount)) return;

            HeadTrackingSession session = _tracking.GetSession(playerIndex);

            if (session.PositionActive)
            {
                Vector3 worldOffset = PositionApplicator.ToHorizonLockedWorld(
                    session.PositionOffset, state.FrameState.StoredRotation);
                state.FrameState.SetPosition(camTransform, state.FrameState.StoredPosition + worldOffset);
            }

            if (session.RotationActive)
            {
                TrackingPose head = session.Rotation;
                Quaternion tracked = WorldSpaceYaw
                    ? CameraRotationComposer.ComposeAdditive(state.FrameState.StoredRotation, head.Yaw, head.Pitch, head.Roll)
                    : state.FrameState.StoredRotation * CameraRotationComposer.GetTrackingOnlyRotation(head.Yaw, -head.Pitch, head.Roll);
                state.FrameState.SetRotation(camTransform, tracked);
            }
        }

        private void OnEndCameraRendering(UnityEngine.Camera cam)
        {
            if (!_initialized) return;

            int camId = cam.GetInstanceID();
            if (!_cameraStates.TryGetValue(camId, out var state)) return;
            if (state.PlayerIndex < 0) return;

            // Restore before game logic reads the transform, so aim/physics/raycasts see clean state.
            state.FrameState.Restore(cam.transform, Time.frameCount);
        }

        public void ResetTracking()
        {
            if (!_initialized) return;
            _tracking.Reset();
        }

        public void InvalidateCamera()
        {
            _cameraStates.Clear();
            ResetTracking();
        }

        public void RecenterAll()
        {
            _tracking.Recenter();
        }

        public bool IsAnyPlayerReceiving()
        {
            return _initialized && _tracking.IsAnyReceiving;
        }

        public string GetConnectionStatus()
        {
            return _tracking.GetConnectionStatus();
        }

        private void OnDestroy()
        {
            if (_isHooked)
            {
                RenderPipelineHelper.UnregisterCallbacks();
                _isHooked = false;
            }

            if (_config != null)
            {
                _config.File.SettingChanged -= OnConfigSettingChanged;
            }

            _tracking?.Dispose();
        }
    }
}
