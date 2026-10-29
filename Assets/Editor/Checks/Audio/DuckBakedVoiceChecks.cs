using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Batch-mode smoke checks for actual decoding, composition and obsolete-request cancellation.</summary>
public static class DuckBakedVoiceChecks
{
    static readonly Stack<IEnumerator> running = new Stack<IEnumerator>();
    static double deadline;
    static int checks;
    [MenuItem("Tools/DuckZ/Validation/Audio/Test Baked Playback")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) Begin();
        else
        {
            SessionState.SetBool("DuckBakedVoiceChecks.Pending", true);
            EditorApplication.EnterPlaymode();
        }
    }
    [InitializeOnLoadMethod]
    static void ResumeAfterReload()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("DuckBakedVoiceChecks.Pending", false)) Begin();
        };
    }
    static void Begin()
    {
        SessionState.SetBool("DuckBakedVoiceChecks.Pending", false);
        running.Clear(); checks = 0;
        deadline = EditorApplication.timeSinceStartup + 120;
        running.Push(CheckPlayback());
        EditorApplication.update += Tick;
    }
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++; Debug.Log("Duck baked voice PASS: " + message);
    }
    static IEnumerator CheckPlayback()
    {
        string json = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, DuckLocalSpeech.Folder, "manifest.json"));
        var catalog = DuckLocalSpeech.ParseCatalog(json);
        var entry = catalog.Values.First();
        AudioClip first = null;
        yield return DuckLocalSpeech.Load(entry.text, clip => first = clip, () => true);
        Check(first != null && first.frequency == 24000 && first.channels == 1, "real Ogg decodes at 24 kHz mono");
        Check(first.loadType == AudioClipLoadType.DecompressOnLoad, "playback uses Decompress On Load rather than streaming or compressed mixing");
        Check(Math.Abs(first.samples - entry.samples) < 256, "decoded duration agrees with generated manifest");
        var data = new float[first.samples];
        Check(first.GetData(data, 0) && data.Any(v => Math.Abs(v) > .001f), "decoded clip contains audible samples");
        UnityEngine.Object.DestroyImmediate(first);

        var pieces = new List<DuckLocalSpeech.Entry>();
        Check(!DuckLocalSpeech.Resolve("This is deliberately not a baked line", catalog, pieces), "unknown text cannot fall back to inference");
        Check(pieces.Count == 0, "failed resolution leaves no partial clips");
        var prefix = catalog.ContainsKey(DuckLocalSpeech.Prefix) ? catalog[DuckLocalSpeech.Prefix] : catalog.Values.Skip(1).First();
        string combinedText = prefix.text + " " + entry.text;
        Check(DuckLocalSpeech.Resolve(combinedText, catalog, pieces) && pieces.Count == 2, "separate clips resolve as a composed reply");
        AudioClip combined = null;
        yield return DuckLocalSpeech.Load(combinedText, clip => combined = clip, () => true);
        Check(combined != null && Math.Abs(combined.samples - entry.samples - prefix.samples) < 512, "composed playback retains both complete clips");
        UnityEngine.Object.DestroyImmediate(combined);

        bool called = false;
        yield return DuckLocalSpeech.Load(entry.text, clip => { called = true; if (clip) UnityEngine.Object.DestroyImmediate(clip); }, () => false);
        Check(!called, "obsolete request does no work");
        int polls = 0;
        yield return DuckLocalSpeech.Load(entry.text, clip => { called = true; if (clip) UnityEngine.Object.DestroyImmediate(clip); }, () => ++polls < 3);
        Check(!called, "request cancelled during loading cannot publish a clip");
        int after = Resources.FindObjectsOfTypeAll<AudioClip>().Length;
        for (int i = 0; i < 4; i++)
        {
            AudioClip clip = null;
            yield return DuckLocalSpeech.Load(entry.text, value => clip = value, () => true);
            Check(clip != null, "repeat load succeeds");
            UnityEngine.Object.DestroyImmediate(clip);
        }
        // Destroy() in loader finally blocks is deferred until a player/editor update.
        yield return null; yield return null;
        Check(Resources.FindObjectsOfTypeAll<AudioClip>().Length <= after, "repeated loads do not retain a growing clip cache");
    }
    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Duck baked voice playback check timed out");
            if (running.Count == 0)
            {
                EditorApplication.update -= Tick;
                Debug.Log($"DuckBakedVoiceChecks: ALL {checks} PASSED");
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }
            var current = running.Peek();
            if (!current.MoveNext()) { (current as IDisposable)?.Dispose(); running.Pop(); }
            else if (current.Current is IEnumerator nested) running.Push(nested);
        }
        catch (Exception ex)
        {
            EditorApplication.update -= Tick;
            foreach (var iterator in running) (iterator as IDisposable)?.Dispose();
            running.Clear(); Debug.LogException(ex);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
