using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

public static class RailHoldControlBuilder
{
    [MenuItem("Duck Rails/Authoring/Install Bridge Hold Button")]
    public static void InstallBridge()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
        try
        {
            foreach (var path in new[] { "Assets/Scenes/Expedition.unity", "Assets/DuckRails/Authoring/Week2Baseline.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                foreach (var control in UnityEngine.Object.FindObjectsByType<RailControl>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (control.action == RailControl.Action.Bridge) Install(control);
                foreach (var label in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (label.text.Contains("Press the outside button. Wait until fully lowered."))
                        label.text = label.text.Replace("Press the outside button. Wait until fully lowered.", "Hold the outside button to lower. Release pauses progress.");
                EditorSceneManager.SaveScene(scene);
            }
            const string prefabPath = "Assets/DuckRails/Blocks/bridge-finale-v1.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (var control in root.GetComponentsInChildren<RailControl>(true))
                    if (control.action == RailControl.Action.Bridge) Install(control);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }
    [MenuItem("Duck Rails/Build Radio and Horn Hold Buttons")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Compound.unity");
        foreach (var control in UnityEngine.Object.FindObjectsByType<RailControl>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Install(control);
        foreach (var label in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (label.text.Contains("Hold T / use button")) label.text = "RADIO\nHold the TALK button";
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.RestoreSceneManagerSetup(setup);
    }
    public static void Install(RailControl control)
    {
        if (control.action != RailControl.Action.Radio && control.action != RailControl.Action.Horn && control.action != RailControl.Action.Bridge) return;
        if (control.GetComponent<RailHoldControl>() != null) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/PressableButton.prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, control.transform);
        go.name = "Hold button";
        go.transform.localPosition = new Vector3(0, .22f, 0);
        go.transform.localRotation = Quaternion.Euler(90, 0, 0);
        go.transform.localScale = Vector3.one * 1.5f;
        var hold = control.gameObject.AddComponent<RailHoldControl>();
        hold.button = go.GetComponent<PressableButton>(); hold.radio = control.action == RailControl.Action.Radio;
        hold.bridge = control.action == RailControl.Action.Bridge;
        var label = go.GetComponentInChildren<TMP_Text>(); label.text = hold.bridge ? "HOLD\nLOWER" : hold.radio ? "HOLD\nTALK" : "HOLD\nHORN"; label.fontSize = .65f;
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(label);
    }
}
