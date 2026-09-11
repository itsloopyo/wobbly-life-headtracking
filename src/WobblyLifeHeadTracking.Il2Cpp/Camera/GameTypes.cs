using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace WobblyLifeHeadTracking.Camera
{
    /// <summary>
    /// The parts of the IL2CPP side that cannot be written as ordinary typed calls.
    ///
    /// The mod compiles against Unity's published reference assemblies and against no
    /// game assembly at all, because the build has to work on a machine that does not
    /// own the game. At runtime BepInEx replaces the Unity assemblies with the ones
    /// Il2CppInterop generated from this install, and a member whose signature differs
    /// between the two - an array, a delegate, a game type - cannot be called from
    /// compiled IL. Those members are reached by reflection here and nowhere else.
    /// </summary>
    internal static class GameTypes
    {
        // Camera.allCameras is Camera[] in the reference assembly and
        // Il2CppReferenceArray<Camera> in the generated one, so it is read by
        // reflection and enumerated through IEnumerable, which both shapes offer.
        private static PropertyInfo _allCameras;

        // GameplayCamera lives in the game's own Game.dll, which has no build-time
        // reference. Il2CppInterop generates a proxy for it into BepInEx/interop and
        // loads it before any plugin runs, so it is reachable by name.
        private static Type _gameplayCameraType;
        private static MethodInfo _getGameplayCameraComponent;
        private static MethodInfo _getLocalPlayerId;
        private static bool _resolved;

        /// <summary>Whether per-player camera assignment is available this session.</summary>
        internal static bool HasPlayerAssignment => _getLocalPlayerId != null;

        internal static void Resolve(Action<string> log)
        {
            if (_resolved) return;

            _allCameras = typeof(UnityEngine.Camera).GetProperty(
                "allCameras", BindingFlags.Public | BindingFlags.Static);
            if (_allCameras == null)
            {
                throw new MissingMemberException(
                    "UnityEngine.Camera.allCameras was not found on the generated interop assembly.");
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                _gameplayCameraType = assembly.GetType("GameplayCamera");
                if (_gameplayCameraType != null) break;
            }

            if (_gameplayCameraType == null)
            {
                throw new TypeLoadException("GameplayCamera proxy type was not found in the generated interop assemblies.");
            }

            // GetComponent<GameplayCamera>() hands back the proxy already typed, which
            // is both the "is this a gameplay camera" test and the receiver
            // GetLocalPlayerid needs. Picked out of GetMethods rather than by
            // signature: the interop GameObject carries several GetComponent
            // overloads and only one of them is the zero-argument generic.
            foreach (MethodInfo candidate in typeof(GameObject).GetMethods(
                         BindingFlags.Public | BindingFlags.Instance))
            {
                if (candidate.Name != "GetComponent") continue;
                if (!candidate.IsGenericMethodDefinition) continue;
                if (candidate.GetParameters().Length != 0) continue;

                _getGameplayCameraComponent = candidate.MakeGenericMethod(_gameplayCameraType);
                break;
            }

            _getLocalPlayerId = _gameplayCameraType.GetMethod(
                "GetLocalPlayerid", BindingFlags.Public | BindingFlags.Instance);

            if (_getGameplayCameraComponent == null || _getLocalPlayerId == null)
            {
                throw new MissingMethodException("GameplayCamera.GetLocalPlayerid or GameObject.GetComponent<T>() is unavailable.");
            }

            log("GameplayCamera reflection initialized - multiplayer support enabled");
            _resolved = true;
        }

        /// <summary>Every camera Unity currently knows about, in no particular order.</summary>
        internal static IEnumerable AllCameras()
        {
            return (IEnumerable)_allCameras.GetValue(null, null);
        }

        /// <summary>
        /// The split-screen player this camera belongs to, or -1 when it is not a
        /// gameplay camera. Classified by the presence of the game's own
        /// GameplayCamera component, never by the camera's name: the UI, water and
        /// reflection cameras are named after the gameplay camera they belong to.
        /// </summary>
        internal static int GetPlayerIndex(UnityEngine.Camera cam)
        {
            object gameplayCamera = _getGameplayCameraComponent.Invoke(cam.gameObject, null);
            if (gameplayCamera == null) return -1;

            return (int)_getLocalPlayerId.Invoke(gameplayCamera, null);
        }
    }
}
