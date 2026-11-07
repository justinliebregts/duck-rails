using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Local rendering of shared route/finish progress. No runtime GI or probe captures.</summary>
public sealed class RailAtmosphere
{
    Material originalSky, sky;
    Scene scene;
    Light sun;
    Quaternion sunRotation;
    Color sunColor, ambient, fogColor;
    float sunIntensity, density;
    bool fog;
    FogMode fogMode;
    AmbientMode ambientMode;
    float lastProgress = float.NaN;

    // Route distance gives late joiners and a replacement master the same pre-dawn state.
    // Success blends during the automatic bridge/tunnel ride; failures never receive dawn.
    public static float Progress(float distanceIntoFinale, bool success, double elapsed)
    {
        float pre = Mathf.SmoothStep(0, 1, Mathf.Clamp01(distanceIntoFinale / 25f));
        return success ? 1 + Mathf.SmoothStep(0, 1, Mathf.Clamp01((float)(elapsed - 12) / 8f)) : pre;
    }

    public void Apply(float progress)
    {
        if (sky == null)
        {
            originalSky = RenderSettings.skybox;
            // Use the scene's referenced material so the shader/variant is included in builds.
            if (originalSky == null || originalSky.shader.name != "Skybox/Procedural") return;
            scene = SceneManager.GetActiveScene();
            sky = new Material(originalSky) { name = "Expedition sky (runtime)", hideFlags = HideFlags.DontSave };
            ambientMode = RenderSettings.ambientMode; ambient = RenderSettings.ambientLight;
            fog = RenderSettings.fog; fogMode = RenderSettings.fogMode;
            fogColor = RenderSettings.fogColor; density = RenderSettings.fogDensity;
            sun = RenderSettings.sun;
            if (sun != null) { sunRotation = sun.transform.rotation; sunColor = sun.color; sunIntensity = sun.intensity; }
            RenderSettings.skybox = sky;
        }
        float t = Mathf.Clamp(progress, 0, 2);
        if (t == lastProgress) return;
        lastProgress = t;
        bool dawn = t > 1;
        float blend = dawn ? t - 1 : t;
        sky.SetFloat("_Exposure", Mathf.Lerp(dawn ? .08f : .012f, dawn ? .3f : .08f, blend));
        sky.SetFloat("_AtmosphereThickness", 1f);
        sky.SetColor("_SkyTint", new Color(.5f, .5f, .5f));
        sky.SetColor("_GroundColor", new Color(.12f, .13f, .16f));
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = Mathf.Lerp(dawn ? .065f : .1f, dawn ? .0125f : .065f, blend);
        RenderSettings.fogColor = Color.Lerp(dawn ? PreFog : NightFog, dawn ? DawnFog : PreFog, blend);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(dawn ? PreAmbient : NightAmbient, dawn ? DawnAmbient : PreAmbient, blend);
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(dawn ? -5 : -12, dawn ? 3 : -5, blend), 125, 0);
            sun.color = Color.Lerp(dawn ? new Color(.5f, .55f, .7f) : new Color(.35f, .42f, .6f),
                dawn ? new Color(1, .72f, .48f) : new Color(.5f, .55f, .7f), blend);
            sun.intensity = Mathf.Lerp(dawn ? .06f : .025f, dawn ? .22f : .06f, blend);
        }
    }

    // Night/dawn match horizon samples; pre-dawn is the user's #060603 override.
    public static readonly Color NightFog = new Color(.008f, .008f, .004f);
    public static readonly Color PreFog = new Color32(6, 6, 3, 255);
    public static readonly Color DawnFog = new Color(.439f, .463f, .392f);
    static readonly Color NightAmbient = new Color(.025f, .03f, .045f);
    static readonly Color PreAmbient = new Color(.055f, .065f, .09f);
    static readonly Color DawnAmbient = new Color(.22f, .20f, .19f);

    public void Restore()
    {
        if (sky == null) return;
        // A new active scene owns its own environment.
        if (SceneManager.GetActiveScene() == scene)
        {
            RenderSettings.skybox = originalSky;
            RenderSettings.ambientMode = ambientMode; RenderSettings.ambientLight = ambient;
            RenderSettings.fog = fog; RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = fogColor; RenderSettings.fogDensity = density;
        }
        if (sun != null) { sun.transform.rotation = sunRotation; sun.color = sunColor; sun.intensity = sunIntensity; }
        if (Application.isPlaying) Object.Destroy(sky);
        else Object.DestroyImmediate(sky);
        sky = null;
        lastProgress = float.NaN;
    }
}
