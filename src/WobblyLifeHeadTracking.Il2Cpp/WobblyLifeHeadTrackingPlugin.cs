using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using WobblyLifeHeadTracking.Camera;

namespace WobblyLifeHeadTracking
{
    /// <summary>
    /// Entry point for the IL2CPP build of Wobbly Life (the Xbox Game Pass / GDK
    /// copy). The Steam copy is a Mono build and uses the BepInEx 5 plugin in
    /// src/WobblyLifeHeadTracking instead; the two are never deployed together.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class WobblyLifeHeadTrackingPlugin : BasePlugin
    {
        public const string PluginGuid = "com.cameraunlock.wobblylife.headtracking";
        public const string PluginName = "Wobbly Life Head Tracking";
        public const string PluginVersion = "0.0.0";

        // Hides BasePlugin.Log deliberately: the rest of the mod reaches the logger
        // statically, as it does on the Mono build. base.Log is the instance one.
        internal static new ManualLogSource Log { get; private set; }
        internal static ConfigFile ConfigFile { get; private set; }

        // BepInEx roots the plugin instance, so holding the host object here keeps
        // it out of reach of IL2CPP's GC for the lifetime of the process.
        private static GameObject _host;

        public override void Load()
        {
            Log = base.Log;
            ConfigFile = Config;

            // Every managed MonoBehaviour has to exist in the IL2CPP domain before
            // AddComponent can be asked for it, and Unity's message dispatch finds
            // OnPreCull/OnPostRender on an injected type by name once it does.
            ClassInjector.RegisterTypeInIl2Cpp<WobblyLifeHeadTrackingRuntime>();
            ClassInjector.RegisterTypeInIl2Cpp<WobblyLifeCameraController>();
            ClassInjector.RegisterTypeInIl2Cpp<GameplayCameraRenderHook>();

            _host = new GameObject(PluginName);
            Object.DontDestroyOnLoad(_host);
            _host.hideFlags = HideFlags.HideAndDontSave;
            _host.AddComponent<WobblyLifeHeadTrackingRuntime>();
        }
    }
}
