using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DeathFlareShaderChecks
{
    [MenuItem("Tools/DuckZ/Validation/Effects/Validate Flare Fog and Occlusion")]
    public static void Validate()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Effects/DeathFlareGlow.mat");
        Check(material != null && material.shader.name == "DuckZ/Particles/Death Flare No Fog", "material shader assignment");
        Check(material.passCount == 1, "single render pass");
        var previousScene = SceneManager.GetActiveScene();
        var testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        Material reference = null, wallMaterial = null;
        bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            Check(testScene.IsValid() && testScene.isLoaded, "loaded render scene");
            Check(EditorSceneManager.GetActiveScene() == testScene || EditorSceneManager.SetActiveScene(testScene), "isolated render scene");
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.7f;
            RenderSettings.fogColor = Color.black;
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DeathFlareSetup.PrefabPath));
            SceneManager.MoveGameObjectToScene(root, testScene);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var child in root.GetComponentsInChildren<Transform>()) child.gameObject.layer = 31;
            // A stationary particle snapshot makes each render directly comparable.
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>()) ps.Simulate(0.5f, false, true);
            var renderers = root.GetComponentsInChildren<ParticleSystemRenderer>();
            foreach (var renderer in renderers) Check(renderer.sharedMaterial == material, "all flare systems use fog-free material");
            var light = root.GetComponentInChildren<Light>();
            if (light != null) light.enabled = false;
            var camera = new GameObject("Flare render check camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.scene = testScene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(testScene);
            camera.cullingMask = 1 << 31;
            camera.useOcclusionCulling = false;
            camera.transform.position = new Vector3(0, 0, -8);
            camera.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.fieldOfView = 35;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100;

            Color32[] clear = Capture(camera, false);
            Color32[] fog = Capture(camera, true);
            Debug.Log($"[DeathFlare] Render energy {Energy(clear)}, fog delta {Difference(clear, fog)}");
            Check(Energy(clear) > 0.001f, "visible particles");
            Check(Difference(clear, fog) < 0.0001f, "fog leaves flare unchanged");
            Check(!ShaderUtil.ShaderHasError(material.shader), "shader compiles");

            // Control render proves the fixture really applies fog, rather than merely disabling it.
            reference = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            reference.CopyPropertiesFromMaterial(material);
            reference.SetFloat("_Surface", 1);
            reference.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            reference.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            reference.SetFloat("_ZWrite", 0);
            reference.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            foreach (var renderer in renderers) renderer.sharedMaterial = reference;
            Color32[] referenceClear = Capture(camera, false);
            Color32[] referenceFog = Capture(camera, true);
            Check(Difference(referenceClear, referenceFog) > 0.001f, "standard URP particles are obscured by test fog");
            foreach (var renderer in renderers) renderer.sharedMaterial = material;

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = 31;
            SceneManager.MoveGameObjectToScene(wall, testScene);
            wall.transform.position = new Vector3(0, 0, -3);
            wall.transform.localScale = new Vector3(20, 20, 0.5f);
            wallMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            wallMaterial.SetColor("_BaseColor", Color.black);
            wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            Color32[] blocked = Capture(camera, true);
            foreach (var renderer in renderers) renderer.enabled = false;
            Color32[] wallOnly = Capture(camera, true);
            Check(Difference(blocked, wallOnly) < 0.0001f, "opaque geometry still hides flare");

            Directory.CreateDirectory(".utmp/death-flare");
            File.WriteAllText(".utmp/death-flare/fog-checks.txt",
                $"PASS: single pass; shader compiled; all four systems wired; visible through fog; opaque wall occludes.\n" +
                $"Fog difference: {Difference(clear, fog)}\nControl fog difference: {Difference(referenceClear, referenceFog)}\nWall difference: {Difference(blocked, wallOnly)}\n");
            Debug.Log("[DeathFlare] Fog render checks passed: fog-free visibility, standard shader control, opaque-wall occlusion, one render pass.");
        }
        finally
        {
            EditorSceneManager.SetActiveScene(previousScene);
            ShaderUtil.allowAsyncCompilation = asyncCompilation;
            EditorSceneManager.CloseScene(testScene, true);
            if (reference != null) UnityEngine.Object.DestroyImmediate(reference);
            if (wallMaterial != null) UnityEngine.Object.DestroyImmediate(wallMaterial);
        }
    }

    static Color32[] Capture(Camera camera, bool fog)
    {
        RenderSettings.fog = fog;
        var target = RenderTexture.GetTemporary(256, 256, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var image = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            image.Apply();
            return image.GetPixels32();
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    static float Energy(Color32[] pixels)
    {
        double total = 0;
        foreach (var p in pixels) total += p.r + p.g + p.b;
        return (float)(total / (pixels.Length * 3d * 255));
    }

    static float Difference(Color32[] a, Color32[] b)
    {
        double total = 0;
        for (int i = 0; i < a.Length; i++)
            total += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
        return (float)(total / (a.Length * 3d * 255));
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Flare shader check failed: " + message);
    }
}
