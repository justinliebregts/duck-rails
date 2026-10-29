using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>Offline Android extraction cache. File identities, not dialogue versions,
/// determine reuse. Call on a worker; only the APK reader belongs on Unity's thread.</summary>
public sealed class DuckSpeechCache
{
    public const string FolderName = "DuckSpeech-Kokoro";
    readonly string root, manifest;
    readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
    readonly List<string> legacy = new List<string>();
    bool published;
    struct Entry { public long size; public string hash; }
    public string DirectoryPath { get; }

    public DuckSpeechCache(string persistentRoot, string checksums)
    {
        root = Path.GetFullPath(persistentRoot);
        Safe(root);
        manifest = checksums;
        var allowed = new HashSet<string>(DuckTtsCore.BundledFiles, StringComparer.Ordinal);
        foreach (string row in checksums.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(row)) continue;
            string[] fields = row.TrimEnd('\r').Split('\t');
            if (fields.Length != 3 || !allowed.Contains(fields[0]) || entries.ContainsKey(fields[0])
                || !long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out long size)
                || size <= 0 || !Regex.IsMatch(fields[2], "\\A[0-9a-fA-F]{64}\\z"))
                throw new InvalidDataException("Invalid bundled Kokoro manifest.");
            entries.Add(fields[0], new Entry { size = size, hash = fields[2] });
        }
        if (entries.Count != allowed.Count) throw new InvalidDataException("Incomplete bundled Kokoro manifest.");
        DirectoryPath = Path.Combine(root, FolderName);
        Safe(DirectoryPath);
        Directory.CreateDirectory(DirectoryPath);
        foreach (string candidate in Directory.GetDirectories(root, FolderName + "-*"))
        {
            if (Regex.IsMatch(Path.GetFileName(candidate), "\\ADuckSpeech-Kokoro-[0-9A-Fa-f]{16}\\z")
                && !IsLink(candidate)) legacy.Add(candidate);
        }
        // An interrupted write is never considered a usable model.
        foreach (string file in DuckTtsCore.BundledFiles) DeleteFile(DirectoryPath, file + ".partial");
        DeleteFile(DirectoryPath, "checksums.tsv.partial");
    }

    static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    static void Safe(string path)
    {
        if ((Directory.Exists(path) || File.Exists(path)) && IsLink(path))
            throw new IOException("Speech cache cannot use a linked path: " + path);
    }
    string FilePath(string file)
    {
        if (!entries.ContainsKey(file)) throw new InvalidDataException("Unknown Kokoro asset.");
        Safe(root); Safe(DirectoryPath);
        string path = Path.Combine(DirectoryPath, file);
        Safe(path);
        return path;
    }
    bool Matches(string path, Entry entry)
    {
        Safe(path);
        return File.Exists(path) && new FileInfo(path).Length == entry.size
            && string.Equals(DuckTtsCore.HashFile(path), entry.hash, StringComparison.OrdinalIgnoreCase);
    }

    public bool TryReuse(string file)
    {
        string destination = FilePath(file);
        if (Matches(destination, entries[file])) return true;
        foreach (string old in legacy)
        {
            Safe(old);
            string source = Path.Combine(old, file);
            if (!Matches(source, entries[file])) continue;
            // Rename within persistent storage: no second 92 MB model copy.
            if (File.Exists(destination)) File.Delete(destination);
            File.Move(source, destination);
            return true;
        }
        return false;
    }

    public string PartialPath(string file)
    {
        string path = FilePath(file) + ".partial";
        Safe(path);
        return path;
    }
    public void Commit(string file)
    {
        string partial = PartialPath(file);
        if (!Matches(partial, entries[file]))
            throw new InvalidDataException("Bundled voice file is missing or damaged: " + file);
        string destination = FilePath(file);
        // If interrupted between delete and move, next launch extracts again.
        // The manifest is published only after every final file validates.
        if (File.Exists(destination)) File.Delete(destination);
        File.Move(partial, destination);
    }
    public void Publish()
    {
        foreach (var entry in entries)
            if (!Matches(FilePath(entry.Key), entry.Value))
                throw new InvalidDataException("Incomplete extracted voice: " + entry.Key);
        string partial = Path.Combine(DirectoryPath, "checksums.tsv.partial");
        Safe(partial);
        File.WriteAllText(partial, manifest);
        DeleteFile(DirectoryPath, "checksums.tsv");
        File.Move(partial, Path.Combine(DirectoryPath, "checksums.tsv"));
        published = true;
    }

    // Call only after the inference engine successfully opens the published bundle.
    public void CleanupAfterLoad()
    {
        if (!published) throw new InvalidOperationException("Voice cache has not been validated.");
        Safe(root); Safe(DirectoryPath);
        foreach (string old in legacy)
        {
            Safe(old);
            if (!Directory.Exists(old)) continue;
            // Exact, direct-child legacy paths only. Never recursively delete a
            // folder or follow links; preserve unexpected content/game saves.
            foreach (string file in DuckTtsCore.BundledFiles)
            {
                DeleteFile(old, file);
                DeleteFile(old, file + ".partial");
            }
            DeleteFile(old, "checksums.tsv");
            DeleteFile(old, "checksums.tsv.partial");
            if (Directory.GetFileSystemEntries(old).Length == 0) Directory.Delete(old, false);
        }
    }
    static void DeleteFile(string directory, string name)
    {
        Safe(directory);
        string path = Path.Combine(directory, name);
        Safe(path);
        if (File.Exists(path)) File.Delete(path);
    }
}
