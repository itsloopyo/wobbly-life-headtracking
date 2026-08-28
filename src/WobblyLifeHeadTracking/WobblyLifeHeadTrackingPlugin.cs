using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WobblyLifeHeadTracking
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Wobbly Life.exe")]
    [BepInProcess("Wobbly Life_EOS.exe")]
    public class WobblyLifeHeadTrackingPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.cameraunlock.wobblylife.headtracking";
        public const string PluginName = "Wobbly Life Head Tracking";
        public const string PluginVersion = "0.0.0";

        internal static ManualLogSource Log { get; private set; }
        internal static ConfigFile ConfigFile { get; private set; }

        private void Awake()
        {
            Log = Logger;
            ConfigFile = Config;

            // BepInEx builds BepInEx_Manager before this game loads its first scene, so the
            // DontDestroyOnLoad it applies there never takes and the first LoadScene destroys
            // every plugin component sitting on it. Wait until a scene exists, then host the
            // mod on an object of our own, where the call does stick.
            if (SceneManager.GetActiveScene().IsValid())
            {
                CreateRuntime();
                return;
            }

            SceneManager.sceneLoaded += OnFirstSceneLoaded;
        }

        private static void OnFirstSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnFirstSceneLoaded;
            CreateRuntime();
        }

        private static void CreateRuntime()
        {
            var host = new GameObject(PluginName);
            Object.DontDestroyOnLoad(host);
            host.AddComponent<WobblyLifeHeadTrackingRuntime>();
        }
    }
}
