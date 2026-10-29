using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Render-only proposal. Does not change the material asset or live player palette.</summary>
public sealed class DuckGrayscalePalettePreview : EditorWindow
{
    static readonly string[] Names = { "Default yellow", "Crimson red", "Burnt orange", "Lime green", "Forest green", "Teal", "Sky blue", "Royal blue", "Purple", "Hot pink", "Chocolate", "Charcoal" };
    static readonly string[] Hex = { "FFFFFF", "C8102E", "E86800", "82C900", "006B35", "008C88", "42BFFF", "153DC4", "7226B8", "EA258E", "713D20", "080B10" };
    const string Folder = ".utmp/duck-colors/grayscale";
    const int Width = 480, Height = 280;
    Texture2D[] images;
    Vector2 scroll;

    // Optional one-shot request for rendering after script import, without entering Play mode.
    [InitializeOnLoadMethod]
    static void RenderRequestedPreview()
    {
        if (!File.Exists(Folder + "/render.request")) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Folder + "/render.request");
            Generate();
        };
    }

    [MenuItem("Tools/DuckZ/Experiments/Palette/Preview Grayscale Duck Palette", false, 1)]
    public static void Generate()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before generating the palette preview.");
        Directory.CreateDirectory(Folder);
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        bool async = ShaderUtil.allowAsyncCompilation;
        var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        var oldTarget = RenderTexture.active;
        var sheet = new Texture2D(Width * 4, Height * 3, TextureFormat.RGB24, false);
        Material previewMaterial = null;
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.42f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/DuckSkin_Ragdoll.prefab");
            var duck = Instantiate(prefab);
            duck.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var transform in duck.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 31;
            foreach (var behaviour in duck.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var rb in duck.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            duck.SetActive(true);
            var renderers = duck.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var body = renderers.OrderByDescending(r => r.bones.Length).First();
            previewMaterial = new Material(body.sharedMaterial) { hideFlags = HideFlags.HideAndDontSave };
            previewMaterial.shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/Experiments/Palette/DuckGrayscalePreview.shader");
            body.sharedMaterial = previewMaterial;
            foreach (var renderer in renderers) renderer.updateWhenOffscreen = true;
            Bounds bounds = body.bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var camera = new GameObject("Palette preview camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.cullingMask = 1 << 31;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x * Height / Width) * 1.15f;
            camera.transform.position = bounds.center + new Vector3(0, 0, 5);
            camera.transform.LookAt(bounds.center);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.13f, 0.15f, 0.18f);
            camera.targetTexture = target;
            var key = new GameObject("Neutral key light").AddComponent<Light>();
            key.type = LightType.Directional;
            key.cullingMask = 1 << 31;
            key.intensity = 1.1f;
            key.transform.rotation = Quaternion.Euler(25, 155, 0);
            var fill = new GameObject("Neutral fill light").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.cullingMask = 1 << 31;
            fill.intensity = 0.45f;
            fill.transform.rotation = Quaternion.Euler(0, 210, 0);
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < 12; i++)
            {
                ColorUtility.TryParseHtmlString("#" + Hex[i], out Color tint);
                block.SetColor("_DuckTint", tint);
                block.SetFloat("_DuckTintEnabled", i == 0 ? 0 : 1);
                block.SetFloat("_DuckTintStrength", 0.75f);
                body.SetPropertyBlock(block);
                // Warm the skinned face meshes before capturing the first duck.
                camera.Render();
                camera.Render();
                if (ShaderUtil.ShaderHasError(body.sharedMaterial.shader)) throw new Exception("Duck shader failed to compile.");
                RenderTexture.active = target;
                var tile = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                tile.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tile.Apply();
                File.WriteAllBytes($"{Folder}/{i:00}.png", tile.EncodeToPNG());
                sheet.SetPixels((i % 4) * Width, (2 - i / 4) * Height, Width, Height, tile.GetPixels());
                DestroyImmediate(tile);
            }
            sheet.Apply();
            File.WriteAllBytes(Folder + "/palette.png", sheet.EncodeToPNG());
            var html = new StringBuilder("<!doctype html><meta charset='utf-8'><title>Duck palette proposal</title><style>body{background:#171c23;color:#eee;font:16px system-ui;margin:32px}h1{margin-bottom:8px}.grid{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:16px;max-width:1500px}figure{margin:0;background:#21262e;border-radius:10px;overflow:hidden}img{width:100%;display:block}figcaption{padding:12px 16px}small{color:#b0bac8}</style><h1>Grayscale-first duck palette · 75% tint</h1><p>Actual Unity preview-only shader renders under neutral lighting. Grayscale luminance first, then 75% tint. Default yellow is untinted. Preview only; live shader and palette unchanged.</p><div class='grid'>");
            for (int i = 0; i < 12; i++) html.Append($"<figure><img src='{i:00}.png'><figcaption>{i + 1:00} · {Names[i]}<br><small>{(i == 0 ? "Original texture" : "Tint #" + Hex[i])}</small></figcaption></figure>");
            File.WriteAllText(Folder + "/index.html", html.Append("</div>").ToString());
            Debug.Log("Duck palette preview rendered: " + Path.GetFullPath(Folder + "/index.html"));
        }
        finally
        {
            RenderTexture.active = oldTarget;
            if (previewMaterial != null) DestroyImmediate(previewMaterial);
            DestroyImmediate(sheet);
            DestroyImmediate(target);
            ShaderUtil.allowAsyncCompilation = async;
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
        var window = GetWindow<DuckGrayscalePalettePreview>("Grayscale Ducks · 75%");
        window.minSize = new Vector2(800, 600);
        window.LoadImages();
        window.Show();
    }

    void LoadImages()
    {
        if (images != null) foreach (var image in images) if (image != null) DestroyImmediate(image);
        images = new Texture2D[12];
        for (int i = 0; i < 12; i++)
            if (File.Exists($"{Folder}/{i:00}.png"))
            {
                images[i] = new Texture2D(2, 2);
                images[i].LoadImage(File.ReadAllBytes($"{Folder}/{i:00}.png"));
            }
    }

    void OnGUI()
    {
            if (images == null || images.Length != 12) LoadImages();
        EditorGUILayout.LabelField("Grayscale first · 75% tint · Live palette unchanged", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        float width = (position.width - 35) / 4;
        for (int row = 0; row < 3; row++)
        {
            EditorGUILayout.BeginHorizontal();
            for (int col = 0; col < 4; col++)
            {
                int i = row * 4 + col;
                EditorGUILayout.BeginVertical(GUILayout.Width(width));
                var rect = GUILayoutUtility.GetRect(width, width * Height / Width);
                if (images[i] != null) GUI.DrawTexture(rect, images[i], ScaleMode.ScaleToFit);
                GUILayout.Label($"{i + 1:00} · {Names[i]}", EditorStyles.boldLabel);
                GUILayout.Label(i == 0 ? "Untinted" : "#" + Hex[i]);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    void OnDisable()
    {
        if (images != null) foreach (var image in images) if (image != null) DestroyImmediate(image);
        images = null;
    }
}

