using System;
using System.Linq;
using Game.Audio;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Keeps GameAudioSettings.sounds listing every SoundEvent in the project, so RoomRpc can find any sound by name.
public class SoundRegistry : AssetPostprocessor, IPreprocessBuildWithReport
{
    const string SettingsPath = "Assets/Resources/GameAudioSettings.asset";
    static bool refreshQueued;

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (!Refresh(out string error))
            throw new BuildFailedException(error);
    }

    [InitializeOnLoadMethod]
    static void RefreshOncePerSession()
    {
        if (SessionState.GetBool("SoundRegistry.Refreshed", false))
            return;
        SessionState.SetBool("SoundRegistry.Refreshed", true);
        QueueRefresh();
    }

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        bool soundsChanged = imported.Concat(moved).Any(path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(SoundEvent))
            || deleted.Concat(movedFrom).Any(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase));
        if (soundsChanged)
            QueueRefresh();
    }

    static void QueueRefresh()
    {
        if (refreshQueued)
            return;

        // Assets can't be saved while an import is in progress.
        refreshQueued = true;
        EditorApplication.delayCall += () =>
        {
            refreshQueued = false;
            if (!Refresh(out string error))
                Debug.LogError(error);
        };
    }

    [MenuItem("Tools/DuckZ/Maintenance/Refresh Sound Registry")]
    static void RefreshFromMenu()
    {
        if (Refresh(out string error))
            Debug.Log("Sound registry is up to date.");
        else
            Debug.LogError(error);
    }

    /// <summary>False, with a reason, when the registry can't be trusted (missing settings or duplicate names).</summary>
    public static bool Refresh(out string error)
    {
        error = null;
        var settings = AssetDatabase.LoadAssetAtPath<Game.Audio.AudioSettings>(SettingsPath);
        if (settings == null)
        {
            error = $"Sound registry: {SettingsPath} not found.";
            return false;
        }

        SoundEvent[] sounds = AssetDatabase.FindAssets("t:SoundEvent")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<SoundEvent>)
            .Where(sound => sound != null)
            .OrderBy(sound => sound.name, StringComparer.Ordinal)
            .ToArray();

        string[] duplicates = sounds.GroupBy(sound => sound.name).Where(g => g.Count() > 1)
            .Select(g => $"'{g.Key}' ({string.Join(", ", g.Select(AssetDatabase.GetAssetPath))})")
            .ToArray();
        if (duplicates.Length > 0)
            error = "Sound registry: SoundEvent names must be unique so they can be sent over the network. Duplicates: " + string.Join("; ", duplicates);

        if (!sounds.SequenceEqual(settings.sounds ?? new SoundEvent[0]))
        {
            settings.sounds = sounds;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }
        return error == null;
    }
}
