using CameraUnlock.Core.Unity.Tracking;
using UnityEngine;

namespace WobblyLifeHeadTracking.Camera
{
    /// <summary>
    /// Sits on one gameplay camera and puts head tracking on for the render pass only.
    ///
    /// Unity calls OnPreCull and OnPostRender on components of the camera that is
    /// rendering, which is the same window the Mono build gets from Camera.onPreCull /
    /// Camera.onPostRender. Because the tracked pose comes off again in OnPostRender,
    /// everything the game reads from the transform afterwards - aim, raycasts,
    /// physics - sees the pose the game itself set.
    /// </summary>
    public sealed class GameplayCameraRenderHook : MonoBehaviour
    {
        private readonly TransformFrameState _frameState = new TransformFrameState();

        private Transform _camTransform;
        private int _playerIndex = -1;

        internal void Bind(int playerIndex)
        {
            _camTransform = transform;
            _playerIndex = playerIndex;
        }

        private void OnPreCull()
        {
            if (_playerIndex < 0) return;

            WobblyLifeCameraController controller = WobblyLifeCameraController.Instance;
            if (controller == null || !controller.IsTrackingActive) return;

            controller.ApplyTracking(_frameState, _camTransform, _playerIndex);
        }

        private void OnPostRender()
        {
            if (_playerIndex < 0) return;

            // Unconditional: the gate may have closed between the two callbacks, and a
            // pose left applied would become this camera's clean base next frame.
            _frameState.Restore(_camTransform, Time.frameCount);
        }
    }
}
