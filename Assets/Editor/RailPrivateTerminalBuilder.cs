using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RailPrivateTerminalBuilder
{
    const string PrefabPath = "Assets/DuckRails/PressableButton.prefab";
    [MenuItem("Duck Rails/Build Private Keypad")]
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
        var old = world.compound.transform.Find("Private keypad");
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var instructions = world.compound.transform.Find("Private instructions");
        if (instructions != null) instructions.GetComponent<TMP_Text>().text = "PRIVATE EXPEDITIONS\nTouch keys in VR • Click keys on desktop";
        // Keep the existing terminal shell; put the face just in front of its south side.
        var root = new GameObject("Private keypad");
        root.transform.SetParent(world.compound.transform, false);
        root.transform.position = new Vector3(-9, 0, -.56f);
        var terminal = root.AddComponent<RailPrivateTerminal>();
        terminal.display = Label(root.transform, "Display", "ROOM -----\n_____\nChoose five digits", new Vector3(0, 1.85f, -.02f), new Vector2(2.3f, .48f), 1.05f);
        var template = MakeButton();
        var prefab = PrefabUtility.SaveAsPrefabAsset(template.gameObject, PrefabPath);
        UnityEngine.Object.DestroyImmediate(template.gameObject);
        var edits = new List<PressableButton>();
        for (int i = 0; i < 9; i++)
        {
            string digit = (i + 1).ToString();
            var button = Key(prefab, root.transform, digit, -.63f + (i % 3) * .27f, 1.48f - (i / 3) * .25f, .23f);
            UnityEventTools.AddStringPersistentListener(button.onPressed, terminal.Digit, digit);
            edits.Add(button);
        }
        var zero = Key(prefab, root.transform, "0", -.36f, .73f, .23f);
        UnityEventTools.AddStringPersistentListener(zero.onPressed, terminal.Digit, "0"); edits.Add(zero);
        var back = Key(prefab, root.transform, "DEL", -.09f, .73f, .23f);
        UnityEventTools.AddPersistentListener(back.onPressed, terminal.Backspace); edits.Add(back);
        var clear = Key(prefab, root.transform, "CLR", -.63f, .73f, .23f);
        UnityEventTools.AddPersistentListener(clear.onPressed, terminal.Clear); edits.Add(clear);
        terminal.editButtons = edits.ToArray();
        terminal.create = Key(prefab, root.transform, "CREATE", .61f, 1.48f, .68f);
        terminal.join = Key(prefab, root.transform, "JOIN", .61f, 1.23f, .68f);
        terminal.start = Key(prefab, root.transform, "START", .61f, .98f, .68f);
        terminal.leave = Key(prefab, root.transform, "LEAVE", .61f, .73f, .68f);
        UnityEventTools.AddPersistentListener(terminal.create.onPressed, terminal.Create);
        UnityEventTools.AddPersistentListener(terminal.join.onPressed, terminal.Join);
        UnityEventTools.AddPersistentListener(terminal.start.onPressed, terminal.StartRun);
        UnityEventTools.AddPersistentListener(terminal.leave.onPressed, terminal.Leave);
        foreach (var button in root.GetComponentsInChildren<PressableButton>()) PrefabUtility.RecordPrefabInstancePropertyModifications(button);
    }
    static PressableButton MakeButton()
    {
        var go = new GameObject("PressableButton");
        var volume = go.AddComponent<BoxCollider>(); volume.size = new Vector3(.23f, .2f, .07f); volume.isTrigger = true;
        var button = go.AddComponent<PressableButton>();
        var cap = GameObject.CreatePrimitive(PrimitiveType.Cube); cap.name = "Cap"; cap.transform.SetParent(go.transform, false);
        cap.transform.localScale = new Vector3(.23f, .2f, .05f);
        UnityEngine.Object.DestroyImmediate(cap.GetComponent<Collider>());
        cap.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/Materials/Safety.mat");
        button.cap = cap.transform;
        // Label is a sibling under the animated visual so scale doesn't squash the font.
        var visual = new GameObject("Visual").transform; visual.SetParent(go.transform, false); cap.transform.SetParent(visual, false); button.cap = visual;
        Label(visual, "Label", "PRESS", new Vector3(0, 0, -.027f), new Vector2(.22f, .18f), .85f);
        button.audioSource = go.AddComponent<AudioSource>();
        button.audioSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Menu/PauseMenu_ButtonPush.ogg");
        button.audioSource.playOnAwake = false; button.audioSource.spatialBlend = 1; button.audioSource.volume = .35f;
        button.audioSource.minDistance = .3f; button.audioSource.maxDistance = 5;
        return button;
    }
    static PressableButton Key(GameObject prefab, Transform parent, string name, float x, float y, float width)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name; go.transform.localPosition = new Vector3(x, y, 0);
        var button = go.GetComponent<PressableButton>();
        go.GetComponent<BoxCollider>().size = new Vector3(width, .2f, .07f);
        button.cap.Find("Cap").localScale = new Vector3(width, .2f, .05f);
        var label = go.GetComponentInChildren<TMP_Text>(); label.text = name; label.rectTransform.sizeDelta = new Vector2(width - .02f, .18f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<BoxCollider>());
        PrefabUtility.RecordPrefabInstancePropertyModifications(button.cap.Find("Cap"));
        PrefabUtility.RecordPrefabInstancePropertyModifications(label);
        PrefabUtility.RecordPrefabInstancePropertyModifications(label.rectTransform);
        return button;
    }
    static TMP_Text Label(Transform parent, string name, string text, Vector3 pos, Vector2 size, float fontSize)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos;
        var label = go.AddComponent<TextMeshPro>(); label.text = text; label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = size;
        label.color = name == "Label" ? Color.black : Color.white;
        return label;
    }
}
