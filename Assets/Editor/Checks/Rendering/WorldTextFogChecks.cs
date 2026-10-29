using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorldTextFogChecks
{
    [MenuItem("Tools/DuckZ/Validation/Rendering/Validate Text Fog")]
    public static void Validate()
    {
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        bool async = ShaderUtil.allowAsyncCompilation;
        Material material = null;
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            SceneManager.SetActiveScene(scene);
            var camera = new GameObject("Text fog check camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100;
            var label = new GameObject("CC fog check").AddComponent<TextMeshPro>();
            label.gameObject.layer = 31;
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");
            label.text = "CC Creator";
            label.fontSize = 6;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(8, 2);
            foreach (string shaderName in new[] { "TextMeshPro/Distance Field", "TextMeshPro/Mobile/Distance Field" })
            {
                material = new Material(label.font.material);
                material.shader = Shader.Find(shaderName);
                material.SetColor("_OutlineColor", Color.red);
                material.SetFloat("_OutlineWidth", 0.2f);
                material.EnableKeyword("OUTLINE_ON");
                material.EnableKeyword("UNDERLAY_ON");
                material.SetColor("_UnderlayColor", Color.red);
                label.fontSharedMaterial = material;
                foreach (float distance in new[] { 5f, 9.9f, 10.1f, 25f })
                {
                    label.transform.position = new Vector3(0, 0, distance);
                    label.transform.localScale = Vector3.one * distance / 10f;
                    label.color = new Color(1, 0.18431371f, 0.18431371f, 1);
                    label.ForceMeshUpdate();
                    RenderSettings.fog = false;
                    float redClear = Capture(camera);
                    RenderSettings.fog = true;
                    RenderSettings.fogMode = FogMode.ExponentialSquared;
                    RenderSettings.fogDensity = 0.12f;
                    Check(redClear > 0.001f && Capture(camera) < redClear * 0.98f,
                        "red CC nametag fades at " + distance + "m with " + shaderName);
                    // Simulate shine overwriting vertex colours with fully opaque white.
                    var colors = label.textInfo.meshInfo[0].colors32;
                    for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(255, 255, 255, 255);
                    label.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
                    RenderSettings.fog = false;
                    float clear = Capture(camera);
                    Check(clear > 0.001f, "text visible without fog: " + shaderName);
                    foreach (FogMode mode in new[] { FogMode.Linear, FogMode.Exponential, FogMode.ExponentialSquared })
                    {
                        RenderSettings.fog = true;
                        RenderSettings.fogMode = mode;
                        RenderSettings.fogStartDistance = 0;
                        RenderSettings.fogEndDistance = 30;
                        RenderSettings.fogDensity = 0.12f;
                        float fogged = Capture(camera);
                        Check(fogged < clear * 0.98f, "fog attenuates opaque CC shine, outline and shadow: " + shaderName + " " + mode + " " + distance);
                        if (mode == FogMode.ExponentialSquared)
                        {
                            RenderSettings.fogDensity = 0.025f;
                            Check(Capture(camera) > fogged, "night vision increases visibility");
                        }
                    }
                }
                Check(!ShaderUtil.ShaderHasError(material.shader), "shader compiles");
                UnityEngine.Object.DestroyImmediate(material);
                material = null;
            }
            Debug.Log("[TextFog] PASS: both shaders, all fog modes, below/above 10m, opaque CC shine vertex colours, outline/shadow, night vision.");
        }
        finally
        {
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
            ShaderUtil.allowAsyncCompilation = async;
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    static float Capture(Camera camera)
    {
        var target = RenderTexture.GetTemporary(256, 256, 24, RenderTextureFormat.ARGB32);
        var old = RenderTexture.active;
        var image = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            image.Apply();
            double energy = 0;
            foreach (var p in image.GetPixels32()) energy += p.r + p.g + p.b;
            return (float)(energy / (256 * 256 * 3d * 255));
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = old;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("Text fog check failed: " + message);
    }
}