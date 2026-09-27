using System.IO;
using BepInEx.Configuration;
using CameraUnlock.Core.Config;

namespace WobblyLifeHeadTracking.Legacy
{
    /// <summary>
    /// Frozen: the Bind calls the last build before the canonical config ran on the plugin's
    /// <see cref="ConfigFile"/>, each definition's section, key, type, default, description and
    /// acceptable values unchanged. Reads BepInEx\config\com.cameraunlock.wobblylife.headtracking.cfg
    /// exactly as that build did and writes nothing. Never edit this file.
    /// </summary>
    internal static class LegacyConfigReader
    {
        public const string Network = "Network";
        public const string Sensitivity = "Sensitivity";
        public const string Smoothing = "Smoothing";
        public const string Controls = "Controls";
        public const string General = "General";
        public const string Position = "Position";
        public const string GameState = "GameState";

        /// <summary>Every section and key <see cref="Read"/> binds, in the order it binds them.</summary>
        public static readonly LegacyKey[] Keys =
        {
            new LegacyKey(Network, "Player1Port"),
            new LegacyKey(Network, "Player2Port"),
            new LegacyKey(Network, "Player3Port"),
            new LegacyKey(Network, "Player4Port"),
            new LegacyKey(Sensitivity, "YawSensitivity"),
            new LegacyKey(Sensitivity, "PitchSensitivity"),
            new LegacyKey(Sensitivity, "RollSensitivity"),
            new LegacyKey(Smoothing, "LocalSmoothing"),
            new LegacyKey(Smoothing, "RemoteSmoothing"),
            new LegacyKey(Controls, "EnableOnStartup"),
            new LegacyKey(Controls, "ToggleKey"),
            new LegacyKey(Controls, "PositionToggleKey"),
            new LegacyKey(Controls, "YawModeKey"),
            new LegacyKey(General, "WorldSpaceYaw"),
            new LegacyKey(Position, "SensitivityX"),
            new LegacyKey(Position, "SensitivityY"),
            new LegacyKey(Position, "SensitivityZ"),
            new LegacyKey(Position, "LimitX"),
            new LegacyKey(Position, "LimitY"),
            new LegacyKey(Position, "LimitYDown"),
            new LegacyKey(Position, "LimitZ"),
            new LegacyKey(GameState, "DisableInMenus"),
            new LegacyKey(GameState, "DisableWhenPaused"),
        };

        /// <summary>
        /// Reads the settings the way the published build did, through BepInEx's own parser and
        /// clamping. BepInEx read the .cfg once already, in the ConfigFile constructor, so this
        /// turns saving off, reads the file again and then binds. Returns the defaults when the
        /// file is absent, as that build ran on a first start.
        /// </summary>
        public static LegacyConfig Read(ConfigFile config)
        {
            config.SaveOnConfigSet = false;
            if (File.Exists(config.ConfigFilePath))
            {
                config.Reload();
            }

            var c = new LegacyConfig();

            c.Player1Port = BindPort(config, "Player1Port", c.Player1Port, "UDP port for Player 1's OpenTrack data");
            c.Player2Port = BindPort(config, "Player2Port", c.Player2Port, "UDP port for Player 2's OpenTrack data");
            c.Player3Port = BindPort(config, "Player3Port", c.Player3Port, "UDP port for Player 3's OpenTrack data");
            c.Player4Port = BindPort(config, "Player4Port", c.Player4Port, "UDP port for Player 4's OpenTrack data");

            c.YawSensitivity   = BindFloat(config, Sensitivity, "YawSensitivity",   c.YawSensitivity,   0f, 3f, "Horizontal rotation sensitivity multiplier");
            c.PitchSensitivity = BindFloat(config, Sensitivity, "PitchSensitivity", c.PitchSensitivity, 0f, 3f, "Vertical rotation sensitivity multiplier");
            c.RollSensitivity  = BindFloat(config, Sensitivity, "RollSensitivity",  c.RollSensitivity,  0f, 3f, "Tilt rotation sensitivity multiplier");

            c.LocalSmoothing  = BindFloat(config, Smoothing, "LocalSmoothing", c.LocalSmoothing, 0f, 1f,
                "Smoothing applied when the tracker runs on this machine (loopback). 0 = no smoothing, 1 = heavy.");
            c.RemoteSmoothing = BindFloat(config, Smoothing, "RemoteSmoothing", c.RemoteSmoothing, 0f, 1f,
                "Smoothing applied when the tracker is a remote device on the network. 0 = no smoothing, 1 = heavy.");

            c.EnableOnStartup   = config.Bind(Controls, "EnableOnStartup",   c.EnableOnStartup,   "Enable head tracking when game starts").Value;
            c.ToggleKey         = config.Bind(Controls, "ToggleKey",         c.ToggleKey,         "Key to toggle head tracking on/off").Value;
            c.PositionToggleKey = config.Bind(Controls, "PositionToggleKey", c.PositionToggleKey, "Key to cycle tracking mode (6DOF / rotation only / position only)").Value;
            c.YawModeKey        = config.Bind(Controls, "YawModeKey",        c.YawModeKey,        "Key to toggle yaw mode (world-space horizon-locked vs camera-local)").Value;

            c.WorldSpaceYaw = config.Bind(General, "WorldSpaceYaw", c.WorldSpaceYaw,
                "true = horizon-locked yaw (default); false = camera-local yaw. Camera-local produces leaning at extreme pitch.").Value;

            c.PositionSensitivityX = BindFloat(config, Position, "SensitivityX", c.PositionSensitivityX, 0f, 5f, "Lateral (left/right) position sensitivity multiplier");
            c.PositionSensitivityY = BindFloat(config, Position, "SensitivityY", c.PositionSensitivityY, 0f, 5f, "Vertical (up/down) position sensitivity multiplier");
            c.PositionSensitivityZ = BindFloat(config, Position, "SensitivityZ", c.PositionSensitivityZ, 0f, 5f, "Depth (forward/back) position sensitivity multiplier");
            c.PositionLimitX       = BindFloat(config, Position, "LimitX",     c.PositionLimitX,     0f,   1f, "Maximum lateral displacement in meters");
            c.PositionLimitY       = BindFloat(config, Position, "LimitY",     c.PositionLimitY,     0f,   1f, "Maximum upward vertical displacement in meters");
            c.PositionLimitYDown   = BindFloat(config, Position, "LimitYDown", c.PositionLimitYDown, 0f, 0.5f, "Maximum downward vertical displacement in meters");
            c.PositionLimitZ       = BindFloat(config, Position, "LimitZ",     c.PositionLimitZ,     0f,   1f, "Maximum depth displacement in meters");

            c.DisableInMenus    = config.Bind(GameState, "DisableInMenus",    c.DisableInMenus,    "Automatically disable head tracking in menus and non-gameplay scenes").Value;
            c.DisableWhenPaused = config.Bind(GameState, "DisableWhenPaused", c.DisableWhenPaused, "Automatically disable head tracking when the game is paused").Value;

            return c;
        }

        private static int BindPort(ConfigFile config, string key, int defaultPort, string description)
        {
            return config.Bind(Network, key, defaultPort,
                new ConfigDescription(description, new AcceptableValueRange<int>(1024, 65535))).Value;
        }

        private static float BindFloat(ConfigFile config, string section, string key, float defaultValue, float min, float max, string description)
        {
            return config.Bind(section, key, defaultValue,
                new ConfigDescription(description, new AcceptableValueRange<float>(min, max))).Value;
        }
    }
}
