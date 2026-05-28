using System;
using System.Collections.Generic;
using System.Reflection;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Unity.Rendering;
using CameraUnlock.Core.Unity.Tracking;
using UnityEngine;
using WobblyLifeHeadTracking.Config;

namespace WobblyLifeHeadTracking.Camera
{
    public sealed class WobblyLifeCameraController : MonoBehaviour
    {
        public const int MaxPlayers = 4;

        private WobblyLifeConfig _config;

        private readonly OpenTrackReceiver[] _receivers = new OpenTrackReceiver[MaxPlayers];
        private readonly float[] _headYaw = new float[MaxPlayers];
        private readonly float[] _headPitch = new float[MaxPlayers];
        private readonly float[] _headRoll = new float[MaxPlayers];
        private readonly Quaternion[] _headRotations = new Quaternion[MaxPlayers];
        private readonly Quaternion[] _yawWorldRotations = new Quaternion[MaxPlayers];
        private readonly Quaternion[] _pitchRollLocalRotations = new Quaternion[MaxPlayers];
        private readonly SmoothedEulerState[] _smoothedStates = new SmoothedEulerState[MaxPlayers];
        private readonly bool[] _receiverStarted = new bool[MaxPlayers];
        private readonly bool[] _hasTrackingData = new bool[MaxPlayers];

        private readonly PoseInterpolator[] _poseInterpolators = new PoseInterpolator[MaxPlayers];

        private readonly PositionProcessor[] _positionProcessors = new PositionProcessor[MaxPlayers];
        private readonly PositionInterpolator[] _positionInterpolators = new PositionInterpolator[MaxPlayers];
        private readonly Vec3[] _positionOffsets = new Vec3[MaxPlayers];

        public bool WorldSpaceYaw { get; set; } = true;
        public bool PositionEnabled { get; set; } = true;
        public bool RotationEnabled { get; set; } = true;

        private readonly Dictionary<int, CameraFrameState> _cameraStates = new Dictionary<int, CameraFrameState>();

        // Shared sentinel stored in _cameraStates for cameras we've classified as non-gameplay.
        // Lets OnBeginCameraRendering bail with a single dictionary lookup instead of a
        // dictionary miss followed by a separate HashSet hit per camera per render pass.
        private static readonly CameraFrameState NonGameplaySentinel = new CameraFrameState { PlayerIndex = -1 };

        private static Type _gameplayCameraType;
        private static MethodInfo _getLocalPlayerIdMethod;
        private static bool _reflectionInitialized;

        private bool _initialized;
        private bool _isHooked;

        private class CameraFrameState
        {
            public Quaternion StoredRotation;
            public Vector3 StoredWorldPosition;
            public int LastStoredFrame = -1;
            public bool TrackingApplied;
            public bool RotationModified;
            public bool PositionModified;
            public int PlayerIndex = -1;
        }

        // Cached config values, refreshed via SettingChanged subscriptions so the hot path
        // never acquires the ConfigEntry lock per frame.
        private SensitivitySettings _cachedSensitivity;
        private float _cachedSmoothing;
        private float _cachedPositionLimitY;
        private float _cachedPositionLimitYDown;

        public void Initialize(WobblyLifeConfig config)
        {
            _config = config;

            for (int i = 0; i < MaxPlayers; i++)
            {
                _headYaw[i] = 0f;
                _headPitch[i] = 0f;
                _headRoll[i] = 0f;
                _headRotations[i] = Quaternion.identity;
                _yawWorldRotations[i] = Quaternion.identity;
                _pitchRollLocalRotations[i] = Quaternion.identity;
                _smoothedStates[i] = new SmoothedEulerState();
                _hasTrackingData[i] = false;
            }

            PositionSettings posSettings = _config.PositionSettingsFromConfig;
            for (int i = 0; i < MaxPlayers; i++)
            {
                _poseInterpolators[i] = new PoseInterpolator();
                _positionProcessors[i] = new PositionProcessor
                {
                    Settings = posSettings
                };
                _positionInterpolators[i] = new PositionInterpolator();
            }

            int[] ports = _config.PlayerPorts;
            for (int i = 0; i < MaxPlayers; i++)
            {
                int port = ports[i];
                _receivers[i] = new OpenTrackReceiver();
                _receivers[i].Log = msg => WobblyLifeHeadTrackingPlugin.Log?.LogInfo(msg);

                // Start() returning false just means initial bind lost the race; the receiver's
                // own retry thread keeps trying every 5s. Leave the slot live so IsReceiving
                // picks it up the moment retry succeeds.
                _receivers[i].Start(port);
                _receiverStarted[i] = true;
                WobblyLifeHeadTrackingPlugin.Log?.LogInfo($"Player {i + 1} receiver listening on port {port}");
            }

            InitializeReflection();

            RefreshCachedConfig();
            _config.YawSensitivity.SettingChanged += OnConfigSettingChanged;
            _config.PitchSensitivity.SettingChanged += OnConfigSettingChanged;
            _config.RollSensitivity.SettingChanged += OnConfigSettingChanged;
            _config.SmoothingFactor.SettingChanged += OnConfigSettingChanged;
            _config.PositionLimitY.SettingChanged += OnConfigSettingChanged;
            _config.PositionLimitYDown.SettingChanged += OnConfigSettingChanged;

            _initialized = true;
        }

        private void OnConfigSettingChanged(object sender, System.EventArgs e)
        {
            RefreshCachedConfig();
        }

        private void RefreshCachedConfig()
        {
            _cachedSensitivity = new SensitivitySettings(
                _config.YawSensitivity.Value,
                _config.PitchSensitivity.Value,
                _config.RollSensitivity.Value);
            _cachedSmoothing = _config.SmoothingFactor.Value;
            _cachedPositionLimitY = _config.PositionLimitY.Value;
            _cachedPositionLimitYDown = _config.PositionLimitYDown.Value;
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
            return Mathf.Clamp(playerId, 0, MaxPlayers - 1);
        }

        public void UpdateTracking()
        {
            if (!_initialized) return;

            float deltaTime = Time.deltaTime;
            SensitivitySettings sensitivity = _cachedSensitivity;
            float baseSmoothing = _cachedSmoothing;

            // Local floor absorbs velocity kinks from linear interpolation between samples.
            const float BaselineSmoothing = 0.05f;
            if (baseSmoothing < BaselineSmoothing)
                baseSmoothing = BaselineSmoothing;

            bool positionEnabled = PositionEnabled;
            float posLimitYDown = positionEnabled ? _cachedPositionLimitYDown : 0f;
            float posLimitYUp = positionEnabled ? _cachedPositionLimitY : 0f;

            for (int i = 0; i < MaxPlayers; i++)
            {
                var receiver = _receivers[i];
                if (!_receiverStarted[i] || receiver == null) continue;
                if (!receiver.IsReceiving) continue;

                TrackingPose pose = receiver.GetLatestPose();

                TrackingPose interpolated = _poseInterpolators[i].Update(pose, deltaTime);
                pose = interpolated;

                if (!pose.IsValid || !pose.IsDataFresh) continue;

                TrackingPose processed = pose.ApplySensitivity(sensitivity);

                float targetYaw = processed.Yaw;
                float targetPitch = processed.Pitch;
                float targetRoll = processed.Roll;

                _smoothedStates[i].Update(targetYaw, targetPitch, targetRoll,
                    baseSmoothing, deltaTime,
                    out float sYaw, out float sPitch, out float sRoll);
                _headYaw[i] = sYaw;
                _headPitch[i] = sPitch;
                _headRoll[i] = sRoll;

                // Precompose once per frame; the render callback may fire per camera pass.
                Quaternion yawQ = Quaternion.AngleAxis(sYaw, Vector3.up);
                Quaternion pitchQ = Quaternion.AngleAxis(-sPitch, Vector3.right);
                Quaternion rollQ = Quaternion.AngleAxis(sRoll, Vector3.forward);
                _headRotations[i] = rollQ * pitchQ * yawQ;
                _yawWorldRotations[i] = yawQ;
                _pitchRollLocalRotations[i] = rollQ * pitchQ;

                _hasTrackingData[i] = true;

                if (positionEnabled && _positionProcessors[i] != null && _positionInterpolators[i] != null)
                {
                    var rawPos = receiver.GetLatestPosition();
                    var interpolatedPos = _positionInterpolators[i].Update(rawPos, deltaTime);
                    var headRotQ = QuaternionUtils.FromYawPitchRoll(processed.Yaw, -processed.Pitch, processed.Roll);
                    Vec3 posOffset = _positionProcessors[i].Process(interpolatedPos, headRotQ, deltaTime);
                    // Asymmetric Y clamp prevents camera from clipping below eye height.
                    float clampedY = Mathf.Clamp(posOffset.Y, -posLimitYDown, posLimitYUp);
                    _positionOffsets[i] = new Vec3(posOffset.X, clampedY, posOffset.Z);
                }
            }

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
                if (!cam.name.Contains("GameplayCamera"))
                {
                    _cameraStates[camId] = NonGameplaySentinel;
                    return;
                }

                state = new CameraFrameState();
                state.PlayerIndex = GetPlayerIndexForCamera(cam);
                _cameraStates[camId] = state;

                if (state.PlayerIndex >= 0)
                {
                    WobblyLifeHeadTrackingPlugin.Log?.LogInfo($"Camera {cam.name} assigned to player {state.PlayerIndex + 1}");
                }
            }

            int playerIndex = state.PlayerIndex;
            if (playerIndex < 0) return;
            if (!_hasTrackingData[playerIndex]) return;

            int frameCount = Time.frameCount;
            var camTransform = cam.transform;

            // Store the game's transform once per frame because OnBegin can fire multiple
            // times per camera (shadows, reflections) and we must restore the clean value.
            if (state.LastStoredFrame != frameCount)
            {
                state.StoredRotation = camTransform.rotation;
                state.StoredWorldPosition = camTransform.position;
                state.LastStoredFrame = frameCount;
                state.TrackingApplied = false;
                state.RotationModified = false;
                state.PositionModified = false;
            }

            if (state.TrackingApplied) return;

            if (PositionEnabled)
            {
                Vector3 worldOffset = PositionApplicator.ToHorizonLockedWorld(
                    _positionOffsets[playerIndex], state.StoredRotation);
                camTransform.position = state.StoredWorldPosition + worldOffset;
                state.PositionModified = true;
            }

            if (RotationEnabled)
            {
                if (WorldSpaceYaw)
                {
                    // Yaw around world up first (horizon-locked), then pitch+roll camera-local.
                    camTransform.rotation = _yawWorldRotations[playerIndex] * state.StoredRotation * _pitchRollLocalRotations[playerIndex];
                }
                else
                {
                    camTransform.rotation = state.StoredRotation * _headRotations[playerIndex];
                }
                state.RotationModified = true;
            }
            state.TrackingApplied = true;
        }

        private void OnEndCameraRendering(UnityEngine.Camera cam)
        {
            if (!_initialized) return;

            int camId = cam.GetInstanceID();
            if (!_cameraStates.TryGetValue(camId, out var state)) return;

            // Restore before game logic reads transform, so aim/physics/raycasts see clean state.
            // Only touch the components we actually modified - skips a native transform write
            // per camera per pass when only one of rotation/position is enabled.
            if (state.LastStoredFrame == Time.frameCount && state.TrackingApplied)
            {
                var camTransform = cam.transform;
                if (state.RotationModified)
                {
                    camTransform.rotation = state.StoredRotation;
                    state.RotationModified = false;
                }
                if (state.PositionModified)
                {
                    camTransform.position = state.StoredWorldPosition;
                    state.PositionModified = false;
                }
            }
        }

        public void ResetRotation()
        {
            for (int i = 0; i < MaxPlayers; i++)
            {
                _headYaw[i] = 0f;
                _headPitch[i] = 0f;
                _headRoll[i] = 0f;
                _headRotations[i] = Quaternion.identity;
                _yawWorldRotations[i] = Quaternion.identity;
                _pitchRollLocalRotations[i] = Quaternion.identity;
                _smoothedStates[i]?.Reset();
                _poseInterpolators[i]?.Reset();
                _hasTrackingData[i] = false;
                _positionProcessors[i]?.Reset();
                _positionInterpolators[i]?.Reset();
                _positionOffsets[i] = Vec3.Zero;
            }
        }

        public void InvalidateCamera()
        {
            _cameraStates.Clear();
            ResetRotation();
        }

        private bool IsPlayerReceiving(int playerIndex)
        {
            return _receiverStarted[playerIndex]
                && _receivers[playerIndex] != null
                && _receivers[playerIndex].IsReceiving;
        }

        public void RecenterAll()
        {
            for (int i = 0; i < MaxPlayers; i++)
            {
                if (IsPlayerReceiving(i))
                {
                    _receivers[i].Recenter();
                    _smoothedStates[i]?.Reset();
                    _poseInterpolators[i]?.Reset();
                    _positionProcessors[i]?.SetCenter(_receivers[i].GetLatestPosition());
                    _positionInterpolators[i]?.Reset();
                    WobblyLifeHeadTrackingPlugin.Log?.LogInfo($"Player {i + 1} view recentered");
                }
            }
        }

        public bool IsAnyPlayerReceiving()
        {
            for (int i = 0; i < MaxPlayers; i++)
            {
                if (IsPlayerReceiving(i))
                    return true;
            }
            return false;
        }

        public string GetConnectionStatus()
        {
            var connected = new List<int>();
            for (int i = 0; i < MaxPlayers; i++)
            {
                if (IsPlayerReceiving(i))
                    connected.Add(i + 1);
            }
            return connected.Count > 0 ? $"Players {string.Join(", ", connected)} connected" : "No players connected";
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
                _config.YawSensitivity.SettingChanged -= OnConfigSettingChanged;
                _config.PitchSensitivity.SettingChanged -= OnConfigSettingChanged;
                _config.RollSensitivity.SettingChanged -= OnConfigSettingChanged;
                _config.SmoothingFactor.SettingChanged -= OnConfigSettingChanged;
                _config.PositionLimitY.SettingChanged -= OnConfigSettingChanged;
                _config.PositionLimitYDown.SettingChanged -= OnConfigSettingChanged;
            }

            for (int i = 0; i < MaxPlayers; i++)
            {
                _receivers[i]?.Dispose();
            }
        }
    }
}
