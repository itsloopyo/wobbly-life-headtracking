using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
using WobblyLifeHeadTracking.Legacy;

namespace WobblyLifeHeadTracking.Tests.ConfigDifferential
{
    internal enum LoadStatus
    {
        /// <summary>The file was read.</summary>
        Usable,

        /// <summary>BepInEx threw reading the file, so the plugin never loaded.</summary>
        Refused,

        /// <summary>No file: the build ran on its defaults.</summary>
        Absent,
    }

    /// <summary>
    /// What one build ran on for one input: the load status, every setting, the startup state and
    /// the hotkeys it polled. A hotkey is written as its bindings, "modifiers:KeyCode" with the
    /// modifiers as core's KeyModifiers bits (3 is Ctrl+Shift), so a KeyCode with no name still
    /// compares.
    /// </summary>
    internal sealed class LegacyReading
    {
        public LoadStatus Status;
        public LegacyConfig Values;
        public bool Enabled;
        public bool RotationEnabled;
        public bool PositionEnabled;
        public bool WorldSpaceYaw;
        public SortedDictionary<string, string> Hotkeys = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public static readonly FieldInfo[] Fields = typeof(LegacyConfig).GetFields(BindingFlags.Public | BindingFlags.Instance);

        /// <summary>Every difference between two readings, one line each; empty when they agree.</summary>
        public static List<string> Differences(LegacyReading a, LegacyReading b)
        {
            var d = new List<string>();
            if (a.Status != b.Status)
            {
                d.Add("status " + a.Status + " / " + b.Status);
                return d;
            }
            if (a.Status == LoadStatus.Refused) return d;
            foreach (FieldInfo f in Fields)
            {
                object x = f.GetValue(a.Values), y = f.GetValue(b.Values);
                if (!SameValue(x, y)) d.Add(f.Name + " " + Text(x) + " / " + Text(y));
            }
            if (a.Enabled != b.Enabled) d.Add("enabled " + a.Enabled + " / " + b.Enabled);
            if (a.RotationEnabled != b.RotationEnabled) d.Add("rotation " + a.RotationEnabled + " / " + b.RotationEnabled);
            if (a.PositionEnabled != b.PositionEnabled) d.Add("position " + a.PositionEnabled + " / " + b.PositionEnabled);
            if (a.WorldSpaceYaw != b.WorldSpaceYaw) d.Add("yaw " + a.WorldSpaceYaw + " / " + b.WorldSpaceYaw);
            var actions = new SortedSet<string>(a.Hotkeys.Keys, StringComparer.Ordinal);
            actions.UnionWith(b.Hotkeys.Keys);
            foreach (string action in actions)
            {
                string x, y;
                a.Hotkeys.TryGetValue(action, out x);
                b.Hotkeys.TryGetValue(action, out y);
                if (x != y) d.Add("hotkey " + action + " " + (x ?? "none") + " / " + (y ?? "none"));
            }
            return d;
        }

        public static bool SameValue(object x, object y)
        {
            if (x is float fx && y is float fy)
            {
                return BitConverter.ToInt32(BitConverter.GetBytes(fx), 0) == BitConverter.ToInt32(BitConverter.GetBytes(fy), 0);
            }
            return Equals(x, y);
        }

        public static string Text(object value)
        {
            if (value is float f) return f.ToString("R", CultureInfo.InvariantCulture);
            if (value is KeyCode k) return ((int)k).ToString(CultureInfo.InvariantCulture) + "(" + k + ")";
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>A primary key beside a Ctrl+Shift letter, as ChordHotkeys.IsActionPressed polled them.</summary>
        public static string Bindings(KeyCode primary, KeyCode chordLetter)
        {
            var items = new List<string>();
            if (primary != KeyCode.None) items.Add("0:" + ((int)primary).ToString(CultureInfo.InvariantCulture));
            items.Add("3:" + ((int)chordLetter).ToString(CultureInfo.InvariantCulture));
            return string.Join(", ", items.ToArray());
        }

        /// <summary>
        /// The newest published build, the dev pre-release built from 0a3073b: its WobblyLifeConfig
        /// (tests/config_differential/Oracle, byte for byte the tag's file) on the ConfigFile the
        /// loader builds, then its WobblyLifeHeadTrackingRuntime startup and hotkey polling.
        /// </summary>
        public static LegacyReading Oracle(string path)
        {
            bool exists = File.Exists(path);
            ConfigFile file;
            try
            {
                file = BepInExHost.Open(path, "0.0.0");
            }
            catch (ArgumentException)
            {
                return new LegacyReading { Status = LoadStatus.Refused };
            }
            // The dev build saved the whole file after every Bind. A save writes and never reads,
            // so it cannot change a value the build ran on; turned off here, where 23 saves of
            // every corpus input would cost minutes.
            file.SaveOnConfigSet = false;
            var config = new WobblyLifeHeadTracking.Config.WobblyLifeConfig(file);

            var values = new LegacyConfig();
            foreach (FieldInfo f in Fields)
            {
                PropertyInfo entry = typeof(WobblyLifeHeadTracking.Config.WobblyLifeConfig).GetProperty(f.Name);
                if (entry == null) throw new InvalidOperationException("the oracle has no " + f.Name);
                f.SetValue(values, ((ConfigEntryBase)entry.GetValue(config, null)).BoxedValue);
            }
            return Startup(exists, values);
        }

        /// <summary>
        /// The frozen reader (src/WobblyLifeHeadTracking/Legacy) on the ConfigFile the loader
        /// builds, then the startup code and hotkey polling of the commit that froze it.
        /// </summary>
        public static LegacyReading Import(string path)
        {
            bool exists = File.Exists(path);
            ConfigFile file;
            try
            {
                file = BepInExHost.Open(path, "0.0.0");
            }
            catch (ArgumentException)
            {
                return new LegacyReading { Status = LoadStatus.Refused };
            }
            return Startup(exists, LegacyConfigReader.Read(file));
        }

        // The runtime started with tracking per EnableOnStartup and the camera controller's yaw per
        // WorldSpaceYaw. MultiPlayerTrackingManager starts in rotation and position whatever the
        // file says, and no setting chose another mode.
        private static LegacyReading Startup(bool exists, LegacyConfig values)
        {
            var r = new LegacyReading
            {
                Status = exists ? LoadStatus.Usable : LoadStatus.Absent,
                Values = values,
                Enabled = values.EnableOnStartup,
                RotationEnabled = true,
                PositionEnabled = true,
                WorldSpaceYaw = values.WorldSpaceYaw,
            };
            r.Hotkeys["Toggle"] = Bindings(values.ToggleKey, KeyCode.Y);
            r.Hotkeys["CycleTrackingMode"] = Bindings(values.PositionToggleKey, KeyCode.G);
            r.Hotkeys["YawMode"] = Bindings(values.YawModeKey, KeyCode.H);
            return r;
        }
    }
}
