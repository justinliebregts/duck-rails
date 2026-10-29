using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public static class DuckSpeechCacheChecks
{
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Debug.Log("CACHE PASS: " + message);
    }
    static void Reject(Action action, string message)
    {
        try { action(); } catch (InvalidDataException) { Check(true, message); return; }
        throw new Exception(message);
    }
    static string Manifest(string folder) => string.Join("\n", DuckTtsCore.BundledFiles.Select(file =>
        file + "\t" + new FileInfo(Path.Combine(folder, file)).Length + "\t" + DuckTtsCore.HashFile(Path.Combine(folder, file)))) + "\n";
    static int Install(DuckSpeechCache cache, string source)
    {
        int extracted = 0;
        foreach (string file in DuckTtsCore.BundledFiles)
        {
            if (cache.TryReuse(file)) continue;
            File.Copy(Path.Combine(source, file), cache.PartialPath(file), true);
            cache.Commit(file); extracted++;
        }
        cache.Publish(); return extracted;
    }
    static void Seed(string source, string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (string file in DuckTtsCore.BundledFiles) File.Copy(Path.Combine(source, file), Path.Combine(folder, file));
        File.WriteAllText(Path.Combine(folder, "checksums.tsv"), Manifest(source));
    }
    public static void Run()
    {
        string root = Path.GetFullPath("C:/duckz/.utmp/duck-kokoro/cache-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "apk-source"); Directory.CreateDirectory(source);
        foreach (string file in DuckTtsCore.BundledFiles) File.WriteAllText(Path.Combine(source, file), "fixture:" + file);
        string manifest = Manifest(source);
        string oldA = Path.Combine(root, "DuckSpeech-Kokoro-AAAAAAAAAAAAAAAA");
        string oldB = Path.Combine(root, "DuckSpeech-Kokoro-BBBBBBBBBBBBBBBB");
        Seed(source, oldA); Seed(source, oldB);
        File.WriteAllText(Path.Combine(oldA, "kokoro.onnx"), "damaged");
        string unrelated = Path.Combine(root, "DuckSpeech-Kokoro-personal"); Directory.CreateDirectory(unrelated);
        File.WriteAllText(Path.Combine(unrelated, "kokoro.onnx"), "leave alone");
        File.WriteAllText(Path.Combine(root, "recognition.zip"), "leave alone");
        File.WriteAllText(Path.Combine(oldA, "player-save.txt"), "leave alone");
        Reject(() => new DuckSpeechCache(root, manifest.Replace("vocab.tsv", "../vocab.tsv")), "reject path traversal manifest");
        Reject(() => new DuckSpeechCache(root, manifest + manifest.Split('\n')[0] + "\n"), "reject duplicate manifest entries");
        var cache = new DuckSpeechCache(root, manifest);
        bool guarded = false;
        try { cache.CleanupAfterLoad(); } catch (InvalidOperationException) { guarded = true; }
        Check(guarded, "cleanup forbidden before complete validation");
        Check(Install(cache, source) == 0, "migration uses valid files across old versions without APK extraction");
        Check(!File.Exists(Path.Combine(oldB, "kokoro.onnx")), "valid model moved instead of duplicated");
        Check(Directory.Exists(oldA) && Directory.Exists(oldB), "legacy folders retained until load success");
        DuckTtsCore.ValidateBundle(cache.DirectoryPath);
        cache.CleanupAfterLoad();
        Check(!Directory.Exists(oldB), "obsolete empty cache removed");
        Check(File.Exists(Path.Combine(oldA, "player-save.txt")) && !File.Exists(Path.Combine(oldA, "kokoro.onnx")), "unknown content preserved while old model removed");
        Check(File.Exists(Path.Combine(root, "recognition.zip")) && File.Exists(Path.Combine(unrelated, "kokoro.onnx")), "unrelated speech data untouched");
        string model = Path.Combine(cache.DirectoryPath, "kokoro.onnx");
        DateTime stamp = File.GetLastWriteTimeUtc(model);
        Check(Install(new DuckSpeechCache(root, manifest), source) == 0, "second launch reuses all files");
        File.AppendAllText(Path.Combine(source, "phrases.tsv"), "new dialogue");
        manifest = Manifest(source);
        cache = new DuckSpeechCache(root, manifest);
        Check(Install(cache, source) == 1, "dialogue update replaces exactly one file");
        Check(File.GetLastWriteTimeUtc(model) == stamp, "dialogue update leaves model untouched");
        Check(Directory.GetDirectories(root, DuckSpeechCache.FolderName).Length == 1, "single reusable cache across updates");
        // Interrupted download and interrupted replacement must both self-repair.
        File.WriteAllText(cache.PartialPath("kokoro.onnx"), "truncated");
        cache = new DuckSpeechCache(root, manifest);
        Check(!File.Exists(cache.PartialPath("kokoro.onnx")) && cache.TryReuse("kokoro.onnx"), "interrupted partial discarded, valid old model retained");
        File.WriteAllText(cache.PartialPath("kokoro.onnx"), "bad checksum");
        Reject(() => cache.Commit("kokoro.onnx"), "corrupt replacement rejected before deleting valid model");
        Check(cache.TryReuse("kokoro.onnx"), "valid model survives corrupt update");
        File.Copy(Path.Combine(source, "kokoro.onnx"), cache.PartialPath("kokoro.onnx"), true);
        File.Delete(model); // Simulate process death between delete and rename.
        cache = new DuckSpeechCache(root, manifest);
        Reject(() => cache.Publish(), "incomplete extraction cannot publish manifest");
        Check(Install(cache, source) == 1, "interrupted commit repaired from bundled file");
        File.WriteAllText(model, new string('x', (int)new FileInfo(model).Length));
        Check(Install(new DuckSpeechCache(root, manifest), source) == 1, "same-size corruption repaired by checksum");
        File.Delete(Path.Combine(cache.DirectoryPath, "checksums.tsv"));
        Check(Install(new DuckSpeechCache(root, manifest), source) == 0, "interrupted manifest publication repaired without recopying model");
        DuckTtsCore.ValidateBundle(cache.DirectoryPath);

        string real = Path.Combine(root, "real"); Directory.CreateDirectory(real);
        string bundle = Path.GetFullPath("Tools/DuckVoiceSource");
        string legacy = Path.Combine(real, "DuckSpeech-Kokoro-CCCCCCCCCCCCCCCC"); Seed(bundle, legacy);
        cache = new DuckSpeechCache(real, File.ReadAllText(Path.Combine(bundle, "checksums.tsv")));
        Check(Install(cache, bundle) == 0, "real 92 MB Kokoro model migrates without extraction");
        using (var engine = new DuckTtsCore(cache.DirectoryPath))
        {
            cache.CleanupAfterLoad();
            var samples = engine.Generate("Hello! I'm Duck.");
            Check(samples.Length > 2400 && samples.All(v => !float.IsNaN(v) && !float.IsInfinity(v))
                && samples.Any(v => Math.Abs(v) > .001f), "migrated real model produces audible offline speech");
        }
        Check(!Directory.Exists(legacy), "real legacy cache removed only after successful model load");
        // Remove only our known fixture files after disposing the engine.
        foreach (string file in DuckTtsCore.BundledFiles) File.Delete(Path.Combine(cache.DirectoryPath, file));
        File.Delete(Path.Combine(cache.DirectoryPath, "checksums.tsv"));
        Directory.Delete(cache.DirectoryPath, false); Directory.Delete(real, false);
        Debug.Log("ALL SPEECH CACHE CHECKS PASSED");
    }
}
