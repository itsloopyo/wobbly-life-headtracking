using System;
using CameraUnlock.Core.State;
using UnityEngine;
using UnityEngine.SceneManagement;
using WobblyLifeHeadTracking.Config;

namespace WobblyLifeHeadTracking.State
{
    public sealed class WobblyLifeGameStateDetector : IGameStateDetector
    {
        // Pre-lowercased; IsGameplayScene does a substring test with no allocations per check.
        private static readonly string[] NonGameplayPatterns = new string[]
        {
            "mainmenu",
            "menu",
            "splashscreen",
            "splash",
            "loading",
            "charactercustomization",
            "charactercreation",
            "customization",
            "intro",
            "credits"
        };

        private readonly WobblyLifeConfig _config;
        private readonly Action<string> _log;
        private string _currentSceneName;
        private string _currentSceneNameLower;
        private bool _isGameplayActive;
        private bool _isPaused;
        private bool _disposed;

        public event Action<bool> OnGameplayStateChanged;

        public event Action<bool> OnPauseStateChanged;

        public bool IsGameplayActive => _isGameplayActive;

        public bool IsPaused => _isPaused;

        public string CurrentSceneName => _currentSceneName;

        public bool IsInGameplay
        {
            get
            {
                if (_disposed) return false;

                UpdatePauseState();

                if (_config.DisableInMenus.Value && !_isGameplayActive)
                {
                    return false;
                }

                if (_config.DisableWhenPaused.Value && _isPaused)
                {
                    return false;
                }

                return true;
            }
        }

        public WobblyLifeGameStateDetector(WobblyLifeConfig config, Action<string> log)
        {
            _config = config;
            _log = log;

            Scene currentScene = SceneManager.GetActiveScene();
            UpdateSceneCache(currentScene.name);
            _isGameplayActive = IsGameplayScene();

            SceneManager.sceneLoaded += OnSceneLoaded;

            _log?.Invoke($"GameStateDetector initialized. Current scene: {_currentSceneName}, IsGameplay: {_isGameplayActive}");
        }

        public void InvalidateCache()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            UpdateSceneCache(currentScene.name);
            _isGameplayActive = IsGameplayScene();
        }

        private void UpdateSceneCache(string sceneName)
        {
            _currentSceneName = sceneName;
            _currentSceneNameLower = sceneName?.ToLowerInvariant();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            string previousScene = _currentSceneName;
            bool wasGameplay = _isGameplayActive;

            UpdateSceneCache(scene.name);
            _isGameplayActive = IsGameplayScene();

            _log?.Invoke($"Scene loaded: {_currentSceneName} (from {previousScene}), IsGameplay: {_isGameplayActive}");

            if (wasGameplay != _isGameplayActive)
            {
                OnGameplayStateChanged?.Invoke(_isGameplayActive);

                if (_isGameplayActive)
                {
                    _log?.Invoke("Entered gameplay - head tracking active");
                }
                else
                {
                    _log?.Invoke("Left gameplay - head tracking paused");
                }
            }
        }

        private void UpdatePauseState()
        {
            bool isPaused = Time.timeScale == 0f;

            if (isPaused != _isPaused)
            {
                _isPaused = isPaused;
                OnPauseStateChanged?.Invoke(_isPaused);
            }
        }

        private bool IsGameplayScene()
        {
            if (string.IsNullOrEmpty(_currentSceneNameLower))
            {
                return false;
            }

            foreach (string pattern in NonGameplayPatterns)
            {
                if (_currentSceneNameLower.Contains(pattern))
                {
                    return false;
                }
            }

            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}
