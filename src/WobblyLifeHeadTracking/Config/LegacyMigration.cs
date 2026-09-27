using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Input;
using UnityEngine;
using WobblyLifeHeadTracking.Legacy;

namespace WobblyLifeHeadTracking.Config
{
    /// <summary>
    /// The legacy import: the frozen reader on the plugin's own ConfigFile, then a map, field by
    /// field, from what it read into <see cref="WobblyLifeSettings"/>. The owner runs it once, while
    /// BepInEx\config\CameraUnlock.ini is absent, on
    /// BepInEx\config\com.cameraunlock.wobblylife.headtracking.cfg, which it never writes.
    /// </summary>
    internal static class LegacyMigration
    {
        // What the last build before the canonical config shipped, and so what a pose-shaping
        // value is compared with.
        private static readonly LegacyConfig Shipped = new LegacyConfig();

        public static LegacyImport<WobblyLifeSettings> Import(ConfigFile pluginConfig)
        {
            return new LegacyImport<WobblyLifeSettings>((input, config) => Run(pluginConfig, input, config), LegacyConfigReader.Keys);
        }

        private static ImportResult Run(ConfigFile pluginConfig, LegacyImportInput input, WobblyLifeSettings config)
        {
            bool exists = File.Exists(input.Path);
            LegacyConfig legacy;
            try
            {
                legacy = LegacyConfigReader.Read(pluginConfig);
            }
            catch (ArgumentException e)
            {
                // BepInEx refuses a key it cannot name. The plugin's own ConfigFile already read
                // the file once without throwing, so the file changed in between.
                return ImportResult.Refused("BepInEx cannot read it: " + e.Message);
            }
            finally
            {
                // The frozen reader bound its entries on the plugin's ConfigFile. Unbound, they are
                // not listed by ConfigurationManager, where they would do nothing.
                pluginConfig.Clear();
            }

            var dropped = new List<DroppedValue>();
            var poseShaping = new List<PoseShapingValue>();
            LegacyFollowsDefaultsIni follows = Map(legacy, config, dropped, poseShaping);
            return exists
                ? ImportResult.Imported(dropped, poseShaping, follows.Concepts)
                : ImportResult.Absent(dropped, poseShaping, follows.Concepts);
        }

        /// <summary>
        /// Sets every field from the legacy values and returns the rows left to Defaults.ini: each
        /// global row whose legacy setting holds what the last build shipped, or that the build had
        /// no setting for (owner rule of 2026-09-26).
        /// </summary>
        public static LegacyFollowsDefaultsIni Map(LegacyConfig legacy, WobblyLifeSettings config, ICollection<DroppedValue> dropped,
            ICollection<PoseShapingValue> poseShaping)
        {
            config.UdpPort = legacy.Player1Port;
            config.Player2Port = legacy.Player2Port;
            config.Player3Port = legacy.Player3Port;
            config.Player4Port = legacy.Player4Port;
            config.EnableOnStartup = legacy.EnableOnStartup;
            config.WorldSpaceYaw = legacy.WorldSpaceYaw;
            // The published build always started in rotation and position; no setting chose another
            // mode, and the cycle key walked all three.
            config.RotationEnabled = true;
            config.PositionEnabled = true;
            config.LocalSmoothing = legacy.LocalSmoothing;
            config.RemoteSmoothing = legacy.RemoteSmoothing;
            // PositionLimitZBack had no key: the build ran core's default, the table's too.
            PositionSettings p = config.Position;
            config.Position = new PositionSettings(
                p.SensitivityX, p.SensitivityY, p.SensitivityZ,
                legacy.PositionLimitX, legacy.PositionLimitY, legacy.PositionLimitYDown, legacy.PositionLimitZ, p.LimitZBack,
                legacy.LocalSmoothing, legacy.RemoteSmoothing,
                p.InvertX, p.InvertY, p.InvertZ);
            config.DisableInMenus = legacy.DisableInMenus;
            config.DisableWhenPaused = legacy.DisableWhenPaused;

            config.ToggleKeyName = KeyList(legacy.ToggleKey, KeyCode.Y, "ToggleKey", dropped);
            config.CycleTrackingModeKeyName = KeyList(legacy.PositionToggleKey, KeyCode.G, "PositionToggleKey", dropped);
            config.YawModeKeyName = KeyList(legacy.YawModeKey, KeyCode.H, "YawModeKey", dropped);

            const string s = LegacyConfigReader.Sensitivity;
            const string pos = LegacyConfigReader.Position;
            LegacyPoseShaping.Record(legacy.YawSensitivity, Shipped.YawSensitivity, s, "YawSensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PitchSensitivity, Shipped.PitchSensitivity, s, "PitchSensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.RollSensitivity, Shipped.RollSensitivity, s, "RollSensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PositionSensitivityX, Shipped.PositionSensitivityX, pos, "SensitivityX", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PositionSensitivityY, Shipped.PositionSensitivityY, pos, "SensitivityY", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PositionSensitivityZ, Shipped.PositionSensitivityZ, pos, "SensitivityZ", poseShaping, dropped);

            var follows = new LegacyFollowsDefaultsIni();
            follows.Setting(ConfigConcepts.UdpPort, legacy.Player1Port, Shipped.Player1Port);
            follows.Setting(ConfigConcepts.EnableOnStartup, legacy.EnableOnStartup, Shipped.EnableOnStartup);
            follows.Setting(ConfigConcepts.WorldSpaceYaw, legacy.WorldSpaceYaw, Shipped.WorldSpaceYaw);
            follows.TrackingMode(true);
            follows.Setting(ConfigConcepts.LocalSmoothing, legacy.LocalSmoothing, Shipped.LocalSmoothing);
            follows.Setting(ConfigConcepts.RemoteSmoothing, legacy.RemoteSmoothing, Shipped.RemoteSmoothing);
            follows.Setting(ConfigConcepts.PositionLimitX, legacy.PositionLimitX, Shipped.PositionLimitX);
            follows.Setting(ConfigConcepts.PositionLimitY, legacy.PositionLimitY, Shipped.PositionLimitY);
            follows.Setting(ConfigConcepts.PositionLimitYDown, legacy.PositionLimitYDown, Shipped.PositionLimitYDown);
            follows.Setting(ConfigConcepts.PositionLimitZ, legacy.PositionLimitZ, Shipped.PositionLimitZ);
            follows.NotInLegacy(ConfigConcepts.PositionLimitZBack);
            // The Ctrl+Shift letter was fixed in code, so a hotkey is unchanged exactly where its key is.
            follows.Setting(ConfigConcepts.ToggleKey, legacy.ToggleKey, Shipped.ToggleKey);
            follows.Setting(ConfigConcepts.CycleTrackingModeKey, legacy.PositionToggleKey, Shipped.PositionToggleKey);
            follows.Setting(ConfigConcepts.YawModeKey, legacy.YawModeKey, Shipped.YawModeKey);
            return follows;
        }

        /// <summary>
        /// A legacy hotkey as a key list: the key the player set, through core's N3 (a Ctrl, Shift
        /// or Alt key alone unbinds and is logged), then the Ctrl+Shift letter ChordHotkeys polled
        /// beside it. A KeyCode with no name in core's key list (a number BepInEx read into the
        /// enum) keeps its text, which the owner cannot write, so the import is deferred rather
        /// than the key changed.
        /// </summary>
        private static string KeyList(KeyCode primary, KeyCode chordLetter, string legacyKey, ICollection<DroppedValue> dropped)
        {
            var items = new List<string>();
            string plain;
            try
            {
                plain = LegacyNormalisations.KeyCodeToBindings((int)primary, LegacyConfigReader.Controls, legacyKey, dropped);
            }
            catch (ArgumentException)
            {
                plain = primary.ToString();
            }
            if (plain.Length > 0) items.Add(plain);
            items.Add(KeyBindings.Format(new[] { new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)chordLetter) }));
            return string.Join(", ", items.ToArray());
        }
    }
}
