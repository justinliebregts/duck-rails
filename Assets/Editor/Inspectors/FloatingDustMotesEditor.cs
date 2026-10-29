using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FloatingDustMotes))]
public sealed class FloatingDustMotesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var motes = (FloatingDustMotes)target;
        if (DrawDefaultInspector() && Application.isPlaying)
            motes.ApplySettings();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Play Mode Tuning", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Desktop: Scene toolbar → Desktop → Play, then select DesktopPlayer / Camera / FloatingDustMotes. " +
            "Inspector edits apply live. Play Mode changes are temporary — copy values or Apply Prefab Overrides onto FloatingDustMotes.prefab to keep them.",
            MessageType.Info);

        if (Application.isPlaying && !EditorUtility.IsPersistent(motes)
            && motes.gameObject.scene.IsValid())
        {
            EditorGUILayout.LabelField("Live Particles", motes.LiveParticleCount.ToString());
        }
    }

    public override bool RequiresConstantRepaint() => Application.isPlaying;
}
