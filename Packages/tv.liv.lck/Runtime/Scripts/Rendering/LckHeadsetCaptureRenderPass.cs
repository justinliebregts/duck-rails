#if LCK_UNITY_URP
using System;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif
using UnityEngine.Rendering.Universal;

namespace Liv.Lck.Rendering
{
    /// <summary>
    /// Render pass that blits the XR camera's color buffer into the LCK headset capture target.
    /// On Unity 6+ uses the Render Graph API via <see cref="RecordRenderGraph"/>.
    /// On older URP versions uses the legacy <see cref="Execute"/> path.
    /// </summary>
    public class LckHeadsetCaptureRenderPass : ScriptableRenderPass
    {
        private const string PassName = "LCK Headset Capture";
        private const int TextureArrayBlitPassIndex = 1;
        private const int Texture2DBlitPassIndex = 2;
        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");

        private LckHeadsetCamera _headsetCamera;
        private Camera _camera;

#if UNITY_2022_1_OR_NEWER
        private RTHandle _cachedTargetHandle;
        private RenderTexture _cachedTargetRT;
#endif

        public void Setup(LckHeadsetCamera headsetCamera, Camera camera)
        {
            _headsetCamera = headsetCamera;
            _camera = camera;
        }

#if UNITY_6000_0_OR_NEWER
        private class PassData
        {
            public TextureHandle Source;
            public TextureHandle Destination;
            public Material Material;
            public int PassIndex;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();

            var target = _headsetCamera.ActiveTargetTexture;
            if (target == null || _headsetCamera.MaterialInstance == null)
                return;

            int passIndex;
            if (_headsetCamera.UseTextureArrayBlit)
                passIndex = TextureArrayBlitPassIndex;
            else if (_headsetCamera.UseTexture2DBlit)
                passIndex = Texture2DBlitPassIndex;
            else
                return;

            // When URP's active color target is an intermediate RT, our fullscreen triangle
            // needs a Y-flip on UV-at-top platforms. When URP is rendering directly to the
            // back buffer/XR swapchain, the source is already in final-target orientation.
            bool flipY = SystemInfo.graphicsUVStartsAtTop && !resourceData.isActiveTargetBackBuffer;
            _headsetCamera.UpdateMaterialForCaptureWithExplicitFlip(_camera, flipY);

            // Cache the RTHandle wrapper to avoid per-frame allocation.
            if (_cachedTargetRT != target)
            {
                _cachedTargetHandle?.Release();

                // Wrap via RenderTargetIdentifier so the import path does not inspect the
                // underlying RT descriptor. The CameraTrackTexture carries both a color
                // and a depth format which the plain ImportTexture overload rejects.
                _cachedTargetHandle = RTHandles.Alloc(new RenderTargetIdentifier(target));
                _cachedTargetRT = target;
            }

            var source = resourceData.activeColorTexture;

            // Provide an explicit RenderTargetInfo with only the color format so the
            // Render Graph does not see the depth/stencil format on the underlying RT.
            var targetInfo = new RenderTargetInfo
            {
                width = target.width,
                height = target.height,
                volumeDepth = target.volumeDepth,
                msaaSamples = target.antiAliasing,
                format = target.graphicsFormat
            };
            var destination = renderGraph.ImportTexture(_cachedTargetHandle, targetInfo);

            using (var builder = renderGraph.AddUnsafePass<PassData>(PassName, out var passData))
            {
                passData.Source = source;
                passData.Destination = destination;
                passData.Material = _headsetCamera.MaterialInstance;
                passData.PassIndex = passIndex;

                builder.UseTexture(source);
                builder.UseTexture(destination, AccessFlags.Write);

                // Prevent the render graph optimizer from culling this pass since our destination is
                // an imported external texture that nothing else in the graph reads.
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    context.cmd.SetRenderTarget(data.Destination);
                    context.cmd.SetGlobalTexture(BlitTextureId, data.Source);
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    cmd.DrawProcedural(Matrix4x4.identity, data.Material, data.PassIndex,
                        MeshTopology.Triangles, 3);
                });
            }

            _headsetCamera.MarkCapturedByRenderFeature();
        }
#endif

#if UNITY_6000_0_OR_NEWER
        [Obsolete("This pass uses RecordRenderGraph on Unity 6+.", false)]
#endif
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
#if !UNITY_6000_0_OR_NEWER
            var target = _headsetCamera.ActiveTargetTexture;
            if (target == null)
                return;

            // Use URP's camera color target. BuiltinRenderTextureType.CameraTarget does not
            // resolve to the correct buffer inside a render pass.
#if UNITY_2022_1_OR_NEWER
            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
            bool flipY = ShouldFlipSourceForFullscreenDraw(source);
#else
            var source = renderingData.cameraData.renderer.cameraColorTarget;
#endif
            var material = _headsetCamera.MaterialInstance;

#if UNITY_2022_1_OR_NEWER
            _headsetCamera.UpdateMaterialForCaptureWithExplicitFlip(_camera, flipY);
            _headsetCamera.UpdateBlitScaleBias(GetBlitScaleBias(source));
#else
            _headsetCamera.UpdateMaterialForCapture(_camera);
            _headsetCamera.UpdateBlitScaleBias(new Vector4(1f, 1f, 0f, 0f));
#endif

            var cmd = CommandBufferPool.Get(PassName);

            int passIndex = -1;
#if UNITY_2022_1_OR_NEWER
            if (material != null)
                passIndex = SelectShaderPassForSource(_headsetCamera, source);
#else
            if (material != null && _headsetCamera.UseTextureArrayBlit)
                passIndex = TextureArrayBlitPassIndex;
            else if (material != null && _headsetCamera.UseTexture2DBlit)
                passIndex = Texture2DBlitPassIndex;
#endif

            cmd.DisableScissorRect();
            BeginMonoTargetCopy(cmd, ref renderingData);
            try
            {
                if (passIndex >= 0)
                {
#if UNITY_2022_1_OR_NEWER
                    // Blitter.BlitCameraTexture resolves the source RTHandle to a Texture
                    // (RTHandle.op_Implicit -> rt). On XR the camera colour target aliases the
                    // swapchain through a RenderTargetIdentifier and rt is null, so that call
                    // throws ArgumentNullException every frame. Bind by identifier instead,
                    // which is valid for swapchain-backed targets. The blit scale/bias is
                    // already on the material via UpdateBlitScaleBias above.
                    if (source != null && source.rt != null)
                    {
                        Blitter.BlitCameraTexture(cmd, source, GetTargetHandle(target), material, passIndex);
                    }
                    else
                    {
                        cmd.SetRenderTarget(target);
                        cmd.SetGlobalTexture(BlitTextureId, source);
                        cmd.DrawProcedural(Matrix4x4.identity, material, passIndex, MeshTopology.Triangles, 3);
                    }
#else
                    cmd.SetRenderTarget(target);
                    cmd.SetGlobalTexture(BlitTextureId, source);
                    cmd.DrawProcedural(Matrix4x4.identity, material, passIndex, MeshTopology.Triangles, 3);
#endif
                }
                else
                {
                    cmd.Blit(source, target);
                }
            }
            finally
            {
                // Must always run once BeginMonoTargetCopy has issued StopSinglePass. If the
                // blit throws in between, the XR layout is left holding active passes and URP
                // tears it down mid-frame ("the XR layout still contains active passes"),
                // which distorts the headset view.
                EndMonoTargetCopy(cmd, ref renderingData);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            _headsetCamera.MarkCapturedByRenderFeature();
#endif
        }

#if UNITY_2022_1_OR_NEWER && !UNITY_6000_0_OR_NEWER
        private RTHandle GetTargetHandle(RenderTexture target)
        {
            if (_cachedTargetRT != target)
            {
                _cachedTargetHandle?.Release();
                _cachedTargetHandle = RTHandles.Alloc(new RenderTargetIdentifier(target), "LCK Headset Capture Target");
                _cachedTargetRT = target;
            }

            return _cachedTargetHandle;
        }

        private static Vector4 GetBlitScaleBias(RTHandle source)
        {
            if (source != null && source.useScaling)
            {
                var scale = source.rtHandleProperties.rtHandleScale;
                return new Vector4(scale.x, scale.y, 0f, 0f);
            }

            return new Vector4(1f, 1f, 0f, 0f);
        }

        private static int SelectShaderPassForSource(LckHeadsetCamera headsetCamera, RTHandle source)
        {
            var sourceRt = source == null ? null : source.rt;
            if (sourceRt != null)
            {
                if (sourceRt.dimension == TextureDimension.Tex2DArray)
                    return headsetCamera.UseTextureArrayBlit ? TextureArrayBlitPassIndex : -1;

                if (sourceRt.dimension == TextureDimension.Tex2D)
                    return Texture2DBlitPassIndex;
            }

            if (headsetCamera.UseTextureArrayBlit)
                return TextureArrayBlitPassIndex;

            return headsetCamera.UseTexture2DBlit ? Texture2DBlitPassIndex : -1;
        }

        private static bool ShouldFlipSourceForFullscreenDraw(RTHandle source)
        {
            if (!SystemInfo.graphicsUVStartsAtTop || source == null)
                return false;

            // A null backing RT means the RTHandle aliases a RenderTargetIdentifier
            // such as the camera back buffer or XR swapchain. Those sources are already
            // in final-target orientation; intermediate render textures are not.
            return source.rt != null;
        }
#endif

        private static void BeginMonoTargetCopy(CommandBuffer cmd, ref RenderingData renderingData)
        {
#if UNITY_2022_2_OR_NEWER && ENABLE_VR && ENABLE_XR_MODULE
            if (renderingData.cameraData.xr.enabled && renderingData.cameraData.xr.supportsFoveatedRendering)
                cmd.SetFoveatedRenderingMode(FoveatedRenderingMode.Disabled);

            if (renderingData.cameraData.xr.enabled)
                renderingData.cameraData.xr.StopSinglePass(cmd);
#endif
        }

        private static void EndMonoTargetCopy(CommandBuffer cmd, ref RenderingData renderingData)
        {
#if UNITY_2022_2_OR_NEWER && ENABLE_VR && ENABLE_XR_MODULE
            if (renderingData.cameraData.xr.enabled)
                renderingData.cameraData.xr.StartSinglePass(cmd);
#endif
        }

        public void Dispose()
        {
#if UNITY_2022_1_OR_NEWER
            _cachedTargetHandle?.Release();
            _cachedTargetHandle = null;
            _cachedTargetRT = null;
#endif
        }
    }
}
#endif
