using System.Collections.Generic;
using System.IO;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Config.Testing;
using WobblyLifeHeadTracking.Legacy;

namespace WobblyLifeHeadTracking.Tests.ConfigDifferential
{
    /// <summary>
    /// The differential test's inputs: no file, an empty file, the first-run output of the one
    /// published build (the rolling dev pre-release, whose installer carries a Steam and an Xbox Game
    /// Pass payload; there is no v* release and no predecessor repo), and core's mutation corpus over
    /// the Steam one. That build shipped no plugin config and no launcher seed of one.
    /// tests/config_differential/provenance.tsv says where each came from.
    /// </summary>
    internal static class Corpus
    {
        public static readonly string[] PublishedTags = { "dev" };

        /// <summary>Each first-run file, named by the tag and, for the Game Pass payload, "-il2cpp".</summary>
        public static readonly string[] FirstRunFiles = { "dev", "dev-il2cpp" };

        private static readonly string[] None = new string[0];

        /// <summary>
        /// One descriptor per key the frozen reader binds: an alternate value BepInEx reads, and
        /// one value outside each AcceptableValueRange, which BepInEx clamps. No legacy row names
        /// a chord: ChordHotkeys added the Ctrl+Shift letter in code.
        /// </summary>
        public static readonly MutationKey[] Keys =
        {
            Number(LegacyConfigReader.Network, "Player1Port", "5555", "1023", "65536"),
            Number(LegacyConfigReader.Network, "Player2Port", "5556", "1023", "65536"),
            Number(LegacyConfigReader.Network, "Player3Port", "5557", "1023", "65536"),
            Number(LegacyConfigReader.Network, "Player4Port", "5558", "1023", "65536"),
            Number(LegacyConfigReader.Sensitivity, "YawSensitivity", "2", "-0.1", "3.5"),
            Number(LegacyConfigReader.Sensitivity, "PitchSensitivity", "2", "-0.1", "3.5"),
            Number(LegacyConfigReader.Sensitivity, "RollSensitivity", "2", "-0.1", "3.5"),
            Number(LegacyConfigReader.Smoothing, "LocalSmoothing", "0.5", "-0.1", "1.1"),
            Number(LegacyConfigReader.Smoothing, "RemoteSmoothing", "0.3", "-0.1", "1.1"),
            Bool(LegacyConfigReader.Controls, "EnableOnStartup", "false"),
            Hotkey(LegacyConfigReader.Controls, "ToggleKey", "F8"),
            Hotkey(LegacyConfigReader.Controls, "PositionToggleKey", "F10"),
            Hotkey(LegacyConfigReader.Controls, "YawModeKey", "F7"),
            Bool(LegacyConfigReader.General, "WorldSpaceYaw", "false"),
            Number(LegacyConfigReader.Position, "SensitivityX", "2", "-0.1", "5.5"),
            Number(LegacyConfigReader.Position, "SensitivityY", "2", "-0.1", "5.5"),
            Number(LegacyConfigReader.Position, "SensitivityZ", "2", "-0.1", "5.5"),
            Number(LegacyConfigReader.Position, "LimitX", "0.25", "-0.1", "1.1"),
            Number(LegacyConfigReader.Position, "LimitY", "0.25", "-0.1", "1.1"),
            Number(LegacyConfigReader.Position, "LimitYDown", "0.1", "-0.1", "0.6"),
            Number(LegacyConfigReader.Position, "LimitZ", "0.25", "-0.1", "1.1"),
            Bool(LegacyConfigReader.GameState, "DisableInMenus", "false"),
            Bool(LegacyConfigReader.GameState, "DisableWhenPaused", "false"),
        };

        /// <summary>Every input as (name, bytes); null bytes is no file.</summary>
        public static IEnumerable<KeyValuePair<string, byte[]>> Inputs()
        {
            yield return new KeyValuePair<string, byte[]>("no file", null);
            yield return new KeyValuePair<string, byte[]>("empty file", new byte[0]);
            foreach (string name in FirstRunFiles)
            {
                yield return new KeyValuePair<string, byte[]>(name + " first run", FirstRun(name));
            }
            foreach (IniMutation m in IniMutations.Generate(FirstRun("dev"), LegacyConfigReader.Keys, Keys))
            {
                yield return new KeyValuePair<string, byte[]>("corpus: " + m.Name, m.Bytes);
            }
        }

        /// <summary>The .cfg a published build wrote at its first start, extracted from its release DLL once.</summary>
        public static byte[] FirstRun(string name)
        {
            return File.ReadAllBytes(Path.Combine(RepoPaths.Root, "tests", "config_differential", "data", "first-run", name + ".cfg"));
        }

        private static MutationKey Number(string section, string key, string alternate, params string[] outOfRange)
        {
            return new MutationKey(section, key, alternate, outOfRange, false, new ChordSwitch[0]);
        }

        private static MutationKey Bool(string section, string key, string alternate)
        {
            return new MutationKey(section, key, alternate, None, false, new ChordSwitch[0]);
        }

        private static MutationKey Hotkey(string section, string key, string alternate)
        {
            return new MutationKey(section, key, alternate, None, true, new ChordSwitch[0]);
        }
    }
}
