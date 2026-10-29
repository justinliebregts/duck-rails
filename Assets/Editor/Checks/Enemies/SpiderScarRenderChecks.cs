using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Run in batch mode with graphics enabled; asserts actual pixels, not particle count.
public static class SpiderScarRenderChecks
{
    static Camera camera;
    static RenderTexture target;
    static int frames;
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<UniversalRendererData>());
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        camera = new GameObject("Scar test camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 0, -2);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.white;
        camera.orthographic = true;
        camera.orthographicSize = 0.7f;
        target = new RenderTexture(256, 256, 24); target.Create(); camera.targetTexture = target;
        var pool = new GameObject("Scar pool").AddComponent<SpiderScarPool>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(SpiderScarPool).GetMethod("Awake", flags).Invoke(pool, null);
        var surface = new GameObject("Surface anchor").transform;
        typeof(SpiderScarPool).GetMethod("Place", flags).Invoke(pool,
            new object[] { surface, Vector3.zero, Vector3.forward, 1f, Time.time, 0.7f, 45f });
        EditorApplication.update += Render;
    }
    static void Render()
    {
        if (++frames < 20) return;
        EditorApplication.update -= Render;
        try
        {
            camera.Render();
            RenderTexture.active = target;
            var pixels = new Texture2D(256, 256, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); pixels.Apply();
            int black = 0, white = 0;
            foreach (var p in pixels.GetPixels32())
            {
                if (p.r < 20 && p.g < 20 && p.b < 20) black++;
                if (p.r > 230 && p.g > 230 && p.b > 230) white++;
            }
            if (black < 1000 || white < 1000) throw new Exception($"Scar failed pixel check: black={black}, white={white}");
            System.IO.Directory.CreateDirectory("Temp");
            System.IO.File.WriteAllBytes("Temp/SpiderScarRender.png", pixels.EncodeToPNG());
            Debug.Log($"SpiderScarRenderChecks: ALL PASSED; black={black}, white={white}");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}

