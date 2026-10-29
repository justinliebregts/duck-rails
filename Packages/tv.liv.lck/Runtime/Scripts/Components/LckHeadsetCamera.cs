using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace Liv.Lck
{
    public enum EyeSelection
    {
        Left = 0,
        Right = 1
    }

    public enum HeadsetCropMode
    {
        /// <summary>Letterbox / pillarbox to fit the full eye view onto the target RT.</summary>
        Fit = 0,
        /// <summary>Zoom and crop so the target RT is completely filled, discarding overflow.</summary>
        ZoomFill = 1
    }

    /// <summary>
    /// Captures the VR headset's eye output for LCK recording, streaming, and Echo.
    /// Blits the selected eye (left or right) from the XR camera target into an LCK render texture
    /// each frame, using a texture-array-aware shader when available or a two-pass blit fallback.
    /// Works with both SRP and built-in render pipelines.
    /// </summary>
    public class LckHeadsetCamera : MonoBehaviour, ILckCamera
    {
        [SerializeField]
        private string _cameraId;

        [SerializeField]
        private Camera _xrCamera;

        [SerializeField]
        private EyeSelection _eye = EyeSelection.Left;

        [SerializeField]
        private HeadsetCropMode _cropMode = HeadsetCropMode.Fit;

        [SerializeField]
        private Material _blitMaterial;

        public string CameraId => _cameraId;

        public EyeSelection Eye
        {
            get => _eye;
            set
            {
                _eye = value;
                if (_materialInstance != null)
                {
                    _materialInstance.SetFloat(SliceIndexId, (float)_eye);
                    UpdateSourceScaleOffset();
                }
            }
        }

        public HeadsetCropMode CropMode
        {
            get => _cropMode;
            set
            {
                _cropMode = value;
                InvalidateScaleOffsetCache();
            }
        }

        internal bool IsActive { get; private set; }
        internal RenderTexture ActiveTargetTexture { get; private set; }

        /// <summary>
        /// Active instances available for the URP render feature to find.
        /// Populated when <see cref="ActivateCamera"/> is called, cleared on <see cref="DeactivateCamera"/>.
        /// </summary>
        internal static readonly System.Collections.Generic.List<LckHeadsetCamera> _activeInstances =
            new System.Collections.Generic.List<LckHeadsetCamera>();

        private RenderTexture _intermediateRT;
        private bool _useTextureArray;
        private bool _useSRP;
        private bool _isMultiPass;
        private bool _isTextureArrayStereo;
        private bool _isDoubleWideStereo;
        private bool _stereoModeDetected;
        private bool _captureInitialized;
        private Camera _resolvedCamera;
        private int _lastRenderFeatureCaptureFrame = -1;

        private CommandBuffer _cmd;
        private Material _materialInstance;

        // Scale/offset cache: avoids recomputing and re-setting material properties every frame.
        private int _cachedSrcW, _cachedSrcH, _cachedDstW, _cachedDstH;
        private HeadsetCropMode _cachedCropMode;

        private const string CmdBufferName = "LCK Headset Capture";
        private const int TextureArrayBlitPassIndex = 1;
        private const int Texture2DBlitPassIndex = 2;
        private static readonly int SliceIndexId = Shader.PropertyToID("_SliceIndex");
        private static readonly int ScaleOffsetId = Shader.PropertyToID("_ScaleOffset");
        private static readonly int SourceScaleOffsetId = Shader.PropertyToID("_SourceScaleOffset");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int FlipYId = Shader.PropertyToID("_FlipY");

        private void Awake()
        {
            if (string.IsNullOrEmpty(_cameraId))
            {
                _cameraId = System.Guid.NewGuid().ToString();
            }

            _useTextureArray = SystemInfo.supports2DArrayTextures && _blitMaterial != null;
            _useSRP = GraphicsSettings.defaultRenderPipeline != null;
            _cmd = new CommandBuffer { name = CmdBufferName };

            if (_blitMaterial != null)
            {
                _materialInstance = new Material(_blitMaterial);
                _materialInstance.SetFloat(SliceIndexId, (float)_eye);
                _materialInstance.SetVector(SourceScaleOffsetId, new Vector4(1f, 1f, 0f, 0f));
                _materialInstance.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                // Sentinel: pass 1 shader falls back to _ProjectionParams.x when _FlipY < 0.
                // Modern URP paths override this explicitly from render target state.
                _materialInstance.SetFloat(FlipYId, -1f);
            }

            if (_blitMaterial == null)
            {
                LckLog.LogWarning($"{nameof(LckHeadsetCamera)}: No blit material assigned - falling back to simple blit. Eye selection will not work.");
            }
            else if (!SystemInfo.supports2DArrayTextures)
            {
                LckLog.LogWarning($"{nameof(LckHeadsetCamera)}: Device does not support 2D array textures - texture-array eye selection will not work.");
            }

            LckMediator.RegisterCamera(this);

            if (_useSRP)
            {
#if LCK_UNITY_URP
                Rendering.LckHeadsetCaptureAutoSetup.EnsureFeaturePresent();
#endif
                RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
            }
            else
            {
                Camera.onPostRender += OnPostRenderBuiltIn;
            }
        }

        private void OnDestroy()
        {
            if (_useSRP)
            {
                RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            }
            else
            {
                Camera.onPostRender -= OnPostRenderBuiltIn;
            }

            if (IsActive)
            {
                IsActive = false;
                ActiveTargetTexture = null;
                _activeInstances.Remove(this);
            }

            _cmd?.Release();
            _cmd = null;

            if (_materialInstance != null)
            {
                Destroy(_materialInstance);
                _materialInstance = null;
            }

            LckMediator.UnregisterCamera(this);
            ReleaseIntermediateRT();
        }

        public void ActivateCamera(RenderTexture renderTexture)
        {
            ActiveTargetTexture = renderTexture;
            IsActive = true;

            if (!_activeInstances.Contains(this))
                _activeInstances.Add(this);

            if (!_captureInitialized)
                InitializeCapture();

#if LCK_UNITY_URP
            // Re-validate on every activation, not just first init. URP may have swapped
            // pipeline asset since we last checked (quality level change, per-platform
            // preset), in which case the active renderer has no capture feature and we
            // would silently drop to the endCameraRendering fallback, which produces
            // flickering and stretched output.
            if (_useSRP && !Rendering.LckHeadsetCaptureAutoSetup.EnsureFeaturePresent())
            {
                LckLog.LogWarning($"{nameof(LckHeadsetCamera)}: LckHeadsetCaptureRenderFeature is not present on the " +
                                  "active URP renderer. Falling back to endCameraRendering capture, which can show " +
                                  "flickering or stretched output.");
            }
#endif
        }

        private void InitializeCapture()
        {
            _captureInitialized = true;
            DetectStereoModeIfNeeded();

            if (!_stereoModeDetected)
            {
                LckLog.LogWarning($"{nameof(LckHeadsetCamera)}: XR device is not active - stereo rendering mode could not be detected. " +
                                  "Headset capture may not work correctly.");
            }

#if LCK_UNITY_URP
            if (_useSRP && !Rendering.LckHeadsetCaptureAutoSetup.EnsureFeaturePresent())
            {
                LckLog.LogWarning($"{nameof(LckHeadsetCamera)}: Failed to auto-add LckHeadsetCaptureRenderFeature to URP renderer. " +
                                  "Headset capture will use the endCameraRendering fallback.");
            }
#endif
        }

        public void DeactivateCamera()
        {
            IsActive = false;
            ActiveTargetTexture = null;
            _activeInstances.Remove(this);
        }

        public Camera GetCameraComponent()
        {
            if (_resolvedCamera == null)
                _resolvedCamera = _xrCamera != null ? _xrCamera : Camera.main;

            return _resolvedCamera;
        }

        /// <summary>
        /// Checks whether the given camera is the one we should capture from.
        /// When <see cref="_xrCamera"/> is explicitly assigned, we match exactly.
        /// Otherwise we auto-detect by accepting any stereo-rendering camera.
        /// </summary>
        internal bool IsTargetCamera(Camera cam)
        {
            if (_xrCamera != null)
                return cam == _xrCamera;

            if (cam.stereoEnabled)
                return true;

            // Fallback to Camera.main for XR simulators that don't set stereoEnabled.
            // On device, the stereo check above matches the XR camera first.
            return cam == Camera.main;
        }

        /// <summary>
        /// Called by the URP render feature after it captures a frame, so the
        /// <see cref="OnEndCameraRendering"/> fallback knows to skip this frame.
        /// </summary>
        internal void MarkCapturedByRenderFeature()
        {
            _lastRenderFeatureCaptureFrame = Time.frameCount;
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (!IsActive || ActiveTargetTexture == null)
                return;

            // If the render feature already captured this frame, skip the fallback blit.
            if (_lastRenderFeatureCaptureFrame == Time.frameCount)
                return;

            if (!IsTargetCamera(cam))
                return;

            if (!ShouldCaptureEye(cam))
                return;

            _cmd.Clear();
            PopulateCommandBuffer(_cmd, cam);
            context.ExecuteCommandBuffer(_cmd);
            context.Submit();
        }

        private void OnPostRenderBuiltIn(Camera cam)
        {
            if (!IsActive || ActiveTargetTexture == null)
                return;

            if (!IsTargetCamera(cam))
                return;

            if (!ShouldCaptureEye(cam))
                return;

            _cmd.Clear();
            PopulateCommandBuffer(_cmd, cam);
            Graphics.ExecuteCommandBuffer(_cmd);
        }

        private void DetectStereoModeIfNeeded()
        {
            if (_stereoModeDetected)
                return;
            if (!XRSettings.isDeviceActive)
                return;

            var stereoRenderingMode = XRSettings.stereoRenderingMode;
            _isMultiPass = stereoRenderingMode == XRSettings.StereoRenderingMode.MultiPass;
            _isTextureArrayStereo = IsTextureArrayStereoMode(stereoRenderingMode);
            _isDoubleWideStereo = stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePass;
            _stereoModeDetected = true;
        }

        internal static bool IsTextureArrayStereoMode(XRSettings.StereoRenderingMode stereoRenderingMode)
        {
            return stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassInstanced ||
                   stereoRenderingMode.ToString() == "SinglePassMultiview";
        }

        /// <summary>
        /// In single-pass instanced mode, eye selection is handled by the shader (array slice indexing),
        /// so we capture every callback. In multi-pass, each eye renders as a separate pass and we only
        /// capture the one matching <see cref="_eye"/>.
        /// </summary>
        internal bool ShouldCaptureEye(Camera cam)
        {
            if (!_isMultiPass)
                return true;

            var activeEye = cam.stereoActiveEye;
            if (activeEye == Camera.MonoOrStereoscopicEye.Mono)
                return true;

            var selectedEye = _eye == EyeSelection.Left
                ? Camera.MonoOrStereoscopicEye.Left
                : Camera.MonoOrStereoscopicEye.Right;
            return activeEye == selectedEye;
        }

        internal Material MaterialInstance => _materialInstance;
        internal bool UseTextureArrayBlit => _stereoModeDetected && _useTextureArray && _isTextureArrayStereo;
        internal bool UseTexture2DBlit => _stereoModeDetected && _materialInstance != null && !_isTextureArrayStereo;

        /// <summary>
        /// Updates the material's scale/offset uniforms for the current source/target dimensions.
        /// Leaves <c>_FlipY</c> at its sentinel value so the shader auto-detects the flip via
        /// <c>_ProjectionParams.x</c>. Used only by old compatibility paths that cannot query
        /// the active URP source target state directly.
        /// </summary>
        internal void UpdateMaterialForCapture(Camera cam)
        {
            if (_materialInstance != null)
            {
                _materialInstance.SetFloat(FlipYId, -1f);
            }

            UpdateMaterialScaleOffsetForCapture(cam);
        }

        internal void UpdateMaterialForCaptureWithExplicitFlip(Camera cam, bool flipY)
        {
            if (_materialInstance != null)
            {
                _materialInstance.SetFloat(FlipYId, flipY ? 1f : 0f);
            }

            UpdateMaterialScaleOffsetForCapture(cam);
        }

        internal void UpdateBlitScaleBias(Vector4 blitScaleBias)
        {
            if (_materialInstance != null)
            {
                _materialInstance.SetVector(BlitScaleBiasId, blitScaleBias);
            }
        }

        private void UpdateMaterialScaleOffsetForCapture(Camera cam)
        {
            if (_materialInstance == null)
                return;

            int srcW = XRSettings.eyeTextureWidth > 0 ? XRSettings.eyeTextureWidth : cam.pixelWidth;
            int srcH = XRSettings.eyeTextureHeight > 0 ? XRSettings.eyeTextureHeight : cam.pixelHeight;
            UpdateScaleOffsetIfNeeded(srcW, srcH);
            UpdateSourceScaleOffset();
        }

        /// <summary>
        /// Updates the material's scale/offset uniforms AND sets <c>_FlipY</c> explicitly based
        /// on whether the source image is in the platform's native orientation (i.e. not
        /// already flipped by the pipeline's projection matrix).
        /// </summary>
        internal void UpdateMaterialForCapture(Camera cam, bool isSourceNativeOrientation)
        {
            // On platforms where texture UV(0,0) is at the top (D3D/Vulkan/Metal), our
            // fullscreen triangle's UV(0,0) maps to the bottom of the output, so a Y-flip
            // is needed UNLESS the source image has already been flipped by the pipeline.
            bool flipY = SystemInfo.graphicsUVStartsAtTop && isSourceNativeOrientation;
            UpdateMaterialForCaptureWithExplicitFlip(cam, flipY);
        }

        internal void PopulateCommandBuffer(CommandBuffer cmd, Camera cam)
        {
            // This method runs from endCameraRendering (after URP's FinalBlit has resolved any
            // intermediate) or OnPostRender (built-in pipeline). The source (CameraTarget) is
            // always in the platform's native convention. On D3D/Vulkan/Metal, texture UV(0,0)
            // maps to the top of the image while cmd.Blit maps UV(0,0) to the bottom of the
            // output, so a Y-flip is needed on those platforms.
            bool flipY = SystemInfo.graphicsUVStartsAtTop;

            if (UseTextureArrayBlit)
            {
                // Single-pass instanced: sample the correct eye slice from the texture array.
                int srcW = XRSettings.eyeTextureWidth > 0 ? XRSettings.eyeTextureWidth : cam.pixelWidth;
                int srcH = XRSettings.eyeTextureHeight > 0 ? XRSettings.eyeTextureHeight : cam.pixelHeight;
                UpdateScaleOffsetIfNeeded(srcW, srcH);
                UpdateSourceScaleOffset();

                if (_materialInstance != null)
                    _materialInstance.SetFloat(FlipYId, flipY ? 1f : 0f);

                UpdateBlitScaleBias(new Vector4(1f, 1f, 0f, 0f));
                cmd.SetRenderTarget(ActiveTargetTexture);
                cmd.DisableScissorRect();
                cmd.SetViewport(new Rect(0f, 0f, ActiveTargetTexture.width, ActiveTargetTexture.height));
                cmd.SetGlobalTexture(BlitTextureId, BuiltinRenderTextureType.CameraTarget);
                cmd.DrawProcedural(Matrix4x4.identity, _materialInstance, TextureArrayBlitPassIndex,
                    MeshTopology.Triangles, 3);
            }
            else if (UseTexture2DBlit)
            {
                UpdateMaterialForCaptureWithExplicitFlip(cam, flipY);
                UpdateBlitScaleBias(new Vector4(1f, 1f, 0f, 0f));
                cmd.SetRenderTarget(ActiveTargetTexture);
                cmd.DisableScissorRect();
                cmd.SetViewport(new Rect(0f, 0f, ActiveTargetTexture.width, ActiveTargetTexture.height));
                cmd.SetGlobalTexture(BlitTextureId, BuiltinRenderTextureType.CameraTarget);
                cmd.DrawProcedural(Matrix4x4.identity, _materialInstance, Texture2DBlitPassIndex,
                    MeshTopology.Triangles, 3);
            }
            else
            {
                // Multi-pass or fallback: camera target is already the correct eye as a plain 2D texture.
                PrepareIntermediateRT(cam);
                cmd.Blit(BuiltinRenderTextureType.CameraTarget, _intermediateRT);

                // Reuse ComputeScaleOffset for ZoomFill; Fit falls back to identity
                // because the simple blit path can't render letterbox/pillarbox bars.
                var so = _cropMode == HeadsetCropMode.ZoomFill
                    ? ComputeScaleOffset(_intermediateRT.width, _intermediateRT.height)
                    : new Vector4(1f, 1f, 0f, 0f);

                float sy = flipY ? -so.y : so.y;
                float oy = flipY ? so.w + so.y : so.w;
                cmd.Blit(_intermediateRT, ActiveTargetTexture, new Vector2(so.x, sy), new Vector2(so.z, oy));
            }
        }

        private void UpdateScaleOffsetIfNeeded(int srcW, int srcH)
        {
            int dstW = ActiveTargetTexture.width;
            int dstH = ActiveTargetTexture.height;

            if (srcW == _cachedSrcW && srcH == _cachedSrcH &&
                dstW == _cachedDstW && dstH == _cachedDstH &&
                _cropMode == _cachedCropMode)
                return;

            _cachedSrcW = srcW;
            _cachedSrcH = srcH;
            _cachedDstW = dstW;
            _cachedDstH = dstH;
            _cachedCropMode = _cropMode;
            _materialInstance.SetVector(ScaleOffsetId, ComputeScaleOffset(srcW, srcH));
        }

        private void InvalidateScaleOffsetCache()
        {
            _cachedSrcW = 0;
            _cachedSrcH = 0;
        }

        private Vector4 ComputeScaleOffset(int srcW, int srcH)
        {
            float srcAspect = (float)srcW / srcH;
            float dstAspect = (float)ActiveTargetTexture.width / ActiveTargetTexture.height;

            float scaleX, scaleY, offsetX, offsetY;

            if (_cropMode == HeadsetCropMode.ZoomFill)
            {
                if (srcAspect < dstAspect)
                {
                    // Source narrower: fill width, crop top/bottom
                    float ratio = srcAspect / dstAspect;
                    scaleX = 1f;
                    offsetX = 0f;
                    scaleY = ratio;
                    offsetY = (1f - ratio) * 0.5f;
                }
                else
                {
                    // Source wider: fill height, crop sides
                    float ratio = dstAspect / srcAspect;
                    scaleX = ratio;
                    offsetX = (1f - ratio) * 0.5f;
                    scaleY = 1f;
                    offsetY = 0f;
                }
            }
            else
            {
                if (srcAspect < dstAspect)
                {
                    // Source is narrower (taller) than destination → pillarbox
                    float ratio = srcAspect / dstAspect;
                    scaleX = 1f / ratio;
                    offsetX = -(1f - ratio) * 0.5f * scaleX;
                    scaleY = 1f;
                    offsetY = 0f;
                }
                else
                {
                    // Source is wider than destination → letterbox
                    float ratio = dstAspect / srcAspect;
                    scaleX = 1f;
                    offsetX = 0f;
                    scaleY = 1f / ratio;
                    offsetY = -(1f - ratio) * 0.5f * scaleY;
                }
            }

            return new Vector4(scaleX, scaleY, offsetX, offsetY);
        }

        private void UpdateSourceScaleOffset()
        {
            if (_materialInstance == null)
                return;

            var sourceScaleOffset = new Vector4(1f, 1f, 0f, 0f);
            if (_isDoubleWideStereo)
            {
                sourceScaleOffset = _eye == EyeSelection.Left
                    ? new Vector4(0.5f, 1f, 0f, 0f)
                    : new Vector4(0.5f, 1f, 0.5f, 0f);
            }

            _materialInstance.SetVector(SourceScaleOffsetId, sourceScaleOffset);
        }

        private void PrepareIntermediateRT(Camera cam)
        {
            int w = cam.pixelWidth;
            int h = cam.pixelHeight;

            if (_intermediateRT != null && _intermediateRT.width == w && _intermediateRT.height == h)
                return;

            ReleaseIntermediateRT();

            var desc = new RenderTextureDescriptor(w, h, ActiveTargetTexture.graphicsFormat, 0)
            {
                msaaSamples = 1
            };
            _intermediateRT = RenderTexture.GetTemporary(desc);
        }

        private void ReleaseIntermediateRT()
        {
            if (_intermediateRT != null)
            {
                RenderTexture.ReleaseTemporary(_intermediateRT);
                _intermediateRT = null;
            }
        }
    }
}
