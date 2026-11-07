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
            for (int i = 0; i < 3; i++)
            {
                controller.Apply(i);
                Check(RenderSettings.skybox != original, "Isolated sky material");
                Check(RenderSettings.skybox.shader.name == "Skybox/Procedural", "Built-in sky");
                Check(RenderSettings.skybox.GetFloat("_SunDisk") == 0, "Sun disk hidden");
                Check(Mathf.Approximately(RenderSettings.fogDensity, i == 0 ? .1f : i == 1 ? .065f : .0125f), "Stage fog");
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                File.WriteAllBytes("output/atmosphere/stage-" + i + ".png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            controller.Restore();
            Check(RenderSettings.skybox == original, "Restore original material");
            Check(RenderSettings.fogColor == fog && RenderSettings.fogDensity == density, "Restore fog");
            Check(RenderSettings.ambientMode == mode && RenderSettings.ambientLight == ambient, "Restore ambient");
            Check(RenderSettings.sun.transform.rotation == rotation && RenderSettings.sun.intensity == intensity, "Restore sun");
            controller.Apply(2); controller.Restore(); controller.Apply(0);
            Check(Mathf.Approximately(RenderSettings.skybox.GetFloat("_Exposure"), .012f), "Replay returns to dark night");
            Check(Mathf.Approximately(original.GetFloat("_Exposure"), .012f), "Shared material unmodified");
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
