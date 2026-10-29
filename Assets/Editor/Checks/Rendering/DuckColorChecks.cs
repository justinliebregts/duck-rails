using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DuckColorChecks
{
    [MenuItem("Tools/DuckZ/Validation/Rendering/Validate Player Colors")]
    public static void Run()
    {
        var actors = Enumerable.Range(1, 12).ToArray();
        var slots = DuckPlayerColors.Allocate(null, actors.Reverse().ToArray());
        Check(slots.OrderBy(a => a).SequenceEqual(actors), "twelve unique random slots");
        int departedSlot = Array.IndexOf(slots, actors[3]);
        actors[3] = 50;
        var replacement = DuckPlayerColors.Allocate(slots, actors);
        Check(replacement[departedSlot] == 50 && replacement.Where((a, i) => i != departedSlot).SequenceEqual(slots.Where((a, i) => i != departedSlot)), "departure reuses only the free slot");
        Check(DuckPlayerColors.Allocate(replacement, actors.Reverse().ToArray()).SequenceEqual(replacement), "master migration preserves slots");
        Check(Enumerable.Range(0, 12).Select(DuckPlayerColors.Tint).Distinct().Count() == 12, "unique palette");

        foreach (string path in new[] { "Assets/Prefabs/Player/DuckPlayer.prefab", "Assets/Prefabs/Photon/PhotonPlayer.prefab", "Assets/Prefabs/Player/DuckSkin_Ragdoll.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var body = renderers.OrderByDescending(r => r.bones.Length).First();
                Check(body.sharedMaterials.All(m => m != null && m.HasProperty("_DuckTint")), path + " supports tint");
                var materials = renderers.Select(r => r.sharedMaterials).ToArray();
                var visuals = DuckSkinVisuals.Attach(root, DuckSkinSource.Manual);
                var colorField = typeof(DuckSkinVisuals).GetField("colorIndex", BindingFlags.Instance | BindingFlags.NonPublic);
                var apply = typeof(DuckSkinVisuals).GetMethod("ApplyTint", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int i = 0; i < 12; i++)
                {
                    colorField.SetValue(visuals, i);
                    apply.Invoke(visuals, null);
                    var block = new MaterialPropertyBlock();
                    body.GetPropertyBlock(block);
                    Check(block.GetColor("_DuckTint") == DuckPlayerColors.Tint(i), path + " tint " + i);
                    Check(block.GetFloat("_DuckTintEnabled") == (i == 0 ? 0 : 1), "default bypass");
                    for (int r = 0; r < renderers.Length; r++)
                    {
                        Check(renderers[r].sharedMaterials.SequenceEqual(materials[r]), "shared material identities preserved");
                        if (renderers[r] != body)
                            Check(!renderers[r].HasPropertyBlock(), "eyes/mouth receive no property block");
                    }
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Force a real forward-pass compilation and render, including the blue tint.
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        bool async = ShaderUtil.allowAsyncCompilation;
        var target = new RenderTexture(128, 128, 24);
        Texture2D image = null;
        var oldTarget = RenderTexture.active;
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Skins/DuckSkin_Default/DuckSkin_Default.mat");
            Check(material.shader.name == "DuckZ/Duck Body Tint", "body shader assigned");
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.GetComponent<Renderer>().sharedMaterial = material;
            var block = new MaterialPropertyBlock();
            block.SetColor("_DuckTint", DuckPlayerColors.Tint(7));
            block.SetFloat("_DuckTintEnabled", 1);
            sphere.GetComponent<Renderer>().SetPropertyBlock(block);
            var light = new GameObject("Color check light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(20, -30, 0);
            var camera = new GameObject("Color check camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.transform.position = new Vector3(0, 0, -2);
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.Render();
            Check(!ShaderUtil.ShaderHasError(material.shader), "shader compiles");
            RenderTexture.active = target;
            image = new Texture2D(128, 128, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
            image.Apply();
            Color center = image.GetPixel(64, 64);
            Check(center.b > center.r * 1.3f && center.b > center.g, "yellow texture renders blue with tint");
            System.IO.Directory.CreateDirectory(".utmp/duck-colors");
            System.IO.File.WriteAllBytes(".utmp/duck-colors/blue.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = oldTarget;
            UnityEngine.Object.DestroyImmediate(target);
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            ShaderUtil.allowAsyncCompilation = async;
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
        Debug.Log("DuckColorChecks PASS: allocation, slot reuse, migration, 12 tints on local/remote/ragdoll prefabs, shared materials, untouched face, shader compilation and blue render.");
    }

    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("DuckColorChecks: " + message);
    }
}
