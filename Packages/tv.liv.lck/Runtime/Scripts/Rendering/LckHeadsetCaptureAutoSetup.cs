#if LCK_UNITY_URP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if !UNITY_6000_0_OR_NEWER
using System.Reflection;
#endif

namespace Liv.Lck.Rendering
{
    /// <summary>
    /// Automatically adds <see cref="LckHeadsetCaptureRenderFeature"/> to the active URP renderer
    /// at runtime if the user has not already configured it in their URP asset.
    /// Appending at runtime guarantees the capture runs after all developer renderer features.
    /// On Unity 6+ uses the public <c>rendererDataList</c> API.
    /// On older versions uses reflection to access the internal renderer data fields.
    /// </summary>
    internal static class LckHeadsetCaptureAutoSetup
    {
        private static bool _earlySetupAttempted;

#if !UNITY_6000_0_OR_NEWER
        private const BindingFlags _bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;

        // URP 12+ (Unity 2021.2+): internal field is an array of renderer data
        private static readonly FieldInfo _rendererDataListField = typeof(UniversalRenderPipelineAsset)
            .GetField("m_RendererDataList", _bindingFlags);

        // URP 10-11 (Unity 2020.x - 2021.1): internal field is a single renderer data
        private static readonly FieldInfo _rendererDataField = typeof(UniversalRenderPipelineAsset)
            .GetField("m_RendererData", _bindingFlags);

        // SetDirty may not be public in older URP versions; fall back to isInvalidated property
        private static readonly System.Reflection.MethodInfo _setDirtyMethod = typeof(ScriptableRendererData)
            .GetMethod("SetDirty", BindingFlags.Public | BindingFlags.Instance, null, System.Type.EmptyTypes, null);

        private static readonly System.Reflection.PropertyInfo _isInvalidatedProperty = typeof(ScriptableRendererData)
            .GetProperty("isInvalidated", _bindingFlags);
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureFeaturePresentBeforeFirstRender()
        {
            if (_earlySetupAttempted)
                return;

            _earlySetupAttempted = true;

            if (GraphicsSettings.defaultRenderPipeline == null && QualitySettings.renderPipeline == null)
                return;

            EnsureFeaturePresent(true);
        }

        internal static bool EnsureFeaturePresent()
        {
            return EnsureFeaturePresent(false);
        }

        private static bool EnsureFeaturePresent(bool suppressWarnings)
        {
            // NOTE: deliberately does NOT early-out on LckHeadsetCaptureRenderFeature.IsConfigured.
            // That flag is set inside Create(), which runs on the instance we build in
            // CreateFeature() before it is attached to anything, and on any renderer URP
            // instantiates. It therefore means "a feature object existed at some point",
            // not "the active renderer has the feature". Trusting it meant we never
            // re-validated after URP swapped pipeline asset (quality level change, or a
            // different per-platform preset), leaving the new renderer with no feature and
            // silently dropping capture onto the endCameraRendering fallback. It also made
            // this method report success when the feature had been added to zero renderers,
            // suppressing the fallback warning that should have surfaced the problem.
            var pipelineAsset = UniversalRenderPipeline.asset;
            if (pipelineAsset == null)
            {
                if (!suppressWarnings)
                    LckLog.LogWarning("Cannot auto-add LckHeadsetCaptureRenderFeature: URP pipeline asset is null.");
                return false;
            }

            var rendererDataList = GetRendererDataList(pipelineAsset);
            if (rendererDataList == null)
            {
                if (!suppressWarnings)
                {
                    LckLog.LogWarning(
                        "LCK could not automatically add LckHeadsetCaptureRenderFeature to your URP renderer. " +
                        "Please add it manually: select your URP Renderer asset, click 'Add Renderer Feature', " +
                        "and choose 'LckHeadsetCaptureRenderFeature'.");
                }
                return false;
            }

            bool added = false;
            bool allPresent = true;

            foreach (var rendererData in rendererDataList)
            {
                if (rendererData == null)
                {
                    LckLog.LogWarning("Cannot auto-add LckHeadsetCaptureRenderFeature: a URP renderer data entry is null.");
                    allPresent = false;
                    continue;
                }

                if (HasFeature<LckHeadsetCaptureRenderFeature>(rendererData))
                    continue;

                var feature = CreateFeature();
                rendererData.rendererFeatures.Add(feature);
                InvalidateRendererData(rendererData);
                added = true;

                // Verify rather than assume: if the add did not take, capture would fall
                // back silently and the view would show flicker/stretching artifacts.
                if (!HasFeature<LckHeadsetCaptureRenderFeature>(rendererData))
                {
                    allPresent = false;
                    if (!suppressWarnings)
                    {
                        LckLog.LogWarning(
                            $"LckHeadsetCaptureRenderFeature could not be attached to URP renderer '{rendererData.name}'.");
                    }
                }
            }

            if (added)
            {
                LckLog.Log($"LckHeadsetCaptureRenderFeature automatically added to URP renderer(s) of " +
                           $"'{pipelineAsset.name}' at runtime.");
            }

            return allPresent;
        }

        private static ScriptableRendererData[] GetRendererDataList(UniversalRenderPipelineAsset asset)
        {
#if UNITY_6000_0_OR_NEWER
            return asset.rendererDataList.ToArray();
#else
            // Try m_RendererDataList first (URP 12+), fall back to m_RendererData (URP 10-11)
            if (_rendererDataListField?.GetValue(asset) is ScriptableRendererData[] list)
            {
                return list;
            }

            if (_rendererDataField?.GetValue(asset) is ScriptableRendererData single && single != null)
            {
                return new[] { single };
            }

            return null;
#endif
        }

        private static bool HasFeature<T>(ScriptableRendererData rendererData) where T : ScriptableRendererFeature
        {
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature is T)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Marks the renderer data as invalidated so URP recreates the renderer,
        /// picking up dynamically added features.
        /// </summary>
        private static void InvalidateRendererData(ScriptableRendererData data)
        {
#if UNITY_6000_0_OR_NEWER
            data.SetDirty();
#else
            // Try SetDirty (public in some URP versions), fall back to isInvalidated property
            if (_setDirtyMethod != null)
            {
                _setDirtyMethod.Invoke(data, null);
            }
            else if (_isInvalidatedProperty != null)
            {
                _isInvalidatedProperty.SetValue(data, true);
            }
#endif
        }

        private static LckHeadsetCaptureRenderFeature CreateFeature()
        {
            var feature = ScriptableObject.CreateInstance<LckHeadsetCaptureRenderFeature>();
            feature.name = nameof(LckHeadsetCaptureRenderFeature);
            feature.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
#if UNITY_2021_2_OR_NEWER
            feature.SetActive(true);
#endif
            feature.Create();
            return feature;
        }
    }
}
#endif
