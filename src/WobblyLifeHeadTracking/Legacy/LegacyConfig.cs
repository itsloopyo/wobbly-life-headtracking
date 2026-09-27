using UnityEngine;

namespace WobblyLifeHeadTracking.Legacy
{
    /// <summary>
    /// Frozen: every setting the last build before the canonical config read from
    /// BepInEx\config\com.cameraunlock.wobblylife.headtracking.cfg, with that build's defaults.
    /// Both payloads, Mono and IL2CPP, read the same file. A default the runtime config later
    /// moves changes only what a new file holds, never what an old file without the key means.
    /// Never edit this file.
    /// </summary>
    internal sealed class LegacyConfig
    {
        // Network
        public int Player1Port = 4242;
        public int Player2Port = 4243;
        public int Player3Port = 4244;
        public int Player4Port = 4245;

        // Sensitivity
        public float YawSensitivity = 1.0f;
        public float PitchSensitivity = 1.0f;
        public float RollSensitivity = 1.0f;

        // Smoothing
        public float LocalSmoothing = 0.0f;
        public float RemoteSmoothing = 0.15f;

        // Controls
        public bool EnableOnStartup = true;
        public KeyCode ToggleKey = KeyCode.End;
        public KeyCode PositionToggleKey = KeyCode.PageUp;
        public KeyCode YawModeKey = KeyCode.PageDown;

        // General
        public bool WorldSpaceYaw = true;

        // Position
        public float PositionSensitivityX = 1.0f;
        public float PositionSensitivityY = 1.0f;
        public float PositionSensitivityZ = 1.0f;
        public float PositionLimitX = 0.30f;
        public float PositionLimitY = 0.15f;
        public float PositionLimitYDown = 0.05f;
        public float PositionLimitZ = 0.40f;

        // GameState
        public bool DisableInMenus = true;
        public bool DisableWhenPaused = true;
    }
}
