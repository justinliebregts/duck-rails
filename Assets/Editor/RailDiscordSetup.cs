using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Creates the editable compound "Join the Discord!" station without regenerating compound art.</summary>
public static class RailDiscordSetup
{
    public const string Folder = "Assets/DuckRails/Compound/Discord";
    public const string PrefabPath = Folder + "/DiscordStation.prefab";
    public const string HeadingText = "Join the Discord!";
    // Inner right partition of the information nook, facing the rating station; see docs/subway-compound.md.
    static readonly Vector3 Nook = new Vector3(-5.65f, 0, 3.4f);
    static readonly Quaternion NookRotation = Quaternion.Euler(0, -65, 0);
    static Quaternion Rotation => NookRotation * Quaternion.Euler(0, 90, 0);
    static Vector3 Position => Nook + NookRotation * new Vector3(2.53f, 0, -.35f) + Rotation * new Vector3(-.8f, 0, 0);

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
        var root = new GameObject("Discord Station");
        try
        {
            var action = root.AddComponent<RailDiscordLink>();
            Label(root.transform, "Heading", HeadingText, new Vector3(0, 1.62f, -.06f), new Vector2(1.6f, .3f));

            var buttonObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/PressableButton.prefab"), root.transform);
            buttonObject.name = "JOIN DISCORD";
            buttonObject.transform.localPosition = new Vector3(0, 1.3f, -.055f);
            var button = buttonObject.GetComponent<PressableButton>();
            button.GetComponent<BoxCollider>().size = new Vector3(.75f, .24f, .07f);
            button.cap.Find("Cap").localScale = new Vector3(.75f, .24f, .05f);
            var label = button.GetComponentInChildren<TMP_Text>();
            label.font = Font; label.text = "JOIN DISCORD"; label.fontSize = .85f;
            label.enableAutoSizing = true; label.fontSizeMin = .3f; label.fontSizeMax = .85f;
            label.rectTransform.sizeDelta = new Vector2(.71f, .2f);
            UnityEventTools.AddPersistentListener(button.onPressed, action.OpenInvite);
            foreach (var component in buttonObject.GetComponentsInChildren<Component>())
                if (component != null) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");

    static TMP_Text Label(Transform parent, string name, string text, Vector3 position, Vector2 size)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
        var label = go.AddComponent<TextMeshPro>(); label.font = Font; label.text = text; label.color = Color.white;
        label.fontSize = 1.4f; label.enableAutoSizing = true; label.fontSizeMin = .5f; label.fontSizeMax = 1.4f;
        label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = size;
        return label;
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
        Debug.Log("[Discord] PASS: one saved station, button callback, invite URL, CutePixel labels. Browser launch remains queued.");
    }
}
