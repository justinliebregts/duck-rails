using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Local rendering of shared route/finish progress. No runtime GI or probe captures.</summary>
public sealed class RailAtmosphere
{
    public const float NightViewDistance = 120f;
    public const float DawnViewDistance = 175f;
    public const string StormSkyShader = "Duckz/Animated Storm Sky";
    public const string MountainsResource = "Rails/DistantMountains";
    public static float PlayerViewDistance { get; private set; } = NightViewDistance;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetViewDistance() => PlayerViewDistance = NightViewDistance;

    static readonly int ZenithId = Shader.PropertyToID("_ZenithColor");
    static readonly int HorizonId = Shader.PropertyToID("_HorizonColor");
    static readonly int LowerId = Shader.PropertyToID("_LowerColor");
    static readonly int CloudEdgeId = Shader.PropertyToID("_CloudEdgeColor");
    static readonly int CloudShadowId = Shader.PropertyToID("_CloudShadowColor");
    static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    static readonly int CoverageId = Shader.PropertyToID("_CloudCoverage");
    static readonly int OpacityId = Shader.PropertyToID("_CloudOpacity");
    static readonly int GlowColorId = Shader.PropertyToID("_SunGlowColor");
    static readonly int GlowStrengthId = Shader.PropertyToID("_SunGlowStrength");
    static readonly int GlowDirectionId = Shader.PropertyToID("_SunGlowDirection");
    static readonly int NearId = Shader.PropertyToID("_NearColor");
    static readonly int FarId = Shader.PropertyToID("_FarColor");

    Material originalSky, sky, mountainMaterial;
    RailDistantMountains mountains;
    Scene scene;
    Light sun, sunlight;
    Quaternion sunRotation;
    Color sunColor, ambient, fogColor;
    float sunIntensity, density;
    bool fog, active;
    FogMode fogMode;
    AmbientMode ambientMode;
    float lastProgress = float.NaN;
    RailAtmosphereSettings settings;

    public RailAtmosphere(RailAtmosphereSettings settings = null)
    {
        this.settings = settings;
    }

    /// <summary>The runtime sky copy while applied, otherwise null.</summary>
    public Material Sky => sky;
    /// <summary>The runtime distant mountain ring while applied, otherwise null.</summary>
    public RailDistantMountains Mountains => mountains;

    public static bool IsStormSky(Material material) => material != null && material.shader != null && material.shader.name == StormSkyShader;

    // Route distance gives late joiners and a replacement master the same pre-dawn state.
    // Success blends during the automatic bridge/tunnel ride; failures never receive dawn.
    public static float Progress(float distanceIntoFinale, bool success, double elapsed)
    {
        float pre = Mathf.SmoothStep(0, 1, Mathf.Clamp01(distanceIntoFinale / 25f));
        return success ? 1 + Mathf.SmoothStep(0, 1, Mathf.Clamp01((float)(elapsed - 12) / 8f)) : pre;
    }

    public void Apply(float progress)
    {
        if (settings == null) settings = Resources.Load<RailAtmosphereSettings>(RailAtmosphereSettings.ResourcePath);
        // Use the same success/dawn blend as fog, including late joins and replay.
        PlayerViewDistance = Mathf.Lerp(NightViewDistance, DawnViewDistance, Mathf.Clamp01(progress - 1f));
        if (!active)
        {
            originalSky = RenderSettings.skybox;
            // Use the scene's referenced material so the shader/variant is included in builds.
            if (!IsStormSky(originalSky)) return;
            active = true;
            scene = SceneManager.GetActiveScene();
            sky = new Material(originalSky) { name = "Expedition sky (runtime)", hideFlags = HideFlags.DontSave };
            ambientMode = RenderSettings.ambientMode; ambient = RenderSettings.ambientLight;
            fog = RenderSettings.fog; fogMode = RenderSettings.fogMode;
            fogColor = RenderSettings.fogColor; density = RenderSettings.fogDensity;
            sun = RenderSettings.sun;
            if (sun != null) { sunRotation = sun.transform.rotation; sunColor = sun.color; sunIntensity = sun.intensity; }
            RenderSettings.skybox = sky;
            CreateMountains();
        }
        if (mountains != null) mountains.FollowRoute();
        float t = Mathf.Clamp(progress, 0, 2);
        if (t == lastProgress) return;
        lastProgress = t;
        bool dawn = t > 1;
        float blend = dawn ? t - 1 : t;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        float nightDensity = settings != null ? settings.nightDensity : .06f;
        float preDensity = settings != null ? settings.preDawnDensity : .05f;
        float dawnDensity = settings != null ? settings.dawnDensity : .0125f;
        Color nightColor = settings != null ? settings.nightColor : NightFog;
        Color preColor = settings != null ? settings.preDawnColor : PreFog;
        Color dawnColor = settings != null ? settings.dawnColor : DawnFog;
        RenderSettings.fogDensity = Mathf.Max(0, Mathf.Lerp(dawn ? preDensity : nightDensity, dawn ? dawnDensity : preDensity, blend));
        RenderSettings.fogColor = Color.Lerp(dawn ? preColor : nightColor, dawn ? dawnColor : preColor, blend);
        var nightSky = settings != null ? settings.nightSky : RailAtmosphereSettings.SkyPhase.Night;
        var preSky = settings != null ? settings.preDawnSky : RailAtmosphereSettings.SkyPhase.PreDawn;
        var dawnSky = settings != null ? settings.dawnSky : RailAtmosphereSettings.SkyPhase.Dawn;
        var phase = RailAtmosphereSettings.SkyPhase.Lerp(dawn ? preSky : nightSky, dawn ? dawnSky : preSky, blend);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = phase.ambient;
        if (sun != null)
        {
            // The scene's baked moonlight: only its transform and values follow the stage.
            sun.transform.rotation = Quaternion.Euler(phase.sunElevation, SunYaw, 0);
            sun.color = Color.Lerp(dawn ? new Color(.5f, .55f, .7f) : new Color(.35f, .42f, .6f),
                dawn ? new Color(1, .72f, .48f) : new Color(.5f, .55f, .7f), blend);
            sun.intensity = Mathf.Lerp(dawn ? .06f : .025f, dawn ? .22f : .06f, blend);
        }
        ApplySunlight(phase);
        ApplySky(phase, RenderSettings.fogColor);
    }

    // Realtime sunlight on top of the baked night lighting (the scene's moonlight is baked-only, so
    // changing it has no runtime effect). No shadows; the light exists only while it is lit.
    void ApplySunlight(RailAtmosphereSettings.SkyPhase phase)
    {
        if (phase.sunlightIntensity <= .001f)
        {
            if (sunlight != null) sunlight.enabled = false;
            return;
        }
        if (sunlight == null)
        {
            var go = new GameObject("Dawn sun (runtime)") { hideFlags = HideFlags.DontSave };
            sunlight = go.AddComponent<Light>();
            sunlight.type = LightType.Directional;
            sunlight.shadows = LightShadows.None;
            sunlight.renderMode = LightRenderMode.ForcePixel;
            // A light created at runtime is realtime by default (lightmapBakeType is editor-only).
        }
        sunlight.enabled = true;
        sunlight.transform.rotation = Quaternion.Euler(phase.sunElevation, SunYaw, 0);
        sunlight.color = phase.sunlight;
        sunlight.intensity = phase.sunlightIntensity;
    }

    /// <summary>The runtime dawn sunlight while it exists, otherwise null.</summary>
    public Light Sunlight => sunlight;

    void ApplySky(RailAtmosphereSettings.SkyPhase phase, Color fogColour)
    {
        sky.SetColor(ZenithId, phase.zenith);
        sky.SetColor(HorizonId, phase.horizon);
        sky.SetColor(CloudEdgeId, phase.cloudEdge);
        sky.SetColor(CloudShadowId, phase.cloudShadow);
        sky.SetFloat(CoverageId, phase.cloudCoverage);
        sky.SetFloat(OpacityId, phase.cloudOpacity);
        sky.SetFloat(ExposureId, phase.exposure);
        // The shader multiplies by exposure; divide in linear space so the lower sky equals the fog colour.
        float exposure = Mathf.Max(phase.exposure, .01f);
        Color lower = fogColour.linear;
        sky.SetColor(LowerId, new Color(lower.r / exposure, lower.g / exposure, lower.b / exposure, 1).gamma);
        sky.SetColor(GlowColorId, phase.sunGlow);
        sky.SetFloat(GlowStrengthId, phase.sunGlowStrength);
        // The glow follows the sun, but stays just above the horizon while the sun is below it.
        Vector3 toSun = -(Quaternion.Euler(Mathf.Max(phase.sunElevation, 3), SunYaw, 0) * Vector3.forward);
        sky.SetVector(GlowDirectionId, toSun);
        if (mountainMaterial != null)
        {
            mountainMaterial.SetColor(NearId, phase.mountainNear);
            mountainMaterial.SetColor(FarId, phase.mountainFar);
        }
    }

    void CreateMountains()
    {
        var prefab = Resources.Load<RailDistantMountains>(MountainsResource);
        if (prefab == null) return;
        mountains = Object.Instantiate(prefab);
        mountains.name = "Distant mountains (runtime)";
        mountains.gameObject.hideFlags = HideFlags.DontSave;
        var renderer = mountains.GetComponent<MeshRenderer>();
        mountainMaterial = new Material(renderer.sharedMaterial) { name = "Distant mountains (runtime)", hideFlags = HideFlags.DontSave };
        renderer.sharedMaterial = mountainMaterial;
        mountains.FollowRoute();
    }

    // Sunrise bearing shared by the directional light and the sky glow.
    public const float SunYaw = 125;
    // Night/dawn match horizon samples; pre-dawn is the user's #060603 override.
    public static readonly Color NightFog = new Color(.008f, .008f, .004f);
    public static readonly Color PreFog = new Color32(6, 6, 3, 255);
    public static readonly Color DawnFog = new Color(.9f, .8f, .62f);

    public void Restore()
    {
        PlayerViewDistance = NightViewDistance;
        if (!active) return;
        // A new active scene owns its own environment.
        if (SceneManager.GetActiveScene() == scene)
        {
            RenderSettings.skybox = originalSky;
            RenderSettings.ambientMode = ambientMode; RenderSettings.ambientLight = ambient;
            RenderSettings.fog = fog; RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = fogColor; RenderSettings.fogDensity = density;
        }
        if (sun != null) { sun.transform.rotation = sunRotation; sun.color = sunColor; sun.intensity = sunIntensity; }
        Release(sky);
        Release(mountainMaterial);
        if (mountains != null) Release(mountains.gameObject);
        if (sunlight != null) Release(sunlight.gameObject);
        sky = null; mountainMaterial = null; mountains = null; sunlight = null;
        active = false;
        lastProgress = float.NaN;
    }

    static void Release(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }
}
