using CameraUnlock.Core.Data;
using CameraUnlock.Core.Tracking;
using CameraUnlock.Core.Unity.Tracking;
using UnityEngine;
using WobblyLifeHeadTracking.Config;

namespace WobblyLifeHeadTracking.Camera
{
    /// <summary>
    /// Owns the tracking pipeline and keeps a <see cref="GameplayCameraRenderHook"/>
    /// attached to every gameplay camera in the scene.
    ///
    /// The Mono build subscribes to Camera.onPreCull once and filters there. That hook
    /// is a static delegate field of an Il2CppSystem type on the IL2CPP side and
    /// cannot be subscribed from compiled IL, so this build puts an injected
    /// MonoBehaviour on each gameplay camera instead and lets Unity call its
    /// OnPreCull/OnPostRender directly. The frame shape is the same either way:
    /// tracking goes on before the camera renders and comes off after.
    /// </summary>
    public sealed class WobblyLifeCameraController : MonoBehaviour
    {
        private const int CameraScanIntervalFrames = 30;

        private WobblyLifeConfig _config;
        private MultiPlayerTrackingManager _tracking;

        private int _nextScanFrame;
        private bool _initialized;
        private bool _loggedFirstApply;

        public bool WorldSpaceYaw { get; set; } = true;

        /// <summary>
        /// Whether the render hooks should apply anything. The Mono build gates by
        /// simply not calling into the controller; here the hooks fire off Unity's own
        /// camera callbacks, so the gate has to be a flag they can read.
        /// </summary>
        public bool IsTrackingActive { get; set; }

        public MultiPlayerTrackingManager Tracking => _tracking;

        // The render hooks reach the controller through this rather than being handed
        // it. Both types are registered in the IL2CPP domain, and ClassInjector cannot
        // build a trampoline for a method that takes another injected type - it wants
        // an IntPtr constructor the reference assembly's MonoBehaviour does not have,
        // and the whole registration fails with ArgumentNullException(con). There is
        // exactly one controller, on the plugin's host object.
        internal static WobblyLifeCameraController Instance { get; private set; }

        public void Initialize(WobblyLifeConfig config)
        {
            Instance = this;
            _config = config;

            _tracking = new MultiPlayerTrackingManager(_config.PlayerPorts)
            {
                Log = msg => WobblyLifeHeadTrackingPlugin.Log?.LogInfo(msg)
            };
            ApplyTrackingSettings();
            GameTypes.Resolve(msg => WobblyLifeHeadTrackingPlugin.Log?.LogInfo(msg));
            _tracking.Start();

            // Sensitivity/smoothing/position settings are pushed into the tracking
            // manager, so re-push whenever any config value changes (changes are rare;
            // re-applying everything is cheaper than tracking which entry changed).
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

        public void UpdateTracking()
        {
            if (!_initialized) return;

            _tracking.Update(Time.deltaTime);

            if (Time.frameCount >= _nextScanFrame)
            {
                _nextScanFrame = Time.frameCount + CameraScanIntervalFrames;
                AttachHooks();
            }
        }

        private void AttachHooks()
        {
            foreach (object entry in GameTypes.AllCameras())
            {
                var cam = entry as UnityEngine.Camera;
                if (cam == null) continue;

                var hook = cam.gameObject.GetComponent<GameplayCameraRenderHook>();
                int playerIndex = GameTypes.GetPlayerIndex(cam);
                if (playerIndex < 0)
                {
                    if (hook != null) hook.Bind(-1);
                    continue;
                }

                playerIndex = Mathf.Clamp(playerIndex, 0, _tracking.PlayerCount - 1);

                bool added = hook == null;
                if (added) hook = cam.gameObject.AddComponent<GameplayCameraRenderHook>();
                hook.Bind(playerIndex);
                if (added) WobblyLifeHeadTrackingPlugin.Log?.LogInfo(
                    $"Camera {cam.name} assigned to player {playerIndex + 1}");
            }
        }

        /// <summary>
        /// Writes this player's tracked pose onto the camera transform, saving the
        /// game's clean pose in <paramref name="frameState"/> first. Called from the
        /// camera's own OnPreCull.
        /// </summary>
        internal void ApplyTracking(TransformFrameState frameState, Transform camTransform, int playerIndex)
        {
            if (!_initialized) return;
            if (!_tracking.HasPose(playerIndex)) return;
            if (!frameState.BeginFrame(camTransform, Time.frameCount)) return;

            HeadTrackingSession session = _tracking.GetSession(playerIndex);

            if (!_loggedFirstApply)
            {
                _loggedFirstApply = true;
                // Proof that Unity is dispatching OnPreCull to the injected hook. On
                // IL2CPP that is not a given - a magic method the class injector failed
                // to register is silently never called, which presents as "the mod
                // loaded and nothing happens" with nothing else in the log to go on.
                WobblyLifeHeadTrackingPlugin.Log?.LogInfo(
                    $"Applying head tracking to player {playerIndex + 1}'s camera " +
                    $"(rotation {(session.RotationActive ? "on" : "off")}, " +
                    $"position {(session.PositionActive ? "on" : "off")})");
            }

            if (session.PositionActive)
            {
                Vector3 worldOffset = PositionApplicator.ToHorizonLockedWorld(
                    session.PositionOffset, frameState.StoredRotation);
                frameState.SetPosition(camTransform, frameState.StoredPosition + worldOffset);
            }

            if (session.RotationActive)
            {
                TrackingPose head = session.Rotation;
                Quaternion tracked = WorldSpaceYaw
                    ? CameraRotationComposer.ComposeAdditive(frameState.StoredRotation, head.Yaw, head.Pitch, head.Roll)
                    : frameState.StoredRotation * CameraRotationComposer.GetTrackingOnlyRotation(head.Yaw, -head.Pitch, head.Roll);
                frameState.SetRotation(camTransform, tracked);
            }
        }

        public void ResetTracking()
        {
            if (!_initialized) return;
            _tracking.Reset();
        }

        /// <summary>
        /// Reclassifies cameras on the next update, including cameras that survived a scene change.
        /// </summary>
        public void InvalidateCamera()
        {
            _nextScanFrame = 0;
            ResetTracking();
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
            if (_config != null)
            {
                _config.File.SettingChanged -= OnConfigSettingChanged;
            }

            if (Instance == this)
            {
                Instance = null;
            }

            _tracking?.Dispose();
        }
    }
}
