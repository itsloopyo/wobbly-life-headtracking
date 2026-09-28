using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Config.Testing;
using CameraUnlock.Core.Input;
using WobblyLifeHeadTracking.Config;
using WobblyLifeHeadTracking.Legacy;
using UnityEngine;
using Xunit;

namespace WobblyLifeHeadTracking.Tests.ConfigDifferential
{
    /// <summary>
    /// Comparison 2: the frozen reader (the import) against the owner's Load, which imports the
    /// legacy file into a new CameraUnlock.ini (the migration), over every input. What may differ
    /// is only what data/config-format.json approves: pose shaping (the sensitivities) and a
    /// hotkey on a Ctrl, Shift or Alt key alone, plus the rows that follow Defaults.ini.
    /// </summary>
    public class MigrationTests : IDisposable
    {
        private readonly string scratch = RepoPaths.Scratch();
        private readonly DefaultsFile defaults;

        public MigrationTests()
        {
            defaults = Migration.ScratchDefaults(scratch);
        }

        public void Dispose()
        {
            foreach (string file in Directory.GetFiles(scratch, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(scratch, true);
        }

        [Fact]
        public void ComparisonTwo()
        {
            var failures = new List<string>();
            int migrated = 0, created = 0, refused = 0;
            foreach (KeyValuePair<string, byte[]> input in Corpus.Inputs())
            {
                string where = input.Key + ": ";
                LegacyReading import = LegacyReading.Import(DifferentialTests.Place(scratch, "import", input.Value));
                Migration m = Migration.Run(Path.Combine(scratch, "game"), input.Value, defaults);

                if (import.Status == LoadStatus.Refused)
                {
                    // BaseUnityPlugin's own ConfigFile throws on this file before any mod code
                    // runs, in the published build and in this one alike.
                    if (!m.BepInExRefused) failures.Add(where + "the frozen reader refused it and BepInEx did not");
                    LegacyKept(failures, where, m, input.Value);
                    refused++;
                    continue;
                }

                if (import.Status == LoadStatus.Absent)
                {
                    Check(failures, where, m.Loaded.Status == ConfigLoadStatus.Created, "status " + m.Loaded.Status + ", not Created");
                    Check(failures, where, SameFields(Migration.Defaults(), m.Loaded.Config), "a first start does not run on the defaults");
                    Check(failures, where, Names(m) == WobblyLifeConfigOwner.FileName, "the folder holds " + Names(m));
                    LegacyKept(failures, where, m, input.Value);
                    created++;
                    continue;
                }

                var expected = Migration.Defaults();
                var dropped = new List<DroppedValue>();
                var poseShaping = new List<PoseShapingValue>();
                LegacyFollowsDefaultsIni follows = LegacyMigration.Map(import.Values, expected, dropped, poseShaping);
                CheckRules(failures, where, import, expected, dropped, poseShaping);

                Check(failures, where, m.Loaded.Status == ConfigLoadStatus.Migrated, "status " + m.Loaded.Status + ", not Migrated");
                if (m.Loaded.Status != ConfigLoadStatus.Migrated) continue;
                // A row left to Defaults.ini holds what default gives, here the built-in value:
                // LimitY and LimitYDown shipped at 0.15 and 0.05 and follow the schema's 0.2.
                string migratedDifference = Difference(Expected(expected, follows), Migration.Fields(m.Loaded.Config));
                Check(failures, where, migratedDifference == null, migratedDifference);
                Check(failures, where, Names(m) == WobblyLifeConfigOwner.FileName + ", " + BepInExHost.Guid + ".cfg", "the folder holds " + Names(m));
                foreach (DroppedValue d in dropped)
                {
                    string line = m.LegacyPath + ": " + d.Describe();
                    Check(failures, where, m.Loaded.Log.Contains(line), "the log does not name " + d.Describe());
                }
                string lint = Lint(File.ReadAllBytes(m.ConfigPath));
                Check(failures, where, lint == null, "the migrated file " + lint);
                SecondLoad(failures, where, m);
                migrated++;
                LegacyKept(failures, where, m, input.Value);
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures.Take(40).ToArray()));
            Assert.True(migrated > 500, migrated + " inputs migrated");
            Assert.True(created == 1, created + " inputs were a first start");
            Assert.True(refused < migrated / 5, refused + " refused by BepInEx, of " + migrated);
        }

        /// <summary>
        /// A read-only copy of every input with a legacy file imports as a writable one does, and
        /// keeps its attribute, bytes and write time.
        /// </summary>
        [Fact]
        public void ReadOnlyLegacyFileImportsTheSame()
        {
            var failures = new List<string>();
            int migrated = 0;
            var inputs = Corpus.Inputs().Where(i => i.Value != null).ToList();
            inputs.Add(new KeyValuePair<string, byte[]>("edited", Edited()));
            foreach (KeyValuePair<string, byte[]> input in inputs)
            {
                string where = input.Key + ": ";
                Migration writable = Migration.Run(Path.Combine(scratch, "writable"), input.Value, defaults);
                Migration readOnly = Migration.Run(Path.Combine(scratch, "readonly"), input.Value, defaults, true);

                Check(failures, where, (File.GetAttributes(readOnly.LegacyPath) & FileAttributes.ReadOnly) != 0, "the legacy file lost its read-only attribute");
                LegacyKept(failures, where, readOnly, input.Value);
                Check(failures, where, readOnly.BepInExRefused == writable.BepInExRefused, "BepInEx refused one copy and not the other");
                if (writable.BepInExRefused || readOnly.BepInExRefused) continue;

                Check(failures, where, readOnly.Loaded.Status == writable.Loaded.Status, "status " + readOnly.Loaded.Status + ", writable " + writable.Loaded.Status);
                Check(failures, where, SameFields(writable.Loaded.Config, readOnly.Loaded.Config), "the read-only copy runs on " + Difference(writable.Loaded.Config, readOnly.Loaded.Config));
                bool written = File.Exists(writable.ConfigPath);
                Check(failures, where, File.Exists(readOnly.ConfigPath) == written, "CameraUnlock.ini exists for one copy only");
                if (written && File.Exists(readOnly.ConfigPath))
                {
                    Check(failures, where, File.ReadAllBytes(writable.ConfigPath).SequenceEqual(File.ReadAllBytes(readOnly.ConfigPath)), "the two copies wrote different CameraUnlock.ini");
                }
                if (writable.Loaded.Status == ConfigLoadStatus.Migrated) migrated++;
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures.Take(40).ToArray()));
            Assert.True(migrated > 500, migrated + " read-only inputs migrated");
        }

        /// <summary>
        /// Fresh equals upgrade: the first-run file of each payload of the dev build imports with
        /// Defaults.ini at the built-in values into exactly the committed file. No build shipped a
        /// config file or a launcher seed.
        /// </summary>
        [Fact]
        public void EveryPublishedFirstRunImportsIntoTheCommittedFile()
        {
            byte[] committed = File.ReadAllBytes(RenderTests.CommittedPath);
            foreach (string tag in Corpus.FirstRunFiles)
            {
                Migration m = Migration.Run(Path.Combine(scratch, "game"), Corpus.FirstRun(tag), defaults);
                Assert.Equal(ConfigLoadStatus.Migrated, m.Loaded.Status);
                Assert.True(committed.SequenceEqual(File.ReadAllBytes(m.ConfigPath)), tag);
            }
        }

        /// <summary>
        /// Every sensitivity shipped at 1 and the x inversion was code, so a first-run file drops
        /// nothing.
        /// </summary>
        [Fact]
        public void ThePublishedFirstRunDropsNothing()
        {
            foreach (string tag in Corpus.FirstRunFiles)
            {
                var dropped = new List<DroppedValue>();
                MapOf(Corpus.FirstRun(tag), dropped);
                Assert.Empty(dropped);
            }
        }

        // The global rows the table does not keep for the game, in the order the import gives them.
        private static readonly string[] FollowingRows =
        {
            "[Network] UdpPort", "[General] EnableOnStartup", "[General] WorldSpaceYaw",
            "[General] RotationEnabled", "[Position] PositionEnabled",
            "[Smoothing] LocalSmoothing", "[Smoothing] RemoteSmoothing",
            "[Position] PositionLimitX", "[Position] PositionLimitY", "[Position] PositionLimitYDown",
            "[Position] PositionLimitZ", "[Position] PositionLimitZBack",
            "[Hotkeys] ToggleKey", "[Hotkeys] CycleTrackingModeKey", "[Hotkeys] YawModeKey",
        };

        // A Defaults.ini that differs from the built-in values on every row in FollowingRows, and
        // from the corpus alternate on every row but the booleans, where no third value exists.
        private const string OtherDefaults =
            "[CameraUnlock]\r\nConfigFormat=1\r\n" +
            "[Network]\r\nUdpPort=4250\r\n" +
            "[General]\r\nEnableOnStartup=false\r\nWorldSpaceYaw=false\r\nRotationEnabled=false\r\n" +
            "[Smoothing]\r\nLocalSmoothing=0.25\r\nRemoteSmoothing=0.6\r\n" +
            "[Position]\r\nPositionEnabled=true\r\nPositionLimitX=0.35\r\nPositionLimitY=0.35\r\nPositionLimitYDown=0.35\r\n" +
            "PositionLimitZ=0.35\r\nPositionLimitZBack=0.35\r\n" +
            "[Hotkeys]\r\nToggleKey=F2\r\nCycleTrackingModeKey=F3\r\nYawModeKey=F4\r\n";

        // Each legacy key the map carries into a global row, with the rows it sets.
        private static readonly KeyValuePair<string, string[]>[] LegacyRows =
        {
            Rows("Player1Port", "[Network] UdpPort"),
            Rows("EnableOnStartup", "[General] EnableOnStartup"),
            Rows("WorldSpaceYaw", "[General] WorldSpaceYaw"),
            Rows("LocalSmoothing", "[Smoothing] LocalSmoothing"),
            Rows("RemoteSmoothing", "[Smoothing] RemoteSmoothing"),
            Rows("LimitX", "[Position] PositionLimitX"),
            Rows("LimitY", "[Position] PositionLimitY"),
            Rows("LimitYDown", "[Position] PositionLimitYDown"),
            Rows("LimitZ", "[Position] PositionLimitZ"),
            Rows("ToggleKey", "[Hotkeys] ToggleKey"),
            Rows("PositionToggleKey", "[Hotkeys] CycleTrackingModeKey"),
            Rows("YawModeKey", "[Hotkeys] YawModeKey"),
        };

        // The row behind each field Migration.Fields lists; any other field is local or no row.
        private static readonly Dictionary<string, string> FieldRows = new Dictionary<string, string>
        {
            { "UdpPort", "[Network] UdpPort" },
            { "EnableOnStartup", "[General] EnableOnStartup" },
            { "WorldSpaceYaw", "[General] WorldSpaceYaw" },
            { "RotationEnabled", "[General] RotationEnabled" },
            { "PositionEnabled", "[Position] PositionEnabled" },
            { "LocalSmoothing", "[Smoothing] LocalSmoothing" },
            { "Position.LocalSmoothing", "[Smoothing] LocalSmoothing" },
            { "RemoteSmoothing", "[Smoothing] RemoteSmoothing" },
            { "Position.RemoteSmoothing", "[Smoothing] RemoteSmoothing" },
            { "Position.LimitX", "[Position] PositionLimitX" },
            { "Position.LimitY", "[Position] PositionLimitY" },
            { "Position.LimitYDown", "[Position] PositionLimitYDown" },
            { "Position.LimitZ", "[Position] PositionLimitZ" },
            { "Position.LimitZBack", "[Position] PositionLimitZBack" },
            { "ToggleKey", "[Hotkeys] ToggleKey" },
            { "CycleTrackingModeKey", "[Hotkeys] CycleTrackingModeKey" },
            { "YawModeKey", "[Hotkeys] YawModeKey" },
        };

        /// <summary>
        /// A setting the player never changed follows Defaults.ini (owner rule of 2026-09-26): the
        /// empty file and the dev build's first-run file leave every global row to it, and under
        /// a Defaults.ini that differs on every such row the migrated file holds default on each
        /// and the session runs on Defaults.ini's values.
        /// </summary>
        [Fact]
        public void AnUntouchedFileFollowsDefaultsIni()
        {
            DefaultsFile other = OtherDefaultsFile();
            WobblyLifeSettings fresh = FreshUnder(other);
            SortedDictionary<string, string> builtIn = Migration.Fields(Migration.Defaults());
            foreach (KeyValuePair<string, string> f in Migration.Fields(fresh))
            {
                // The tracking mode differs as a pair: position only, where the built-in mode is both.
                if (FieldRows.ContainsKey(f.Key) && f.Key != "PositionEnabled") Assert.True(f.Value != builtIn[f.Key], f.Key + " does not differ in the other Defaults.ini");
            }

            var inputs = new List<KeyValuePair<string, byte[]>> { new KeyValuePair<string, byte[]>("empty file", new byte[0]) };
            foreach (string tag in Corpus.FirstRunFiles) inputs.Add(new KeyValuePair<string, byte[]>(tag + " first run", Corpus.FirstRun(tag)));
            foreach (KeyValuePair<string, byte[]> input in inputs)
            {
                Assert.Equal(FollowingRows, RowsOf(MapOf(input.Value, new List<DroppedValue>())));
                AssertMigration(input.Key, input.Value, other, fresh, new string[0]);
            }
        }

        /// <summary>
        /// A setting the player changed keeps the player's value: the dev first run with one
        /// legacy key at the corpus alternate leaves every global row but the ones it sets to
        /// Defaults.ini, and those take the value the import read.
        /// </summary>
        [Fact]
        public void AChangedSettingKeepsThePlayersValue()
        {
            DefaultsFile other = OtherDefaultsFile();
            WobblyLifeSettings fresh = FreshUnder(other);
            foreach (KeyValuePair<string, string[]> legacyKey in LegacyRows)
            {
                MutationKey key = Corpus.Keys.Single(k => k.Key == legacyKey.Key);
                byte[] edited = Edit(Corpus.FirstRun("dev"), key.Section, legacyKey.Key, key.Alternate);
                Assert.Equal(FollowingRows.Except(legacyKey.Value).ToArray(), RowsOf(MapOf(edited, new List<DroppedValue>())));
                AssertMigration(legacyKey.Key + " = " + key.Alternate, edited, other, fresh, legacyKey.Value);
            }
        }

        /// <summary>
        /// N3: a legacy hotkey on a Ctrl, Shift or Alt key alone imports as unbound, logged as
        /// ModifierKey, and the player keeps the Ctrl+Shift chord ChordHotkeys polled beside it.
        /// </summary>
        [Fact]
        public void AModifierKeyHotkeyUnbindsAndKeepsTheChord()
        {
            byte[] edited = Edit(Corpus.FirstRun("dev"), LegacyConfigReader.Controls, "ToggleKey", "LeftShift");
            var dropped = new List<DroppedValue>();
            LegacyFollowsDefaultsIni follows = MapOf(edited, dropped);
            Assert.DoesNotContain(ConfigConcepts.ToggleKey, follows.Concepts);
            DroppedValue modifier = dropped.Single(d => d.Rule == DropRule.ModifierKey);
            Assert.Equal(LegacyConfigReader.Controls, modifier.Section);
            Assert.Equal("ToggleKey", modifier.Key);
            Assert.Equal("LeftShift", modifier.Value);

            Migration m = Migration.Run(Path.Combine(scratch, "modifier"), edited, defaults);
            Assert.Equal(ConfigLoadStatus.Migrated, m.Loaded.Status);
            Assert.Equal("Ctrl+Shift+Y", m.Loaded.Config.ToggleKeyName);
            Assert.Equal("Ctrl+Shift+Y", FileRows(m.ConfigPath)["[Hotkeys] ToggleKey"]);
            Assert.Contains(m.LegacyPath + ": " + modifier.Describe(), m.Loaded.Log);
        }

        /// <summary>
        /// N1: a key code Unity names no key for, which BepInEx reads into the enum from a number
        /// in the .cfg, imports as unbound, logged as KeyCodeOutOfRange, and the player keeps the
        /// Ctrl+Shift chord ChordHotkeys polled beside it.
        /// </summary>
        [Fact]
        public void AKeyCodeUnityNamesNoKeyForUnbindsAndKeepsTheChord()
        {
            byte[] edited = Edit(Corpus.FirstRun("dev"), LegacyConfigReader.Controls, "ToggleKey", "10");
            var dropped = new List<DroppedValue>();
            LegacyFollowsDefaultsIni follows = MapOf(edited, dropped);
            Assert.DoesNotContain(ConfigConcepts.ToggleKey, follows.Concepts);
            DroppedValue unnamed = dropped.Single(d => d.Rule == DropRule.KeyCodeOutOfRange);
            Assert.Equal(LegacyConfigReader.Controls, unnamed.Section);
            Assert.Equal("ToggleKey", unnamed.Key);
            Assert.Equal("10", unnamed.Value);

            Migration m = Migration.Run(Path.Combine(scratch, "unnamed"), edited, defaults);
            Assert.Equal(ConfigLoadStatus.Migrated, m.Loaded.Status);
            Assert.Equal("Ctrl+Shift+Y", m.Loaded.Config.ToggleKeyName);
            Assert.Equal("Ctrl+Shift+Y", FileRows(m.ConfigPath)["[Hotkeys] ToggleKey"]);
            Assert.Contains(m.LegacyPath + ": " + unnamed.Describe(), m.Loaded.Log);
        }

        /// <summary>
        /// A sensitivity is no setting: one the player changed is dropped and logged, and the
        /// migrated file is the committed one.
        /// </summary>
        [Fact]
        public void AChangedSensitivityIsDropped()
        {
            byte[] edited = Edit(Corpus.FirstRun("dev"), LegacyConfigReader.Sensitivity, "YawSensitivity", "2");
            Migration m = Migration.Run(Path.Combine(scratch, "sensitivity"), edited, defaults);
            Assert.Equal(ConfigLoadStatus.Migrated, m.Loaded.Status);
            var dropped = new List<DroppedValue>();
            MapOf(edited, dropped);
            DroppedValue sensitivity = dropped.Single();
            Assert.Equal(DropRule.PoseShaping, sensitivity.Rule);
            Assert.Equal("YawSensitivity", sensitivity.Key);
            Assert.Contains(m.LegacyPath + ": " + sensitivity.Describe(), m.Loaded.Log);
            Assert.True(File.ReadAllBytes(RenderTests.CommittedPath).SequenceEqual(File.ReadAllBytes(m.ConfigPath)));
        }

        private void AssertMigration(string name, byte[] legacy, DefaultsFile other, WobblyLifeSettings fresh, string[] changed)
        {
            LegacyReading import = LegacyReading.Import(DifferentialTests.Place(scratch, "import", legacy));
            WobblyLifeSettings expected = Migration.Defaults();
            LegacyMigration.Map(import.Values, expected, new List<DroppedValue>(), new List<PoseShapingValue>());

            Migration m = Migration.Run(Path.Combine(scratch, "other"), legacy, other);
            Assert.True(m.Loaded.Status == ConfigLoadStatus.Migrated, name + ": " + m.Loaded.Status);
            Dictionary<string, string> written = FileRows(m.ConfigPath);
            foreach (string row in FollowingRows.Except(changed))
            {
                Assert.True(written[row] == "default", name + ": " + row + "=" + written[row] + " does not follow Defaults.ini");
            }
            SortedDictionary<string, string> want = Migration.Fields(expected), ini = Migration.Fields(fresh);
            foreach (KeyValuePair<string, string> f in Migration.Fields(m.Loaded.Config))
            {
                string row;
                bool followsIni = FieldRows.TryGetValue(f.Key, out row) && !changed.Contains(row);
                string wanted = followsIni ? ini[f.Key] : want[f.Key];
                Assert.True(f.Value == wanted, name + ": " + f.Key + " is " + f.Value + ", not " + wanted);
            }
        }

        private DefaultsFile OtherDefaultsFile()
        {
            string path = Path.Combine(scratch, "other-profile", "CameraUnlock", "Defaults.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, OtherDefaults, new System.Text.UTF8Encoding(false));
            return DefaultsFile.At(path);
        }

        // What a first start runs on under the given Defaults.ini.
        private WobblyLifeSettings FreshUnder(DefaultsFile file)
        {
            Migration m = Migration.Run(Path.Combine(scratch, "fresh"), null, file);
            Assert.Equal(ConfigLoadStatus.Created, m.Loaded.Status);
            return m.Loaded.Config;
        }

        private LegacyFollowsDefaultsIni MapOf(byte[] legacy, List<DroppedValue> dropped)
        {
            LegacyReading import = LegacyReading.Import(DifferentialTests.Place(scratch, "import", legacy));
            return LegacyMigration.Map(import.Values, Migration.Defaults(), dropped, new List<PoseShapingValue>());
        }

        private static string[] RowsOf(LegacyFollowsDefaultsIni follows)
        {
            return follows.Concepts.Select(c => "[" + c.Section + "] " + c.Key).ToArray();
        }

        private static KeyValuePair<string, string[]> Rows(string legacyKey, params string[] rows)
        {
            return new KeyValuePair<string, string[]>(legacyKey, rows);
        }

        // The legacy file with one key's value replaced, or the key added under its section where
        // the file has no line for it.
        private static byte[] Edit(byte[] legacy, string section, string key, string value)
        {
            var lines = System.Text.Encoding.ASCII.GetString(legacy).Split('\n').ToList();
            int hits = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                if (!lines[i].StartsWith(key + " = ", StringComparison.Ordinal)) continue;
                lines[i] = key + " = " + value + (lines[i].EndsWith("\r", StringComparison.Ordinal) ? "\r" : "");
                hits++;
            }
            if (hits == 0)
            {
                int header = lines.FindIndex(l => l.TrimEnd('\r') == "[" + section + "]");
                if (header < 0) throw new InvalidOperationException("the dev first run has no section " + section);
                lines.Insert(header + 1, key + " = " + value + "\r");
                hits = 1;
            }
            if (hits != 1) throw new InvalidOperationException("the dev first run holds " + hits + " lines of " + key);
            return System.Text.Encoding.ASCII.GetBytes(string.Join("\n", lines.ToArray()));
        }

        /// <summary>Each row of a canonical file as "[Section] Key" to its value text.</summary>
        private static Dictionary<string, string> FileRows(string path)
        {
            var rows = new Dictionary<string, string>();
            string section = null;
            foreach (string line in File.ReadAllText(path).Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith(";", StringComparison.Ordinal)) continue;
                if (line.StartsWith("[", StringComparison.Ordinal))
                {
                    section = line;
                    continue;
                }
                int eq = line.IndexOf('=');
                rows[section + " " + line.Substring(0, eq)] = line.Substring(eq + 1);
            }
            return rows;
        }

        private void SecondLoad(List<string> failures, string where, Migration first)
        {
            byte[] config = File.ReadAllBytes(first.ConfigPath);
            byte[] legacy = File.ReadAllBytes(first.LegacyPath);
            DateTime legacyWritten = File.GetLastWriteTimeUtc(first.LegacyPath);
            DateTime configWritten = File.GetLastWriteTimeUtc(first.ConfigPath);
            ConfigOwner<WobblyLifeSettings> owner = Migration.Reopen(first.LegacyPath, defaults);
            ConfigLoadResult<WobblyLifeSettings> again = owner.Load();
            Check(failures, where, again.Status == ConfigLoadStatus.Canonical, "second load " + again.Status);
            Check(failures, where, SameFields(first.Loaded.Config, again.Config), "the second load reads other values");
            Check(failures, where, again.Log.Any(l => l.Contains(first.LegacyPath + " is left as it was and is not read.")), "the second load does not say the legacy file is not read");
            Check(failures, where, config.SequenceEqual(File.ReadAllBytes(first.ConfigPath)) && configWritten == File.GetLastWriteTimeUtc(first.ConfigPath), "the second load rewrote CameraUnlock.ini");
            Check(failures, where, legacy.SequenceEqual(File.ReadAllBytes(first.LegacyPath)) && legacyWritten == File.GetLastWriteTimeUtc(first.LegacyPath), "the second load changed the legacy file");
        }

        /// <summary>The legacy file keeps the bytes and the write time it had before the load, or is still absent.</summary>
        private static void LegacyKept(List<string> failures, string where, Migration m, byte[] input)
        {
            if (input == null)
            {
                Check(failures, where, !File.Exists(m.LegacyPath), "a legacy file appeared");
                return;
            }
            Check(failures, where, File.ReadAllBytes(m.LegacyPath).SequenceEqual(input), "the legacy file changed");
            Check(failures, where, File.GetLastWriteTimeUtc(m.LegacyPath) == m.LegacyWritten, "the legacy file was rewritten");
        }

        /// <summary>The approved rules, field by field, from the frozen reader to the map.</summary>
        private static void CheckRules(List<string> failures, string where, LegacyReading import, WobblyLifeSettings mapped,
            List<DroppedValue> dropped, List<PoseShapingValue> poseShaping)
        {
            LegacyConfig l = import.Values;
            Check(failures, where, mapped.UdpPort == l.Player1Port && mapped.Player2Port == l.Player2Port
                && mapped.Player3Port == l.Player3Port && mapped.Player4Port == l.Player4Port, "ports");
            Check(failures, where, mapped.EnableOnStartup == import.Enabled, "EnableOnStartup");
            Check(failures, where, mapped.WorldSpaceYaw == import.WorldSpaceYaw, "WorldSpaceYaw");
            Check(failures, where, mapped.RotationEnabled == import.RotationEnabled && mapped.PositionEnabled == import.PositionEnabled, "tracking mode");
            Check(failures, where, LegacyReading.SameValue(mapped.LocalSmoothing, l.LocalSmoothing) && LegacyReading.SameValue(mapped.RemoteSmoothing, l.RemoteSmoothing), "smoothing");
            Check(failures, where, LegacyReading.SameValue(mapped.Position.LimitX, l.PositionLimitX)
                && LegacyReading.SameValue(mapped.Position.LimitY, l.PositionLimitY)
                && LegacyReading.SameValue(mapped.Position.LimitYDown, l.PositionLimitYDown)
                && LegacyReading.SameValue(mapped.Position.LimitZ, l.PositionLimitZ)
                && LegacyReading.SameValue(mapped.Position.LimitZBack, 0.10f), "position limits");
            Check(failures, where, mapped.DisableInMenus == l.DisableInMenus && mapped.DisableWhenPaused == l.DisableWhenPaused, "game state");

            var polled = new Dictionary<string, string>
            {
                { "Toggle", mapped.ToggleKeyName },
                { "CycleTrackingMode", mapped.CycleTrackingModeKeyName },
                { "YawMode", mapped.YawModeKeyName },
            };
            var legacyKeys = new Dictionary<string, KeyValuePair<string, KeyCode>>
            {
                { "Toggle", new KeyValuePair<string, KeyCode>("ToggleKey", l.ToggleKey) },
                { "CycleTrackingMode", new KeyValuePair<string, KeyCode>("PositionToggleKey", l.PositionToggleKey) },
                { "YawMode", new KeyValuePair<string, KeyCode>("YawModeKey", l.YawModeKey) },
            };
            foreach (KeyValuePair<string, string> action in polled)
            {
                string bindings = Migration.Polled(action.Value);
                if (bindings == null) continue;
                string published = import.Hotkeys[action.Key];
                if (IsModifier(legacyKeys[action.Key].Value) || HasNoName(legacyKeys[action.Key].Value))
                {
                    // N3 and N1: a modifier alone, or a code Unity names no key for, is unbound,
                    // the chord kept.
                    published = published.Substring(published.IndexOf(", ", StringComparison.Ordinal) + 2);
                }
                Check(failures, where, bindings == published, action.Key + " polls " + bindings + ", the published build " + import.Hotkeys[action.Key]);
            }

            var shipped = new LegacyConfig();
            var expectedShaping = new[]
            {
                Shaping(LegacyConfigReader.Sensitivity, "YawSensitivity", l.YawSensitivity, shipped.YawSensitivity),
                Shaping(LegacyConfigReader.Sensitivity, "PitchSensitivity", l.PitchSensitivity, shipped.PitchSensitivity),
                Shaping(LegacyConfigReader.Sensitivity, "RollSensitivity", l.RollSensitivity, shipped.RollSensitivity),
                Shaping(LegacyConfigReader.Position, "SensitivityX", l.PositionSensitivityX, shipped.PositionSensitivityX),
                Shaping(LegacyConfigReader.Position, "SensitivityY", l.PositionSensitivityY, shipped.PositionSensitivityY),
                Shaping(LegacyConfigReader.Position, "SensitivityZ", l.PositionSensitivityZ, shipped.PositionSensitivityZ),
            };
            Check(failures, where, poseShaping.Count == expectedShaping.Length, poseShaping.Count + " pose-shaping values");
            var expectedDropped = new List<string>();
            foreach (object[] e in expectedShaping)
            {
                PoseShapingValue v = poseShaping.FirstOrDefault(p => p.Section == (string)e[0] && p.Key == (string)e[1]);
                Check(failures, where, v != null && v.Folded == (bool)e[2], e[1] + " pose shaping");
                if (!(bool)e[2]) expectedDropped.Add("PoseShaping [" + e[0] + "] " + e[1]);
            }
            foreach (KeyValuePair<string, KeyCode> hotkey in legacyKeys.Values)
            {
                if (IsModifier(hotkey.Value)) expectedDropped.Add("ModifierKey [" + LegacyConfigReader.Controls + "] " + hotkey.Key);
                if (HasNoName(hotkey.Value)) expectedDropped.Add("KeyCodeOutOfRange [" + LegacyConfigReader.Controls + "] " + hotkey.Key);
            }
            var actualDropped = dropped.Select(d => d.Rule + " [" + d.Section + "] " + d.Key).ToList();
            Check(failures, where, expectedDropped.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(actualDropped.OrderBy(x => x, StringComparer.Ordinal)),
                "dropped " + string.Join("; ", actualDropped.ToArray()));
        }

        private static bool IsModifier(KeyCode key)
        {
            return key >= KeyCode.RightShift && key <= KeyCode.LeftAlt;
        }

        private static bool HasNoName(KeyCode key)
        {
            return key != KeyCode.None && !KeyBindings.HasName((int)key);
        }

        private static object[] Shaping(string section, string key, float value, float shipped)
        {
            return new object[] { section, key, value == shipped };
        }

        private static object[] Shaping(string section, string key, bool value, bool shipped)
        {
            return new object[] { section, key, value == shipped };
        }

        private static bool SameFields(WobblyLifeSettings a, WobblyLifeSettings b)
        {
            return Difference(a, b) == null;
        }

        // The mapped fields, with every field of a row left to Defaults.ini at the built-in value
        // the scratch Defaults.ini holds.
        private static SortedDictionary<string, string> Expected(WobblyLifeSettings mapped, LegacyFollowsDefaultsIni follows)
        {
            var rows = new HashSet<string>(RowsOf(follows));
            SortedDictionary<string, string> want = Migration.Fields(mapped);
            SortedDictionary<string, string> builtIn = Migration.Fields(Migration.Defaults());
            foreach (KeyValuePair<string, string> f in FieldRows)
            {
                if (rows.Contains(f.Value)) want[f.Key] = builtIn[f.Key];
            }
            return want;
        }

        private static string Difference(WobblyLifeSettings a, WobblyLifeSettings b)
        {
            return Difference(Migration.Fields(a), Migration.Fields(b));
        }

        private static string Difference(SortedDictionary<string, string> x, SortedDictionary<string, string> y)
        {
            foreach (KeyValuePair<string, string> f in x)
            {
                if (f.Value != y[f.Key]) return f.Key + " " + f.Value + " / " + y[f.Key];
            }
            return null;
        }

        private static string Names(Migration m)
        {
            return string.Join(", ", Directory.GetFiles(Path.GetDirectoryName(m.LegacyPath)).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        }

        /// <summary>
        /// What core's canonical config lint checks that a file the owner wrote can get wrong:
        /// the reader finds nothing to report, the stamp, CRLF only, ASCII only, and the table
        /// reads every line.
        /// </summary>
        internal static string Lint(byte[] bytes)
        {
            CanonicalIni doc = CanonicalIni.Parse(bytes);
            if (!doc.IsReadable) return "is unreadable";
            if (!CanonicalIni.HasStamp(bytes) || doc.FormatVersion != CanonicalIni.ConfigFormat) return "has no [CameraUnlock] ConfigFormat=1";
            if (doc.Diagnostics.Count > 0) return "draws " + doc.Diagnostics[0].Describe();
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] > 0x7E || (bytes[i] < 0x20 && bytes[i] != 0x0D && bytes[i] != 0x0A)) return "holds byte " + bytes[i] + " at " + i;
                if (bytes[i] == 0x0A && (i == 0 || bytes[i - 1] != 0x0D)) return "has an LF without CR at " + i;
                if (bytes[i] == 0x0D && (i + 1 == bytes.Length || bytes[i + 1] != 0x0A)) return "has a CR without LF at " + i;
            }
            if (bytes.Length < 2 || bytes[bytes.Length - 2] != 0x0D || bytes[bytes.Length - 1] != 0x0A) return "does not end in CRLF";
            ApplyReport report = WobblyLifeSettings.Table().Apply(doc, new WobblyLifeSettings());
            if (report.Diagnostics.Count > 0) return "draws " + report.Diagnostics[0].Describe();
            return null;
        }

        /// <summary>A legacy file a player edited away from every default the map carries.</summary>
        internal static byte[] Edited()
        {
            string text = System.Text.Encoding.ASCII.GetString(Corpus.FirstRun("dev"));
            var edits = new Dictionary<string, string>
            {
                { "Player1Port = 4242\r\n", "Player1Port = 5555\r\n" },
                { "Player3Port = 4244\r\n", "Player3Port = 5557\r\n" },
                { "EnableOnStartup = true\r\n", "EnableOnStartup = false\r\n" },
                { "WorldSpaceYaw = true\r\n", "WorldSpaceYaw = false\r\n" },
                { "LocalSmoothing = 0\r\n", "LocalSmoothing = 0.25\r\n" },
                { "RemoteSmoothing = 0.15\r\n", "RemoteSmoothing = 0.4\r\n" },
                { "LimitX = 0.3\r\n", "LimitX = 0.25\r\n" },
                { "LimitY = 0.15\r\n", "LimitY = 0.1\r\n" },
                { "ToggleKey = End\r\n", "ToggleKey = F8\r\n" },
                { "PositionToggleKey = PageUp\r\n", "PositionToggleKey = None\r\n" },
                { "DisableInMenus = true\r\n", "DisableInMenus = false\r\n" },
                { "YawSensitivity = 1\r\n", "YawSensitivity = 1.5\r\n" },
            };
            foreach (KeyValuePair<string, string> e in edits)
            {
                if (!text.Contains(e.Key)) throw new InvalidOperationException("the dev first run has no line " + e.Key);
                text = text.Replace(e.Key, e.Value);
            }
            return System.Text.Encoding.ASCII.GetBytes(text);
        }

        private static void Check(List<string> failures, string where, bool ok, string what)
        {
            if (!ok) failures.Add(where + what);
        }
    }
}
