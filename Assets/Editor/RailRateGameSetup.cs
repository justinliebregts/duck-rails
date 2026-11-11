using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Creates the editable compound rating station without regenerating compound art.</summary>
public static class RailRateGameSetup
{
    public const string Folder = "Assets/DuckRails/Compound/Rating";
    public const string PrefabPath = Folder + "/RateGameStation.prefab";

    [MenuItem("Duck Rails/Compound/Install Rating Station")]
    public static void Install()
    {
        if (!Application.dataPath.Replace('\\', '/').EndsWith("/duck-rails/Assets", StringComparison.OrdinalIgnoreCase) || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run in the Duck Rails editor outside Play Mode.");
        AssetDatabase.Refresh();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) prefab = BuildPrefab();
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Compound.unity");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Compound.unity", OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty) throw new InvalidOperationException("Compound has unsaved edits; save those before installing.");
            var world = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<RailWorld>(true)).Single();
            if (world.compound.GetComponentInChildren<RailRateGame>(true) == null)
            {
                var station = (GameObject)PrefabUtility.InstantiatePrefab(prefab, world.compound.transform);
                Undo.RegisterCreatedObjectUndo(station, "Install rating station");
                // On the inner left partition of the information nook, ahead of its bench.
                Vector3 nook = new Vector3(-5.65f, 0, 3.4f);
                Quaternion rotation = Quaternion.Euler(0, -65, 0);
                station.transform.position = nook + rotation * new Vector3(-2.53f, 0, -.35f);
                station.transform.rotation = rotation * Quaternion.Euler(0, -90, 0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(station.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Validate(world);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    static GameObject BuildPrefab()
    {
        string png = Folder + "/RateGameInstructions.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(png);
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var root = new GameObject("Rate Game Station");
        try
        {
            var action = root.AddComponent<RailRateGame>();
            var canvasObject = new GameObject("Instructions Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(root.transform, false);
            canvasObject.transform.localPosition = new Vector3(.35f, 1.5f, 0);
            canvasObject.transform.localScale = Vector3.one * .0015f;
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1024, 768);
            var image = new GameObject("Instructions PNG - replace texture", typeof(RectTransform), typeof(RawImage));
            image.transform.SetParent(canvasObject.transform, false);
            var rect = image.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.GetComponent<RawImage>().texture = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
            image.GetComponent<RawImage>().raycastTarget = false;

            var buttonObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/PressableButton.prefab"), root.transform);
            buttonObject.name = "RATE GAME";
            buttonObject.transform.localPosition = new Vector3(-.86f, 1.25f, -.055f);
            var button = buttonObject.GetComponent<PressableButton>();
            button.GetComponent<BoxCollider>().size = new Vector3(.65f, .24f, .07f);
            button.cap.Find("Cap").localScale = new Vector3(.65f, .24f, .05f);
            var label = button.GetComponentInChildren<TMP_Text>();
            label.font = Font; label.text = "RATE GAME"; label.fontSize = .85f;
            label.rectTransform.sizeDelta = new Vector2(.61f, .2f);
            UnityEventTools.AddPersistentListener(button.onPressed, action.OpenRatingPage);
            foreach (var component in buttonObject.GetComponentsInChildren<Component>())
                if (component != null) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            action.status = Label(root.transform, "Status", "Share your feedback", new Vector3(-.86f, 1.55f, -.06f), new Vector2(.65f,.32f));
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");
    static TMP_Text Label(Transform parent, string name, string text, Vector3 position, Vector2 size)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
        var label = go.AddComponent<TextMeshPro>(); label.font = Font; label.text = text;
        label.fontSize = .65f; label.enableAutoSizing = true; label.fontSizeMin = .35f; label.fontSizeMax = .65f;
        label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = size;
        return label;
    }

    static void Validate(RailWorld world)
    {
        var actions = world.compound.GetComponentsInChildren<RailRateGame>(true);
        if (actions.Length != 1) throw new InvalidOperationException("Expected one rating station.");
        var action = actions[0]; var button = action.GetComponentInChildren<PressableButton>();
        if (button.onPressed.GetPersistentEventCount() != 1 || button.onPressed.GetPersistentTarget(0) != action ||
            button.onPressed.GetPersistentMethodName(0) != nameof(RailRateGame.OpenRatingPage))
            throw new InvalidOperationException("Rating button callback is not bound.");
        if (action.GetComponentInChildren<RawImage>().texture == null || action.status == null ||
            action.GetComponentInChildren<Canvas>().renderMode != RenderMode.WorldSpace ||
            action.GetComponentsInChildren<TMP_Text>().Any(label => label.font != Font))
            throw new InvalidOperationException("Rating image/canvas/font references are invalid.");
        Debug.Log("[Rate Game] PASS: one saved station, physical button callback, PNG canvas, CutePixel labels. Device review flow remains queued.");
    }
}
