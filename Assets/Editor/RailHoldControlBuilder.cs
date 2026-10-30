using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

public static class RailHoldControlBuilder
{
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
        if (control.action != RailControl.Action.Radio && control.action != RailControl.Action.Horn) return;
        if (control.GetComponent<RailHoldControl>() != null) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/PressableButton.prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, control.transform);
        go.name = "Hold button";
        go.transform.localPosition = new Vector3(0, .22f, 0);
        go.transform.localRotation = Quaternion.Euler(90, 0, 0);
        go.transform.localScale = Vector3.one * 1.5f;
        var hold = control.gameObject.AddComponent<RailHoldControl>();
        hold.button = go.GetComponent<PressableButton>(); hold.radio = control.action == RailControl.Action.Radio;
        var label = go.GetComponentInChildren<TMP_Text>(); label.text = hold.radio ? "HOLD\nTALK" : "HOLD\nHORN"; label.fontSize = .65f;
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(label);
    }
}
