using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Loads baked speech from the installed game. No synthesis, worker, model, or persistent PCM cache.</summary>
public static class DuckLocalSpeech
{
    public const string DefaultVoice = "am_puck";
    public const string Prefix = "Now, where were we?";
    public const string Fallback = "I'm here to help. Shall we look for gold together?";
    public const string Folder = "DuckSpeech/Clips";
    public const int SampleRate = 24000;
    [Serializable] public sealed class Entry
    {
        public string text, file, sha256;
        public int samples, bytes;
    }
    [Serializable] public sealed class Catalog
    {
        public string config, voice, dialogueSha256;
        public int sampleRate, channels, bitrate;
        public Entry[] clips;
    }
    static Dictionary<string, Entry> entries;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCatalog() => entries = null;

    public static Dictionary<string, Entry> ParseCatalog(string json)
    {
        var catalog = JsonUtility.FromJson<Catalog>(json);
        if (catalog == null || catalog.voice != DefaultVoice || catalog.sampleRate != SampleRate
            || catalog.channels != 1 || catalog.bitrate != 32000 || catalog.clips == null)
            throw new InvalidDataException("Duck voice catalog format does not match the baked voice.");
        var result = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var entry in catalog.clips)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.text) || entry.samples <= 0
                || entry.file == null || entry.file.Length != 68 || !entry.file.EndsWith(".ogg", StringComparison.Ordinal)
                || entry.file.Substring(0, 64).Trim("0123456789abcdef".ToCharArray()).Length != 0)
                throw new InvalidDataException("Invalid Duck voice clip entry.");
            result.Add(entry.text, entry);
        }
        return result;
    }

    // Exact lines take precedence. Split only into complete authored clips;
    // this covers helicopter waiting + ETA combinations and the resume prefix.
    public static bool Resolve(string text, Dictionary<string, Entry> catalog, List<Entry> parts)
    {
        if (string.IsNullOrEmpty(text) || text.Length > 600) return false;
        if (catalog.TryGetValue(text, out var exact)) { parts.Add(exact); return true; }
        for (int split = text.Length - 1; split > 0; split--)
        {
            if (text[split] != ' ' || !catalog.TryGetValue(text.Substring(0, split), out var first)) continue;
            int start = parts.Count;
            parts.Add(first);
            if (parts.Count < 4 && Resolve(text.Substring(split + 1), catalog, parts)) return true;
            parts.RemoveRange(start, parts.Count - start);
        }
        return false;
    }

    static string Url(string file)
    {
        string root = Application.streamingAssetsPath.Replace('\\', '/');
        string path = root + "/" + Folder + "/" + file;
        return root.Contains("://") ? path : new Uri(path).AbsoluteUri;
    }

    public static IEnumerator Load(string text, Action<AudioClip> complete, Func<bool> stillWanted)
    {
        if (!stillWanted()) yield break;
        if (entries == null)
        {
            using (var request = UnityWebRequest.Get(Url("manifest.json")))
            {
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (!stillWanted()) { request.Abort(); yield break; }
                    yield return null;
                }
                if (!stillWanted()) yield break;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    {
                        if (DuckzLog.AllowError(out var logNotice1))
                            logNotice1.Write("Duck voice catalog could not load: " + request.error);
                    }
                    complete(null); yield break;
                }
                try { entries = ParseCatalog(request.downloadHandler.text); }
                catch (Exception ex)
                {
                    if (DuckzLog.AllowError(out var logNotice2))
                        logNotice2.Write("Duck voice catalog: " + ex.Message);
                }
            }
        }
        var parts = new List<Entry>();
        if (entries == null || !Resolve(text, entries, parts))
        {
            {
                if (DuckzLog.AllowError(out var logNotice3, text))
                    logNotice3.Write("Missing baked Duck line. Regenerate voice clips: " + text);
            }
            complete(null); yield break;
        }
        var loaded = new List<AudioClip>();
        try
        {
            foreach (var part in parts)
            {
                if (!stillWanted()) yield break;
                using (var request = UnityWebRequestMultimedia.GetAudioClip(Url(part.file), AudioType.OGGVORBIS))
                {
                    var handler = (DownloadHandlerAudioClip)request.downloadHandler;
                    handler.compressed = false;
                    handler.streamAudio = false;
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        if (!stillWanted()) { request.Abort(); yield break; }
                        yield return null;
                    }
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        {
                            if (DuckzLog.AllowError(out var logNotice4, part.file))
                                logNotice4.Write("Duck voice clip could not load: " + request.error);
                        }
                        complete(null); yield break;
                    }
                    var clip = DownloadHandlerAudioClip.GetContent(request);
                    if (clip != null) loaded.Add(clip);
                    if (!stillWanted()) yield break;
                    if (clip == null || clip.channels != 1 || clip.frequency != SampleRate)
                    {
                        if (DuckzLog.AllowError(out var logNotice5, part.file))
                            logNotice5.Write("Invalid baked Duck audio format.");
                        complete(null); yield break;
                    }
                }
            }
            if (loaded.Count == 1)
            {
                var clip = loaded[0];
                loaded.Clear(); // Ownership transfers to the dialogue.
                complete(clip);
            }
            else
            {
                // Only decode the current composed reply. Prefix/ETA clips stay separate on disk.
                int length = 0;
                foreach (var clip in loaded) length += clip.samples;
                var samples = new float[length];
                int offset = 0;
                foreach (var clip in loaded)
                {
                    var segment = new float[clip.samples];
                    if (!clip.GetData(segment, 0)) { complete(null); yield break; }
                    Array.Copy(segment, 0, samples, offset, segment.Length);
                    offset += segment.Length;
                }
                var combined = AudioClip.Create("Duck composed reply", length, 1, SampleRate, false);
                if (!combined.SetData(samples, 0)) { UnityEngine.Object.Destroy(combined); complete(null); yield break; }
                complete(combined);
            }
        }
        finally
        {
            foreach (var clip in loaded) UnityEngine.Object.Destroy(clip);
        }
    }
}
