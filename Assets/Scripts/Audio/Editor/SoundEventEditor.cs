using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Game.Audio.Editor
{
    [CustomEditor(typeof(SoundEvent))]
    public sealed class SoundEventEditor : UnityEditor.Editor
    {
        AudioClip lastClip;
        string lastSelection;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Preview uses randomized clip, pitch and volume. Distance, spatializer, mixer effects and reverb are bypassed. Custom rolloff is not supported in this version.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Preview"))
                    {
                        if (((SoundEvent)target).TrySelect(lastClip, out var selection))
                        {
                            lastClip = selection.Clip;
                            lastSelection = $"{selection.Clip.name}  |  Length {FormatLength(selection.Clip.length)}  |  Pitch {selection.Pitch:0.000}  |  Volume {selection.Volume:0.00}";
                            SoundPreview.Play(selection);
                        }
                        else lastSelection = "Assign at least one clip first.";
                    }
                    if (GUILayout.Button("Stop")) SoundPreview.Stop();
                }
            }
            if (!string.IsNullOrEmpty(lastSelection)) EditorGUILayout.HelpBox(lastSelection, MessageType.None);
        }
        static string FormatLength(float seconds)
        {
            if (seconds >= 60f)
            {
                int minutes = (int)(seconds / 60f);
                return $"{minutes}:{(seconds - minutes * 60f):00.00}";
            }
            return $"{seconds:0.00}s";
        }
        void OnDisable() => SoundPreview.Stop();
    }

    [InitializeOnLoad]
    internal static class SoundPreview
    {
        static readonly Type AudioUtil = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AudioUtil");
        static readonly MethodInfo UpdateAudio = AudioUtil?.GetMethod("UpdateAudio", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        static readonly MethodInfo SetListener = AudioUtil?.GetMethod("SetListenerTransform", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        static GameObject previewObject;
        static AudioSource source;
        static double deadline;
        static bool started;
        static readonly object[] listenerArgs = new object[1];

        static SoundPreview()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.playModeStateChanged += _ => Stop();
            EditorApplication.update += Tick;
        }
        internal static void Play(SoundSelection selection)
        {
            Stop();
            if (UpdateAudio == null || SetListener == null)
            {
                Debug.LogWarning("This Unity version does not expose the editor audio preview API.");
                return;
            }
            previewObject = EditorUtility.CreateGameObjectWithHideFlags("Sound Event Preview", HideFlags.HideAndDontSave, typeof(AudioSource));
            source = previewObject.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = selection.Clip;
            source.pitch = selection.Pitch;
            source.volume = selection.Volume;
            source.spatialBlend = 0f;
            source.spatialize = false;
            source.bypassEffects = source.bypassListenerEffects = source.bypassReverbZones = true;
            source.ignoreListenerPause = source.ignoreListenerVolume = true;
            deadline = EditorApplication.timeSinceStartup + 10;
            if (selection.Clip.loadState == AudioDataLoadState.Unloaded) selection.Clip.LoadAudioData();
            Tick();
        }
        static void Tick()
        {
            if (source == null) return;
            if (source.clip == null || source.clip.loadState == AudioDataLoadState.Failed
                || EditorApplication.timeSinceStartup > deadline) { Stop(); return; }
            listenerArgs[0] = previewObject.transform;
            SetListener.Invoke(null, listenerArgs);
            if (!started && source.clip.loadState == AudioDataLoadState.Loaded)
            {
                source.Play();
                started = true;
                deadline = EditorApplication.timeSinceStartup + source.clip.length / source.pitch + 0.25;
            }
            UpdateAudio.Invoke(null, null);
        }
        internal static void Stop()
        {
            if (source != null) source.Stop();
            if (previewObject != null) UnityEngine.Object.DestroyImmediate(previewObject);
            previewObject = null;
            source = null;
            started = false;
            // Release the editor listener reference before destroying/reloading assemblies.
            listenerArgs[0] = null;
            SetListener?.Invoke(null, listenerArgs);
        }
    }
}
