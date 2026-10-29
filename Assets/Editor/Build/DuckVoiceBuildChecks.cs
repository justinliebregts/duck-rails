using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Every build must have complete, intact baked speech. Never silently ship missing lines.</summary>
public sealed class DuckVoiceBuildChecks : IPreprocessBuildWithReport
{
    public int callbackOrder => -10;
    public void OnPreprocessBuild(BuildReport report) => Validate();

    [MenuItem("Tools/DuckZ/Validation/Audio/Validate Baked Dialogue")]
    public static void Validate()
    {
        string root = Path.Combine(Application.streamingAssetsPath, DuckLocalSpeech.Folder);
        string manifest = Path.Combine(root, "manifest.json");
        if (!File.Exists(manifest)) Fail("No complete voice manifest exists.");
        var catalog = DuckLocalSpeech.ParseCatalog(File.ReadAllText(manifest));
        var bank = JsonUtility.FromJson<DuckCompanionDialogue.Bank>(File.ReadAllText("Assets/Resources/DuckDialogue.json"));
        var required = new HashSet<string>(bank.groups.SelectMany(g => g.lines), StringComparer.Ordinal)
        { DuckLocalSpeech.Prefix, DuckLocalSpeech.Fallback, "Ouch!" };
        if (!required.SetEquals(catalog.Keys)) Fail("Dialogue changed since the last voice bake.");
        foreach (var entry in catalog.Values)
        {
            using (var sha = SHA256.Create())
            {
                string id = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(entry.text))).Replace("-", "").ToLowerInvariant();
                if (entry.file != id + ".ogg") Fail("Clip is mapped to the wrong text: " + entry.text);
            }
            string path = Path.Combine(root, entry.file);
            if (!File.Exists(path) || new FileInfo(path).Length != entry.bytes) Fail("Missing or truncated clip: " + entry.text);
            using (var sha = SHA256.Create())
            using (var file = File.OpenRead(path))
            {
                string hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
                if (hash != entry.sha256) Fail("Clip checksum mismatch: " + entry.text);
            }
        }
        foreach (string line in required)
            Resolve(DuckLocalSpeech.Prefix + " " + line, catalog);
        var waiting = bank.groups.First(g => g.intent == "helicopter_waiting").lines;
        var timings = bank.groups.Where(g => g.intent.StartsWith("helicopter_eta_", StringComparison.Ordinal)).SelectMany(g => g.lines);
        foreach (string start in waiting)
            foreach (string end in timings)
            {
                Resolve(start + " " + end, catalog);
                Resolve(DuckLocalSpeech.Prefix + " " + start + " " + end, catalog);
            }
        if (Directory.Exists(Path.Combine(Application.streamingAssetsPath, "DuckSpeech/Kokoro")))
            throw new BuildFailedException("Development-only Kokoro model must not be in StreamingAssets. "
                + "Move it to Tools/DuckVoiceSource, then run Tools/DuckZ/Validation/Audio/Validate Baked Dialogue. No voice bake is needed.");
        foreach (string path in OnnxPluginPaths())
        {
            var plugin = AssetImporter.GetAtPath(path) as PluginImporter;
            if (plugin == null) continue;
            var enabled = new List<string>();
            if (plugin.GetCompatibleWithAnyPlatform()) enabled.Add("Any Platform");
            if (plugin.GetCompatibleWithPlatform(BuildTarget.Android)) enabled.Add("Android");
            if (plugin.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64)) enabled.Add("Windows x86_64");
            if (enabled.Count > 0)
                throw new BuildFailedException($"ONNX Runtime is enabled in a player build: {path}. Enabled compatibility: {string.Join(", ", enabled)}. "
                    + "Run Tools/DuckZ/Maintenance/Repair ONNX Plugin Compatibility, then validate again. No voice bake is needed.");
        }
        Debug.Log($"Duck baked voice: {catalog.Count} clips; all dialogue and composed replies covered; no runtime TTS assets.");
    }

    static void Resolve(string text, Dictionary<string, DuckLocalSpeech.Entry> catalog)
    {
        var parts = new List<DuckLocalSpeech.Entry>();
        if (!DuckLocalSpeech.Resolve(text, catalog, parts) || string.Join(" ", parts.Select(p => p.text)) != text)
            Fail("Uncovered spoken combination: " + text);
    }

    static void Fail(string reason) => throw new BuildFailedException(reason
        + " Run Tools/generate_duck_voice.ps1, then validate again. New dialogue needs an incremental voice bake.");

    static IEnumerable<string> OnnxPluginPaths() => AssetDatabase.GetAllAssetPaths()
        .Where(p => p.StartsWith("Assets/Plugins/DuckSpeech/", StringComparison.Ordinal)
            && p.IndexOf("onnxruntime", StringComparison.OrdinalIgnoreCase) >= 0);

    [MenuItem("Tools/DuckZ/Maintenance/Repair ONNX Plugin Compatibility")]
    public static void RepairOnnxPluginCompatibility()
    {
        // Use Unity's importer API so the imported state and serialized metadata agree.
        // Preserve Editor compatibility and leave Vosk recognition plugins untouched.
        foreach (string path in OnnxPluginPaths().ToArray())
        {
            var plugin = AssetImporter.GetAtPath(path) as PluginImporter;
            if (plugin == null) continue;
            plugin.SetCompatibleWithAnyPlatform(false);
            plugin.SetCompatibleWithPlatform(BuildTarget.Android, false);
            plugin.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false);
            plugin.SaveAndReimport();
        }
        Validate();
    }

    [MenuItem("Tools/DuckZ/Authoring/Audio/Generate New or Changed Clips")]
    static void Generate()
    {
        // Explicit action: generation can take minutes; builds validate rather than silently doing expensive work.
        var start = new System.Diagnostics.ProcessStartInfo("powershell.exe")
        {
            Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + Path.GetFullPath("Tools/generate_duck_voice.ps1") + "\"",
            WorkingDirectory = Path.GetFullPath("."), UseShellExecute = false, CreateNoWindow = true,
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
        };
        System.Diagnostics.Process.Start(start);
        Debug.Log("Duck voice bake started. Progress: .utmp/duck-voice-generation.log. Validate after completion.");
    }
}
