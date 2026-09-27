using CameraUnlock.Core.Config;

namespace WobblyLifeHeadTracking.Config
{
    /// <summary>
    /// Everything the mod reads from BepInEx\config\CameraUnlock.ini, on both the Steam and the Xbox
    /// Game Pass build. Unity-free, so the test project compiles it and holds the committed file to
    /// it. Player 1's port is the UdpPort concept; players 2 to 4 are local rows.
    /// </summary>
    public sealed class WobblyLifeSettings : HeadTrackingConfigData
    {
        public const string DisplayName = "Wobbly Life";

        public int Player2Port { get; set; } = 4243;

        public int Player3Port { get; set; } = 4244;

        public int Player4Port { get; set; } = 4245;

        public bool DisableInMenus { get; set; } = true;

        public bool DisableWhenPaused { get; set; } = true;

        /// <summary>One UDP port per local player, in player order.</summary>
        public int[] PlayerPorts
        {
            get { return new[] { UdpPort, Player2Port, Player3Port, Player4Port }; }
        }

        public static ConfigTable<WobblyLifeSettings> Table()
        {
            return HeadTrackingConfigTable.Create<WobblyLifeSettings>(
                    ConfigConcepts.UdpPort,
                    ConfigConcepts.EnableOnStartup,
                    ConfigConcepts.WorldSpaceYaw,
                    ConfigConcepts.RotationEnabled,
                    ConfigConcepts.LocalSmoothing,
                    ConfigConcepts.RemoteSmoothing,
                    ConfigConcepts.PositionEnabled,
                    ConfigConcepts.PositionLimitX,
                    ConfigConcepts.PositionLimitY,
                    ConfigConcepts.PositionLimitYDown,
                    ConfigConcepts.PositionLimitZ,
                    ConfigConcepts.PositionLimitZBack,
                    ConfigConcepts.ToggleKey,
                    ConfigConcepts.CycleTrackingModeKey,
                    ConfigConcepts.YawModeKey)
                .Select(ConfigConcepts.UdpPort)
                .Comment("UDP port player 1's tracker sends to (OpenTrack protocol). Split-screen\n" +
                         "players 2 to 4 use Player2Port to Player4Port below. Every player needs\n" +
                         "a port of their own.")
                .Select(ConfigConcepts.WorldSpaceYaw).Writable()
                .Select(ConfigConcepts.RotationEnabled).Writable()
                .Select(ConfigConcepts.PositionEnabled).Writable()
                .Local("Network", "Player2Port", c => c.Player2Port, (c, v) => c.Player2Port = v, new IntCodec(),
                    "UDP ports for split-screen players 2 to 4, one tracker each.")
                .Range(1, 65535)
                .Local("Network", "Player3Port", c => c.Player3Port, (c, v) => c.Player3Port = v, new IntCodec(), "")
                .Range(1, 65535)
                .Local("Network", "Player4Port", c => c.Player4Port, (c, v) => c.Player4Port = v, new IntCodec(), "")
                .Range(1, 65535)
                .Local("GameState", "DisableInMenus", c => c.DisableInMenus, (c, v) => c.DisableInMenus = v, new BoolCodec(),
                    "true: head tracking pauses in menus and other scenes that are not gameplay.")
                .Local("GameState", "DisableWhenPaused", c => c.DisableWhenPaused, (c, v) => c.DisableWhenPaused = v, new BoolCodec(),
                    "true: head tracking pauses while the game is paused.");
        }
    }
}
