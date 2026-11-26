using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

/// <summary>Creates the editable compound rating station without regenerating compound art.</summary>
public static class RailRateGameSetup
{
    public const string Folder = "Assets/DuckRails/Compound/Rating";
    public const string PrefabPath = Folder + "/RateGameStation.prefab";

    public const float ButtonHeight = .38f;
    const string InstructionsPath = Folder + "/RateGameInstructions.png";
    const string RatingAppeal = "Please rate us! It helps a ton for making new content.";

    static readonly Color BodyColor = new Color32(255, 227, 177, 255);
    static readonly Color ButtonLabelColor = new Color32(48, 25, 9, 255);
    static readonly int[] WalkthroughRows = { 258, 771, 1284 };
    static readonly string[] WalkthroughSteps = { "1. Scroll to Reviews", "2. Select See all", "3. Write a Review" };

    [MenuItem("Duck Rails/Compound/Install Rating Station")]
    public static void Install()
    {
        if (!Application.dataPath.Replace('\\', '/').EndsWith("/duck-rails/Assets", StringComparison.OrdinalIgnoreCase) || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run in the Duck Rails editor outside Play Mode.");
        AssetDatabase.Refresh();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) prefab = BuildPrefab();
        else if (prefab.transform.Find("Panel housing") == null) prefab = UpgradePrefab();
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
                // North partition of Station Info, facing the Discord station.
                station.transform.SetPositionAndRotation(new Vector3(-6.6f, 0, 2.5f), Quaternion.identity);
                PrefabUtility.RecordPrefabInstancePropertyModifications(station.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Validate(world);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    static GameObject UpgradePrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            AuthorPanel(root.GetComponent<RailRateGame>());
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static GameObject BuildPrefab()
    {
        var root = new GameObject("RateGameStation");
        try
        {
            var action = root.AddComponent<RailRateGame>();
            var canvas = new GameObject("Instructions Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(root.transform, false);
            canvas.renderMode = RenderMode.WorldSpace;
            var firstImage = new GameObject(WalkthroughSteps[0]).AddComponent<RawImage>();
            firstImage.transform.SetParent(canvas.transform, false);

            var buttonObject = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/PressableButton.prefab"), root.transform);
            buttonObject.name = "RATE GAME";
            var button = buttonObject.GetComponent<PressableButton>();
            UnityEventTools.AddPersistentListener(button.onPressed, action.OpenRatingPage);
            action.status = Label(root.transform, "Status");
            AuthorPanel(action);
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static Material PanelMaterial(string name) =>
        AssetDatabase.LoadAssetAtPath<Material>($"{Folder}/Rating{name}.mat");

    static void AuthorPanel(RailRateGame action)
    {
        var shell = PanelMaterial("Housing");
        var face = PanelMaterial("Face");
        var accent = PanelMaterial("Trim");
        var buttonMaterial = PanelMaterial("Button");
        var panel = new GameObject("Panel housing").transform;
        panel.SetParent(action.transform, false);
        Plate(panel, "Wall mount", new Vector3(0, .67f, .022f), new Vector3(2.14f, 1.04f, .12f), shell);
        Plate(panel, "Inset border", new Vector3(0, .67f, -.041f), new Vector3(2.10f, 1.00f, .008f), accent);
        Plate(panel, "Face plate", new Vector3(0, .67f, -.045f), new Vector3(2.07f, .97f, .01f), face);
        Plate(panel, "Header rule", new Vector3(0, 1.022f, -.051f), new Vector3(1.91f, .007f, .003f), accent);
        Plate(panel, "Button bezel", new Vector3(0, ButtonHeight, -.053f), new Vector3(.79f, .168f, .012f), shell);

        PanelLabel(panel, "RATE DUCK RAILS", 0, 1.09f, 1.91f, 1.3f, buttonMaterial.GetColor("_BaseColor"));
        var appeal = Label(panel, "Rating appeal");
        SetLabel(appeal, RatingAppeal, new Vector3(0, .97f, -.056f),
            new Vector2(1.91f, .1f), .78f, TextAlignmentOptions.Midline, BodyColor);

        var canvas = action.GetComponentInChildren<Canvas>().GetComponent<RectTransform>();
        canvas.localPosition = new Vector3(0, .752f, -.059f);
        canvas.localScale = Vector3.one * .001f;
        canvas.sizeDelta = new Vector2(2000, 310);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(InstructionsPath);
        var firstImage = canvas.GetComponentInChildren<RawImage>();
        for (int i = 0; i < WalkthroughRows.Length; i++)
        {
            float x = (i - 1) * .656f;
            var image = i == 0 ? firstImage : new GameObject(WalkthroughSteps[i]).AddComponent<RawImage>();
            image.name = WalkthroughSteps[i];
            var rect = image.rectTransform;
            rect.SetParent(canvas, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(620, 310);
            rect.anchoredPosition3D = new Vector3(x * 1000, 0, 0);
            image.texture = texture;
            image.raycastTarget = false;
            // Each strip is 992 x 496 in the 1024 x 2048 source, measured from the top-left.
            image.uvRect = new Rect(16f / 1024, 1f - (WalkthroughRows[i] + 496f) / 2048, 992f / 1024, 496f / 2048);

            Plate(panel, "Walkthrough frame " + (i + 1), new Vector3(x, .752f, -.052f),
                new Vector3(.628f, .318f, .004f), accent);
            PanelLabel(panel, WalkthroughSteps[i], x, .548f, .636f, .68f, BodyColor);
        }

        var button = action.GetComponentInChildren<PressableButton>();
        button.transform.localPosition = new Vector3(0, ButtonHeight, -.083f);
        button.pressOffset = new Vector3(0, 0, .019f);
        button.GetComponent<BoxCollider>().size = new Vector3(.74f, .134f, .07f);
        var cap = button.cap.Find("Cap");
        cap.localScale = new Vector3(.74f, .134f, .047f);
        cap.GetComponent<MeshRenderer>().sharedMaterial = buttonMaterial;
        SetLabel(button.GetComponentInChildren<TMP_Text>(), "RATE GAME", new Vector3(0, 0, -.026f),
            new Vector2(.69f, .123f), 1.04f, TextAlignmentOptions.Midline, ButtonLabelColor);
        foreach (var component in button.GetComponentsInChildren<Component>())
            if (component != null) PrefabUtility.RecordPrefabInstancePropertyModifications(component);

        SetLabel(action.status, string.Empty,
            new Vector3(0, .24f, -.055f), new Vector2(1.96f, .09f), .72f, TextAlignmentOptions.Midline, BodyColor);
        action.status.enableAutoSizing = true;
        action.status.fontSizeMin = .5f;
        action.status.fontSizeMax = .72f;
    }

    static void PanelLabel(Transform parent, string text, float x, float y, float width, float fontSize, Color color)
    {
        SetLabel(Label(parent, text), text, new Vector3(x, y, -.056f),
            new Vector2(width, .1f), fontSize, TextAlignmentOptions.Midline, color);
    }

    static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");

    static Transform Plate(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go.transform;
    }

    static TMP_Text Label(Transform parent, string name)
    {
        var label = new GameObject(name).AddComponent<TextMeshPro>();
        label.transform.SetParent(parent, false);
        return label;
    }

    static void SetLabel(TMP_Text label, string text, Vector3 position, Vector2 size, float fontSize,
        TextAlignmentOptions alignment, Color color)
    {
        label.font = Font;
        label.text = text;
        label.color = color;
        label.fontSize = fontSize;
        label.enableAutoSizing = false;
        label.alignment = alignment;
        label.rectTransform.sizeDelta = size;
        label.transform.localPosition = position;
    }

    static void Validate(RailWorld world)
    {
        var actions = world.compound.GetComponentsInChildren<RailRateGame>(true);
        if (actions.Length != 1) throw new InvalidOperationException("Expected one rating station.");
        var action = actions[0];
        var button = action.GetComponentInChildren<PressableButton>();
        if (button == null || button.onPressed.GetPersistentEventCount() != 1 || button.onPressed.GetPersistentTarget(0) != action ||
            button.onPressed.GetPersistentMethodName(0) != nameof(RailRateGame.OpenRatingPage))
            throw new InvalidOperationException("Rating button callback is not bound.");

        var cards = action.GetComponentsInChildren<RawImage>();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(InstructionsPath);
        var labels = action.GetComponentsInChildren<TMP_Text>();
        if (cards.Length != WalkthroughSteps.Length || texture == null || texture.width != 1024 || texture.height != 2048 ||
            cards.Any(card => card.texture != texture || card.raycastTarget) || action.status == null ||
            action.GetComponentInChildren<Canvas>().renderMode != RenderMode.WorldSpace ||
            labels.Any(label => label.font != Font) || !labels.Any(label => label.text == RatingAppeal))
            throw new InvalidOperationException("Rating walkthrough, canvas or font references are invalid.");

        var labelColor = button.GetComponentInChildren<TMP_Text>().color;
        var capMaterial = button.cap.Find("Cap").GetComponent<MeshRenderer>().sharedMaterial;
        if (!Mathf.Approximately(button.transform.position.y, ButtonHeight) ||
            capMaterial == null || capMaterial != PanelMaterial("Button") || labelColor != ButtonLabelColor)
            throw new InvalidOperationException("Rating button height or gold styling is invalid.");

        Debug.Log("[Rate Game] PASS: physical button at 0.38 m, callback, gold styling, three walkthrough cards and CutePixel labels.");
    }
}
