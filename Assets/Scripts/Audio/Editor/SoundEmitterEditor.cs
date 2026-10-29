using UnityEditor;
using UnityEngine;

namespace Game.Audio.Editor
{
    [CustomEditor(typeof(SoundEmitter)), CanEditMultipleObjects]
    public sealed class SoundEmitterEditor : UnityEditor.Editor
    {
        static bool showDebug;
        static float fadeSeconds = 1f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            showDebug = EditorGUILayout.Foldout(showDebug, "Debug", true);
            if (!showDebug) return;
            using (new EditorGUI.IndentLevelScope())
            {
                if (!EditorApplication.isPlaying)
                {
                    EditorGUILayout.HelpBox("Enter Play mode to test. Playback goes through the AudioManager pool.", MessageType.Info);
                    return;
                }
                if (AudioManager.instance == null)
                {
                    EditorGUILayout.HelpBox("No AudioManager in the scene.", MessageType.Warning);
                    return;
                }
                if (targets.Length == 1)
                    EditorGUILayout.LabelField("Loop", ((SoundEmitter)target).IsLooping ? "Playing" : "Stopped");

                if (GUILayout.Button("Play Once")) ForEach(e => e.Play());
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Start Loop")) ForEach(e => e.StartLoop());
                    if (GUILayout.Button("Stop Loop")) ForEach(e => e.StopLoop());
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    fadeSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Fade Seconds", fadeSeconds));
                    if (GUILayout.Button("Fade Out Loop", GUILayout.Width(110))) ForEach(e => e.FadeOutLoop(fadeSeconds));
                }
            }
        }

        void ForEach(System.Action<SoundEmitter> action)
        {
            foreach (var t in targets) action((SoundEmitter)t);
        }

        public override bool RequiresConstantRepaint() => showDebug && EditorApplication.isPlaying;
    }
}
