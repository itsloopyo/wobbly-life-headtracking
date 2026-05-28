using BepInEx.Configuration;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Data;
using UnityEngine;

namespace WobblyLifeHeadTracking.Config
{
    public sealed class WobblyLifeConfig : IHeadTrackingConfig
    {
        private static readonly float[] ReticleColorRgbaValue = { 1f, 1f, 1f, 1f };

        public ConfigEntry<int> Player1Port { get; }
        public ConfigEntry<int> Player2Port { get; }
        public ConfigEntry<int> Player3Port { get; }
        public ConfigEntry<int> Player4Port { get; }

        private readonly int[] _playerPorts;
        public int[] PlayerPorts => _playerPorts;

        public ConfigEntry<float> YawSensitivity { get; }
        public ConfigEntry<float> PitchSensitivity { get; }
        public ConfigEntry<float> RollSensitivity { get; }

        public ConfigEntry<float> SmoothingFactor { get; }

        public ConfigEntry<bool> EnableOnStartupEntry { get; }
        public ConfigEntry<KeyCode> ToggleKeyEntry { get; }
        public ConfigEntry<KeyCode> RecenterKeyEntry { get; }
        public ConfigEntry<KeyCode> PositionToggleKeyEntry { get; }
        public ConfigEntry<KeyCode> YawModeKeyEntry { get; }

        public ConfigEntry<bool> WorldSpaceYaw { get; }

        public ConfigEntry<float> PositionSensitivityX { get; }
        public ConfigEntry<float> PositionSensitivityY { get; }
        public ConfigEntry<float> PositionSensitivityZ { get; }
        public ConfigEntry<float> PositionLimitX { get; }
        public ConfigEntry<float> PositionLimitY { get; }
        public ConfigEntry<float> PositionLimitYDown { get; }
        public ConfigEntry<float> PositionLimitZ { get; }
        public ConfigEntry<float> PositionSmoothing { get; }

        public ConfigEntry<bool> DisableInMenus { get; }
        public ConfigEntry<bool> DisableWhenPaused { get; }


        public WobblyLifeConfig(ConfigFile config)
        {
            ConfigEntry<int> BindPort(string key, int defaultPort, string description) =>
                config.Bind("Network", key, defaultPort,
                    new ConfigDescription(description, new AcceptableValueRange<int>(1024, 65535)));

            ConfigEntry<float> BindFloat(string section, string key, float defaultValue, float min, float max, string description) =>
                config.Bind(section, key, defaultValue,
                    new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

            Player1Port = BindPort("Player1Port", 4242, "UDP port for Player 1's OpenTrack data");
            Player2Port = BindPort("Player2Port", 4243, "UDP port for Player 2's OpenTrack data");
            Player3Port = BindPort("Player3Port", 4244, "UDP port for Player 3's OpenTrack data");
            Player4Port = BindPort("Player4Port", 4245, "UDP port for Player 4's OpenTrack data");

            YawSensitivity   = BindFloat("Sensitivity", "YawSensitivity",   1.0f, 0f, 3f, "Horizontal rotation sensitivity multiplier");
            PitchSensitivity = BindFloat("Sensitivity", "PitchSensitivity", 1.0f, 0f, 3f, "Vertical rotation sensitivity multiplier");
            RollSensitivity  = BindFloat("Sensitivity", "RollSensitivity",  1.0f, 0f, 3f, "Tilt rotation sensitivity multiplier");

            SmoothingFactor = BindFloat("Smoothing", "SmoothingFactor", 0f, 0f, 0.95f,
                "Movement smoothing (0 = none/immediate, higher = smoother but more latency)");

            EnableOnStartupEntry    = config.Bind("Controls", "EnableOnStartup",   true,             "Enable head tracking when game starts");
            ToggleKeyEntry          = config.Bind("Controls", "ToggleKey",         KeyCode.End,      "Key to toggle head tracking on/off");
            RecenterKeyEntry        = config.Bind("Controls", "RecenterKey",       KeyCode.Home,     "Key to recenter view to current head position");
            PositionToggleKeyEntry  = config.Bind("Controls", "PositionToggleKey", KeyCode.PageUp,   "Key to toggle positional tracking on/off");
            YawModeKeyEntry         = config.Bind("Controls", "YawModeKey",        KeyCode.PageDown, "Key to toggle yaw mode (world-space horizon-locked vs camera-local)");

            WorldSpaceYaw = config.Bind("General", "WorldSpaceYaw", true,
                "true = horizon-locked yaw (default); false = camera-local yaw. Camera-local produces leaning at extreme pitch.");

            PositionSensitivityX = BindFloat("Position", "SensitivityX", 1.0f, 0f, 5f, "Lateral (left/right) position sensitivity multiplier");
            PositionSensitivityY = BindFloat("Position", "SensitivityY", 1.0f, 0f, 5f, "Vertical (up/down) position sensitivity multiplier");
            PositionSensitivityZ = BindFloat("Position", "SensitivityZ", 1.0f, 0f, 5f, "Depth (forward/back) position sensitivity multiplier");
            PositionLimitX       = BindFloat("Position", "LimitX",        0.30f, 0f,   1f, "Maximum lateral displacement in meters");
            PositionLimitY       = BindFloat("Position", "LimitY",        0.15f, 0f,   1f, "Maximum upward vertical displacement in meters");
            PositionLimitYDown   = BindFloat("Position", "LimitYDown",    0.05f, 0f, 0.5f, "Maximum downward vertical displacement in meters");
            PositionLimitZ       = BindFloat("Position", "LimitZ",        0.40f, 0f,   1f, "Maximum depth displacement in meters");
            PositionSmoothing    = BindFloat("Position", "Smoothing",     0.15f, 0f, 0.95f, "Position smoothing (0 = instant, higher = smoother but more latency)");

            DisableInMenus    = config.Bind("GameState", "DisableInMenus",    true, "Automatically disable head tracking in menus and non-gameplay scenes");
            DisableWhenPaused = config.Bind("GameState", "DisableWhenPaused", true, "Automatically disable head tracking when the game is paused");

            _playerPorts = new[] { Player1Port.Value, Player2Port.Value, Player3Port.Value, Player4Port.Value };
        }

        public int UdpPort => Player1Port.Value;

        public bool EnableOnStartup => EnableOnStartupEntry.Value;

        public SensitivitySettings Sensitivity => new SensitivitySettings(
            YawSensitivity.Value,
            PitchSensitivity.Value,
            RollSensitivity.Value
        );

        public string RecenterKeyName => RecenterKeyEntry.Value.ToString();

        public string ToggleKeyName => ToggleKeyEntry.Value.ToString();

        public float Smoothing => SmoothingFactor.Value;

        public PositionSettings PositionSettingsFromConfig => new PositionSettings(
            PositionSensitivityX.Value,
            PositionSensitivityY.Value,
            PositionSensitivityZ.Value,
            PositionLimitX.Value,
            PositionLimitY.Value,
            PositionLimitZ.Value,
            0.10f,
            PositionSmoothing.Value,
            invertX: true, invertY: false, invertZ: true
        );

        public bool AimDecouplingEnabled => false;

        public bool ShowDecoupledReticle => false;

        public float[] ReticleColorRgba => ReticleColorRgbaValue;
    }
}
