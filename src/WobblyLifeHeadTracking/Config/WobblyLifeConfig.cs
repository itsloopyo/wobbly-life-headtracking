using BepInEx.Configuration;
using CameraUnlock.Core.Data;
using UnityEngine;

namespace WobblyLifeHeadTracking.Config
{
    public sealed class WobblyLifeConfig
    {
        public ConfigFile File { get; }

        public ConfigEntry<int> Player1Port { get; }
        public ConfigEntry<int> Player2Port { get; }
        public ConfigEntry<int> Player3Port { get; }
        public ConfigEntry<int> Player4Port { get; }

        private readonly int[] _playerPorts;
        public int[] PlayerPorts => _playerPorts;

        public ConfigEntry<float> YawSensitivity { get; }
        public ConfigEntry<float> PitchSensitivity { get; }
        public ConfigEntry<float> RollSensitivity { get; }

        public ConfigEntry<float> LocalSmoothing { get; }
        public ConfigEntry<float> RemoteSmoothing { get; }

        public ConfigEntry<bool> EnableOnStartup { get; }
        public ConfigEntry<KeyCode> ToggleKey { get; }
        public ConfigEntry<KeyCode> PositionToggleKey { get; }
        public ConfigEntry<KeyCode> YawModeKey { get; }

        public ConfigEntry<bool> WorldSpaceYaw { get; }

        public ConfigEntry<float> PositionSensitivityX { get; }
        public ConfigEntry<float> PositionSensitivityY { get; }
        public ConfigEntry<float> PositionSensitivityZ { get; }
        public ConfigEntry<float> PositionLimitX { get; }
        public ConfigEntry<float> PositionLimitY { get; }
        public ConfigEntry<float> PositionLimitYDown { get; }
        public ConfigEntry<float> PositionLimitZ { get; }

        public ConfigEntry<bool> DisableInMenus { get; }
        public ConfigEntry<bool> DisableWhenPaused { get; }

        public WobblyLifeConfig(ConfigFile config)
        {
            File = config;

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

            // Smoothing covers both rotation and position. The value used is selected per
            // connection from the packet source address, so a player with a local tracker
            // and a player on a phone over WiFi each get the setting that suits them.
            LocalSmoothing  = BindFloat("Smoothing", "LocalSmoothing",  0f, 0f, 1f,
                "Smoothing applied when the tracker runs on this machine (loopback). 0 = no smoothing, 1 = heavy.");
            RemoteSmoothing = BindFloat("Smoothing", "RemoteSmoothing", 0.15f, 0f, 1f,
                "Smoothing applied when the tracker is a remote device on the network. 0 = no smoothing, 1 = heavy.");

            EnableOnStartup   = config.Bind("Controls", "EnableOnStartup",   true,             "Enable head tracking when game starts");
            ToggleKey         = config.Bind("Controls", "ToggleKey",         KeyCode.End,      "Key to toggle head tracking on/off");
            PositionToggleKey = config.Bind("Controls", "PositionToggleKey", KeyCode.PageUp,   "Key to cycle tracking mode (6DOF / rotation only / position only)");
            YawModeKey        = config.Bind("Controls", "YawModeKey",        KeyCode.PageDown, "Key to toggle yaw mode (world-space horizon-locked vs camera-local)");

            WorldSpaceYaw = config.Bind("General", "WorldSpaceYaw", true,
                "true = horizon-locked yaw (default); false = camera-local yaw. Camera-local produces leaning at extreme pitch.");

            PositionSensitivityX = BindFloat("Position", "SensitivityX", 1.0f, 0f, 5f, "Lateral (left/right) position sensitivity multiplier");
            PositionSensitivityY = BindFloat("Position", "SensitivityY", 1.0f, 0f, 5f, "Vertical (up/down) position sensitivity multiplier");
            PositionSensitivityZ = BindFloat("Position", "SensitivityZ", 1.0f, 0f, 5f, "Depth (forward/back) position sensitivity multiplier");
            PositionLimitX       = BindFloat("Position", "LimitX",        0.30f, 0f,   1f, "Maximum lateral displacement in meters");
            PositionLimitY       = BindFloat("Position", "LimitY",        0.15f, 0f,   1f, "Maximum upward vertical displacement in meters");
            PositionLimitYDown   = BindFloat("Position", "LimitYDown",    0.05f, 0f, 0.5f, "Maximum downward vertical displacement in meters");
            PositionLimitZ       = BindFloat("Position", "LimitZ",        0.40f, 0f,   1f, "Maximum depth displacement in meters");

            DisableInMenus    = config.Bind("GameState", "DisableInMenus",    true, "Automatically disable head tracking in menus and non-gameplay scenes");
            DisableWhenPaused = config.Bind("GameState", "DisableWhenPaused", true, "Automatically disable head tracking when the game is paused");

            _playerPorts = new[] { Player1Port.Value, Player2Port.Value, Player3Port.Value, Player4Port.Value };
        }

        public SensitivitySettings Sensitivity => new SensitivitySettings(
            YawSensitivity.Value,
            PitchSensitivity.Value,
            RollSensitivity.Value
        );

        public PositionSettings PositionSettingsFromConfig => new PositionSettings(
            PositionSensitivityX.Value,
            PositionSensitivityY.Value,
            PositionSensitivityZ.Value,
            PositionLimitX.Value,
            PositionLimitY.Value,
            PositionLimitYDown.Value,
            PositionLimitZ.Value,
            0.10f,
            LocalSmoothing.Value,
            RemoteSmoothing.Value,
            invertX: true, invertY: false, invertZ: false
        );
    }
}
