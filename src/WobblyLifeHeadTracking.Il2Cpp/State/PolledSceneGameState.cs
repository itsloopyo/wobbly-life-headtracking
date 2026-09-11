using System;
using CameraUnlock.Core.Unity.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WobblyLifeHeadTracking.State
{
    /// <summary>
    /// Gameplay-vs-menu classification for the IL2CPP build, by the same scene-name
    /// patterns core's SceneGameStateDetector uses.
    ///
    /// It cannot use that detector: it subscribes to SceneManager.sceneLoaded, and
    /// under Il2CppInterop that event takes an Il2CppSystem delegate rather than the
    /// UnityAction the reference assembly declares, so the subscription throws
    /// MissingMethodException the first time the calling method is jitted. Polling
    /// the active scene name asks for nothing but a string and a float.
    /// </summary>
    public sealed class PolledSceneGameState
    {
        private const float PollIntervalSeconds = 0.1f;

        private readonly string[] _nonGameplayPatterns;
        private readonly Action<string> _log;

        private float _nextPollTime;
        private string _sceneName = string.Empty;
        private bool _isGameplayScene;
        private bool _isPaused;

        public bool DisableInMenuScenes { get; set; } = true;
        public bool DisableWhenPaused { get; set; } = true;

        /// <summary>Fired when a scene change flips the gameplay classification.</summary>
        public event Action<bool> GameplayStateChanged;

        public string CurrentSceneName => _sceneName;

        public PolledSceneGameState(Action<string> log = null)
        {
            _nonGameplayPatterns = SceneGameStateDetector.DefaultNonGameplayPatterns;
            _log = log;
            Poll();
            _log?.Invoke($"Scene state polling started. Scene: {_sceneName}, IsGameplay: {_isGameplayScene}");
        }

        public bool IsInGameplay
        {
            get
            {
                if (Time.unscaledTime >= _nextPollTime)
                {
                    Poll();
                }

                if (DisableInMenuScenes && !_isGameplayScene) return false;
                if (DisableWhenPaused && _isPaused) return false;
                return true;
            }
        }

        private void Poll()
        {
            _nextPollTime = Time.unscaledTime + PollIntervalSeconds;
            _isPaused = Time.timeScale <= 0f;

            string name = SceneManager.GetActiveScene().name ?? string.Empty;
            if (name == _sceneName) return;

            _sceneName = name;
            bool wasGameplay = _isGameplayScene;
            _isGameplayScene = Classify(name);

            _log?.Invoke($"Scene changed to '{name}' - IsGameplay: {_isGameplayScene}");

            if (_isGameplayScene != wasGameplay)
            {
                GameplayStateChanged?.Invoke(_isGameplayScene);
            }
        }

        private bool Classify(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;

            string lower = sceneName.ToLowerInvariant();
            foreach (string pattern in _nonGameplayPatterns)
            {
                if (lower.Contains(pattern)) return false;
            }
            return true;
        }
    }
}
