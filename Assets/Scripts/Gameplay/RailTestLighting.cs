using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Local development visibility override. Never edits baked data or scene assets.</summary>
public sealed class RailTestLighting : MonoBehaviour
{
    const string Preference = "DuckRails.TestLighting.Bright";
    static RailTestLighting instance;
    static bool requested;
    public static bool BrightRequested
    {
        get
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return UnityEditor.SessionState.GetBool(Preference, false);
#endif
            return requested;
        }
    }
    public static bool Active => requested && Application.isPlaying && RailTestTools.Allowed;
    public static string Status => !BrightRequested ? "Gameplay lighting" :
        Active ? "Bright lighting (local preview)" : "Bright lighting selected — applies in a ready private run";
    Light fill, headFill;
    Camera renderingCamera;
    bool savedFog, captured;
    AmbientMode savedAmbientMode;
    Color savedAmbient;
    float savedAmbientIntensity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        requested = false;
#if UNITY_EDITOR
        requested = UnityEditor.SessionState.GetBool(Preference, false);
#endif
        if (!RailDiagnostics.Development || instance != null) return;
        var go = new GameObject("Development lighting preview") { hideFlags = HideFlags.DontSave };
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RailTestLighting>();
    }
    public static void Bright() => Select(true);
    public static void Gameplay() => Select(false);
    static void Select(bool bright)
    {
        if (!RailDiagnostics.Development) return;
        requested = bright;
#if UNITY_EDITOR
        UnityEditor.SessionState.SetBool(Preference, bright);
#endif
        if (!bright && instance != null) instance.Restore();
    }
    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCamera;
        RenderPipelineManager.endCameraRendering += EndCamera;
    }
    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCamera;
        RenderPipelineManager.endCameraRendering -= EndCamera;
        Restore();
    }
    void OnDestroy() { if (instance == this) instance = null; }
    void LateUpdate() { if (!Active) Restore(); }
    void BeginCamera(ScriptableRenderContext context, Camera camera)
    {
        if (!Active || camera.cameraType != CameraType.Game || captured) return;
        savedFog = RenderSettings.fog;
        savedAmbientMode = RenderSettings.ambientMode;
        savedAmbient = RenderSettings.ambientLight;
        savedAmbientIntensity = RenderSettings.ambientIntensity;
        renderingCamera = camera; captured = true;
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
        RenderSettings.ambientIntensity = 1.3f;
        if (fill == null)
        {
            fill = CreateLight("Bright preview directional", LightType.Directional, 3);
            fill.transform.rotation = Quaternion.Euler(50, 30, 0);
            headFill = CreateLight("Bright preview room fill", LightType.Point, 150);
            headFill.range = 40;
        }
        headFill.transform.position = camera.transform.position;
        fill.enabled = headFill.enabled = true;
    }
    Light CreateLight(string name, LightType type, float intensity)
    {
        var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(transform, false);
        var light = go.AddComponent<Light>();light.enabled = false;
        light.type = type;light.intensity = intensity;light.color = Color.white;
        light.lightmapBakeType = LightmapBakeType.Realtime;light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel;
        return light;
    }
    void EndCamera(ScriptableRenderContext context, Camera camera)
    { if (camera == renderingCamera) Restore(); }
    void Restore()
    {
        if (fill != null) fill.enabled = false;
        if (headFill != null) headFill.enabled = false;
        if (!captured) return;
        // Capture every render, so scene changes and the dawn transition remain authoritative.
        RenderSettings.fog = savedFog;
        RenderSettings.ambientMode = savedAmbientMode;
        RenderSettings.ambientLight = savedAmbient;
        RenderSettings.ambientIntensity = savedAmbientIntensity;
        captured = false;renderingCamera = null;
    }
}
