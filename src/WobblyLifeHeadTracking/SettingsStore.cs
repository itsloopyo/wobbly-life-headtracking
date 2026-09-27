using System;
using System.Collections.Generic;
using BepInEx.Logging;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Input;
using WobblyLifeHeadTracking.Config;

namespace WobblyLifeHeadTracking
{
    /// <summary>
    /// The settings live in BepInEx\config\CameraUnlock.ini, read and written by core's config
    /// owner, with rows set to default following the player's Defaults.ini. Nothing is bound
    /// through BepInEx's ConfigFile at runtime, so ConfigurationManager does not list them. The
    /// plugin's .cfg, which earlier builds read, is imported once while CameraUnlock.ini is absent
    /// and never written. Shared by the Steam and the Xbox Game Pass plugin.
    /// </summary>
    internal static class SettingsStore
    {
        private static ManualLogSource Log => WobblyLifeHeadTrackingPlugin.Log;

        private static ConfigOwner<WobblyLifeSettings> _owner;

        public static WobblyLifeSettings Load()
        {
            _owner = new ConfigOwner<WobblyLifeSettings>(
                WobblyLifeConfigOwner.Options(WobblyLifeHeadTrackingPlugin.ConfigFile, DefaultsFile.PerUser(),
                    message => Log.LogWarning(message)));
            ConfigLoadResult<WobblyLifeSettings> loaded = _owner.Load();
            bool usable = loaded.Status == ConfigLoadStatus.Canonical
                          || loaded.Status == ConfigLoadStatus.Migrated
                          || loaded.Status == ConfigLoadStatus.Created;
            WriteLog(loaded.Log, loaded.Diagnostics, usable);
            Log.LogInfo("Config: " + loaded.Status);

            // The published build did not load at all on a .cfg BepInEx refused to read.
            if (loaded.Status == ConfigLoadStatus.LegacyRefused)
            {
                throw new InvalidOperationException(loaded.Reason);
            }
            return loaded.Config;
        }

        /// <summary>
        /// Called after a toggle has applied its new value. A save that fails is logged and the
        /// session keeps the new value.
        /// </summary>
        public static void Save(Action<WobblyLifeSettings> change)
        {
            ConfigSaveResult saved = _owner.Save(change);
            if (saved.Status == ConfigSaveStatus.Saved)
            {
                foreach (string line in saved.Log) Log.LogInfo(line);
                return;
            }
            foreach (string line in saved.Log) Log.LogWarning(line);
            Log.LogWarning("Config not saved (" + saved.Status + "): " + saved.Reason + " The change applies to this session only.");
        }

        // The table's hotkey codec has read every list of a loaded file. Only a legacy import the
        // owner deferred, over a key with no name, hands one over that does not parse; that action
        // then has no keys this session.
        public static KeyBinding[] Hotkeys(string row, string text)
        {
            KeyBinding[] bindings;
            string error;
            if (KeyBindings.TryParse(text, out bindings, out error)) return bindings;
            Log.LogError(row + "=" + text + " is not a hotkey list (" + error + "), so it has no keys this session.");
            return new KeyBinding[0];
        }

        // The owner writes each diagnostic as "<path>: <description>" among lines that only report
        // what it did, so the complaints are picked out by their text.
        private static void WriteLog(IEnumerable<string> lines, IEnumerable<CanonicalDiagnostic> diagnostics, bool usable)
        {
            var complaints = new HashSet<string>();
            foreach (CanonicalDiagnostic diagnostic in diagnostics) complaints.Add(diagnostic.Describe());
            foreach (string line in lines)
            {
                bool complaint = false;
                foreach (string c in complaints)
                {
                    if (line.EndsWith(c, StringComparison.Ordinal)) complaint = true;
                }
                if (usable && !complaint) Log.LogInfo(line);
                else Log.LogWarning(line);
            }
        }
    }
}
