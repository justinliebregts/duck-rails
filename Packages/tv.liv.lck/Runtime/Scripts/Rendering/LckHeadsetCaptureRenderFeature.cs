#if LCK_UNITY_URP
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Liv.Lck.Rendering
{
    /// <summary>
    /// URP Renderer Feature that captures the XR camera's output for LCK headset view.
    /// This participates in the Render Graph lifecycle, ensuring the camera color buffer
    /// is still alive when the capture blit executes, unlike the endCameraRendering
    /// callback fallback which can read from a released buffer on some platforms.
    /// Add this feature to your URP Renderer Data asset (done automatically by LCK validation).
    /// </summary>
    public class LckHeadsetCaptureRenderFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// True once any instance of this feature has had <see cref="Create"/> called.
        /// This is informational only. It does NOT mean the currently active URP renderer
        /// contains the feature - Create() also runs on instances built by
        /// LckHeadsetCaptureAutoSetup before they are attached, and on renderers URP
        /// instantiates for other pipeline assets. Use
        /// <c>LckHeadsetCaptureAutoSetup.EnsureFeaturePresent()</c> to determine whether
        /// capture will actually run through the render feature.
        /// </summary>
        internal static bool IsConfigured { get; private set; }

        private LckHeadsetCaptureRenderPass _pass;

        public override void Create()
        {
            IsConfigured = true;
            _pass = new LckHeadsetCaptureRenderPass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (LckHeadsetCamera._activeInstances.Count == 0)
                return;

            Camera cam = renderingData.cameraData.camera;

            foreach (var headsetCamera in LckHeadsetCamera._activeInstances)
            {
                if (!headsetCamera.IsTargetCamera(cam))
                    continue;

                if (!headsetCamera.ShouldCaptureEye(cam))
                    continue;

                _pass.Setup(headsetCamera, cam);
                renderer.EnqueuePass(_pass);
                return;
            }
        }
    }
}
#endif
