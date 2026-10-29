#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

/// <summary>Local MPPM rendezvous only; authentication and world loading stay unchanged.</summary>
[InitializeOnLoad]
public static class RailEditorQuickStart
{
    const string MenuPath = "Duck Rails/Two Ducks Quick Start";
    const string HostTag = "RailsQuickStart";
    static string Key => MppmStandaloneCredentials.PrefsKeyForAssets(Application.dataPath) + ".QuickStart";

    public static bool Enabled
    {
        get => EditorPrefs.GetBool(Key + ".Enabled", true);
        set => EditorPrefs.SetBool(Key + ".Enabled", value);
    }

    static RailEditorQuickStart()
    {
        // A random local rendezvous avoids sharing a hard-coded Photon room with
        // other developers. Keep it while an additional editor is still running.
        if (CurrentPlayer.IsMainEditor && !EditorPrefs.HasKey(Key + ".Room"))
            EditorPrefs.SetString(Key + ".Room", "editor-" + Guid.NewGuid().ToString("N"));
    }

    public static bool TryGetRoom(out string room)
    {
        room = null;
        if (!ShouldQuickStart(Enabled, CurrentPlayer.IsMainEditor, CurrentPlayer.Tags, SelectedMainEditorTag())) return false;
        room = EditorPrefs.GetString(Key + ".Room", "");
        if (!string.IsNullOrEmpty(room)) return true;
        Debug.LogWarning("Two Ducks quick start has no shared room. Open the main Duck Rails editor first.");
        return false;
    }

    public static bool ShouldQuickStart(bool enabled, bool mainEditor, IReadOnlyList<string> runtimeTags, string selectedMainTag)
    {
        // Main-editor runtime tags can be empty/stale even with a tagged scenario
        // selected. Its launch configuration is the source of truth. Do not let a
        // stale runtime tag enable quick start after switching back to solo Play.
        return enabled && (mainEditor ? selectedMainTag == HostTag : runtimeTags != null && runtimeTags.Contains("Player2"));
    }

    static string SelectedMainEditorTag()
    {
        if (!CurrentPlayer.IsMainEditor) return null;
        // Unity 6.3 keeps its active scenario API internal. Read the serialized
        // selection instead of reflecting into its lifecycle methods or relying
        // on an asset name (which can be renamed or exist without being selected).
        foreach (var manager in Resources.FindObjectsOfTypeAll<ScriptableObject>())
        {
            if (manager.GetType().FullName != "Unity.PlayMode.Editor.PlayModeManager") continue;
            using (var state = new SerializedObject(manager))
            {
                var scenario = state.FindProperty("m_Config")?.objectReferenceValue;
                if (scenario == null) return null;
                using (var config = new SerializedObject(scenario))
                {
                    if (config.FindProperty("m_EnableEditors")?.boolValue != true) return null;
                    return config.FindProperty("m_MainEditorInstance.m_PlayerTag")?.stringValue;
                }
            }
        }
        return null;
    }

    [MenuItem(MenuPath)]
    static void Toggle() => Enabled = !Enabled;

    [MenuItem(MenuPath, true)]
    static bool Validate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
#endif
