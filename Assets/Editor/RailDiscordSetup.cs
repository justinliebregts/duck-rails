using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Creates the editable compound "Join the Discord!" station without regenerating compound art.</summary>
public static class RailDiscordSetup
{
    public const string Folder = "Assets/DuckRails/Compound/Discord";
    public const string PrefabPath = Folder + "/DiscordStation.prefab";
    public const string HeadingText = "DUCK RAILS DISCORD";
    const float ButtonHeight = .38f;
    // South partition of Station Info, facing the rating station.
    static readonly Vector3 Position = new Vector3(-7.3f, 0, -2.5f);
    static readonly Quaternion Rotation = Quaternion.Euler(0, 180, 0);

    [MenuItem("Duck Rails/Compound/Install Discord Station")]
    public static void Install()
    {
        if (!Application.dataPath.Replace('\\', '/').EndsWith("/duck-rails/Assets", StringComparison.OrdinalIgnoreCase) || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run in the Duck Rails editor outside Play Mode.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) prefab = BuildPrefab();
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Compound.unity");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Compound.unity", OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty) throw new InvalidOperationException("Compound has unsaved edits; save those before installing.");
            var world = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<RailWorld>(true)).Single();
            if (world.compound.GetComponentInChildren<RailDiscordLink>(true) == null)
            {
                var station = (GameObject)PrefabUtility.InstantiatePrefab(prefab, world.compound.transform);
                Undo.RegisterCreatedObjectUndo(station, "Install Discord station");
                station.transform.SetPositionAndRotation(Position, Rotation);
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
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/DuckRails/Compound", "Discord");
        var root = new GameObject("DiscordStation");
        try
        {
            var action = root.AddComponent<RailDiscordLink>();
            var buttonObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/PressableButton.prefab"), root.transform);
            buttonObject.name = "JOIN DISCORD";
            var button = buttonObject.GetComponent<PressableButton>();
            UnityEventTools.AddPersistentListener(button.onPressed, action.OpenInvite);
            AuthorPanel(root);
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");

    static void AuthorPanel(GameObject root)
    {
        var shell = PanelMaterial("Housing", new Color32(45, 50, 69, 255));
        var face = PanelMaterial("Face", new Color32(20, 24, 43, 255));
        var accent = PanelMaterial("Trim", new Color32(185, 195, 242, 255));
        var blurple = PanelMaterial("Button", new Color32(65, 76, 203, 255));
        var parent = root.transform;
        var panel = new GameObject("Panel housing").transform;
        panel.SetParent(parent, false);
        Plate(panel, "Wall mount", new Vector3(0, .675f, .022f), new Vector3(1.66f, .91f, .12f), shell);
        Plate(panel, "Inset border", new Vector3(0, .675f, -.041f), new Vector3(1.62f, .87f, .008f), accent);
        Plate(panel, "Face plate", new Vector3(0, .675f, -.045f), new Vector3(1.59f, .84f, .01f), face);
        Plate(panel, "Header rule", new Vector3(0, .907f, -.051f), new Vector3(1.43f, .007f, .003f), accent);
        Plate(panel, "Logo badge", new Vector3(-.604f, 1.013f, -.052f), new Vector3(.222f, .152f, .006f), accent);

        var logo = new GameObject("Discord logo").AddComponent<SpriteRenderer>();
        logo.transform.SetParent(panel, false);
        logo.transform.localPosition = new Vector3(-.604f, 1.013f, -.057f);
        logo.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/DiscordClyde.png");
        logo.transform.localScale = Vector3.one * (.149f / logo.sprite.bounds.size.x);

        var heading = parent.Find("Heading");
        var title = heading == null ? Label(parent, "Heading") : heading.GetComponent<TMP_Text>();
        SetLabel(title, HeadingText, new Vector3(.115f, 1.013f, -.055f), new Vector2(1.1f, .164f),
            1.3f, TextAlignmentOptions.MidlineLeft, Color.white);
        string[] benefits = { "Find crewmates", "Get game updates", "Share ideas and feedback", "Giveaways" };
        for (int i = 0; i < benefits.Length; i++)
        {
            float y = .831f - i * .099f;
            var marker = Plate(panel, "Bullet " + (i + 1), new Vector3(-.683f, y, -.052f),
                new Vector3(.013f, .013f, .004f), accent);
            marker.localRotation = Quaternion.Euler(0, 0, 45);
            SetLabel(Label(panel, benefits[i]), benefits[i], new Vector3(.043f, y, -.055f), new Vector2(1.28f, .122f),
                1f, TextAlignmentOptions.MidlineLeft, new Color32(224, 227, 255, 255));
        }

        Plate(panel, "Button bezel", new Vector3(0, ButtonHeight, -.053f), new Vector3(.79f, .168f, .012f), shell);
        var button = root.GetComponentInChildren<PressableButton>();
        button.transform.localPosition = new Vector3(0, ButtonHeight, -.083f);
        button.pressOffset = new Vector3(0, 0, .019f);
        button.GetComponent<BoxCollider>().size = new Vector3(.74f, .134f, .07f);
        var cap = button.cap.Find("Cap");
        cap.localScale = new Vector3(.74f, .134f, .047f);
        cap.GetComponent<MeshRenderer>().sharedMaterial = blurple;
        SetLabel(button.GetComponentInChildren<TMP_Text>(), "JOIN DISCORD", new Vector3(0, 0, -.026f),
            new Vector2(.69f, .123f), 1.04f, TextAlignmentOptions.Midline, Color.white);
        foreach (var component in button.GetComponentsInChildren<Component>())
            if (component != null) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
    }

    static Material PanelMaterial(string name, Color color)
    {
        var path = $"{Folder}/Discord{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

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
        var actions = world.compound.GetComponentsInChildren<RailDiscordLink>(true);
        if (actions.Length != 1) throw new InvalidOperationException("Expected one Discord station.");
        var action = actions[0]; var button = action.GetComponentInChildren<PressableButton>();
        if (button == null || button.onPressed.GetPersistentEventCount() != 1 || button.onPressed.GetPersistentTarget(0) != action ||
            button.onPressed.GetPersistentMethodName(0) != nameof(RailDiscordLink.OpenInvite))
            throw new InvalidOperationException("Discord button callback is not bound.");
        if (action.inviteUrl != RailDiscordLink.DefaultInvite)
            throw new InvalidOperationException("Discord invite URL differs from the default: " + action.inviteUrl);
        var labels = action.GetComponentsInChildren<TMP_Text>(true);
        if (!labels.Any(l => l.text == HeadingText) || labels.Any(l => l.font != Font))
            throw new InvalidOperationException("Discord heading/font references are invalid.");
        var logo = action.GetComponentInChildren<SpriteRenderer>();
        if (logo == null || logo.sprite == null || !Mathf.Approximately(button.transform.localPosition.y, ButtonHeight))
            throw new InvalidOperationException("Discord logo or button height is invalid.");
        Debug.Log("[Discord] PASS: one saved station, button callback and height, invite URL, logo, CutePixel labels.");
    }
}
