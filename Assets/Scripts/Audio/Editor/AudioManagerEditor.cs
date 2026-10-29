using UnityEditor;

namespace Game.Audio.Editor
{
    [CustomEditor(typeof(AudioManager))]
    public sealed class AudioManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var manager = (AudioManager)target;
            EditorGUILayout.HelpBox("Pool size is read once at startup. Lower priority numbers are more important. Gameplay loops are protected; budgeted ambience can be reclaimed by gameplay.", MessageType.Info);
            if (!EditorApplication.isPlaying) return;
            EditorGUILayout.LabelField("Active / capacity", $"{manager.ActiveCount} / {manager.Capacity}");
            EditorGUILayout.LabelField("Ambience / limit", $"{manager.ActiveAmbienceCount} / {manager.AmbienceCapacity}");
            EditorGUILayout.LabelField("Peak usage", manager.PeakUsage.ToString());
            EditorGUILayout.LabelField("Skipped requests", manager.SkippedRequests.ToString());
            EditorGUILayout.LabelField("Stolen sounds", manager.StolenSounds.ToString());
        }
        public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;
    }
}
