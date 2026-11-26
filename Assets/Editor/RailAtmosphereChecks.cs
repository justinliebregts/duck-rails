using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RailAtmosphereChecks
{
    [MenuItem("Duck Rails/Lighting/Check Expedition Atmosphere")]
    public static void Run()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/Expedition.unity")
            throw new InvalidOperationException("Open Expedition first.");
        int checks = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
        Check(RailAtmosphere.Progress(-1, false, 100) == 0, "Night before finale");
        Check(RailAtmosphere.Progress(0, false, 100) == 0, "Finale boundary continuous");
        Check(Mathf.Approximately(RailAtmosphere.Progress(12.5f, false, 0), .5f), "Mid pre-dawn");
        Check(RailAtmosphere.Progress(25, false, 100) == 1, "Pre-dawn reached");
        Check(RailAtmosphere.Progress(200, false, 100) == 1, "Failure cannot reach dawn");
        Check(RailAtmosphere.Progress(80, true, 12) == 1, "Bridge ride remains pre-dawn");
        Check(Mathf.Approximately(RailAtmosphere.Progress(80, true, 16), 1.5f), "Tunnel transition");
        Check(RailAtmosphere.Progress(80, true, 20) == 2, "Dawn reached");
        Check(RailAtmosphere.Progress(80, true, 100) == 2, "Late arrival stays dawn");
        var original = RenderSettings.skybox;
        var fog = RenderSettings.fogColor; var density = RenderSettings.fogDensity;
        var ambient = RenderSettings.ambientLight; var mode = RenderSettings.ambientMode;
        var rotation = RenderSettings.sun.transform.rotation; var intensity = RenderSettings.sun.intensity;
        var controller = new RailAtmosphere();
        var settings = Resources.Load<RailAtmosphereSettings>(RailAtmosphereSettings.ResourcePath);
        var go = new GameObject("Atmosphere capture") { hideFlags = HideFlags.HideAndDontSave };
        var camera = go.AddComponent<Camera>(); camera.enabled = false;
        camera.clearFlags = CameraClearFlags.Skybox; camera.fieldOfView = 70;
        camera.farClipPlane = 250; camera.nearClipPlane = .1f;
        var route = UnityEngine.Object.FindFirstObjectByType<RailRoute>();
        camera.transform.position = route.finaleRoot.position + new Vector3(0, 3, 15);
        camera.transform.rotation = Quaternion.Euler(-5, 0, 0);
        var rt = new RenderTexture(960, 540, 24);
        var previous = RenderTexture.active;
        Directory.CreateDirectory("output/atmosphere");
        try
        {
            Check(RailAtmosphere.IsStormSky(original), "Expedition uses the storm sky");
            float originalExposure = original.GetFloat("_Exposure");
            var phases = new[] { settings.nightSky, settings.preDawnSky, settings.dawnSky };
            // Forward along the route, toward the sunrise bearing, and sideways past the block edge.
            var views = new[] { ("forward", 0f), ("sunrise", RailAtmosphere.SunYaw + 180), ("side", 90f) };
            for (int i = 0; i < 3; i++)
            {
                controller.Apply(i);
                var sky = RenderSettings.skybox;
                Check(sky != original && sky == controller.Sky, "Isolated sky material");
                Check(RailAtmosphere.IsStormSky(sky), "Storm sky");
                Check(Mathf.Approximately(RenderSettings.fogDensity, i == 0 ? settings.nightDensity : i == 1 ? settings.preDawnDensity : settings.dawnDensity), "Stage fog");
                Check(Mathf.Approximately(sky.GetFloat("_Exposure"), phases[i].exposure), "Stage sky exposure");
                Check(sky.GetColor("_HorizonColor") == phases[i].horizon && sky.GetColor("_ZenithColor") == phases[i].zenith, "Stage sky colours");
                Check(Mathf.Approximately(sky.GetFloat("_SunGlowStrength"), phases[i].sunGlowStrength), "Stage sunrise glow");
                Color lower = sky.GetColor("_LowerColor").linear * phases[i].exposure, fogLinear = RenderSettings.fogColor.linear;
                Check(Mathf.Abs(lower.r - fogLinear.r) < .002f && Mathf.Abs(lower.g - fogLinear.g) < .002f && Mathf.Abs(lower.b - fogLinear.b) < .002f, "Lower sky matches fog");
                Vector4 glow = sky.GetVector("_SunGlowDirection");
                Check(Mathf.Abs(Mathf.Asin(glow.y) * Mathf.Rad2Deg - Mathf.Max(phases[i].sunElevation, 3)) < .1f && Vector3.Dot(glow, -RenderSettings.sun.transform.forward) > .9f, "Glow on sunrise bearing");
                Check(Mathf.Approximately(sky.GetFloat("_CloudCoverage"), phases[i].cloudCoverage), "Stage cloud coverage");
                Check(RenderSettings.ambientLight == phases[i].ambient, "Stage ambient");
                var sunlight = controller.Sunlight;
                bool lit = phases[i].sunlightIntensity > .001f;
                Check(lit ? sunlight != null && sunlight.enabled && sunlight.type == LightType.Directional && sunlight.shadows == LightShadows.None
                    && Mathf.Approximately(sunlight.intensity, phases[i].sunlightIntensity) && sunlight.gameObject.hideFlags == HideFlags.DontSave
                    : sunlight == null || !sunlight.enabled, "Stage realtime sunlight");
                var ring = controller.Mountains;
                Check(ring != null && ring.gameObject.hideFlags == HideFlags.DontSave, "Mountain ring created, never saved");
                var ringMaterial = ring.GetComponent<MeshRenderer>().sharedMaterial;
                Check(ringMaterial.GetColor("_NearColor") == phases[i].mountainNear && ringMaterial.GetColor("_FarColor") == phases[i].mountainFar, "Stage mountain colours");
                float routeCentre = (route.startingStation.transform.position.z + route.finaleRoot.position.z + route.finale.length) * .5f;
                Check(Mathf.Approximately(ring.transform.position.z, routeCentre) && ring.transform.position.x == 0, "Mountain ring centred on route");
                camera.farClipPlane = RailAtmosphere.PlayerViewDistance;
                foreach (var (name, yaw) in views)
                {
                    camera.transform.rotation = Quaternion.Euler(-5, yaw, 0);
                    camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                    var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                    File.WriteAllBytes("output/atmosphere/stage-" + i + "-" + name + ".png", image.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(image);
                }
            }
            // Deep night (before the mid shop block) and its fade to night; edit mode has no live route, so 0 means night.
            Check(RailAtmosphere.MidpointLift() == 1, "No live route keeps night");
            controller.Apply(-1);
            Check(Mathf.Approximately(RenderSettings.skybox.GetFloat("_Exposure"), settings.deepNightSky.exposure), "Deep night sky exposure");
            Check(RenderSettings.ambientLight == settings.deepNightSky.ambient, "Deep night ambient");
            Check(RenderSettings.fogColor == settings.nightColor && Mathf.Approximately(RenderSettings.fogDensity, settings.nightDensity), "Deep night uses night fog");
            Check(settings.deepNightSky.exposure < settings.nightSky.exposure, "Deep night darker than night");
            controller.Apply(-.5f);
            Check(Mathf.Approximately(RenderSettings.skybox.GetFloat("_Exposure"), (settings.deepNightSky.exposure + settings.nightSky.exposure) / 2), "Deep night fade midpoint");
            controller.Apply(0);
            Check(Mathf.Approximately(RenderSettings.skybox.GetFloat("_Exposure"), settings.nightSky.exposure), "Fade ends at night");
            controller.Restore();
            Check(controller.Mountains == null && UnityEngine.Object.FindObjectsByType<RailDistantMountains>(FindObjectsSortMode.None).Length == 0, "Mountain ring removed");
            Check(controller.Sunlight == null && GameObject.Find("Dawn sun (runtime)") == null, "Dawn sunlight removed");
            Check(RenderSettings.skybox == original, "Restore original material");
            Check(RenderSettings.fogColor == fog && RenderSettings.fogDensity == density, "Restore fog");
            Check(RenderSettings.ambientMode == mode && RenderSettings.ambientLight == ambient, "Restore ambient");
            Check(RenderSettings.sun.transform.rotation == rotation && RenderSettings.sun.intensity == intensity, "Restore sun");
            controller.Apply(2); controller.Restore(); controller.Apply(0);
            Check(Mathf.Approximately(RenderSettings.skybox.GetFloat("_Exposure"), settings.nightSky.exposure), "Replay returns to dark night");
            Check(RenderSettings.skybox.GetFloat("_SunGlowStrength") == settings.nightSky.sunGlowStrength, "Replay clears sunrise glow");
            Check(Mathf.Approximately(original.GetFloat("_Exposure"), originalExposure), "Shared material unmodified");
            File.WriteAllText("output/atmosphere/checks.txt", checks + " checks passed. Editor rendering only; headset and multiplayer pending.\n");
            Debug.Log("Atmosphere: " + checks + " checks passed.");
        }
        finally
        {
            controller.Restore(); camera.targetTexture = null; RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
