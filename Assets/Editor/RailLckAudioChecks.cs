using System;
using System.Reflection;
using System.Runtime.Serialization;
using Liv.Lck;
using Liv.Lck.Collections;
using UnityEditor;
using UnityEngine;

public static class RailLckAudioChecks
{
    [MenuItem("Duck Rails/Tests/LIV Audio Listener Rebinding")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
        var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        var enabled = Array.ConvertAll(listeners, l => l.enabled);
        GameObject first = null, next = null;
        try
        {
            foreach (var listener in listeners) listener.enabled = false;
            var type = typeof(LckService).Assembly.GetType("Liv.Lck.LckAudioMixer", true);
            // Exercise the production binding logic without starting native recording/microphone services.
            var mixer = FormatterServices.GetUninitializedObject(type);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            type.GetField("_gameAudioBuffer", flags).SetValue(mixer, new AudioBuffer(96000));
            type.GetField("_captureEnabled", flags).SetValue(mixer, true);
            bool Bind() => (bool)type.GetMethod("VerifyAudioCaptureComponent", flags).Invoke(mixer, null);
            ILckAudioSource Source() => (ILckAudioSource)type.GetField("_gameAudioSource", flags).GetValue(mixer);
            int checks = 0;
            void Check(bool pass, string name) { if (!pass) throw new Exception(name); checks++; }

            first = new GameObject("LIV audio check old listener");
            first.AddComponent<AudioListener>();
            Check(Bind() && Source().IsCapturing(), "Initial listener captures");
            var old = Source();
            UnityEngine.Object.DestroyImmediate(first);
            Check(old != null && (UnityEngine.Object)old == null, "Destroyed interface retains managed reference");
            Check(!Bind() && Source() == null, "Missing listener is safe and clears stale capture");
            next = new GameObject("LIV audio check replacement listener");
            var replacement = next.AddComponent<AudioListener>();
            Check(Bind() && Source().IsCapturing(), "Replacement resumes capture");
            Check(((Component)Source()).gameObject == next, "Capture follows replacement listener");
            var current = Source();
            Check(Bind() && ReferenceEquals(current, Source()), "Stable listener reuses capture");
            replacement.enabled = false;
            Check(!Bind() && !current.IsCapturing(), "Disabled listener stops stale capture");
            replacement.enabled = true;
            Check(Bind() && ReferenceEquals(current, Source()) && current.IsCapturing(), "Reenabled listener resumes");
            type.GetField("_captureEnabled", flags).SetValue(mixer, false);
            UnityEngine.Object.DestroyImmediate(next);
            next = new GameObject("LIV audio check idle replacement");
            next.AddComponent<AudioListener>();
            Check(Bind() && !Source().IsCapturing(), "Idle replacement stays idle");
            Debug.Log($"LIV audio listener checks passed: {checks}. No recording or Play Mode started.");
        }
        finally
        {
            if (first != null) UnityEngine.Object.DestroyImmediate(first);
            if (next != null) UnityEngine.Object.DestroyImmediate(next);
            for (int i = 0; i < listeners.Length; i++)
                if (listeners[i] != null) listeners[i].enabled = enabled[i];
        }
    }
}
