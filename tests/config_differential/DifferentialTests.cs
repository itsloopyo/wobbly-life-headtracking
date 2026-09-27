using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using WobblyLifeHeadTracking.Legacy;
using Xunit;

namespace WobblyLifeHeadTracking.Tests.ConfigDifferential
{
    /// <summary>
    /// Comparison 1: the newest published build's reader (the oracle) against the frozen reader
    /// (the import), over every input. What differs is what players see change that the
    /// conversion did not cause. No commit since the dev build changed how the file is read, so
    /// nothing may differ.
    /// </summary>
    public class DifferentialTests
    {
        // The oracle is the dev build's own reader, byte for byte; `git show dev:<path> | sha256sum`.
        private static readonly Dictionary<string, string> OracleHashes = new Dictionary<string, string>
        {
            { "tests/config_differential/Oracle/WobblyLifeConfig.cs", "68d9ee66a8812f1487315095b6b638ec08707c12dda7264f5a6b8d7871d85a39" },
        };

        // The frozen import. A change to either file changes how players' legacy files are read.
        private static readonly Dictionary<string, string> FrozenHashes = new Dictionary<string, string>
        {
            { "src/WobblyLifeHeadTracking/Legacy/LegacyConfig.cs", "804855773fb064269990c3314e810973224deda87c3fc41c72b3c90655d67577" },
            { "src/WobblyLifeHeadTracking/Legacy/LegacyConfigReader.cs", "48970c922a07b5d2a7752b2276851e4d3538bb0ab2c3cab7fd5113145b1e40e4" },
        };

        [Fact]
        public void OracleIsThePublishedReader()
        {
            foreach (KeyValuePair<string, string> file in OracleHashes)
            {
                Assert.Equal(file.Value, Sha256(file.Key));
            }
        }

        [Fact]
        public void FrozenImportIsUnchanged()
        {
            foreach (KeyValuePair<string, string> file in FrozenHashes)
            {
                Assert.Equal(file.Value, Sha256(file.Key));
            }
        }

        [Fact]
        public void KeysAreEveryDefinitionTheReaderBinds()
        {
            string dir = RepoPaths.Scratch();
            ConfigFile file = BepInExHost.Open(Path.Combine(dir, BepInExHost.Guid + ".cfg"), "0.0.0");
            LegacyConfigReader.Read(file);
            var bound = file.Keys.Select(d => d.Section + "\n" + d.Key).ToList();
            var listed = LegacyConfigReader.Keys.Select(k => k.Section + "\n" + k.Key).ToList();
            Assert.Equal(bound.OrderBy(x => x, StringComparer.Ordinal), listed.OrderBy(x => x, StringComparer.Ordinal));
            Assert.Empty(Directory.GetFiles(dir));
            Directory.Delete(dir, true);
        }

        [Fact]
        public void ComparisonOne()
        {
            string dir = RepoPaths.Scratch();
            var unexpected = new List<string>();
            int inputs = 0;
            int refused = 0;
            foreach (KeyValuePair<string, byte[]> input in Corpus.Inputs())
            {
                inputs++;
                string oraclePath = Place(dir, "oracle", input.Value);
                string importPath = Place(dir, "import", input.Value);
                DateTime written = input.Value == null ? default(DateTime) : File.GetLastWriteTimeUtc(importPath);

                LegacyReading oracle = LegacyReading.Oracle(oraclePath);
                LegacyReading import = LegacyReading.Import(importPath);

                if (input.Value == null)
                {
                    Assert.False(File.Exists(importPath), input.Key + ": the frozen reader created the file");
                }
                else
                {
                    Assert.True(File.ReadAllBytes(importPath).SequenceEqual(input.Value), input.Key + ": the frozen reader changed the file");
                    Assert.Equal(written, File.GetLastWriteTimeUtc(importPath));
                }

                if (oracle.Status == LoadStatus.Refused && import.Status == LoadStatus.Refused)
                {
                    refused++;
                    continue;
                }
                List<string> differences = LegacyReading.Differences(oracle, import);
                if (differences.Count == 0) continue;
                unexpected.Add(input.Key + ": " + string.Join("; ", differences.ToArray()));
            }
            Directory.Delete(dir, true);
            Assert.True(unexpected.Count == 0, string.Join("\n", unexpected.Take(40).ToArray()));
            Assert.True(inputs > 500, "the corpus produced " + inputs + " inputs");
            Assert.True(refused < inputs / 10, refused + " of " + inputs + " inputs made BepInEx refuse the file");
        }

        /// <summary>
        /// The readings above run BepInEx's own ConfigFile, the one vendor/bepinex ships for the
        /// Steam build. The Game Pass build reads the same .cfg through the same frozen reader
        /// compiled against BepInEx 6, whose first-run file is one of the inputs.
        /// </summary>
        [Fact]
        public void ReadsWithTheVendoredBepInEx()
        {
            Assert.Equal(new Version(5, 4, 23, 5), typeof(ConfigFile).Assembly.GetName().Version);
        }

        internal static string Place(string dir, string name, byte[] bytes)
        {
            string folder = Path.Combine(dir, name);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, BepInExHost.Guid + ".cfg");
            if (bytes != null) File.WriteAllBytes(path, bytes);
            return path;
        }

        internal static string Sha256(string repoPath)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(RepoPaths.Root, repoPath.Replace('/', Path.DirectorySeparatorChar)));
            using (SHA256 sha = SHA256.Create())
            {
                var text = new StringBuilder();
                foreach (byte b in sha.ComputeHash(bytes)) text.Append(b.ToString("x2"));
                return text.ToString();
            }
        }
    }
}
