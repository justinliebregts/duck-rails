using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Compound departure-gate launch button for private staging, plus the wall hint that
/// replaced the old private keypad. Room create/join now lives in the pause menu.
/// </summary>
public static class RailLaunchButtonBuilder
{
    const string PrefabPath = "Assets/DuckRails/PressableButton.prefab";
    const string FontPath = "Assets/Fonts/CutePixel SDF.asset";
    public const string LaunchName = "Private launch", HintName = "Private room hint";
    public const string HintText = "Want to create a private room?\nDo so from the pause menu";
    static readonly string[] Retired = { "Private keypad", "Private terminal", "Private instructions" };

    [MenuItem("Duck Rails/Compound/Install Launch Button")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.isDirty) throw new InvalidOperationException("Save scene edits first.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var compound = EditorSceneManager.OpenScene("Assets/Scenes/Compound.unity");
        Install(UnityEngine.Object.FindAnyObjectByType<RailWorld>());
        EditorSceneManager.SaveScene(compound);
        EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    public static void Install(RailWorld world)
    {
        var parent = world.compound.transform;
        foreach (string name in Retired)
            for (var old = parent.Find(name); old != null; old = parent.Find(name))
                UnityEngine.Object.DestroyImmediate(old.gameObject);
        foreach (string name in new[] { LaunchName, HintName })
            for (var old = parent.Find(name); old != null; old = parent.Find(name))
                UnityEngine.Object.DestroyImmediate(old.gameObject);

        // Hint sits on the controls mounting wall where the keypad used to be.
        var hint = Label(parent, HintName, HintText, new Vector3(-4.9f, 1.0f, 8.37f), new Vector2(2.4f, .9f), 2f);
        hint.enableAutoSizing = true; hint.fontSizeMin = .5f; hint.fontSizeMax = 2f;
        hint.textWrappingMode = TextWrappingModes.Normal;

        // Launch control on the departure gate, below the countdown board text.
        var root = new GameObject(LaunchName);
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(0, 0, 8.62f);
        root.transform.rotation = Quaternion.identity;
        var launch = root.AddComponent<RailLaunchButton>();
        launch.prompt = Label(root.transform, "Launch prompt", "Owner, push to start!", new Vector3(0, .8f, 0), new Vector2(3.2f, .35f), 2.4f);
        launch.prompt.enableAutoSizing = true; launch.prompt.fontSizeMin = .5f; launch.prompt.fontSizeMax = 2.4f;
        launch.button = Key(root.transform, "Launch expedition", "LAUNCH\nEXPEDITION", 0, .46f, .9f, .32f);
        EditorUtility.SetDirty(launch);
        EditorUtility.SetDirty(world);
        EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
    }

    static PressableButton Key(Transform parent, string name, string text, float x, float y, float width, float height)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Missing " + PrefabPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name; go.transform.localPosition = new Vector3(x, y, 0);
        var button = go.GetComponent<PressableButton>();
        go.GetComponent<BoxCollider>().size = new Vector3(width, height, .07f);
        button.cap.Find("Cap").localScale = new Vector3(width, height, .05f);
        var label = go.GetComponentInChildren<TMP_Text>();
        label.text = text; label.rectTransform.sizeDelta = new Vector2(width - .04f, height - .04f);
        label.enableAutoSizing = true; label.fontSizeMin = .3f; label.fontSizeMax = 1.4f;
        SetFont(label);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<BoxCollider>());
        PrefabUtility.RecordPrefabInstancePropertyModifications(button.cap.Find("Cap"));
        PrefabUtility.RecordPrefabInstancePropertyModifications(label);
        PrefabUtility.RecordPrefabInstancePropertyModifications(label.rectTransform);
        return button;
    }

    static TMP_Text Label(Transform parent, string name, string text, Vector3 localPos, Vector2 size, float fontSize)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = localPos;
        var label = go.AddComponent<TextMeshPro>(); label.text = text; label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = size;
        label.color = Color.white;
        SetFont(label);
        return label;
    }

    static void SetFont(TMP_Text label)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) label.font = font;
    }
}
