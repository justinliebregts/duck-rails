using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NightVisionGoggles))]
public sealed class NightVisionGogglesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Apply Night Vision Settings"))
                ((NightVisionGoggles)target).ApplySettings();
        }
    }
}
